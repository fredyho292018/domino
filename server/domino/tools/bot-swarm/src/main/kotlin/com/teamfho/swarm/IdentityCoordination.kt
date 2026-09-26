package com.teamfho.swarm

import java.security.MessageDigest
import java.util.Base64
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

// Ephemeral proof only: never a credential or an authenticated backend identity.
data class IdentityProof(val session:String,val project:String,val scope:String,val ready:Boolean,val fingerprint:String)
object IdentityCoordination {
    fun session(nonce:ByteArray)=Base64.getEncoder().encodeToString(MessageDigest.getInstance("SHA-256").digest(nonce))
    fun proof(nonce:ByteArray,uid:String,project:String="teamfho-domino",scope:String="TEST",ready:Boolean=true):IdentityProof {
        require(nonce.size==32 && uid.isNotBlank())
        val mac=Mac.getInstance("HmacSHA256");mac.init(SecretKeySpec(nonce,"HmacSHA256"))
        return IdentityProof(session(nonce),project,scope,ready,Base64.getEncoder().encodeToString(mac.doFinal(uid.toByteArray(Charsets.UTF_8))))
    }
    fun compare(session:String,proofs:List<IdentityProof>):Map<String,String> {
        require(proofs.size==4)
        proofs.forEach {
            require(it.session==session && it.project=="teamfho-domino" && it.scope=="TEST" && it.ready)
            require(Base64.getDecoder().decode(it.fingerprint).size==32)
        }
        val names=listOf("UNITY","SLOT_01","SLOT_02","SLOT_03")
        val result=linkedMapOf<String,String>()
        for(i in 0..3)for(j in i+1..3)result["${names[i]}_EQUALS_${names[j]}"]=if(proofs[i].fingerprint==proofs[j].fingerprint)"YES" else "NO"
        val distinct=proofs.map{it.fingerprint}.distinct().size
        result["DISTINCT_PLAYER_COUNT"]=distinct.toString()
        result["DISTINCT_MATCH_PLAYERS"]=if(distinct==4)"YES" else "NO"
        return result
    }
}
