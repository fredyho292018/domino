package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.realtime.ConnectionOutbound
import com.teamfho.domino.realtime.PresenceStore
import org.springframework.beans.factory.ObjectProvider
import org.springframework.stereotype.Component
import java.util.concurrent.*

/** Bounded preparation worker; no socket writes and no durable queries on heartbeat. */
@Component
class SocialPresenceRuntime(database:ObjectProvider<Firestore>,private val invalidation:SocialInvalidationRuntime,
    private val store:PresenceStore,private val redisFactory:ObjectProvider<org.springframework.data.redis.connection.RedisConnectionFactory>):AutoCloseable {
    private val reader=SocialPresenceAuthorization {database.ifAvailable?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")}
    private val subscriptions=ConcurrentHashMap<String,SocialPresenceSubscriptions>()
    private val observations=ConcurrentHashMap<String,Pair<String,Boolean>>()
    private val states=ConcurrentHashMap<String,SocialPresenceState>()
    private val maintenance=java.util.concurrent.atomic.AtomicBoolean()
    private var bus:SocialPresencePubSub?=null
    private var nextBus=0L
    private var busBackoff=1L
    private var nextRead=0L
    private var nextWrite=0L
    private val cached=object:PresenceStore {
        override fun touch(uid:String,connectionId:String,serverId:String)=error("Read only")
        override fun remove(uid:String,connectionId:String)=error("Read only")
        override fun onlinePlayers()=error("Read only")
        override fun states(uids:List<String>)=uids.associateWith{states[it]?:SocialPresenceState.UNKNOWN}
    }
    private val worker=ThreadPoolExecutor(2,2,0,TimeUnit.MILLISECONDS,ArrayBlockingQueue(128),
        ThreadFactory{r->Thread(r,"social-presence").also{it.isDaemon=true}},ThreadPoolExecutor.AbortPolicy())
    fun subscribe(connection:String,viewer:String,writer:ConnectionOutbound,ids:List<String>,generation:Long) {
        try {
            worker.execute {
                if(!writer.isAccepting())return@execute
                try {
                    val sub=subscriptions.computeIfAbsent(connection){SocialPresenceSubscriptions(connection,viewer,writer,invalidation.index,reader,reader::resolve,cached)}
                    sub.replace(ids,generation)
                    sub.refresh()
                    if(!writer.isAccepting())close(connection)
                } catch(_:Exception){writer.offerControl("SOCIAL_PRESENCE_ERROR",mapOf("code" to "PRESENCE_REQUEST_UNAVAILABLE","generation" to generation))}
            }
        }catch(_:RejectedExecutionException){writer.offerControl("SOCIAL_PRESENCE_ERROR",mapOf("code" to "PRESENCE_REQUEST_UNAVAILABLE","generation" to generation))}
    }
    fun close(connection:String){subscriptions.remove(connection)?.close()}
    fun observeMatch(state:com.teamfho.domino.online.OnlineState) {
        val version=state.match.updatedAt.toEpochMilli().toString().padStart(20,'0')+state.match.lastSequence.toString().padStart(20,'0')
        val active=state.match.participants.size>=2 && state.match.status !in setOf(com.teamfho.domino.match.MatchStatus.FINISHED,com.teamfho.domino.match.MatchStatus.CANCELLED)
        state.match.participants.forEach { p->p.playerUid?.let {uid->
            if(observations.size<10_000 || observations.containsKey(uid))observations.compute(uid){_,old->
                if(old==null || version>=old.first)version to (active && p.connectionState!=com.teamfho.domino.match.ConnectionState.ABANDONED) else old
            }
        }}
    }
    @org.springframework.scheduling.annotation.Scheduled(fixedDelay=1000)
    fun maintain() {
        if(!maintenance.compareAndSet(false,true))return
        try{worker.execute {
            try {
                if(bus==null && System.nanoTime()>=nextBus)try {
                    redisFactory.ifAvailable?.let {factory->
                        val candidate=SocialPresencePubSub(factory,
                            {uid->invalidation.index.forTarget(uid).forEach{h->subscriptions[h.connection]?.stateChanged()}},
                            {subscriptions.values.forEach{it.changed()}})
                        try{candidate.start();bus=candidate}catch(e:Exception){candidate.close();throw e}
                    }
                }catch(_:Exception){nextBus=System.nanoTime()+TimeUnit.SECONDS.toNanos(busBackoff);busBackoff=minOf(30,busBackoff*2)}
                if(System.nanoTime()>=nextWrite) for((uid,value) in observations.entries.toList().take(100)) {
                    try{store.matchActivity(uid,value.first,value.second);observations.remove(uid,value)}catch(_:Exception){
                        nextWrite=System.nanoTime()+TimeUnit.SECONDS.toNanos(5);break
                    }
                }
                val targets=invalidation.index.targetIds()
                states.keys.retainAll(targets.toSet())
                var unavailable=System.nanoTime()<nextRead
                targets.chunked(50).forEach {batch->
                    val current=if(unavailable)batch.associateWith{SocialPresenceState.UNKNOWN} else try{store.states(batch)}catch(_:Exception){
                        unavailable=true;nextRead=System.nanoTime()+TimeUnit.SECONDS.toNanos(5)
                        batch.associateWith{SocialPresenceState.UNKNOWN}
                    }
                    current.forEach{(uid,state)->if(states.put(uid,state)!=state)
                        invalidation.index.forTarget(uid).forEach{h->subscriptions[h.connection]?.stateChanged()}}
                }
                subscriptions.values.forEach {it.refresh()}
            } finally {maintenance.set(false)}
        }}catch(_:RejectedExecutionException){maintenance.set(false)}
    }
    @jakarta.annotation.PreDestroy
    override fun close(){worker.shutdownNow();bus?.close();subscriptions.values.forEach{it.close()};subscriptions.clear();observations.clear();states.clear()}
}
