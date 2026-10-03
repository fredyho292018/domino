package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.*

class BasicProfileOnboardingTests {
    private val repo=ProgressMemory().also {
        it.initialize()
        it.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        it.docs["onboardingCatalogs/2"]=GameCatalogCodec.map(OnboardingCatalogV2.canonical())
        it.docs["coachCatalogs/1"]=GameCatalogCodec.map(CoachCatalogSeed.canonical())
    }
    private val user=FirebaseIdentity("fixture-player",true)
    private val now=Instant.parse("2026-09-30T00:00:00Z")
    private fun service()=OnboardingProgressService(repo,AuthoritativeCoachValidation(),Clock.fixed(now,ZoneOffset.UTC))
    private fun op()=UUID.randomUUID().toString()
    private fun start()=service().start(user,OnboardingStartRequest(op(),0))
    private fun answers()=listOf(
        OnboardingAnswer("FIRST_NAME",OnboardingQuestionType.TEXT,textValue="  Jose\u0301  "),
        OnboardingAnswer("LAST_NAME",OnboardingQuestionType.TEXT,textValue="李"),
        OnboardingAnswer("DISPLAY_NAME",OnboardingQuestionType.TEXT,textValue="Fixture_2"),
        OnboardingAnswer("COUNTRY",OnboardingQuestionType.COUNTRY_SELECT," cu "),
        OnboardingAnswer("PREFERRED_LANGUAGE",OnboardingQuestionType.LOCALE_SELECT,"es"))
    private fun request(s:OnboardingResponse,answers:List<OnboardingAnswer> = answers(),zone:String?=null)=SaveStepRequest(op(),s.revision,2,
        mapOf("profile" to s.domainRevisions.profile,"preferences" to s.domainRevisions.preferences),OnboardingStepAction.SAVE,answers,zone)
    private fun save(s:OnboardingResponse)=service().save(user,"BASIC_PROFILE_STEP",request(s)).onboarding
    private fun fail(code:String,action:()->Unit){assertEquals(code,assertFailsWith<OnboardingFailure>(block=action).code)}
    private fun coachReady():OnboardingResponse {
        val basic=save(start())
        val experience=service().save(user,"EXPERIENCE_STEP",SaveStepRequest(op(),basic.revision,2,mapOf("domino" to basic.domainRevisions.domino),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("DOMINO_EXPERIENCE",OnboardingQuestionType.SINGLE_SELECT,"RULES_KNOWN")))).onboarding
        return service().save(user,"COACH_STEP",SaveStepRequest(op(),experience.revision,2,mapOf("domino" to experience.domainRevisions.domino),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,"SOFIA")))).onboarding
    }
    @Test fun `alias conflict rolls back every basic profile field and preserves retry state`() {
        val initial=start()
        repo.docs[PlayerAliasReservations.path("Fixture_2")]=mapOf("state" to "CLAIMED","playerId" to "another-fixture","normalizationVersion" to 1L)
        val before=repo.docs.toMap()
        fail("DISPLAY_NAME_TAKEN"){save(initial)}
        assertEquals(before,repo.docs)
        assertEquals(initial,service().get(user))
    }
    @Test fun `keeping own alias preserves reservation and saves private fields`() {
        val initial=start()
        val reservation=repo.docs[PlayerAliasReservations.path("Fixture")]
        val result=service().save(user,"BASIC_PROFILE_STEP",request(initial,answers().map{if(it.questionKey=="DISPLAY_NAME")it.copy(textValue=" Fixture ")else it})).onboarding
        assertEquals("Fixture",result.basicProfile!!.displayName)
        assertEquals("José",result.basicProfile.firstName)
        assertEquals(reservation,repo.docs[PlayerAliasReservations.path("Fixture")])
    }
    @Test fun `new v2 starts basic without assuming alias completes profile`() {
        val s=start();assertEquals("BASIC_PROFILE_STEP",s.currentStepKey);assertEquals(2,s.catalogVersion)
        assertTrue(s.completedStepKeys.isEmpty());assertEquals("Fixture",s.basicProfile!!.displayName)
        assertNull(s.basicProfile.firstName);assertNull(s.basicProfile.countryCode)
    }
    @Test fun `unicode normalized domain save and reload prefill resume experience`() {
        val s=save(start());val restored=service().get(user)
        assertEquals(s,restored);assertEquals("EXPERIENCE_STEP",s.currentStepKey)
        assertEquals("José",s.basicProfile!!.firstName);assertEquals("李",s.basicProfile.lastName)
        assertEquals("CU",s.basicProfile.countryCode);assertEquals("es",s.basicProfile.preferredLocale)
        assertEquals("es",repo.docs.getValue("players/fixture-player")["language"])
        assertEquals(1,s.domainRevisions.profile);assertEquals(1,s.domainRevisions.preferences)
        assertEquals("José",s.answers.single{it.questionKey=="FIRST_NAME"}.textValue)
        assertFalse(repo.docs.keys.any{it.endsWith("/answers")})
    }
    @Test fun `every required basic answer missing rejects all writes`() {
        val s=start();val before=repo.docs.toMap()
        answers().forEach{missing->fail("ONBOARDING_REQUIRED_FIELD_MISSING"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().filter{it!=missing}))}}
        assertEquals(before,repo.docs)
    }
    @Test fun `empty names controls and excessive graphemes rejected`() {
        val s=start();listOf(""," ","A\u0001B","A\u202EB","A".repeat(81)).forEach { value ->
            fail("PROFILE_FIELD_INVALID"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="FIRST_NAME")it.copy(textValue=value)else it}))}
        }
    }
    @Test fun `display alias policy retained and private names never auto public`() {
        val s=start();listOf("x","Álvaro","Name With Spaces").forEach { value ->
            fail("DISPLAY_NAME_INVALID"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="DISPLAY_NAME")it.copy(textValue=value)else it}))}
        }
        fail("DISPLAY_NAME_RESERVED"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="DISPLAY_NAME")it.copy(textValue="admin")else it}))}
    }
    @Test fun `invalid or inferred country and unsupported explicit locale rejected`() {
        val s=start()
        listOf("ZZ","Cuba","").forEach{value->fail("PROFILE_FIELD_INVALID"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="COUNTRY")it.copy(optionKey=value)else it}))}}
        listOf("fr","es-US","").forEach{value->fail("LANGUAGE_UNSUPPORTED"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="PREFERRED_LANGUAGE")it.copy(optionKey=value)else it}))}}
    }
    @Test fun `wrong answer representation and duplicate question rejected`() {
        val s=start()
        fail("ONBOARDING_INVALID_ANSWER"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers()+answers().first()))}
        fail("ONBOARDING_INVALID_ANSWER"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="FIRST_NAME")it.copy(optionKey="INJECTED")else it}))}
    }
    @Test fun `stale profile preferences or progress rejects save`() {
        val s=start()
        listOf(request(s).copy(expectedRevision=0),request(s).copy(domainRevisions=mapOf("profile" to 9,"preferences" to 0)),
            request(s).copy(domainRevisions=mapOf("profile" to 0,"preferences" to 9))).forEach{r->fail("REVISION_MISMATCH"){service().save(user,"BASIC_PROFILE_STEP",r)}}
    }
    @Test fun `idempotent retry keeps initial timestamp revisions and receipt snapshot`() {
        val s=start();val r=request(s);val first=service().save(user,"BASIC_PROFILE_STEP",r);val before=repo.docs.toMap()
        assertEquals(first,service().save(user,"BASIC_PROFILE_STEP",r));assertEquals(before,repo.docs)
        assertEquals(s.startedAt,first.onboarding.startedAt)
        fail("IDEMPOTENCY_CONFLICT"){service().save(user,"BASIC_PROFILE_STEP",r.copy(detectedTimeZone="UTC"))}
    }
    @Test fun `failed last field leaves player preferences progress and receipt unchanged`() {
        val s=start();val before=repo.docs.toMap()
        fail("LANGUAGE_UNSUPPORTED"){service().save(user,"BASIC_PROFILE_STEP",request(s,answers().map{if(it.questionKey=="PREFERRED_LANGUAGE")it.copy(optionKey="fr")else it}))}
        assertEquals(before,repo.docs)
    }
    @Test fun `root metadata and social projection preserved atomically`() {
        val root="players/fixture-player";repo.docs[root]=repo.docs.getValue(root)+mapOf("isTestAccount" to true,"otherMetadata" to "retained")
        repo.docs["$root/publicIdentity/current"]=mapOf("publicPlayerId" to "fixture-public")
        repo.docs["publicPlayerProfiles/fixture-public"]=mapOf("displayName" to "Fixture","discoverable" to true)
        val old=repo.docs.getValue(root);save(start());val updated=repo.docs.getValue(root)
        listOf("uid","createdAt","lastSeenAt","accountType","status","isTestAccount","otherMetadata").forEach{assertEquals(old[it],updated[it])}
        val public=repo.docs.getValue("publicPlayerProfiles/fixture-public")
        assertEquals("Fixture_2",public["displayName"]);assertEquals(true,public["discoverable"])
        listOf("firstName","lastName","countryCode","preferredLocale","timeZone").forEach{assertFalse(it in public)}
    }
    @Test fun `missing public projection fails without partial private writes`() {
        repo.docs["players/fixture-player/publicIdentity/current"]=mapOf("publicPlayerId" to "fixture-missing")
        val s=start();val before=repo.docs.toMap()
        fail("DEPENDENCY_UNAVAILABLE"){save(s)};assertEquals(before,repo.docs)
    }
    @Test fun `back edit and language switch preserve later answers and pin`() {
        val before=coachReady();val back=service().cursor(user,OnboardingCursorRequest(op(),before.revision,2,"BASIC_PROFILE_STEP"))
        val r=request(back,answers().map{if(it.questionKey=="PREFERRED_LANGUAGE")it.copy(optionKey="en")else it})
        val after=service().save(user,"BASIC_PROFILE_STEP",r).onboarding
        assertEquals(2,after.catalogVersion);assertEquals(before.startedAt,after.startedAt)
        assertEquals(before.completedStepKeys.toSet(),after.completedStepKeys.toSet());assertEquals("CONTACTS_STEP",after.currentStepKey)
        assertEquals("SOFIA",after.answers.single{it.questionKey=="COACH_SELECTION"}.optionKey)
        assertEquals("en",after.basicProfile!!.preferredLocale);assertEquals(back.revision+1,after.revision)
    }
    @Test fun `detected timezone initializes while existing preference is preserved`() {
        val first=service().save(user,"BASIC_PROFILE_STEP",request(start(),zone="America/Chicago")).onboarding
        assertEquals("America/Chicago",first.basicProfile!!.timeZone)
        val second=service().save(user,"BASIC_PROFILE_STEP",request(first,zone="Europe/Madrid")).onboarding
        assertEquals("America/Chicago",second.basicProfile!!.timeZone)
    }
    @Test fun `invalid or missing timezone nonblocking and never inferred`() {
        val first=service().save(user,"BASIC_PROFILE_STEP",request(start(),zone="Invalid/Zone")).onboarding
        assertNull(first.basicProfile!!.timeZone)
        assertNull(OnboardingProfileBinding.initializeTimeZone(null,null))
        assertEquals("UTC",OnboardingProfileBinding.initializeTimeZone(null,"UTC"))
    }
    @Test fun `full v2 completion with optional skips does not touch trial`() {
        var s=coachReady();val entitlement=repo.docs.getValue("players/fixture-player/entitlementState/current")
        listOf("CONTACTS_STEP","MEMBERSHIP_STEP").forEach{step->s=service().save(user,step,SaveStepRequest(op(),s.revision,2,emptyMap(),OnboardingStepAction.SKIP,emptyList())).onboarding}
        assertEquals(OnboardingStatus.COMPLETED,service().complete(user,OnboardingCompleteRequest(op(),s.revision,2)).status)
        assertEquals(entitlement,repo.docs.getValue("players/fixture-player/entitlementState/current"))
    }
    @Test fun `completion without required basic fields rejected even with forged completed outcome`() {
        val s=coachReady();val root="players/fixture-player";val original=repo.docs.getValue(root)
        listOf("firstName","lastName","countryCode").forEach{field->
            repo.docs[root]=original-field
            fail("ONBOARDING_INCOMPLETE"){service().complete(user,OnboardingCompleteRequest(op(),s.revision,2))}
        }
        repo.docs[root]=original+("displayName" to "x")
        fail("ONBOARDING_INCOMPLETE"){service().complete(user,OnboardingCompleteRequest(op(),s.revision,2))}
        repo.docs[root]=original
    }
    @Test fun `completion without basic outcome experience or coach fails`() {
        val s=start();fail("ONBOARDING_INCOMPLETE"){service().complete(user,OnboardingCompleteRequest(op(),s.revision,2))}
        val basic=save(s);fail("ONBOARDING_INCOMPLETE"){service().complete(user,OnboardingCompleteRequest(op(),basic.revision,2))}
    }
    @Test fun `v1 in progress remains v1 after pointer changes`() {
        repo.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 1)
        val old=start();repo.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        val restored=service().get(user);assertEquals(1,restored.catalogVersion);assertEquals("EXPERIENCE_STEP",restored.currentStepKey)
        assertNull(restored.basicProfile);assertEquals(old,restored)
    }
    @Test fun `legacy completed state not reopened`() {
        val legacy=PlayerOnboarding(status=OnboardingStatus.COMPLETED,completedAt=now,updatedAt=now,completionOrigin=CompletionOrigin.LEGACY_EXEMPT)
        repo.docs["players/fixture-player/onboarding/current"]=FoundationDocumentCodec.encode(legacy)
        fail("ONBOARDING_ALREADY_COMPLETED"){start()};assertNull(service().get(user).basicProfile)
    }
    @Test fun `unverified password cannot save private profile`() {
        val s=start();val invalid=user.copy(isAnonymous=false,hasPasswordProvider=true,signInProvider="password")
        fail("EMAIL_VERIFICATION_REQUIRED"){service().save(invalid,"BASIC_PROFILE_STEP",request(s))}
    }
    @Test fun `missing persisted locale fails closed at completion`() {
        val state=coachReady();val path="players/fixture-player/preferences/current"
        repo.docs[path]=repo.docs.getValue(path)-"preferredLocale"
        assertFails{service().complete(user,OnboardingCompleteRequest(op(),state.revision,2))}
    }
    @Test fun `completed v1 flow remains completed after v2 available`() {
        val old=PlayerOnboarding(status=OnboardingStatus.COMPLETED,catalogVersion=1,startedAt=now,completedAt=now,
            updatedAt=now,revision=4,completionOrigin=CompletionOrigin.FLOW)
        repo.docs["players/fixture-player/onboarding/current"]=FoundationDocumentCodec.encode(old)
        assertEquals(OnboardingStatus.COMPLETED,service().get(user).status)
        fail("ONBOARDING_ALREADY_COMPLETED"){service().start(user,OnboardingStartRequest(op(),4))}
        assertEquals(1,service().get(user).catalogVersion)
    }
    @Test fun `invalid persisted coach and absent experience cannot complete v2`() {
        val state=coachReady();val path="players/fixture-player/dominoProfile/current"
        val original=FoundationDocumentCodec.decode(repo.docs.getValue(path),DominoProfile::class.java)
        repo.docs[path]=FoundationDocumentCodec.encode(original.copy(experienceLevel=null))
        fail("ONBOARDING_INCOMPLETE"){service().complete(user,OnboardingCompleteRequest(op(),state.revision,2))}
        repo.docs[path]=FoundationDocumentCodec.encode(original.copy(preferredCoachKey="UNKNOWN"))
        fail("COACH_NOT_FOUND"){service().complete(user,OnboardingCompleteRequest(op(),state.revision,2))}
    }

}
