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

