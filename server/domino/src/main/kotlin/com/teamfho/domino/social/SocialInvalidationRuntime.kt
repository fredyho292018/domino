package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.ListenerRegistration
import com.teamfho.domino.catalog.GameCatalogCodec
import org.slf4j.LoggerFactory
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.SmartLifecycle
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.data.redis.connection.RedisConnectionFactory
import org.springframework.data.redis.connection.Message
import org.springframework.data.redis.connection.SubscriptionListener
import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.listener.ChannelTopic
import org.springframework.data.redis.listener.RedisMessageListenerContainer
import org.springframework.data.redis.connection.MessageListener
import org.springframework.util.backoff.ExponentialBackOff
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicBoolean

/** A separate Pub/Sub connection and recovery loop: no coupling to the A.2 command circuit. */
class SocialInvalidationPubSub(factory:RedisConnectionFactory,private val redis:StringRedisTemplate,
    private val receive:(SocialInvalidation)->Unit,private val reconnect:()->Unit,private val degraded:()->Unit):AutoCloseable {
    companion object {const val CHANNEL="domino:v1:social:authorization-invalidated"}
    private val container=RedisMessageListenerContainer()
    private val deliveries=ThreadPoolExecutor(1,1,0,TimeUnit.MILLISECONDS,ArrayBlockingQueue(128),
        ThreadFactory{r->Thread(r,"social-pubsub-delivery").also{it.isDaemon=true}},ThreadPoolExecutor.AbortPolicy())
    private val subscriptions=Executors.newSingleThreadExecutor{r->Thread(r,"social-pubsub-subscription").also{it.isDaemon=true}}
    init {
        container.setConnectionFactory(factory)
        container.setTaskExecutor(deliveries)
        container.setSubscriptionExecutor(subscriptions)
        container.setRecoveryBackoff(ExponentialBackOff(1000,2.0).also{it.maxInterval=30_000})
        container.setErrorHandler {degraded()}
        container.addMessageListener(object:MessageListener,SubscriptionListener {
            override fun onMessage(message:Message,pattern:ByteArray?) {
                try {
                    require(message.body.size<=4096)
                    val event=GameCatalogCodec.mapper.readValue(String(message.body,Charsets.UTF_8),SocialInvalidation::class.java).validate()
                    receive(event)
                }catch(_:Exception){/* Invalid internal messages never grant permissions. */}
            }
            override fun onChannelSubscribed(channel:ByteArray,count:Long){reconnect()}
            override fun onChannelUnsubscribed(channel:ByteArray,count:Long){degraded()}
        },ChannelTopic(CHANNEL))
        container.afterPropertiesSet()
    }
    fun start()=container.start()
    fun publish(event:SocialInvalidation){redis.convertAndSend(CHANNEL,GameCatalogCodec.json(event.validate()))}
    override fun close(){container.stop();container.destroy();deliveries.shutdownNow();subscriptions.shutdownNow()}
}

/** No per-event threads or per-heartbeat reads. Idle dispatch is listener-driven;
 * reconciliation runs only while authorization handles exist, every five seconds.
 * A 30s delivery lease fails closed if recovery stalls, including a silent lost message. */
