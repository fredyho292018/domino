package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.EnumSource
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong
import kotlin.test.*

class SocialTestClock:SocialNanoClock {
    val value=AtomicLong()
    override fun now()=value.get()
    fun seconds(n:Long){value.addAndGet(TimeUnit.SECONDS.toNanos(n))}
}
class SocialRateGateTests {
    @ParameterizedTest @EnumSource(FallbackBucket::class)
    fun `tokens refill at exact policy rate and actors are independent`(bucket:FallbackBucket) {
        val clock=SocialTestClock();val l=LocalSocialLimiter(clock)
        repeat(bucket.burst){assertEquals(RateDecision.ALLOW,l.decide("a",bucket))}
        assertEquals(RateDecision.DENY_429,l.decide("a",bucket));assertEquals(RateDecision.ALLOW,l.decide("b",bucket))
        clock.seconds((60/bucket.perMinute).toLong());assertEquals(RateDecision.ALLOW,l.decide("a",bucket));assertEquals(RateDecision.DENY_429,l.decide("a",bucket))
    }
    @Test fun `concurrent isolated buckets never overshoot`() {
        val l=LocalSocialLimiter(SocialTestClock());val pool=Executors.newFixedThreadPool(12)
        try {
            val results=pool.invokeAll((1..100).flatMap{FallbackBucket.entries.map{b->Callable{b to l.decide("actor",b)}}}).map{it.get()}
            FallbackBucket.entries.forEach{b->assertEquals(b.burst,results.count{it.first==b && it.second==RateDecision.ALLOW})}
        } finally {pool.shutdownNow()}
    }
    @Test fun `capacity reserves safety expires inactive entries without active eviction`() {
        val clock=SocialTestClock();val l=LocalSocialLimiter(clock)
        repeat(9000){assertEquals(RateDecision.ALLOW,l.decide("r$it",FallbackBucket.READ))}
        assertEquals(RateDecision.UNAVAILABLE_503,l.decide("read-overflow",FallbackBucket.READ))
        repeat(1000){assertEquals(RateDecision.ALLOW,l.decide("s$it",FallbackBucket.SAFETY))}
        assertEquals(10000,l.size());assertEquals(RateDecision.UNAVAILABLE_503,l.decide("s-overflow",FallbackBucket.SAFETY))
        repeat(3){l.decide("r0",FallbackBucket.READ)};assertEquals(RateDecision.DENY_429,l.decide("r0",FallbackBucket.READ))
        val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->error("down")},l)
        assertEquals(RateDecision.ALLOW,gate.decide("overflow-block",SocialOperation.BLOCK))
        clock.seconds(299);l.decide("r0",FallbackBucket.READ);clock.seconds(2)
        assertEquals(RateDecision.ALLOW,l.decide("fresh",FallbackBucket.READ));assertEquals(2,l.size())
    }
    @Test fun `fallback is instance local and not global authority`() {
        val a=LocalSocialLimiter();val b=LocalSocialLimiter()
        repeat(4){a.decide("actor",FallbackBucket.READ)}
        assertEquals(RateDecision.DENY_429,a.decide("actor",FallbackBucket.READ));assertEquals(RateDecision.ALLOW,b.decide("actor",FallbackBucket.READ))
    }
    @ParameterizedTest @EnumSource(SocialOperation::class)
    fun `complete down policy operation matrix`(operation:SocialOperation) {
        val attempts=AtomicInteger();val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->attempts.incrementAndGet();throw java.net.ConnectException("test")})
        val expected=if(operation.fallback==null)RateDecision.UNAVAILABLE_503 else RateDecision.ALLOW
        assertEquals(expected,gate.decide("actor",operation))
        assertTrue(attempts.get()<=1)
    }
    @Test fun `valid distributed denial stays 429 without circuit or fallback`() {
        val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->false})
        repeat(10){assertEquals(RateDecision.DENY_429,gate.decide("actor",SocialOperation.PROFILE))}
        assertEquals(SocialRedisCircuit.State.CLOSED,gate.circuit.state);assertEquals(0,gate.local.size())
        assertEquals(429,assertFailsWith<SocialFailure>{gate.check("actor",SocialOperation.PROFILE)}.status)
    }
    @Test fun `circuit opens after three failures skips calls backs off and recovers`() {
        val clock=SocialTestClock();val circuit=SocialRedisCircuit(clock);val attempts=AtomicInteger();var healthy=false
        val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->attempts.incrementAndGet();if(!healthy)error("down");true},LocalSocialLimiter(clock),circuit)
        repeat(3){gate.decide("a",SocialOperation.SEARCH)};assertEquals(SocialRedisCircuit.State.OPEN,circuit.state)
        repeat(50){assertEquals(RateDecision.UNAVAILABLE_503,gate.decide("a",SocialOperation.SEARCH))};assertEquals(3,attempts.get())
        for(expected in listOf(10L,20L,30L,30L)){clock.seconds(circuit.openSeconds);gate.decide("a",SocialOperation.SEARCH);assertEquals(expected,circuit.openSeconds)}
        healthy=true;clock.seconds(30);assertEquals(RateDecision.ALLOW,gate.decide("a",SocialOperation.PROFILE));assertEquals(SocialRedisCircuit.State.CLOSED,circuit.state)
        val before=attempts.get();gate.decide("a",SocialOperation.PROFILE);assertEquals(before+1,attempts.get());assertEquals(0,gate.local.size())
    }
    @Test fun `one half open probe concurrent callers cannot storm dependency`() {
        val clock=SocialTestClock();val circuit=SocialRedisCircuit(clock)
        repeat(3){circuit.complete(circuit.enter()!!,false)};clock.seconds(5)
        val pool=Executors.newFixedThreadPool(10)
        try {val tickets=pool.invokeAll((1..100).map{Callable{circuit.enter()}}).mapNotNull{it.get()};assertEquals(1,tickets.size);circuit.complete(tickets.single(),false);assertEquals(10,circuit.openSeconds)}finally{pool.shutdownNow()}
    }
    @Test fun `stale parallel completion cannot close reopened circuit`() {
        val c=SocialRedisCircuit();val tickets=(1..4).map{c.enter()!!}
        tickets.take(3).forEach{c.complete(it,false)};c.complete(tickets.last(),true)
        assertEquals(SocialRedisCircuit.State.OPEN,c.state)
    }
    @Test fun `bounded worker timeout leaves at most two underlying calls and no waiting tasks`() {
        val gate=CountDownLatch(1);val entered=AtomicInteger()
        BoundedSocialRedis(DistributedSocialGate{_,_->entered.incrementAndGet();gate.await();true},50).use {adapter->
            try {
                repeat(2){val start=System.nanoTime();assertFails{adapter.allowed("a",SocialOperation.PROFILE)};assertTrue((System.nanoTime()-start)<TimeUnit.SECONDS.toNanos(1))}
                repeat(100){assertFailsWith<RejectedExecutionException>{adapter.allowed("a",SocialOperation.PROFILE)}}
                assertEquals(2,entered.get());assertEquals(2,adapter.activeWorkers());assertEquals(0,adapter.queuedTasks())
            } finally {gate.countDown()}
        }
    }
    @Test fun `privacy comparator all visibility boolean contact directions and mixed patches`() {
        for(old in SocialVisibility.entries)for(next in SocialVisibility.entries) {
            val c=SocialPrivacySettings(presenceVisibility=old,matchActivityVisibility=old)
            val expected=when {old==next->SocialOperation.PRIVACY_NOOP;next.ordinal<old.ordinal->SocialOperation.PRIVACY_LOOSEN;else->SocialOperation.PRIVACY_TIGHTEN}
            assertEquals(expected,PrivacyDirection.classify(c,PrivacyPatch(revision=1,presenceVisibility=next)))
            assertEquals(expected,PrivacyDirection.classify(c,PrivacyPatch(revision=1,matchActivityVisibility=next)))
        }
        val c=SocialPrivacySettings(discoverableByName=true)
        assertEquals(SocialOperation.PRIVACY_TIGHTEN,PrivacyDirection.classify(c,PrivacyPatch(false,1,ContactPermission.NO_ONE,ContactPermission.NO_ONE)))
        assertEquals(SocialOperation.PRIVACY_LOOSEN,PrivacyDirection.classify(c,PrivacyPatch(false,1,presenceVisibility=SocialVisibility.EVERYONE)))
        assertEquals(SocialOperation.PRIVACY_LOOSEN,PrivacyDirection.classify(c.copy(discoverableByName=false),PrivacyPatch(true,1)))
    }
    @Test fun `memory model and local timing samples are bounded estimates`() {
        for(size in listOf(100,1000,10000)) {
            val l=LocalSocialLimiter();val start=System.nanoTime()
            repeat(size){assertEquals(RateDecision.ALLOW,l.decide("u$it",FallbackBucket.SAFETY))}
            println("A2_LOCAL entries=$size estimatedBytes=${size*384L+65536} admissionTotalMicros=${(System.nanoTime()-start)/1000}")
            assertEquals(size,l.size())
        }
        val healthy=ResilientSocialRateGate(DistributedSocialGate{_,_->true});val down=ResilientSocialRateGate(DistributedSocialGate{_,_->error("down")})
        repeat(3){down.decide("a",SocialOperation.SEARCH)}
        for((name,g) in listOf("healthy_fake" to healthy,"open" to down)) {val start=System.nanoTime();repeat(1000){g.decide("a",SocialOperation.PROFILE)};println("A2_LATENCY $name avgNanos=${(System.nanoTime()-start)/1000}")}
    }
}
