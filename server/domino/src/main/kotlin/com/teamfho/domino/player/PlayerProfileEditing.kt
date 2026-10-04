package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

data class PlayerProfileEditRequest(val firstName:String,val lastName:String,val displayName:String,
    val country:String,val preferredLanguage:String,val expectedProfileRevision:Long,val expectedPreferencesRevision:Long)
data class PlayerProfileEditResponse(val firstName:String?,val lastName:String?,val displayName:String,
    val country:String?,val preferredLanguage:String,val profileRevision:Long,val preferencesRevision:Long)

/** Profile editing does not read or write onboarding state. The existing repository provides atomic buffered transactions. */
class PlayerProfileEditingService(private val repository:OnboardingProgressRepository,private val clock:Clock=Clock.systemUTC()) {
    private fun root(identity:FirebaseIdentity):String {
        onboardingCheck(identity.uid.isNotBlank() && identity.uid.length<=128 && '/' !in identity.uid && identity.uid !in setOf(".",".."),"REQUEST_INVALID",400)
        return "players/${identity.uid}"
    }
    private fun response(p:Player,prefs:PlayerPreferences)=PlayerProfileEditResponse(p.firstName,p.lastName,p.displayName,p.countryCode,prefs.preferredLocale,p.profileRevision,prefs.revision)
    fun get(identity:FirebaseIdentity)=repository.transaction { tx ->
        val path=root(identity);val raw=tx.read(path)?:throw OnboardingFailure("PLAYER_NOT_FOUND",404)
        onboardingCheck(raw["status"]=="ACTIVE","PLAYER_NOT_ACTIVE",403)
        response(FirestoreFoundationMapping.player(raw,identity.uid),FoundationDocumentCodec.decode(tx.read("$path/preferences/current")?:throw OnboardingFailure("PLAYER_STATE_CONFLICT"),PlayerPreferences::class.java))
    }
    fun update(identity:FirebaseIdentity,r:PlayerProfileEditRequest):PlayerProfileEditResponse {
        fun field(code:String,block:()->String):String=try{block()}catch(e:Exception){throw OnboardingFailure(code,400)}
        val first=field("FIRST_NAME_INVALID"){requireNotNull(PlayerProfileRules.name(r.firstName))}
        val last=field("LAST_NAME_INVALID"){requireNotNull(PlayerProfileRules.name(r.lastName))}
        val country=field("COUNTRY_INVALID"){requireNotNull(PlayerProfileRules.country(r.country))}
        val language=field("LANGUAGE_UNSUPPORTED"){PlayerProfileRules.locale(r.preferredLanguage)}
        val alias=try{DisplayNameRules.validate(r.displayName)}catch(e:DisplayNameException){throw OnboardingFailure(if(e.reserved)"DISPLAY_NAME_RESERVED" else "DISPLAY_NAME_INVALID",400)}
        onboardingCheck(r.expectedProfileRevision>=0 && r.expectedPreferencesRevision>=0,"REQUEST_INVALID",400)
        return repository.transaction {tx ->
            val path=root(identity);val raw=tx.read(path)?:throw OnboardingFailure("PLAYER_NOT_FOUND",404)
            onboardingCheck(raw["status"]=="ACTIVE","PLAYER_NOT_ACTIVE",403)
            val p=FirestoreFoundationMapping.player(raw,identity.uid)
            val prefs=FoundationDocumentCodec.decode(tx.read("$path/preferences/current")?:throw OnboardingFailure("PLAYER_STATE_CONFLICT"),PlayerPreferences::class.java)
            val unchanged=p.firstName==first && p.lastName==last && p.displayName==alias && p.countryCode==country && prefs.preferredLocale==language && p.language==language
            // A retry of the same confirmed result is a read-only success, even with old revision tokens.
            if(unchanged)return@transaction response(p,prefs)
            onboardingCheck(p.profileRevision==r.expectedProfileRevision && prefs.revision==r.expectedPreferencesRevision,"REVISION_MISMATCH")
            val reservations=try{PlayerAliasReservations.prepare(tx::read,identity.uid,p.displayName,alias)}catch(e:PlayerFoundationException){throw OnboardingFailure(e.code.name)}
            val publicId=if(alias!=p.displayName)tx.read("$path/publicIdentity/current")?.get("publicPlayerId") as? String else null
            if(publicId!=null)onboardingCheck(Regex("[A-Za-z0-9_-]{1,128}").matches(publicId),"DEPENDENCY_UNAVAILABLE",503)
            val public=publicId?.let{tx.read("publicPlayerProfiles/$it")?:throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)}
            val now=clock.instant();val stamp=com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,now.nano)
            val next=p.copy(firstName=first,lastName=last,displayName=alias,countryCode=country,language=language,profileRevision=Math.addExact(p.profileRevision,1))
            val nextPrefs=if(prefs.preferredLocale==language)prefs else prefs.copy(preferredLocale=language,revision=Math.addExact(prefs.revision,1),updatedAt=now)
            reservations.forEach{(key,value)->tx.write(key,value)}
            tx.write(path,raw+mapOf("firstName" to first,"lastName" to last,"displayName" to alias,"countryCode" to country,"language" to language,"profileRevision" to next.profileRevision,"updatedAt" to stamp))
            if(nextPrefs!=prefs)tx.write("$path/preferences/current",FoundationDocumentCodec.encode(nextPrefs))
            if(public!=null)tx.write("publicPlayerProfiles/$publicId",public+mapOf("displayName" to alias,"normalizedDisplayName" to com.teamfho.domino.social.SocialNames.normalize(alias),"updatedAt" to stamp))
            response(next,nextPrefs)
        }
    }
}
@Configuration(proxyBeanMethods=false)
class PlayerProfileEditingConfiguration {
    @Bean fun playerProfileEditingService(repository:OnboardingProgressRepository)=PlayerProfileEditingService(repository)
}
@RestController
@RequestMapping("/api/v1/player/profile")
class PlayerProfileEditingController(private val service:PlayerProfileEditingService) {
    @GetMapping("/editable") fun get(@AuthenticationPrincipal identity:FirebaseIdentity)=ResponseEntity.ok().header("Cache-Control","no-store").body(service.get(identity))
    @PutMapping(consumes=["application/json"]) fun update(@AuthenticationPrincipal identity:FirebaseIdentity,request:jakarta.servlet.http.HttpServletRequest):ResponseEntity<PlayerProfileEditResponse> {
        val value=try {
            val bytes=request.inputStream.readNBytes(8193);require(bytes.size<=8192)
            val mapper=com.teamfho.domino.catalog.GameCatalogCodec.mapper;val tree=mapper.readTree(bytes)
            for(key in listOf("firstName","lastName","displayName","country","preferredLanguage"))require(tree[key]?.isString==true)
            for(key in listOf("expectedProfileRevision","expectedPreferencesRevision"))require(tree[key]?.isIntegralNumber==true)
            mapper.readValue(bytes,PlayerProfileEditRequest::class.java)
        }catch(e:Exception){throw OnboardingFailure("REQUEST_INVALID",400)}
        return ResponseEntity.ok().header("Cache-Control","no-store").body(service.update(identity,value))
    }
    @ExceptionHandler(OnboardingFailure::class) fun failure(e:OnboardingFailure)=ResponseEntity.status(e.status).header("Cache-Control","no-store").body(mapOf("code" to e.code,"message" to "Profile request could not be completed."))
}

