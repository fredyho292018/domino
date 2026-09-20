package com.teamfho.domino.entitlement

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.match.MatchIds
import java.time.Instant
import java.time.temporal.ChronoUnit
import java.util.concurrent.TimeUnit

class FirestoreEntitlements(private val db: Firestore): EntitlementRepository {
    private fun root(uid:String)=db.document("players/${MatchIds.document(uid)}/entitlementState/current")
    private fun decode(data:Map<String,Any>?)=data?.let { MatchCodec.read(it,EntitlementState::class.java) } ?: EntitlementState()
    override fun read(uid:String)=decode(root(uid).get().get(5,TimeUnit.SECONDS).data)
    override fun trial(uid:String,now:Instant,policy:SubscriptionPolicy):Pair<EntitlementState,Boolean> {
        val player=db.document("players/${MatchIds.document(uid)}")
        val marker=db.document("developmentTestAccounts/$uid")
        val promotion=db.document("players/$uid/promotions/initial-premium-trial")
        val grantRef=db.document("players/$uid/entitlementGrants/initial-premium-trial")
        return db.runTransaction { tx ->
            val state=decode(tx.get(root(uid)).get().data)
            if(state.trialConsumed) state to false else {
                val p=tx.get(player).get()
                val test=tx.get(marker).get()
                val previous=tx.get(promotion).get()
                val oldGrant=tx.get(grantRef).get()
                check(p.exists())
                if(test.getBoolean("isTestAccount")==true || !policy.enabled || !policy.promotionalTrialEnabled ||
                    (policy.trialRequiresLinkedAccount && p.getString("accountType")!="REGISTERED")) state to false
                else {
                    // An existing immutable marker is never replaced, even after projection repair.
                    val grant=if(previous.exists() || oldGrant.exists()) {
                        check(oldGrant.exists());MatchCodec.read(oldGrant.data!!,EntitlementGrant::class.java)
                    } else EntitlementGrant("initial-premium-trial",EntitlementSource.PROMOTIONAL_TRIAL,
                        validFrom=now,validUntil=now.plus(policy.promotionalTrialDays.toLong(),ChronoUnit.DAYS),
                        createdAt=now,policyVersion=policy.policyVersion,reason="WELCOME_PROMOTION",grantedBy="bootstrap")
                    val next=state.copy(revision=state.revision+1,trialConsumed=true,grants=state.grants.filter{it.id!=grant.id}+grant)
                    tx.set(root(uid),MatchCodec.map(next))
                    if(!previous.exists()) tx.create(promotion,mapOf("trialConsumed" to true,"grantId" to grant.id,"trialGrantedAt" to grant.validFrom.toString(),"trialEndsAt" to grant.validUntil.toString()))
                    if(!oldGrant.exists()) {
                        tx.create(grantRef,MatchCodec.map(grant))
                        tx.create(db.document("players/$uid/entitlementAudit/TRIAL_GRANTED"),mapOf("type" to "TRIAL_GRANTED","at" to now.toString(),"grantId" to grant.id))
                    }
                    next to !oldGrant.exists()
                }
            }
        }.get(15,TimeUnit.SECONDS)
    }
    override fun adminGrant(uid:String,grant:EntitlementGrant):EntitlementState {
        require(grant.source==EntitlementSource.ADMIN_GRANT)
        MatchIds.document(grant.id)
        return db.runTransaction {tx ->
            val state=decode(tx.get(root(uid)).get().data)
            val ref=db.document("players/$uid/entitlementGrants/${grant.id}")
            val existing=tx.get(ref).get()
            if(existing.exists()) {check(MatchCodec.read(existing.data!!,EntitlementGrant::class.java)==grant);state}
            else {
                // Keep the projection bounded; expired evidence stays in immutable grant documents.
                val retained=state.grants.filter {it.source==EntitlementSource.PROMOTIONAL_TRIAL || it.validUntil>grant.createdAt}
                require(retained.size<64)
                val next=state.copy(revision=state.revision+1,grants=retained+grant)
                tx.create(ref,MatchCodec.map(grant));tx.set(root(uid),MatchCodec.map(next))
                tx.create(db.document("players/$uid/entitlementAudit/admin-${grant.id}"),mapOf("type" to "ADMIN_GRANTED","at" to grant.createdAt.toString(),"reason" to grant.reason,"grantedBy" to grant.grantedBy,"grantId" to grant.id))
                next
            }
        }.get(15,TimeUnit.SECONDS)
    }
}
