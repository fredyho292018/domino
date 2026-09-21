package com.teamfho.domino.social

import java.util.UUID
import java.util.concurrent.Executor
import java.util.concurrent.RejectedExecutionException

data class SocialAuthorizationSnapshot(val allowed:Boolean,val pairRevision:Long,val privacyRevision:Long)
fun interface SocialAuthorizationReader {
    /** Consumer-specific permission, read from a consistent durable transaction; never from Pub/Sub. */
    fun read(viewer:String,target:String):SocialAuthorizationSnapshot
}

/** No sockets or presence payloads. Owned locally and allocated only when a consumer registers. */
class LocalSocialAuthorizationIndex(private val executor:Executor,private val nanoTime:()->Long=System::nanoTime,
    private val maxConnections:Int=10_000,private val maxPerConnection:Int=50,
    private val metrics:io.micrometer.core.instrument.MeterRegistry?=null) {
    private val handles=mutableMapOf<String,Handle>()
    private val targets=mutableMapOf<String,MutableSet<String>>()
    private val viewers=mutableMapOf<String,MutableSet<String>>()
    private val pairs=mutableMapOf<String,MutableSet<String>>()
    private val connections=mutableMapOf<String,MutableSet<String>>()
    private var recoveredUntil=Long.MIN_VALUE
    private val leaseNanos=30_000_000_000L
    inner class Handle internal constructor(val id:String,val connection:String,val viewer:String,val target:String,
        internal val reader:SocialAuthorizationReader) : AutoCloseable {
        val pair=SocialPairIdentity.id(viewer,target)
        internal var pairRevision=0L
        internal var privacyRevision=0L
        internal var generation=0L
        internal var valid=false
        internal var allowed=false
        internal var expires=Long.MIN_VALUE
        fun canDeliver():Boolean=synchronized(this@LocalSocialAuthorizationIndex) {
            handles[id]===this && valid && allowed && nanoTime()<expires && nanoTime()<recoveredUntil
        }
        override fun close()=remove(id)
    }
    @Synchronized fun register(connection:String,viewer:String,target:String,reader:SocialAuthorizationReader):Handle {
        require(connection.isNotBlank() && viewer.isNotBlank() && target.isNotBlank())
        require(connections.containsKey(connection) || connections.size<maxConnections)
        val owned=connections[connection]
        // An identity switch cannot reuse permissions from the preceding account.
        if(owned?.firstOrNull()?.let{handles[it]?.viewer!=viewer}==true)closeConnection(connection)
        require(connections[connection].orEmpty().size<maxPerConnection)
        val h=Handle(UUID.randomUUID().toString(),connection,viewer,target,reader)
        handles[h.id]=h
        fun add(index:MutableMap<String,MutableSet<String>>,key:String){index.getOrPut(key){mutableSetOf()}.add(h.id)}
        add(connections,connection);add(targets,target);add(viewers,viewer);add(pairs,h.pair)
        schedule(listOf(h))
        return h
    }
    @Synchronized fun invalidate(event:SocialInvalidation) {
        event.validate()
        val ids=if(event.type==SocialInvalidationType.PAIR_AUTHORIZATION_CHANGED)pairs[event.pairId]else targets[event.targetUid]
        val changed=ids.orEmpty().mapNotNull{handles[it]}.filter { h->
            val old=if(event.pairId!=null)h.pairRevision else h.privacyRevision
            if(event.revision<=old) {
                metrics?.counter(if(event.revision==old)"social_invalidation_duplicate" else "social_invalidation_stale")?.increment()
                false
            } else {
                if(event.pairId!=null)h.pairRevision=event.revision else h.privacyRevision=event.revision
                h.valid=false;h.allowed=false;h.generation++;true
            }
        }
        schedule(changed)
    }
    /** Global failure is different from targeted normal invalidation. Never preserve optimistic delivery. */
    @Synchronized fun recoveryFailed(){recoveredUntil=Long.MIN_VALUE;handles.values.forEach{it.valid=false;it.allowed=false;it.generation++}}
    @Synchronized fun recovered(){recoveredUntil=nanoTime()+leaseNanos;refreshExpired()}
    @Synchronized fun refreshExpired() {
        val stale=handles.values.filter{!it.valid || nanoTime()>=it.expires}
        stale.forEach{it.valid=false;it.allowed=false;it.generation++}
        schedule(stale)
    }
    private fun schedule(values:List<Handle>) {
        // A single durable read is reused for identical consumer/viewer/target handles.
        values.groupBy{Triple(it.reader,it.viewer,it.target)}.forEach { (key,group)->
            val generations=group.associate{it.id to it.generation}
            try {executor.execute {
                metrics?.counter("social_invalidation_reauthorization")?.increment()
                val snapshot=try{key.first.read(key.second,key.third)}catch(_:Exception){null}
                synchronized(this) {
                    group.forEach { h->
                        if(handles[h.id]===h && h.generation==generations[h.id] && snapshot!=null &&
                            snapshot.pairRevision>=h.pairRevision && snapshot.privacyRevision>=h.privacyRevision) {
                            h.pairRevision=snapshot.pairRevision;h.privacyRevision=snapshot.privacyRevision
                            h.allowed=snapshot.allowed;h.valid=true;h.expires=nanoTime()+leaseNanos
                            if(!snapshot.allowed)metrics?.counter("social_invalidation_revoked")?.increment()
                        }
                    }
                }
            }}catch(_:RejectedExecutionException){/* Already invalid. A bounded later refresh retries. */}
        }
    }
    @Synchronized private fun remove(id:String) {
        val h=handles.remove(id)?:return
        h.valid=false;h.allowed=false;h.generation++
        for((index,key) in listOf(connections to h.connection,targets to h.target,viewers to h.viewer,pairs to h.pair)) {
            index[key]?.let{it.remove(id);if(it.isEmpty())index.remove(key)}
        }
    }
    @Synchronized fun closeConnection(connection:String){connections[connection]?.toList()?.forEach(::remove)}
    @Synchronized fun closeAccount(viewer:String){viewers[viewer]?.toList()?.forEach(::remove)}
    @Synchronized fun size()=handles.size
    @Synchronized fun indexSizes()=listOf(connections.size,targets.size,viewers.size,pairs.size)
}
