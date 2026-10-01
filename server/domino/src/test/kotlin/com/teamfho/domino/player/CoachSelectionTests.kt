package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.*

class CoachSelectionTests {
    private val repo=ProgressMemory().also {
        it.initialize()
        it.docs["coachCatalogs/1"]=GameCatalogCodec.map(CoachCatalogSeed.canonical())
        it.docs["onboardingCatalogs/2"]=GameCatalogCodec.map(CoachCatalogSeed.compatibleOnboarding())
        it.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
    }
    private val user=FirebaseIdentity("fixture-player",true)
    private val now=Instant.parse("2026-09-30T00:00:00Z")
    private val service=OnboardingProgressService(repo,AuthoritativeCoachValidation(),Clock.fixed(now,ZoneOffset.UTC))
    private fun op()=UUID.randomUUID().toString()
    private fun ready():OnboardingResponse {
        service.start(user,OnboardingStartRequest(op(),0))
        return service.save(user,"EXPERIENCE_STEP",SaveStepRequest(op(),1,2,mapOf("domino" to 0),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("DOMINO_EXPERIENCE",OnboardingQuestionType.SINGLE_SELECT,"STRATEGY")))).onboarding
    }
    private fun request(state:OnboardingResponse,key:String="LUCIA")=SaveStepRequest(op(),state.revision,2,mapOf("domino" to state.domainRevisions.domino),OnboardingStepAction.SAVE,
        listOf(OnboardingAnswer("COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,key)))
    private fun save(state:OnboardingResponse,key:String="LUCIA")=service.save(user,"COACH_STEP",request(state,key))
    private fun fail(code:String,action:()->Unit){assertEquals(code,assertFailsWith<OnboardingFailure>(block=action).code)}
    @Test fun `valid selection persists only canonical domain reference and advances`() {
        val result=save(ready());assertEquals("LUCIA",result.domino!!.preferredCoachKey);assertEquals(1,result.domino.selectedCoachCatalogVersion)
        assertEquals("CONTACTS_STEP",result.onboarding.currentStepKey);assertEquals(ExperienceLevel.STRATEGY,result.domino.experienceLevel)
        val domain=repo.docs.getValue("players/fixture-player/dominoProfile/current")
        listOf("name","avatar","description","email").forEach{assertFalse(it in domain)}
    }
    @Test fun `unknown localized and arbitrary keys are rejected without writes`() {
        val state=ready();val before=repo.docs.toMap()
        listOf("UNKNOWN","Lucía","lucia","../LUCIA").forEach{key->fail("COACH_NOT_FOUND"){save(state,key)}}
        assertEquals(before,repo.docs)
    }
    @Test fun `inactive coach cannot be newly selected`() {
        val p=CoachCatalogSeed.canonical();repo.docs["coachCatalogs/1"]=GameCatalogCodec.map(p.copy(coaches=p.coaches.map{if(it.key=="LUCIA")it.copy(active=false)else it}))
        val state=ready();fail("COACH_NOT_SELECTABLE"){save(state)}
    }
    @Test fun `same operation replay preserves revision timestamp and single selection`() {
        val state=ready();val r=request(state);val first=service.save(user,"COACH_STEP",r);val before=repo.docs.toMap()
        assertEquals(first,service.save(user,"COACH_STEP",r));assertEquals(before,repo.docs);assertEquals(state.startedAt,first.onboarding.startedAt)
        fail("IDEMPOTENCY_CONFLICT"){service.save(user,"COACH_STEP",r.copy(answers=listOf(r.answers.single().copy(optionKey="DAVID"))))}
    }
    @Test fun `stale coach revision rejected`() {
        val state=ready();save(state);fail("REVISION_MISMATCH"){save(state,"DAVID")}
    }
    @Test fun `back and changing coach retains experience and start`() {
        val first=save(ready()).onboarding
        val back=service.cursor(user,OnboardingCursorRequest(op(),first.revision,2,"COACH_STEP"))
        val changed=save(back,"DAVID")
        assertEquals("DAVID",changed.domino!!.preferredCoachKey);assertEquals(ExperienceLevel.STRATEGY,changed.domino.experienceLevel)
        assertEquals(first.startedAt,changed.onboarding.startedAt);assertEquals("CONTACTS_STEP",changed.onboarding.currentStepKey)
    }
    @Test fun `experience only and coach only cannot complete`() {
        val started=service.start(user,OnboardingStartRequest(op(),0))
        fail("ONBOARDING_STEP_NOT_REACHABLE"){save(started)}
        fail("ONBOARDING_INCOMPLETE"){service.complete(user,OnboardingCompleteRequest(op(),started.revision,2))}
        val p=PlayerOnboarding(status=OnboardingStatus.IN_PROGRESS,catalogVersion=2,currentStepKey="COACH_STEP",completedStepKeys=listOf("COACH_STEP"),startedAt=now,updatedAt=now,revision=1)
        repo.docs["players/fixture-player/onboarding/current"]=FoundationDocumentCodec.encode(p)
        fail("ONBOARDING_INCOMPLETE"){service.complete(user,OnboardingCompleteRequest(op(),1,2))}
    }
    @Test fun `experience without coach rejects completion`() {
        val state=ready();fail("ONBOARDING_INCOMPLETE"){service.complete(user,OnboardingCompleteRequest(op(),state.revision,2))}
    }
    @Test fun `authoritative complete with optional skips preserves entitlements and catalog`() {
        var state=save(ready()).onboarding
        val protected=repo.docs.filterKeys{!it.contains("/onboarding/") && !it.contains("/mutationReceipts/")}
        listOf("CONTACTS_STEP","MEMBERSHIP_STEP").forEach{step->state=service.save(user,step,SaveStepRequest(op(),state.revision,2,emptyMap(),OnboardingStepAction.SKIP,emptyList())).onboarding}
        val done=service.complete(user,OnboardingCompleteRequest(op(),state.revision,2))
        assertEquals(OnboardingStatus.COMPLETED,done.status);assertEquals(now,done.completedAt)
        assertEquals(protected,repo.docs.filterKeys{!it.contains("/onboarding/") && !it.contains("/mutationReceipts/")})
    }
    @Test fun `new publication inactive does not replace pinned historical selection`() {
        val state=save(ready()).onboarding
        val newer=CoachCatalogSeed.canonical().copy(catalogVersion=2,coaches=CoachCatalogSeed.canonical().coaches.map{it.copy(active=false)})
        repo.docs["coachCatalogs/2"]=GameCatalogCodec.map(newer);repo.docs["systemConfig/coachCatalog"]=mapOf("publishedVersion" to 2)
        assertEquals(OnboardingStatus.COMPLETED,service.complete(user,OnboardingCompleteRequest(op(),state.revision,2)).status)
        assertEquals("LUCIA",service.get(user).answers.single{it.questionKey=="COACH_SELECTION"}.optionKey)
    }
    @Test fun `existing inactive selection retained but unknown historical reference fails`() {
        val state=save(ready()).onboarding
        // Synthetic historical inactive document exercises retention; production publications are immutable.
        val p=CoachCatalogSeed.canonical();repo.docs["coachCatalogs/1"]=GameCatalogCodec.map(p.copy(coaches=p.coaches.map{it.copy(active=false)}))
        val retained=save(state).onboarding
        assertEquals(OnboardingStatus.COMPLETED,service.complete(user,OnboardingCompleteRequest(op(),retained.revision,2)).status)
    }
    @Test fun `missing persisted coach fails completion without substitution`() {
        val state=save(ready()).onboarding;val path="players/fixture-player/dominoProfile/current"
        val profile=FoundationDocumentCodec.decode(repo.docs.getValue(path),DominoProfile::class.java)
        repo.docs[path]=FoundationDocumentCodec.encode(profile.copy(preferredCoachKey="UNKNOWN"))
        fail("COACH_NOT_FOUND"){service.complete(user,OnboardingCompleteRequest(op(),state.revision,2))}
        assertEquals("UNKNOWN",FoundationDocumentCodec.decode(repo.docs.getValue(path),DominoProfile::class.java).preferredCoachKey)
    }
    @Test fun `incompatible missing and unpinned publications fail closed`() {
        val state=ready();fail("ONBOARDING_CATALOG_VERSION_MISMATCH"){service.save(user,"COACH_STEP",request(state).copy(catalogVersion=1))}
        repo.docs.remove("coachCatalogs/1");fail("COACH_CATALOG_NOT_FOUND"){save(state)}
        repo.docs["onboardingCatalogs/2"]=GameCatalogCodec.map(CoachCatalogSeed.compatibleOnboarding().copy(coachCatalogVersion=null))
        fail("COACH_CATALOG_VERSION_MISMATCH"){save(state)}
    }
    @Test fun `legacy completion preserved and account paths remain isolated`() {
        repo.initialize("fixture-other")
        repo.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        val otherBefore=repo.docs.filterKeys{it.startsWith("players/fixture-other/")}
        save(ready());assertEquals(otherBefore,repo.docs.filterKeys{it.startsWith("players/fixture-other/")})
        val legacy=PlayerOnboarding(status=OnboardingStatus.COMPLETED,completedAt=now,updatedAt=now,completionOrigin=CompletionOrigin.LEGACY_EXEMPT)
        repo.docs["players/fixture-player/onboarding/current"]=FoundationDocumentCodec.encode(legacy)
        assertEquals(CompletionOrigin.LEGACY_EXEMPT,service.complete(user,OnboardingCompleteRequest(op(),0,2)).completionOrigin)
    }
}
