package com.teamfho.domino.economy.reward

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import java.time.Instant
import java.util.concurrent.Executors
import java.util.concurrent.Callable
import kotlin.test.*

internal fun fallbackPolicyService(policy: MonetizationPolicy = MonetizationPolicy()) =
    MonetizationPolicyService(MonetizationPolicyRepository { PolicyRead.NotFound }, policy)

class PersistentMonetizationPolicyTests {
    private val clock = RewardClock(Instant.parse("2026-09-13T12:00:00Z"))
    private var calls = 0
    private var read: PolicyRead = PolicyRead.NotFound
    private val service = MonetizationPolicyService(MonetizationPolicyRepository { calls++; read }, MonetizationPolicy(), clock)
    private fun advance() { clock.value = clock.instant().plusSeconds(60) }
    private fun v2() = MonetizationPolicy(2, true, RewardedRules(coins=15,cooldownSeconds=180,maxPerHour=4,maxPerDay=12,maxCoinsPerDay=180))

    @Test fun `missing document uses H6 defaults and no write`() {
        val resolved=service.resolve()
        assertEquals(MonetizationPolicySource.FALLBACK_CONFIG,resolved.source)
        assertEquals(MonetizationPolicy(),resolved.policy)
    }
    @Test fun `active Firestore takes precedence over environment defaults`() {
        read=PolicyRead.Active(v2());val resolved=service.resolve()
        assertEquals(MonetizationPolicySource.FIRESTORE,resolved.source)
        assertEquals(v2(),resolved.policy)
    }
    @Test fun `cache expiry dynamically changes config without restart`() {
        read=PolicyRead.Active(MonetizationPolicy());service.resolve()
        read=PolicyRead.Active(v2());assertEquals(10L,service.resolve().policy.rewarded.coins);assertEquals(1,calls)
        advance();assertEquals(v2(),service.resolve().policy);assertEquals(2,calls)
        service.invalidate();service.resolve();assertEquals(3,calls)
    }
    @Test fun `outage retains cached policy and retries only once per ttl`() {
        read=PolicyRead.Active(v2());val cached=service.resolve();advance();read=PolicyRead.Unavailable
        assertEquals(cached,service.resolve());repeat(20){assertEquals(cached,service.resolve())};assertEquals(2,calls)
        advance();read=PolicyRead.Active(v2().copy(version=3,adsEnabled=false));assertFalse(service.resolve().policy.adsEnabled)
    }
    @Test fun `outage without cache uses fallback`() {read=PolicyRead.Unavailable;assertEquals(MonetizationPolicy(),service.resolve().policy)}
    @Test fun `malformed policy replaces cached policy with fallback`() {
        read=PolicyRead.Active(v2());service.resolve();advance();read=PolicyRead.Invalid("DOCUMENT_CONTRACT")
        assertEquals(MonetizationPolicySource.FALLBACK_CONFIG,service.resolve().source)
    }
    @Test fun `same version different business values rejected and lower version rejected`() {
        read=PolicyRead.Active(v2());service.resolve();advance()
        read=PolicyRead.Active(v2().copy(adsEnabled=false));assertEquals(MonetizationPolicySource.FALLBACK_CONFIG,service.resolve().source)
        advance();read=PolicyRead.Active(MonetizationPolicy());assertEquals(MonetizationPolicySource.FALLBACK_CONFIG,service.resolve().source)
        advance();read=PolicyRead.Active(v2().copy(version=3,adsEnabled=false));assertFalse(service.resolve().policy.adsEnabled)
    }
    @Test fun `parallel config requests share one Firestore read`() {
        read=PolicyRead.Active(v2());val pool=Executors.newFixedThreadPool(8)
        try { val results=pool.invokeAll((1..30).map { Callable { service.resolve() } });assertTrue(results.all { it.get().policy==v2() });assertEquals(1,calls) }
        finally { pool.shutdownNow() }
    }
    @Test fun `cache ttl configurable and bounded`() {
        assertFailsWith<IllegalArgumentException>{MonetizationPolicyCacheSettings(0)}
        val custom=MonetizationPolicyService(MonetizationPolicyRepository { calls++;read },MonetizationPolicy(),clock,MonetizationPolicyCacheSettings(5))
        custom.resolve();clock.value=clock.instant().plusSeconds(5);custom.resolve();assertEquals(2,calls)
    }
    private val db=mock(Firestore::class.java)
    private val adapter=FirestoreMonetizationPolicyRepository(db)
    private fun document(): Map<String,Any> = mapOf("version" to 2L,"adsEnabled" to true,"rewardedEnabled" to true,
        "rewardCoins" to 15L,"maxPerRound" to 1L,"cooldownSeconds" to 180L,"maxPerHour" to 4L,
        "maxPerDay" to 12L,"maxCoinsPerDay" to 180L,"status" to "ACTIVE")
    @Test fun `strict document maps to immutable policy`() {assertEquals(PolicyRead.Active(v2()),adapter.decode(document()))}
    @ParameterizedTest @ValueSource(strings=["zero","round","cap","negative","float","missing","status","bool","overflow","version","metadata"])
    fun `invalid document is distinguished from absence and outage`(kind:String) {
        val data=document().toMutableMap()
        when(kind){
            "zero"->data["rewardCoins"]=0L;"round"->data["maxPerRound"]=2L;"cap"->data["maxCoinsPerDay"]=10L
            "negative"->data["cooldownSeconds"]=-1L;"float"->data["rewardCoins"]=15.0;"missing"->data.remove("maxPerHour")
            "status"->data["status"]="DRAFT";"bool"->data["adsEnabled"]="true";"overflow"->data["maxPerHour"]=Long.MAX_VALUE
            "version"->data["version"]=0L;"metadata"->data["updatedBy"]="client"
        }
        assertIs<PolicyRead.Invalid>(adapter.decode(data))
    }
    @Test fun `repository reads exact server document and distinguishes outcomes`() {
        val ref=mock(DocumentReference::class.java);val snapshot=mock(DocumentSnapshot::class.java)
        `when`(db.document("systemConfig/monetization")).thenReturn(ref)
        `when`(ref.get()).thenReturn(ApiFutures.immediateFuture(snapshot))
        assertEquals(PolicyRead.NotFound,adapter.read())
        `when`(snapshot.exists()).thenReturn(true);`when`(snapshot.data).thenReturn(document())
        assertEquals(PolicyRead.Active(v2()),adapter.read())
        `when`(ref.get()).thenReturn(ApiFutures.immediateFailedFuture(RuntimeException("not logged")))
        assertEquals(PolicyRead.Unavailable,adapter.read())
        verify(ref,never()).set(anyMap<String,Any>())
    }
    @Test fun `seed uses create precondition and never overwrites`() {
        val ref=mock(DocumentReference::class.java)
        `when`(db.document("systemConfig/monetization")).thenReturn(ref)
        `when`(ref.create(anyMap<String,Any>())).thenReturn(ApiFutures.immediateFuture(mock(WriteResult::class.java)))
        assertTrue(adapter.seedIfAbsent(MonetizationPolicy()))
        `when`(ref.create(anyMap<String,Any>())).thenReturn(ApiFutures.immediateFailedFuture(mock(com.google.api.gax.rpc.AlreadyExistsException::class.java)))
        assertFalse(adapter.seedIfAbsent(v2()))
        verify(ref,never()).set(anyMap<String,Any>())
        verify(ref,never()).update(anyMap<String,Any>())
    }
    @Test fun `seed configuration binds existing yaml placeholders without Firebase initialization`() {
        org.springframework.boot.builder.SpringApplicationBuilder(MonetizationSeedConfiguration::class.java)
            .web(org.springframework.boot.WebApplicationType.NONE).logStartupInfo(false)
            .run("--firebase.project-id=domino-test","--DOMINO_MONETIZATION_REWARD_COINS=17","--DOMINO_MONETIZATION_POLICY_CACHE_SECONDS=7").use { context ->
                assertEquals(17L,context.getBean(MonetizationPolicy::class.java).rewarded.coins)
                assertEquals(7L,context.getBean(MonetizationPolicyCacheSettings::class.java).cacheSeconds)
                assertTrue(context.getBeansOfType(Firestore::class.java).isEmpty())
            }
    }
}
