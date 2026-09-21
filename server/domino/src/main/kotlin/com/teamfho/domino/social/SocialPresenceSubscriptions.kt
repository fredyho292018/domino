package com.teamfho.domino.social

import com.teamfho.domino.realtime.ConnectionOutbound
import com.teamfho.domino.realtime.OfferResult
import com.teamfho.domino.realtime.PresenceStore
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/** One desired set per existing transport. Work is driven by a bounded shared runtime. */
class SocialPresenceSubscriptions(
    private val connection:String, private val viewer:String, private val writer:ConnectionOutbound,
    private val index:LocalSocialAuthorizationIndex, private val reader:SocialAuthorizationReader,
    private val resolve:(String)->String?, private val store:PresenceStore,
) : AutoCloseable {
    private val generation=AtomicLong(0)
    private val revision=AtomicLong(0)
    private val dirty=AtomicBoolean(false)
    private val clearing=AtomicBoolean(false)
    private val closed=AtomicBoolean(false)
    private val handles=ConcurrentHashMap<String,LocalSocialAuthorizationIndex.Handle>()
    @Volatile private var desired:List<String> = emptyList()
    private var lastRequest=Long.MIN_VALUE
    private val sent=mutableMapOf<String,SocialPresenceState>()
    private var sentRevision=-1L

    /** Validate entirely before replacing. The caller serializes subscription commands. */
    @Synchronized fun replace(ids:List<String>,requestGeneration:Long) {
        require(requestGeneration>generation.get() && requestGeneration<=9_007_199_254_740_991L)
        val unique=ids.distinct()
        require(unique.size<=50 && ids.size<=100)
        unique.forEach(SocialNames::publicId)
        val now=System.nanoTime()
        check(unique.isEmpty() || lastRequest==Long.MIN_VALUE || now-lastRequest>=250_000_000L)
        check(!closed.get())
        lastRequest=now
        generation.set(requestGeneration)
        revision.set(0);clearing.set(false)
        handles.values.forEach{it.close()};handles.clear()
        desired=unique
        writer.offerControl("SOCIAL_PRESENCE_SUBSCRIBED",mapOf("generation" to requestGeneration,"count" to unique.size))
        changed()
    }

    /** No network work on the A.3 callback/producer. Invalidation already kills capabilities synchronously. */
    fun changed(){
        if(closed.get())return
        dirty.set(true)
        if(clearing.compareAndSet(false,true)) {
            val stream=revision.incrementAndGet()
            writer.offerControl("SOCIAL_PRESENCE_INVALIDATED",mapOf("generation" to generation.get(),"revision" to stream))
        }
    }

    /** A Redis state hint changes data, never authorization or the stream fence. */
    fun stateChanged(){if(!closed.get())dirty.set(true)}

    @Synchronized fun refresh() {
        if(closed.get())return
        if(writer.snapshot().recoveryReady && writer.acknowledgeEphemeralRecovery())changed()
        if(!dirty.getAndSet(false))return
        val current=generation.get()
        val stream=revision.get()
        val snapshot=sentRevision!=stream
        if(snapshot){sent.clear();sentRevision=stream}
        clearing.set(false)
        for(id in desired) {
            if(!handles.containsKey(id)) {
                val uid=try{resolve(id)}catch(_:Exception){null}
                if(uid!=null && uid!=viewer) {
                    val h=index.register(connection,viewer,uid,reader){changed()}
                    handles[id]=h
                }
            }
        }
        val readings=handles.mapValues{it.value.delivery()}
        val uids=handles.values.map{it.target}.distinct()
        val states=try{store.states(uids)}catch(_:Exception){uids.associateWith{SocialPresenceState.UNKNOWN}}
        desired.forEach { id ->
            val h=handles[id]
            val grant=readings[id]
            // Denied/unknown targets are already UNKNOWN from reliable clear; no privacy reason on wire.
            if(h!=null && grant!=null) {
                val state=projectPresence(states[h.target]?:SocialPresenceState.UNKNOWN,true,grant.second)
                if(!snapshot && sent[id]==state)return@forEach
                val result=writer.offerEphemeral("presence:$id",if(snapshot)"SOCIAL_PRESENCE_SNAPSHOT" else "SOCIAL_PRESENCE_UPDATED",mapOf(
                    "generation" to current,"revision" to stream,
                    "publicPlayerId" to id,"state" to state.name),grant.first)
                if(result==OfferResult.REJECTED_STALE)dirty.set(true) else sent[id]=state
            }
        }
    }
    @Synchronized override fun close() {
        closed.set(true);generation.incrementAndGet();desired=emptyList()
        handles.values.forEach{it.close()};handles.clear();dirty.set(false)
    }
}
