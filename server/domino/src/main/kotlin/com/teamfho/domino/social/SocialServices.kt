package com.teamfho.domino.social

import java.security.SecureRandom
import java.time.Clock
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

class PublicPlayerIdentityService(private val repository: PublicIdentityRepository, private val generator: IdentityGenerator = SecureIdentityGenerator()) {
    fun ensure(uid: String): PublicPlayerIdentity {
        socialCheck(uid.isNotBlank() && uid.length<=128 && '/' !in uid && uid !in setOf(".",".."),"SOCIAL_ACTION_NOT_ALLOWED",403)
        repeat(5) { repository.ensure(uid,generator.next(uid))?.let { return it } }
        throw SocialFailure("SOCIAL_IDENTITY_UNAVAILABLE")
    }
}
/** Authenticated encryption hides candidate names/IDs and binds a cursor to account + query. */
class SocialCursor(secret: ByteArray = ByteArray(32).also(SecureRandom()::nextBytes), private val clock: Clock = Clock.systemUTC()) {
    private val key = SecretKeySpec(secret,"AES")
    fun encode(uid: String,query: String,name: String,id: String): String {
        val iv=ByteArray(12).also(SecureRandom()::nextBytes)
        val c=Cipher.getInstance("AES/GCM/NoPadding"); c.init(Cipher.ENCRYPT_MODE,key,GCMParameterSpec(128,iv))
        c.updateAAD("$uid\n$query".toByteArray())
        return Base64.getUrlEncoder().withoutPadding().encodeToString(iv+c.doFinal("${clock.instant().epochSecond+900}\n$name\n$id".toByteArray()))
    }
    fun decode(value: String?,uid: String,query: String): Pair<String,String>? {
        if(value==null)return null
        try {
            require(value.length<=1024)
            val b=Base64.getUrlDecoder().decode(value);require(b.size>28)
            val c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.DECRYPT_MODE,key,GCMParameterSpec(128,b.copyOfRange(0,12)))
            c.updateAAD("$uid\n$query".toByteArray())
            val fields=String(c.doFinal(b.copyOfRange(12,b.size))).split('\n')
            require(fields.size==3 && fields[0].toLong()>clock.instant().epochSecond)
            return fields[1] to fields[2]
        } catch(_:Exception) {throw SocialFailure("INVALID_SEARCH_QUERY",400)}
    }
}
class SocialAccess(private val profiles: PublicIdentityRepository, private val blocks: BlockRepository) {
    fun resolvePublicPlayer(id: String): SocialCandidate { SocialNames.publicId(id); return profiles.resolve(id) ?: throw SocialFailure("PLAYER_NOT_FOUND",404) }
    fun canDiscover(viewer: String,target: SocialCandidate) = viewer!=target.uid && !blocks.hasBlockEitherDirection(viewer,target.uid)
    fun visible(viewer: String,id: String): SocialCandidate {
        val target=resolvePublicPlayer(id)
        socialCheck(canDiscover(viewer,target),"PLAYER_NOT_FOUND",404)
        return target
    }
}
class SocialPrivacyService(private val identities: PublicPlayerIdentityService,private val repository: SocialPrivacyRepository) {
    fun getEffectiveSocialPrivacy(uid: String): SocialPrivacySettings { identities.ensure(uid);return repository.privacy(uid) }
    fun update(uid: String,patch: PrivacyPatch): SocialPrivacySettings {identities.ensure(uid);return repository.patchPrivacy(uid,patch)}
}
class PublicPlayerProfileService(private val identities: PublicPlayerIdentityService,private val profiles: PublicIdentityRepository,
    private val privacy: SocialPrivacyService,private val access: SocialAccess) {
    fun summary(uid: String): SocialSummary {
        val id=identities.ensure(uid)
        return SocialSummary(profiles.resolve(id.publicPlayerId)?.profile ?: throw SocialFailure("SOCIAL_IDENTITY_UNAVAILABLE"),privacy.getEffectiveSocialPrivacy(uid))
    }
    fun profile(uid: String,id: String)=access.visible(uid,id).profile
}
class PlayerDiscoveryService(private val repository: PublicIdentityRepository,private val privacy: SocialPrivacyRepository,
    private val access: SocialAccess,private val cursors: SocialCursor) {
    fun search(uid: String,mode: String,q: String?,code: String?,cursor: String?,limit: Int): SocialPage<PublicPlayerProfile> {
        socialCheck(limit in 1..50,"INVALID_SEARCH_QUERY")
        if(mode=="FRIEND_CODE") {
            socialCheck(q==null && cursor==null && code!=null,"INVALID_SEARCH_QUERY")
            val id=repository.code(SocialNames.code(code!!)) ?: return SocialPage(emptyList(),null)
            val target=repository.resolve(id)
            return SocialPage(if(target!=null && access.canDiscover(uid,target))listOf(target.profile)else emptyList(),null)
        }
        socialCheck(mode=="NAME" && q!=null && code==null,"INVALID_SEARCH_QUERY")
        val prefix=SocialNames.query(q!!);val after=cursors.decode(cursor,uid,prefix)
        val candidates=repository.search(prefix,after?.first,after?.second,minOf(limit*3,100))
        val output=mutableListOf<PublicPlayerProfile>();var scanned=0
        for(candidate in candidates.items) {
            scanned++
            // Recheck eligibility against authoritative account/test state, not only an old index projection.
            val current=repository.resolve(candidate.profile.publicPlayerId)
            if(current!=null && current.normalizedName.startsWith(prefix) && privacy.privacy(current.uid).discoverableByName && access.canDiscover(uid,current)) output.add(current.profile)
            if(output.size==limit)break
        }
        val last=candidates.items.getOrNull(scanned-1)
        val next=if(last!=null && (scanned<candidates.items.size || candidates.more))cursors.encode(uid,prefix,last.normalizedName,last.profile.publicPlayerId)else null
        return SocialPage(output,next)
    }
}
class BlockService(private val repository: BlockRepository,private val access: SocialAccess,private val cursors: SocialCursor) {
    fun hasBlockEitherDirection(a: String,b: String)=repository.hasBlockEitherDirection(a,b)
    fun set(uid: String,id: String,enabled: Boolean) {
        SocialNames.publicId(id)
        // Unblocking owns an existing safety record; it must still work if the target became ineligible.
        val target=if(enabled)access.resolvePublicPlayer(id) else repository.blockedTarget(uid,id)?:return
        socialCheck(uid!=target.uid,"SELF_RELATION_NOT_ALLOWED")
        // Deliberately does not call visible(): either side can block independently.
        repository.block(uid,target,enabled)
    }
    fun list(uid: String,cursor: String?,limit: Int): SocialPage<BlockRelationship> {
        socialCheck(limit in 1..50,"INVALID_SEARCH_QUERY")
        val after=cursors.decode(cursor,uid,"blocks")?.second
        val rows=repository.blocks(uid,after,limit+1)
        return SocialPage(rows.take(limit),if(rows.size>limit)cursors.encode(uid,"blocks","",rows[limit-1].publicPlayerId)else null)
    }
}
