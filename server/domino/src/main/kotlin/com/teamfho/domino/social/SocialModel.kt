package com.teamfho.domino.social

import java.security.SecureRandom
import java.text.Normalizer
import java.util.Locale

class SocialFailure(val code: String, val status: Int = 503) : RuntimeException(code)
fun socialCheck(ok: Boolean, code: String, status: Int = 400) { if (!ok) throw SocialFailure(code, status) }
data class PublicPlayerIdentity(val uid: String, val publicPlayerId: String, val friendCode: String)
data class PublicPlayerProfile(val publicPlayerId: String, val friendCode: String, val displayName: String, val avatarKey: String? = null)
enum class ContactPermission { EVERYONE, NO_ONE }
enum class SocialVisibility { EVERYONE, FRIENDS, NO_ONE }
data class SocialPrivacySettings(val discoverableByName: Boolean = false,
    val friendRequests: ContactPermission = ContactPermission.EVERYONE,
    val follow: ContactPermission = ContactPermission.EVERYONE,
    val presenceVisibility: SocialVisibility = SocialVisibility.FRIENDS,
    val matchActivityVisibility: SocialVisibility = SocialVisibility.FRIENDS, val revision: Long = 1)
data class PrivacyPatch(val discoverableByName: Boolean, val revision: Long)
data class SocialSummary(val profile: PublicPlayerProfile, val privacy: SocialPrivacySettings)
data class BlockRelationship(val publicPlayerId: String, val displayName: String, val friendCode: String)
data class SocialPage<T>(val items: List<T>, val nextCursor: String?)
data class SocialCandidate(val uid: String, val profile: PublicPlayerProfile, val normalizedName: String)
data class CandidatePage(val items: List<SocialCandidate>, val more: Boolean)
interface IdentityGenerator { fun next(uid: String): PublicPlayerIdentity }
class SecureIdentityGenerator : IdentityGenerator {
    private val random = SecureRandom()
    override fun next(uid: String): PublicPlayerIdentity {
        val bytes = ByteArray(16).also(random::nextBytes)
        val id = java.util.Base64.getUrlEncoder().withoutPadding().encodeToString(bytes)
        return PublicPlayerIdentity(uid, id, "FHO-" + (1..12).map { SocialNames.alphabet[random.nextInt(32)] }.joinToString(""))
    }
}
object SocialNames {
    const val alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"
    fun normalize(value: String) = Normalizer.normalize(value.trim(), Normalizer.Form.NFKC).lowercase(Locale.ROOT)
    fun query(value: String): String {
        val result = normalize(value)
        socialCheck(Regex("[a-z0-9_-]{3,16}").matches(result), "INVALID_SEARCH_QUERY")
        return result
    }
    fun code(value: String): String {
        val result = value.trim().uppercase(Locale.ROOT)
        socialCheck(Regex("FHO-[${alphabet}]{12}").matches(result), "INVALID_FRIEND_CODE")
        return result
    }
    fun publicId(value: String) { socialCheck(Regex("[A-Za-z0-9_-]{22}").matches(value), "PLAYER_NOT_FOUND", 404) }
}

interface PublicIdentityRepository {
    fun ensure(uid: String, candidate: PublicPlayerIdentity): PublicPlayerIdentity?
    fun resolve(publicId: String): SocialCandidate?
    fun code(code: String): String?
    fun search(prefix: String, afterName: String?, afterId: String?, budget: Int): CandidatePage
}
interface SocialPrivacyRepository {
    fun privacy(uid: String): SocialPrivacySettings
    fun patchPrivacy(uid: String, patch: PrivacyPatch): SocialPrivacySettings
}
interface BlockRepository {
    fun blockedTarget(a: String, publicId: String): SocialCandidate?
    fun hasBlockEitherDirection(a: String, b: String): Boolean
    fun block(a: String, target: SocialCandidate, enabled: Boolean)
    fun blocks(a: String, afterId: String?, limit: Int): List<BlockRelationship>
}
