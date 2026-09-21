package com.teamfho.domino.social

import com.teamfho.domino.catalog.GameCatalogCodec
import org.springframework.data.redis.connection.*
import org.springframework.data.redis.listener.*
import org.springframework.util.backoff.ExponentialBackOff
import java.util.concurrent.*

/** Ephemeral hints only. Consumers read current Redis state; payloads never reach a client. */
class SocialPresencePubSub(factory:RedisConnectionFactory,changed:(String)->Unit,recovered:()->Unit):AutoCloseable {
    companion object {const val CHANNEL="domino:v1:social:presence-changed"}
    private val delivery=ThreadPoolExecutor(1,1,0,TimeUnit.MILLISECONDS,ArrayBlockingQueue(128),
        ThreadFactory{r->Thread(r,"presence-pubsub-delivery").also{it.isDaemon=true}},ThreadPoolExecutor.AbortPolicy())
    private val subscription=Executors.newSingleThreadExecutor{r->Thread(r,"presence-pubsub-subscription").also{it.isDaemon=true}}
    private val container=RedisMessageListenerContainer().apply {
        setConnectionFactory(factory);setTaskExecutor(delivery);setSubscriptionExecutor(subscription)
        setRecoveryBackoff(ExponentialBackOff(1000,2.0).also{it.maxInterval=30_000})
        setErrorHandler{recovered()}
        addMessageListener(object:MessageListener,SubscriptionListener {
            override fun onMessage(message:Message,pattern:ByteArray?) {
                try {
                    require(message.body.size<=1024)
                    val root=GameCatalogCodec.mapper.readTree(message.body)
                    require(root.isObject && root.size()==2 && root.path("version").isIntegralNumber && root.path("version").asInt()==1 && root.path("uid").isString)
                    val uid=root.path("uid").asString()
                    require(uid.isNotBlank() && uid.length<=128)
                    changed(uid)
                }catch(_:Exception){recovered()}
            }
            override fun onChannelSubscribed(channel:ByteArray,count:Long){recovered()}
        },ChannelTopic(CHANNEL))
        afterPropertiesSet()
    }
    fun start()=container.start()
    override fun close(){container.stop();container.destroy();delivery.shutdownNow();subscription.shutdownNow()}
}
