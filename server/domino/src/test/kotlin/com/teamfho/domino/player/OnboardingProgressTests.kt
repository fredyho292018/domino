package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import com.google.cloud.Timestamp
import org.junit.jupiter.api.Test
import java.time.Instant
import java.time.Clock
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.*

class ProgressMemory:OnboardingProgressRepository {
    val docs=linkedMapOf<String,Map<String,Any>>()
    var retry=false
    @Synchronized override fun <T> transaction(action:(OnboardingProgressTransaction)->T):T {
        fun attempt(commit:Boolean):T {
            val pending=linkedMapOf<String,Map<String,Any>>()
            val value=action(object:OnboardingProgressTransaction {
                override fun read(path:String)=docs[path]
                override fun write(path:String,value:Map<String,Any>){pending[path]=value}
            })
            if(commit)docs.putAll(pending)
            return value
        }
        if(retry)attempt(false)
        return attempt(true)
    }
    fun initialize(id:String="fixture-player") {
        val at=Instant.parse("2026-01-01T00:00:00Z");val ts=Timestamp.ofTimeSecondsAndNanos(at.epochSecond,0)
        val root="players/$id"
        docs[root]=mapOf("uid" to id,"accountType" to "GUEST","displayName" to "Fixture","status" to "ACTIVE","language" to "en","createdAt" to ts,"updatedAt" to ts,"lastSeenAt" to ts)
        docs["$root/preferences/current"]=FoundationDocumentCodec.encode(PlayerPreferences("en",updatedAt=at))
        docs["$root/dominoProfile/current"]=FoundationDocumentCodec.encode(DominoProfile(updatedAt=at))
        docs["$root/onboarding/current"]=FoundationDocumentCodec.encode(PlayerOnboarding(updatedAt=at))
        docs["$root/entitlementState/current"]=mapOf("trialConsumed" to true)
        docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 1)
        docs["onboardingCatalogs/1"]=GameCatalogCodec.map(OnboardingCatalogSeed.canonical())
    }
}
class OnboardingProgressTests {
    private val repo=ProgressMemory().also{it.initialize()}
    private val identity=FirebaseIdentity("fixture-player",true)
    private val now=Instant.parse("2026-02-01T00:00:00Z")
    private fun service(coach:OnboardingCoachValidation=UnavailableOnboardingCoach)=OnboardingProgressService(repo,coach,Clock.fixed(now,ZoneOffset.UTC))
    private fun op()=UUID.randomUUID().toString()
    private fun start(s:OnboardingProgressService=service())=s.start(identity,OnboardingStartRequest(op(),0))
    private fun save(revision:Long=1,value:String="STRATEGY",domain:Long=0,id:String=op())=SaveStepRequest(id,revision,1,mapOf("domino" to domain),OnboardingStepAction.SAVE,listOf(OnboardingAnswer("DOMINO_EXPERIENCE",OnboardingQuestionType.SINGLE_SELECT,value)))
    private fun failure(code:String,block:()->Unit){assertEquals(code,assertFailsWith<OnboardingFailure>(block=block).code)}
    @Test fun `GET not started has no side effects or private auth fields`() {
        val before=repo.docs.toMap();val r=service().get(identity)
        assertEquals(OnboardingStatus.NOT_STARTED,r.status);assertEquals(before,repo.docs)
        val json=GameCatalogCodec.json(r);listOf("uid","email","phone","token","playerId").forEach{assertFalse(json.contains("\"$it\""))}
    }
    @Test fun `uninitialized legacy GET does not manufacture state`() {
        repo.docs.remove("players/fixture-player/onboarding/current");val before=repo.docs.toMap()
        failure("ONBOARDING_ROLLOUT_UNRESOLVED"){service().get(identity)};assertEquals(before,repo.docs)
    }
    @Test fun `explicit start is required and timestamp is stable across retry and resume`() {
        failure("ONBOARDING_NOT_STARTED"){service().save(identity,"EXPERIENCE_STEP",save(0))}
        val r=OnboardingStartRequest(op(),0);val s=service();val first=s.start(identity,r)
        assertEquals(first,s.start(identity,r));assertEquals(now,first.startedAt)
        assertEquals(first,s.start(identity,OnboardingStartRequest(op(),1)))
    }
    @Test fun `experience saves canonical domain answer and resumes coach after service recreation`() {
        start();val s=service();val r=s.save(identity,"EXPERIENCE_STEP",save())
        assertEquals(ExperienceLevel.STRATEGY,r.domino!!.experienceLevel)
        assertEquals("COACH_STEP",r.onboarding.currentStepKey)
        val restored=service().get(identity);assertEquals(r.onboarding,restored)
        assertEquals("STRATEGY",restored.answers.single().optionKey)
    }
    @Test fun `all four experience options remain stable values`() {
        ExperienceLevel.entries.forEach { level ->
            val local=ProgressMemory().also{it.initialize()};val s=OnboardingProgressService(local,clock=Clock.fixed(now,ZoneOffset.UTC))
            start(s);assertEquals(level,s.save(identity,"EXPERIENCE_STEP",save(value=level.name)).domino!!.experienceLevel)
        }
    }
    @Test fun `invalid localized option and wrong question type are rejected atomically`() {
        start();val before=repo.docs.toMap()
        failure("ONBOARDING_INVALID_ANSWER"){service().save(identity,"EXPERIENCE_STEP",save(value="No sé jugar"))}
        failure("ONBOARDING_INVALID_ANSWER"){service().save(identity,"EXPERIENCE_STEP",save().copy(answers=listOf(OnboardingAnswer("DOMINO_EXPERIENCE",OnboardingQuestionType.COACH_SELECT,"STRATEGY"))))}
        assertEquals(before,repo.docs)
    }
    @Test fun `missing duplicate or unrelated answers are rejected`() {
        start();val a=save()
        failure("ONBOARDING_REQUIRED_FIELD_MISSING"){service().save(identity,"EXPERIENCE_STEP",a.copy(answers=emptyList()))}
        failure("ONBOARDING_INVALID_ANSWER"){service().save(identity,"EXPERIENCE_STEP",a.copy(answers=a.answers+a.answers))}
        failure("ONBOARDING_INVALID_ANSWER"){service().save(identity,"EXPERIENCE_STEP",a.copy(answers=listOf(a.answers.single().copy(questionKey="COUNTRY"))))}
    }
    @Test fun `required steps cannot skip and forward cursor cannot bypass coach`() {
        start()
        failure("ONBOARDING_REQUIRED_STEP_CANNOT_SKIP"){service().save(identity,"EXPERIENCE_STEP",save().copy(action=OnboardingStepAction.SKIP,answers=emptyList(),domainRevisions=emptyMap()))}
        failure("ONBOARDING_STEP_NOT_REACHABLE"){service().cursor(identity,OnboardingCursorRequest(op(),1,1,"MEMBERSHIP_STEP"))}
    }
    @Test fun `catalog pinning survives publication and rejects incompatible client version`() {
        start();repo.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        assertEquals(1,service().get(identity).catalogVersion)
        failure("ONBOARDING_CATALOG_VERSION_MISMATCH"){service().save(identity,"EXPERIENCE_STEP",save().copy(catalogVersion=2))}
        assertEquals(1,service().save(identity,"EXPERIENCE_STEP",save()).onboarding.catalogVersion)
    }
    @Test fun `stale onboarding and domain revisions reject lost updates`() {
        start();service().save(identity,"EXPERIENCE_STEP",save())
        failure("REVISION_MISMATCH"){service().save(identity,"EXPERIENCE_STEP",save())}
        failure("REVISION_MISMATCH"){service().save(identity,"EXPERIENCE_STEP",save(2))}
    }
    @Test fun `receipt retry returns original response after newer update and conflict is rejected`() {
        start();val request=save();val first=service().save(identity,"EXPERIENCE_STEP",request)
        service().save(identity,"EXPERIENCE_STEP",save(2,"COMPETITIVE",1))
        assertEquals(first,service().save(identity,"EXPERIENCE_STEP",request))
        failure("IDEMPOTENCY_CONFLICT"){service().save(identity,"EXPERIENCE_STEP",request.copy(answers=listOf(request.answers.single().copy(optionKey="BEGINNER"))))}
        assertEquals("COMPETITIVE",service().get(identity).answers.single().optionKey)
    }
    @Test fun `transaction callback retry creates one progress update and one receipt`() {
        start();repo.retry=true;val r=save();val before=repo.docs.size
        val result=service().save(identity,"EXPERIENCE_STEP",r)
        assertEquals(2,result.onboarding.revision);assertEquals(before+1,repo.docs.size)
        assertEquals(result,service().save(identity,"EXPERIENCE_STEP",r))
    }
    @Test fun `back edit preserves later completed answers and returns frontier`() {
        val s=fixtureCoachFlow();val before=s.get(identity)
        val back=s.cursor(identity,OnboardingCursorRequest(op(),before.revision,1,"EXPERIENCE_STEP"))
        val edited=s.save(identity,"EXPERIENCE_STEP",save(back.revision,"COMPETITIVE",back.domainRevisions.domino)).onboarding
        assertEquals("CONTACTS_STEP",edited.currentStepKey)
        assertTrue("COACH_STEP" in edited.completedStepKeys)
        assertEquals("FIXTURE_COACH",edited.answers.find{it.type==OnboardingQuestionType.COACH_SELECT}!!.optionKey)
    }
    private fun fixtureCoachFlow():OnboardingProgressService {
        // Only isolated fixtures have an authoritative coach implementation; production stays fail-closed.
        repo.docs["onboardingCatalogs/1"]=GameCatalogCodec.map(OnboardingCatalogSeed.canonical().copy(coachCatalogVersion=1))
        val s=service(OnboardingCoachValidation{_,v,key->onboardingCheck(v==1 && key=="FIXTURE_COACH","COACH_NOT_FOUND",404)})
        start(s);val e=s.save(identity,"EXPERIENCE_STEP",save()).onboarding
        s.save(identity,"COACH_STEP",SaveStepRequest(op(),e.revision,1,mapOf("domino" to e.domainRevisions.domino),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,"FIXTURE_COACH"))))
        return s
    }
    @Test fun `production coach unavailable cannot save arbitrary key or complete`() {
        start();service().save(identity,"EXPERIENCE_STEP",save());val before=repo.docs.toMap()
        failure("DEPENDENCY_UNAVAILABLE"){service().save(identity,"COACH_STEP",SaveStepRequest(op(),2,1,mapOf("domino" to 1),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,"ARBITRARY"))))}
        failure("ONBOARDING_INCOMPLETE"){service().complete(identity,OnboardingCompleteRequest(op(),2,1))}
        assertEquals(before,repo.docs)
    }
    @Test fun `completion revalidates coach even if progress already claims completion`() {
        val s=fixtureCoachFlow();val state=s.get(identity)
        failure("DEPENDENCY_UNAVAILABLE"){service().complete(identity,OnboardingCompleteRequest(op(),state.revision,1))}
    }
    @Test fun `optional contacts and membership skip do not touch entitlements or catalog`() {
        val s=fixtureCoachFlow();val unchanged=repo.docs.filterKeys{!it.contains("/onboarding/") && !it.contains("/mutationReceipts/")}
        var state=s.get(identity)
        listOf("CONTACTS_STEP","MEMBERSHIP_STEP").forEach { key ->
            state=s.save(identity,key,SaveStepRequest(op(),state.revision,1,emptyMap(),OnboardingStepAction.SKIP,emptyList())).onboarding
        }
        assertEquals(listOf("CONTACTS_STEP","MEMBERSHIP_STEP"),state.skippedStepKeys)
        assertEquals(unchanged,repo.docs.filterKeys{!it.contains("/onboarding/") && !it.contains("/mutationReceipts/")})
        assertEquals(OnboardingStatus.COMPLETED,s.complete(identity,OnboardingCompleteRequest(op(),state.revision,1)).status)
    }
    @Test fun `optional steps need not be visited and completion timestamp cannot change`() {
        val s=fixtureCoachFlow();val state=s.get(identity);val request=OnboardingCompleteRequest(op(),state.revision,1)
        val done=s.complete(identity,request);assertEquals(now,done.completedAt)
        assertEquals(done,s.complete(identity,request))
        assertEquals(done,s.complete(identity,OnboardingCompleteRequest(op(),done.revision,1)))
        failure("ONBOARDING_ALREADY_COMPLETED"){s.start(identity,OnboardingStartRequest(op(),done.revision))}
        failure("ONBOARDING_ALREADY_COMPLETED"){s.save(identity,"EXPERIENCE_STEP",save(done.revision))}
    }
    @Test fun `legacy completed exemption is never reset`() {
        repo.docs["players/fixture-player/onboarding/current"]=FoundationDocumentCodec.encode(PlayerOnboarding(status=OnboardingStatus.COMPLETED,completedAt=now,updatedAt=now,completionOrigin=CompletionOrigin.LEGACY_EXEMPT))
        val before=service().get(identity)
        failure("ONBOARDING_ALREADY_COMPLETED"){start()}
        assertEquals(before,service().complete(identity,OnboardingCompleteRequest(op(),0,1)))
    }
    @Test fun `provider verification policy follows authenticated provenance`() {
        failure("EMAIL_VERIFICATION_REQUIRED"){OnboardingWriteAuthorization.check(FirebaseIdentity("fixture",false,false,"password",true))}
        failure("EMAIL_VERIFICATION_REQUIRED"){OnboardingWriteAuthorization.check(FirebaseIdentity("fixture",false,false,"custom",true))}
        failure("AUTH_CONTEXT_UNSUPPORTED"){OnboardingWriteAuthorization.check(FirebaseIdentity("fixture",false,false,"unknown",false))}
        OnboardingWriteAuthorization.check(identity)
        OnboardingWriteAuthorization.check(FirebaseIdentity("fixture",false,true,"password",true))
        listOf("google.com","facebook.com","phone").forEach{OnboardingWriteAuthorization.check(FirebaseIdentity("fixture",false,false,it,true))}
    }
    @Test fun `auth policy is enforced on every mutation and read remains safe`() {
        val user=identity.copy(isAnonymous=false,signInProvider="password",hasPasswordProvider=true)
        assertEquals(OnboardingStatus.NOT_STARTED,service().get(user).status)
        val before=repo.docs.toMap()
        failure("EMAIL_VERIFICATION_REQUIRED"){service().start(user,OnboardingStartRequest(op(),0))}
        failure("EMAIL_VERIFICATION_REQUIRED"){service().save(user,"EXPERIENCE_STEP",save())}
        failure("EMAIL_VERIFICATION_REQUIRED"){service().cursor(user,OnboardingCursorRequest(op(),0,1,"EXPERIENCE_STEP"))}
        failure("EMAIL_VERIFICATION_REQUIRED"){service().complete(user,OnboardingCompleteRequest(op(),0,1))}
        assertEquals(before,repo.docs)
    }
    @Test fun `inactive player and missing step are safe errors`() {
        start();failure("ONBOARDING_STEP_NOT_FOUND"){service().save(identity,"UNKNOWN",save())}
        repo.docs["players/fixture-player"]=repo.docs.getValue("players/fixture-player")+("status" to "DISABLED")
        failure("PLAYER_NOT_ACTIVE"){service().get(identity)}
    }
    @Test fun `concurrent same revision permits only one writer`() {
        start();val pool=java.util.concurrent.Executors.newFixedThreadPool(2)
        try {
            val futures=listOf("BEGINNER","STRATEGY").map{v->pool.submit<Boolean>{try{service().save(identity,"EXPERIENCE_STEP",save(value=v));true}catch(e:OnboardingFailure){assertEquals("REVISION_MISMATCH",e.code);false}}}
            assertEquals(1,futures.count{it.get(5,java.util.concurrent.TimeUnit.SECONDS)})
        }finally{pool.shutdownNow()}
    }
    @Test fun `inactive step and inactive option cannot accept answers`() {
        val seed=OnboardingCatalogSeed.canonical()
        repo.docs["onboardingCatalogs/1"]=GameCatalogCodec.map(seed.copy(options=seed.options.map{if(it.key=="STRATEGY")it.copy(active=false)else it}))
        start();failure("ONBOARDING_INVALID_ANSWER"){service().save(identity,"EXPERIENCE_STEP",save())}
        repo.docs["onboardingCatalogs/1"]=GameCatalogCodec.map(seed.copy(steps=seed.steps.map{if(it.key=="EXPERIENCE_STEP")it.copy(active=false)else it}))
        failure("ONBOARDING_STEP_INACTIVE"){service().save(identity,"EXPERIENCE_STEP",save())}
    }
    @Test fun `operation receipts are identity and endpoint scoped`() {
        val id=op();start();service().save(identity,"EXPERIENCE_STEP",save(id=id))
        failure("IDEMPOTENCY_CONFLICT"){service().cursor(identity,OnboardingCursorRequest(id,2,1,"EXPERIENCE_STEP"))}
        repo.initialize("fixture-other")
        val other=FirebaseIdentity("fixture-other",true)
        service().start(other,OnboardingStartRequest(id,0))
        assertEquals(OnboardingStatus.IN_PROGRESS,service().get(other).status)
        assertEquals("COACH_STEP",service().get(identity).currentStepKey)
    }
    @Test fun `expired receipt does not override revision protection`() {
        start();val request=save();service().save(identity,"EXPERIENCE_STEP",request)
        val later=OnboardingProgressService(repo,clock=Clock.fixed(now.plusSeconds(31L*86400),ZoneOffset.UTC))
        failure("REVISION_MISMATCH"){later.save(identity,"EXPERIENCE_STEP",request)}
    }
}

