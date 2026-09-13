package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.time.Instant
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import kotlin.test.*

class MonetizationPolicyTests {
    private val clock=RewardClock(Instant.parse("2026-09-12T00:00:00Z"))
    private val store=RewardFirestoreTransactions(clock.instant())
    private fun repo(policy:MonetizationPolicy=MonetizationPolicy())=FirestoreRewardIntentRepository(store.firestore,clock,RewardPolicy(),policy)
    private fun time(seconds:Long){clock.value=clock.instant().plusSeconds(seconds);store.now=clock.instant()}
    private fun stamp(at:Instant=clock.instant())=Timestamp.ofTimeSecondsAndNanos(at.epochSecond,at.nano)
    init { store.documents["players/u/wallet/main"]=mapOf("coins" to 0L,"lifetimeCoinsEarned" to 0L,"lifetimeCoinsSpent" to 0L,"createdAt" to stamp(),"updatedAt" to stamp()) }
    private fun verified(repository:FirestoreRewardIntentRepository=repo()):RewardIntent {
        val value=repository.issue("u");repository.verify(VerifiedAdMobEvent(value.intentId,value.intentId,"5224354917",clock.instant()));return value
    }
    private fun credit(repository:FirestoreRewardIntentRepository=repo()):RewardIntent {val value=verified(repository);repository.consume("u",value.intentId);return value}
    @Test fun `typed defaults and safe response`() {
        val p=MonetizationPolicy();assertEquals(10,p.rewarded.coins);assertEquals(1,p.rewarded.maxPerRound)
        assertEquals(120,p.rewarded.cooldownSeconds);assertEquals(5,p.rewarded.maxPerHour)
        assertEquals(20,p.rewarded.maxPerDay);assertEquals(200,p.rewarded.maxCoinsPerDay)
        assertTrue(p.response().adsEnabled);assertEquals(1,p.version)
    }
    @ParameterizedTest @ValueSource(strings=["zero","negative","version","round","hour","day","cap","cooldown","overflow"])
    fun `invalid properties fail fast`(variant:String) {
        assertFailsWith<IllegalArgumentException>{when(variant){
            "zero"->RewardedRules(coins=0);"negative"->RewardedRules(coins=-1);"version"->MonetizationPolicy(version=0)
            "round"->RewardedRules(maxPerRound=0);"hour"->RewardedRules(maxPerHour=0);"day"->RewardedRules(maxPerDay=0)
            "cap"->RewardedRules(coins=15,maxCoinsPerDay=10);"cooldown"->RewardedRules(cooldownSeconds=-1)
            else->RewardedRules(coins=Long.MAX_VALUE)
        }}
    }
    @Test fun `cooldown begins only on consumption and reaches exact 120 boundary`() {
        val r=repo();val v=verified(r);assertTrue(r.eligibility("u").eligible)
        r.consume("u",v.intentId);assertEquals(EligibilityReason.COOLDOWN,r.eligibility("u").reason)
        time(119);assertFalse(r.eligibility("u").eligible);time(1);assertTrue(r.eligibility("u").eligible)
    }
    @Test fun `rolling hour does not reset at clock hour`() {
        time(15*60);val r=repo()
        repeat(5){credit(r);time(10*60)}
        assertEquals(EligibilityReason.HOURLY_LIMIT,r.eligibility("u").reason)
        time(10*60);assertTrue(r.eligibility("u").eligible)
    }
    @Test fun `daily UTC twenty and midnight reset`() {
        val r=repo(MonetizationPolicy(rewarded=RewardedRules(cooldownSeconds=0,maxPerHour=1000)))
        repeat(20){credit(r);time(1)}
        assertEquals(EligibilityReason.DAILY_LIMIT,r.eligibility("u").reason)
        time(86400-20);assertTrue(r.eligibility("u").eligible)
    }
    @Test fun `fifteen coins cap does not partially grant`() {
        val r=repo(MonetizationPolicy(rewarded=RewardedRules(coins=15,cooldownSeconds=0,maxPerHour=1000,maxPerDay=1000)))
        repeat(13){credit(r);time(1)}
        assertEquals(EligibilityReason.DAILY_COIN_LIMIT,r.eligibility("u").reason)
        assertEquals(195L,store.documents["players/u/wallet/main"]?.get("coins"))
        assertEquals("DAILY_COIN_LIMIT",assertFailsWith<RewardFailure>{r.issue("u")}.category)
    }
    @Test fun `one server opportunity is reused until used and rejects arbitrary foreign context`() {
        val r=repo();val a=r.opportunity("u");assertEquals(a,r.opportunity("u"))
        val v=credit(r);assertEquals(a.opportunityId,v.opportunityId)
        assertEquals(EligibilityReason.ROUND_LIMIT,r.eligibility("u",a.opportunityId).reason)
        assertEquals(EligibilityReason.ROUND_LIMIT,r.eligibility("other",a.opportunityId).reason)
        time(120);assertNotEquals(a.opportunityId,r.opportunity("u").opportunityId)
    }
    @Test fun `expired opportunity cannot masquerade as a completed round`() {
        val r=repo();val a=r.opportunity("u");time(601)
        assertEquals(EligibilityReason.ROUND_LIMIT,r.eligibility("u",a.opportunityId).reason)
    }
    @ParameterizedTest @ValueSource(booleans=[true,false])
    fun `kill switches prevent issuance but preserve verified credit`(ads:Boolean) {
        val v=verified();val disabled=repo(MonetizationPolicy(adsEnabled=!ads,rewarded=RewardedRules(enabled=ads)))
        assertFalse(disabled.eligibility("u").eligible)
        assertFailsWith<RewardFailure>{disabled.issue("u")}
        assertEquals(10L,disabled.consume("u",v.intentId).reward.amount)
    }
    @Test fun `old snapshot stays ten new intent fifteen and version changes`() {
        val v=verified();val changed=repo(MonetizationPolicy(version=2,rewarded=RewardedRules(coins=15,cooldownSeconds=0)))
        assertEquals(10L,changed.consume("u",v.intentId).reward.amount)
        val next=verified(changed);assertEquals(15L,next.rewardAmountSnapshot);assertEquals(2L,next.policyVersion)
        assertEquals(25L,changed.consume("u",next.intentId).wallet.coins)
    }
    @Test fun `duplicate consume does not duplicate quota and survives repository restart`() {
        val v=credit();repeat(5){repo().consume("u",v.intentId)}
        assertEquals(4,repo().eligibility("u").remaining.hour)
        assertEquals(19,repo().eligibility("u").remaining.day)
        assertEquals(190,repo().eligibility("u").remaining.coinsToday)
    }
    @Test fun `legacy ledger is counted but other credit sources are not`() {
        store.documents["players/u/walletTransactions/legacy"]=mapOf("type" to "CREDIT","source" to "REWARDED_AD","createdAt" to stamp(),"amount" to 10L)
        store.documents["players/u/walletTransactions/purchase"]=mapOf("type" to "CREDIT","source" to "PURCHASE","createdAt" to stamp(),"amount" to 10000L)
        val e=repo().eligibility("u");assertEquals(EligibilityReason.COOLDOWN,e.reason);assertEquals(190,e.remaining.coinsToday)
    }
    @Test fun `two durable verified intents race at hourly boundary without overshoot`() {
        val r=repo(MonetizationPolicy(rewarded=RewardedRules(cooldownSeconds=0)))
        repeat(4){credit(r);time(1)}
        val a=verified(r)
        // Simulate two previously issued durable grants, not a production verification bypass.
        val b=UUID.randomUUID().toString();val opp=UUID.randomUUID().toString()
        store.documents["rewardIntents/$b"]=store.documents.getValue("rewardIntents/${a.intentId}") + mapOf("intentId" to b,"opportunityId" to opp)
        store.documents["rewardOpportunities/$opp"]=store.documents.getValue("rewardOpportunities/${a.opportunityId}") + mapOf("opportunityId" to opp)
        val pool=Executors.newFixedThreadPool(2)
        try { val results=pool.invokeAll(listOf(a.intentId,b).map { id->Callable {runCatching{r.consume("u",id)} } }).map{it.get()}
            assertEquals(1,results.count{it.isSuccess});assertEquals("HOURLY_LIMIT",(results.single{it.isFailure}.exceptionOrNull() as RewardFailure).category)
        } finally{pool.shutdownNow()}
        assertEquals(50L,store.documents["players/u/wallet/main"]?.get("coins"))
        assertEquals(5,store.documents.keys.count{it.contains("/walletTransactions/")})
        assertEquals(0,r.eligibility("u").remaining.hour)
    }
    @Test fun `rate gate is bounded and resets with fake server clock`() {
        val gate=MonetizationRequestGate(clock);repeat(60){gate.check("u")}
        assertEquals(429,assertFailsWith<RewardFailure>{gate.check("u")}.httpStatus)
        time(60);gate.check("u")
    }
}
