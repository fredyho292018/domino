package com.teamfho.domino.social

import com.google.cloud.firestore.FieldValue
import com.teamfho.domino.match.MatchCodec

enum class SocialInvalidationType { PAIR_AUTHORIZATION_CHANGED, TARGET_PRIVACY_CHANGED }

/** Internal metadata only. A notification can revoke cached knowledge, never grant permission. */
data class SocialInvalidation(val eventId:String, val type:SocialInvalidationType,
    val revision:Long, val pairId:String?=null, val targetUid:String?=null, val schemaVersion:Int=1) {
    fun validate():SocialInvalidation {
        require(schemaVersion==1 && revision>0 && eventId.length in 1..200)
        when(type) {
            SocialInvalidationType.PAIR_AUTHORIZATION_CHANGED -> require(pairId!=null && Regex("[a-f0-9]{64}").matches(pairId) && targetUid==null)
            SocialInvalidationType.TARGET_PRIVACY_CHANGED -> require(targetUid!=null && targetUid.length in 1..128 && '/' !in targetUid && targetUid !in setOf(".","..") && pairId==null)
        }
        require(eventId==id(type,pairId?:targetUid!!,revision))
        return this
    }
    fun durableData():Map<String,Any> = MatchCodec.map(this)+mapOf("createdAt" to FieldValue.serverTimestamp(),"published" to false)
    companion object {
        const val COLLECTION="socialAuthorizationOutbox"
        private fun id(type:SocialInvalidationType,key:String,revision:Long)=
            "${if(type==SocialInvalidationType.PAIR_AUTHORIZATION_CHANGED)"pair" else "privacy"}-${SocialPairIdentity.id(key,key)}-$revision"
        fun pair(id:String,revision:Long)=SocialInvalidation(id(SocialInvalidationType.PAIR_AUTHORIZATION_CHANGED,id,revision),SocialInvalidationType.PAIR_AUTHORIZATION_CHANGED,revision,pairId=id)
        fun privacy(uid:String,revision:Long)=SocialInvalidation(id(SocialInvalidationType.TARGET_PRIVACY_CHANGED,uid,revision),SocialInvalidationType.TARGET_PRIVACY_CHANGED,revision,targetUid=uid)
        fun decode(data:Map<String,Any>)=MatchCodec.read(data-"createdAt"-"published",SocialInvalidation::class.java).validate()
    }
}

/** Invoked only AFTER a successful transaction, never from a retried transaction callback. */
fun interface SocialInvalidationSink { fun committed(events:List<SocialInvalidation>) }

internal fun SocialTransaction.invalidate(event:SocialInvalidation) {
    put("${SocialInvalidation.COLLECTION}/${event.eventId}",event.validate().durableData())
}