class SocialInvalidationRuntime(private val database:()->Firestore?,private val factory:()->RedisConnectionFactory?,
    private val redis:()->StringRedisTemplate?,private val metrics:io.micrometer.core.instrument.MeterRegistry?=null):SmartLifecycle,SocialInvalidationSink,AutoCloseable {
    private val log=LoggerFactory.getLogger(javaClass)
    private val worker=ThreadPoolExecutor(2,2,0,TimeUnit.MILLISECONDS,ArrayBlockingQueue(256),
        ThreadFactory {r->Thread(r,"social-authorization").also{it.isDaemon=true}},ThreadPoolExecutor.AbortPolicy())
    val index=LocalSocialAuthorizationIndex(worker,metrics=metrics)
    private val scheduler=Executors.newSingleThreadScheduledExecutor {r->Thread(r,"social-invalidation").also{it.isDaemon=true}}
    private val pending=AtomicBoolean(true)
    private val reconcile=AtomicBoolean(true)
    private val listenerFailed=AtomicBoolean(false)
    @Volatile private var running=false
    private var feed:FirestoreSocialInvalidationFeed?=null
    private var listener:ListenerRegistration?=null
    private var bus:SocialInvalidationPubSub?=null
    private var cursor:SocialFeedCursor?=null
    private var nextDispatch=0L
    private var backoff=1L
    private var nextRecovery=0L
    private var nextSource=0L
    private var sourceBackoff=1L
    override fun committed(events:List<SocialInvalidation>) {
        // Local invalidation is synchronous and does not wait for Redis or Firestore reads.
        try{events.forEach(index::invalidate)}catch(_:Exception){index.recoveryFailed()}
        if(events.isNotEmpty())metrics?.counter("social_invalidation_outbox_created")?.increment(events.size.toDouble())
        if(events.isNotEmpty())pending.set(true)
    }
    override fun start() {if(!running){running=true;scheduler.scheduleWithFixedDelay(::tick,0,1,TimeUnit.SECONDS)}}
    override fun isRunning()=running
    override fun isAutoStartup()=true
    private fun tick() {
        if(!running)return
        val now=System.nanoTime()
        if(now<nextSource)return
        try {
            if(feed==null) {
                val db=database()?:return
                feed=FirestoreSocialInvalidationFeed(db)
            }
            val source=feed!!
            if(index.size()==0) {
                // No permissions survive this idle boundary. A future registration reads current
                // durable state after a fresh server fence, without scanning unused idle history.
                cursor=null;index.recoveryFailed()
            } else if(cursor==null) {
                index.recoveryFailed();cursor=source.start();index.recovered();reconcile.set(true)
            }
            if(listenerFailed.getAndSet(false)){listener?.remove();listener=null}
            if(listener==null)listener=source.listenPending({pending.set(true)},{listenerFailed.set(true);pending.set(true)})
            if(cursor!=null && index.size()>0 && (reconcile.getAndSet(false) || now>=nextRecovery)) {
                // At most one page per tick; keep delivery closed while catching up a backlog.
                val page=source.recover(cursor!!)
                metrics?.counter("social_invalidation_feed_recovery")?.increment()
                page.events.forEach(index::invalidate)
                cursor=page.cursor
                if(page.caughtUp){index.recovered();nextRecovery=now+TimeUnit.SECONDS.toNanos(5)}
                else {index.recoveryFailed();reconcile.set(true)}
            }
            sourceBackoff=1
        }catch(_:Exception) {
            index.recoveryFailed()
            nextSource=now+TimeUnit.SECONDS.toNanos(sourceBackoff)
            sourceBackoff=minOf(30,sourceBackoff*2)
            log.warn("[SOCIAL_INVALIDATION] recovery unavailable; authorization fail-closed")
        }
        // Pub/Sub failure cannot skip durable reconciliation or enter the A.2 rate circuit.
        if(now<nextDispatch)return
        try {
            if(bus==null) {
                val f=factory();val r=redis()
                if(f!=null && r!=null) {
                    val candidate=SocialInvalidationPubSub(f,r,{metrics?.counter("social_invalidation_received")?.increment();index.invalidate(it)},
                        {reconcile.set(true)},{reconcile.set(true)})
                    try{candidate.start();bus=candidate}catch(e:Exception){candidate.close();throw e}
                }
            }
            if(bus!=null && feed!=null && pending.getAndSet(false)) {
                val events=feed!!.pending()
                for(event in events){bus!!.publish(event);feed!!.published(event);metrics?.counter("social_invalidation_published")?.increment()}
                if(events.size==FirestoreSocialInvalidationFeed.BATCH)pending.set(true)
                backoff=1
            }
        }catch(_:Exception) {
            metrics?.counter("social_invalidation_publish_failed")?.increment()
            pending.set(true)
            nextDispatch=now+TimeUnit.SECONDS.toNanos(backoff)
            backoff=minOf(30,backoff*2)
            log.warn("[SOCIAL_INVALIDATION] publication unavailable; durable events retained")
        }
    }
    override fun stop()=close()
    override fun close(){running=false;scheduler.shutdownNow();listener?.remove();bus?.close();index.recoveryFailed();worker.shutdownNow()}
}

@Configuration(proxyBeanMethods=false)
class SocialInvalidationConfiguration {
    @Bean(destroyMethod="close") fun socialInvalidationRuntime(db:ObjectProvider<Firestore>,
        factory:ObjectProvider<RedisConnectionFactory>,redis:ObjectProvider<StringRedisTemplate>,metrics:io.micrometer.core.instrument.MeterRegistry)=
        SocialInvalidationRuntime({db.ifAvailable},{factory.ifAvailable},{redis.ifAvailable},metrics)
}
