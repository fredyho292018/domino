package com.teamfho.domino.entitlement

import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import java.time.Instant

interface EntitlementRepository {
    fun read(uid: String): EntitlementState
    fun trial(uid: String, now: Instant, policy: SubscriptionPolicy): Pair<EntitlementState, Boolean>
    fun adminGrant(uid: String, grant: EntitlementGrant): EntitlementState
}
data class PolicySnapshot(val policy: SubscriptionPolicy, val available: Boolean)
class SubscriptionPolicyService(private val read: () -> SubscriptionPolicy?,
    private val fallback: SubscriptionPolicy = SubscriptionPolicy(), private val clock: Clock = Clock.systemUTC(),
    val ttlSeconds: Long = 60) {
    private var cached: PolicySnapshot? = null
    private var refreshAt = Instant.MIN
    private var accepted: SubscriptionPolicy? = null
    @Synchronized fun resolve(): PolicySnapshot {
        val now = clock.instant()
        cached?.takeIf { now < refreshAt }?.let { return it }
        val next = try {
            val p = read()
            if(p != null) {
                require(accepted == null || p.policyVersion > accepted!!.policyVersion || p == accepted)
                accepted = p
            }
            PolicySnapshot(p ?: fallback, true)
        } catch (_: Exception) { PolicySnapshot(fallback.copy(promotionalTrialEnabled=false), false) }
        cached=next; refreshAt=now.plusSeconds(ttlSeconds); return next
    }
    @Synchronized fun invalidate() { refreshAt=Instant.MIN }
}

class EntitlementService(val policies: SubscriptionPolicyService, private val repository: EntitlementRepository,
    private val clock: Clock = Clock.systemUTC()) {
    private val log=org.slf4j.LoggerFactory.getLogger(javaClass)
    private val observedExpiry=object:LinkedHashMap<String,Instant>() {
        override fun removeEldestEntry(eldest:MutableMap.MutableEntry<String,Instant>)=size>2048
    }
    @Synchronized private fun resolved(state:EntitlementState,policy:SubscriptionPolicy,uid:String):EffectiveEntitlements {
        val result=EntitlementResolver.resolve(state,policy,clock.instant())
        val end=result.trialEndsAt
        if(end!=null && clock.instant()>=end && observedExpiry[uid]!=end) {
            observedExpiry[uid]=end
            log.info("[ENTITLEMENT] TRIAL_EXPIRED effective=true policyVersion={}",policy.policyVersion)
        }
        return result
    }
    // Bounded, short-lived account snapshots; time is always re-evaluated, never cache a boolean Premium.
    private val cache = object: LinkedHashMap<String,Pair<Instant,EntitlementState>>(16,.75f,true) {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String,Pair<Instant,EntitlementState>>)=size>2048
    }
    @Synchronized private fun state(uid: String): EntitlementState {
        cache[uid]?.takeIf { clock.instant() < it.first }?.let { return it.second }
        val state = try { repository.read(uid) } catch (_: Exception) { throw EntitlementFailure("ENTITLEMENTS_UNAVAILABLE") }
        cache[uid]=clock.instant().plusSeconds(30) to state
        return state
    }
    @Synchronized fun invalidate(uid: String) { cache.remove(uid) }
    fun resolve(uid: String): EffectiveEntitlements {
        val p=policies.resolve()
        if(!p.available) throw EntitlementFailure("ENTITLEMENTS_UNAVAILABLE")
        return resolved(state(uid),p.policy,uid)
    }
    fun summary(uid: String)=try { EntitlementSummary("AVAILABLE",resolve(uid)) }
        catch (_: Exception) { EntitlementSummary("UNAVAILABLE") }
    @Synchronized fun bootstrap(identity: FirebaseIdentity): EntitlementSummary = try {
        val p=policies.resolve()
        if(!p.available) throw EntitlementFailure("ENTITLEMENTS_UNAVAILABLE")
        val prior=state(identity.uid)
        val result=if(p.policy.enabled && p.policy.promotionalTrialEnabled && !prior.trialConsumed &&
            (!p.policy.trialRequiresLinkedAccount || !identity.isAnonymous))
            repository.trial(identity.uid,clock.instant(),p.policy) else prior to false
        cache[identity.uid]=clock.instant().plusSeconds(30) to result.first
        if(result.second)log.info("[ENTITLEMENT] TRIAL_GRANTED policyVersion={}",p.policy.policyVersion)
        EntitlementSummary("AVAILABLE",resolved(result.first,p.policy,identity.uid),result.second)
    } catch (_: Exception) { EntitlementSummary("UNAVAILABLE") }
    @Synchronized fun adminGrant(uid: String, grant: EntitlementGrant): EffectiveEntitlements {
        require(grant.source==EntitlementSource.ADMIN_GRANT)
        repository.adminGrant(uid,grant);cache.remove(uid)
        log.info("[ENTITLEMENT] ADMIN_GRANTED policyVersion={}",grant.policyVersion)
        return resolve(uid)
    }
    fun hasFeature(uid:String,feature:EntitlementFeature):Boolean {
        if(feature in SubscriptionPolicy.PUBLIC_FEATURES) return true
        return feature in resolve(uid).features
    }
    fun requireFeature(uid:String,feature:EntitlementFeature) {
        if(feature in SubscriptionPolicy.PUBLIC_FEATURES) return
        val value=resolve(uid)
        if(feature !in value.features) throw EntitlementFailure("FEATURE_NOT_ENTITLED",feature,value.plan)
    }
    fun limit(uid:String,key:EntitlementLimit)=resolve(uid).limits.getValue(key)
    fun requireCapacity(uid:String,key:EntitlementLimit,current:Int) {
        require(current>=0)
        val limit=limit(uid,key)
        if(!limit.unlimited && current>=limit.maximum!!) throw EntitlementFailure("LIMIT_REACHED",limitKey=key,current=current,maximum=limit.maximum)
    }
}
