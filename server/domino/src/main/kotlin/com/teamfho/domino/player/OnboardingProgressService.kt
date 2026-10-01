package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import java.time.Instant
import java.util.UUID

class OnboardingProgressService(private val repository: OnboardingProgressRepository,
    private val coach: OnboardingCoachValidation = UnavailableOnboardingCoach, private val clock: Clock = Clock.systemUTC()) {
    private data class Snapshot(val player:Player,val preferences:PlayerPreferences,val domino:DominoProfile,val state:PlayerOnboarding)
    private fun root(identity:FirebaseIdentity):String {
        onboardingCheck(identity.uid.isNotBlank() && identity.uid.length<=128 && '/' !in identity.uid && identity.uid !in setOf(".",".."),"REQUEST_INVALID",400)
        return "players/${identity.uid}"
    }
    private fun snapshot(tx:OnboardingProgressTransaction,root:String,identity:FirebaseIdentity):Snapshot {
        val player=tx.read(root) ?: throw OnboardingFailure("PLAYER_NOT_FOUND",404)
        onboardingCheck(player["status"] == "ACTIVE","PLAYER_NOT_ACTIVE",403)
        fun <T> read(part:String,type:Class<T>):T = FoundationDocumentCodec.decode(tx.read("$root/$part/current")
            ?: throw OnboardingFailure("ONBOARDING_ROLLOUT_UNRESOLVED"),type)
        return Snapshot(FirestoreFoundationMapping.player(player,identity.uid),read("preferences",PlayerPreferences::class.java),
            read("dominoProfile",DominoProfile::class.java),read("onboarding",PlayerOnboarding::class.java))
    }
    private fun catalog(tx:OnboardingProgressTransaction,version:Int):OnboardingCatalogPublication {
        onboardingCheck(version>0,"ONBOARDING_CATALOG_VERSION_MISMATCH")
        val data=tx.read("onboardingCatalogs/$version") ?: throw OnboardingFailure("ONBOARDING_CATALOG_NOT_FOUND",404)
        try { return GameCatalogCodec.decode(data,OnboardingCatalogPublication::class.java).also {
            require(it.catalogVersion==version);OnboardingCatalogValidation.validate(it)
        } } catch(e:Exception){throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)}
    }
    private fun ordered(p:OnboardingCatalogPublication)=p.steps.filter{it.active}.sortedWith(compareBy<CatalogStep>{it.sortOrder}.thenBy{it.key})
    private fun response(s:Snapshot,p:OnboardingCatalogPublication?):OnboardingResponse {
        val answers=p?.questions?.filter{it.active}?.mapNotNull { q ->
            val key=when(q.binding){OnboardingBinding.EXPERIENCE_LEVEL->s.domino.experienceLevel?.name;OnboardingBinding.PREFERRED_COACH->s.domino.preferredCoachKey}
            key?.let{OnboardingAnswer(q.key,q.type,it)}
        } ?: emptyList()
        val missing=if(s.state.status==OnboardingStatus.COMPLETED) emptyList() else p?.questions?.filter { q ->
            q.required && q.active && p.steps.any{it.active && q.key in it.questionKeys} && answers.none{it.questionKey==q.key}
        }?.map{it.binding.name} ?: emptyList()
        val o=s.state
        return OnboardingResponse(o.status,o.catalogVersion,o.currentStepKey,o.currentSubstepKey,o.lastCompletedStepKey,o.completedStepKeys,
            o.skippedStepKeys,o.startedAt,o.completedAt,o.updatedAt,o.revision,o.completionOrigin,missing,
            OnboardingDomainRevisions(s.player.profileRevision,s.preferences.revision,s.domino.revision),answers)
    }
    fun get(identity:FirebaseIdentity):OnboardingResponse=repository.transaction { tx ->
        val s=snapshot(tx,root(identity),identity)
        response(s,s.state.catalogVersion?.let{catalog(tx,it)})
    }
    private fun mutate(identity:FirebaseIdentity,operationId:String,expected:Long,scope:String,request:Any,
        change:(OnboardingProgressTransaction,Snapshot,Instant)->Pair<Snapshot,OnboardingCatalogPublication?>):OnboardingMutationResponse {
        OnboardingWriteAuthorization.check(identity)
        val root=root(identity)
        onboardingCheck(runCatching{UUID.fromString(operationId).toString()==operationId}.getOrDefault(false) && expected>=0,"REQUEST_INVALID",400)
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest((scope+"\n"+GameCatalogCodec.semantic(request)).toByteArray())
            .joinToString(""){"%02x".format(it)}
        val now=clock.instant()
        return repository.transaction { tx ->
            val s=snapshot(tx,root,identity)
            val receiptPath="$root/mutationReceipts/$operationId"
            val receipt=tx.read(receiptPath)
            if(receipt!=null) {
                val expiry=receipt["expiresAt"] as? com.google.cloud.Timestamp ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
                if(now<Instant.ofEpochSecond(expiry.seconds,expiry.nanos.toLong())) {
                    onboardingCheck(receipt["requestHash"]==hash && receipt["scope"]==scope,"IDEMPOTENCY_CONFLICT")
                    return@transaction GameCatalogCodec.decode(receipt["result"] ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503),OnboardingMutationResponse::class.java)
                }
            }
            onboardingCheck(s.state.revision==expected,"REVISION_MISMATCH")
            val (next,p)=change(tx,s,now)
            val result=OnboardingMutationResponse(response(next,p),if(next.domino!=s.domino)next.domino else null)
            if(next.state!=s.state) tx.write("$root/onboarding/current",FoundationDocumentCodec.encode(next.state))
            if(next.domino!=s.domino) tx.write("$root/dominoProfile/current",FoundationDocumentCodec.encode(next.domino))
            val expires=now.plusSeconds(30L*86400)
            tx.write(receiptPath,mapOf("scope" to scope,"requestHash" to hash,"result" to GameCatalogCodec.map(result),
                "createdAt" to com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,now.nano),
                "expiresAt" to com.google.cloud.Timestamp.ofTimeSecondsAndNanos(expires.epochSecond,expires.nano)))
            result
        }
    }
    fun start(identity:FirebaseIdentity,r:OnboardingStartRequest):OnboardingResponse=mutate(identity,r.operationId,r.expectedRevision,"START",r) { tx,s,now ->
        onboardingCheck(s.state.status!=OnboardingStatus.COMPLETED,"ONBOARDING_ALREADY_COMPLETED")
        if(s.state.status==OnboardingStatus.IN_PROGRESS) s to catalog(tx,s.state.catalogVersion!!)
        else {
            val version=(tx.read("systemConfig/onboardingCatalog")?.get("publishedVersion") as? Number)?.toLong()
                ?: throw OnboardingFailure("ONBOARDING_CATALOG_NOT_FOUND",404)
            onboardingCheck(version in 1..Int.MAX_VALUE.toLong(),"DEPENDENCY_UNAVAILABLE",503)
            val p=catalog(tx,version.toInt()); val first=ordered(p).firstOrNull() ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
            s.copy(state=s.state.start(p.catalogVersion,first.key,now)) to p
        }
    }.onboarding
    private fun progress(s:Snapshot,version:Int) {
        onboardingCheck(s.state.status!=OnboardingStatus.COMPLETED,"ONBOARDING_ALREADY_COMPLETED")
        onboardingCheck(s.state.status==OnboardingStatus.IN_PROGRESS,"ONBOARDING_NOT_STARTED")
        onboardingCheck(s.state.catalogVersion==version,"ONBOARDING_CATALOG_VERSION_MISMATCH")
    }
    private fun reachable(s:Snapshot,p:OnboardingCatalogPublication,key:String) {
        val steps=ordered(p);val target=steps.indexOfFirst{it.key==key}
        onboardingCheck(target>=0,"ONBOARDING_STEP_INACTIVE")
        onboardingCheck(steps.take(target).all{it.key in s.state.completedStepKeys || it.key in s.state.skippedStepKeys},"ONBOARDING_STEP_NOT_REACHABLE")
    }
    fun save(identity:FirebaseIdentity,key:String,r:SaveStepRequest):OnboardingMutationResponse =
        mutate(identity,r.operationId,r.expectedRevision,"SAVE:$key",r) { tx,s,now ->
            progress(s,r.catalogVersion);val p=catalog(tx,r.catalogVersion)
            val step=p.steps.find{it.key==key} ?: throw OnboardingFailure("ONBOARDING_STEP_NOT_FOUND",404)
            onboardingCheck(step.active,"ONBOARDING_STEP_INACTIVE")
            if(r.action==OnboardingStepAction.SKIP) onboardingCheck(step.skippable && !step.required,"ONBOARDING_REQUIRED_STEP_CANNOT_SKIP",400)
            reachable(s,p,key)
            var domino=s.domino
            if(r.action==OnboardingStepAction.SKIP) onboardingCheck(r.answers.isEmpty() && r.domainRevisions.isEmpty(),"ONBOARDING_INVALID_ANSWER",400)
            else {
                onboardingCheck(step.kind==OnboardingStepKind.QUESTION,"DEPENDENCY_UNAVAILABLE",503)
                val questions=p.questions.filter{it.active && it.key in step.questionKeys}
                onboardingCheck(r.answers.map{it.questionKey}.distinct().size==r.answers.size,"ONBOARDING_INVALID_ANSWER",400)
                onboardingCheck(r.answers.all{a->questions.any{it.key==a.questionKey && it.type==a.type}},"ONBOARDING_INVALID_ANSWER",400)
                onboardingCheck(questions.filter{it.required}.all{q->r.answers.any{it.questionKey==q.key}},"ONBOARDING_REQUIRED_FIELD_MISSING",400)
                onboardingCheck(r.domainRevisions.keys==setOf("domino"),"REQUEST_INVALID",400)
                onboardingCheck(r.domainRevisions["domino"]==s.domino.revision,"REVISION_MISMATCH")
                r.answers.forEach { a ->
                    val q=questions.single{it.key==a.questionKey}
                    when(q.binding) {
                        OnboardingBinding.EXPERIENCE_LEVEL -> {
                            onboardingCheck(p.options.any{it.key==a.optionKey && it.questionKey==q.key && it.active && it.key in q.optionKeys},"ONBOARDING_INVALID_ANSWER",400)
                            domino=domino.copy(experienceLevel=ExperienceLevel.valueOf(a.optionKey))
                        }
                        OnboardingBinding.PREFERRED_COACH -> {
                            if(s.domino.preferredCoachKey==a.optionKey && s.domino.selectedCoachCatalogVersion==p.coachCatalogVersion)
                                coach.validateExisting(tx,p.coachCatalogVersion,a.optionKey)
                            else coach.validate(tx,p.coachCatalogVersion,a.optionKey)
                            onboardingCheck(p.coachCatalogVersion!=null,"DEPENDENCY_UNAVAILABLE",503)
                            domino=domino.copy(preferredCoachKey=a.optionKey,selectedCoachCatalogVersion=p.coachCatalogVersion)
                        }
                    }
                }
                if(domino!=s.domino) domino=domino.copy(revision=Math.addExact(domino.revision,1),updatedAt=now)
            }
            val completed=if(r.action==OnboardingStepAction.SAVE)(s.state.completedStepKeys+key).distinct()else s.state.completedStepKeys-key
            val skipped=if(r.action==OnboardingStepAction.SKIP)(s.state.skippedStepKeys+key).distinct()else s.state.skippedStepKeys-key
            val steps=ordered(p)
            val next=steps.firstOrNull{it.key !in completed && it.key !in skipped}?.key ?: steps.last().key
            val last=if(r.action==OnboardingStepAction.SAVE)key else s.state.lastCompletedStepKey
            s.copy(domino=domino,state=s.state.copy(completedStepKeys=completed,skippedStepKeys=skipped,lastCompletedStepKey=last,
                currentStepKey=next,currentSubstepKey=null,revision=Math.addExact(s.state.revision,1),updatedAt=now)) to p
        }
    fun cursor(identity:FirebaseIdentity,r:OnboardingCursorRequest):OnboardingResponse=mutate(identity,r.operationId,r.expectedRevision,"CURSOR",r) { tx,s,now ->
        progress(s,r.catalogVersion);val p=catalog(tx,r.catalogVersion)
        onboardingCheck(r.targetSubstepKey==null,"ONBOARDING_INVALID_ANSWER",400)
        onboardingCheck(p.steps.any{it.key==r.targetStepKey},"ONBOARDING_STEP_NOT_FOUND",404)
        reachable(s,p,r.targetStepKey)
        s.copy(state=s.state.copy(currentStepKey=r.targetStepKey,currentSubstepKey=null,revision=Math.addExact(s.state.revision,1),updatedAt=now)) to p
    }.onboarding
    fun complete(identity:FirebaseIdentity,r:OnboardingCompleteRequest):OnboardingResponse=mutate(identity,r.operationId,r.expectedRevision,"COMPLETE",r) { tx,s,now ->
        if(s.state.status==OnboardingStatus.COMPLETED) {
            if(s.state.catalogVersion!=null) onboardingCheck(s.state.catalogVersion==r.catalogVersion,"ONBOARDING_CATALOG_VERSION_MISMATCH")
            s to s.state.catalogVersion?.let{catalog(tx,it)}
        } else {
            progress(s,r.catalogVersion);val p=catalog(tx,r.catalogVersion)
            val required=ordered(p).filter{it.required}
            onboardingCheck(required.all{it.key in s.state.completedStepKeys},"ONBOARDING_INCOMPLETE",400)
            try { DisplayNameRules.validate(s.player.displayName) } catch(e:Exception){throw OnboardingFailure("ONBOARDING_INCOMPLETE",400)}
            required.forEach { step -> p.questions.filter{it.active && it.required && it.key in step.questionKeys}.forEach { q ->
                when(q.binding) {
                    OnboardingBinding.EXPERIENCE_LEVEL -> onboardingCheck(p.options.any{it.active && it.questionKey==q.key && it.key==s.domino.experienceLevel?.name},"ONBOARDING_INCOMPLETE",400)
                    OnboardingBinding.PREFERRED_COACH -> {
                        onboardingCheck(s.domino.preferredCoachKey!=null && s.domino.selectedCoachCatalogVersion==p.coachCatalogVersion,"ONBOARDING_INCOMPLETE",400)
                        coach.validateExisting(tx,p.coachCatalogVersion,s.domino.preferredCoachKey)
                    }
                }
            } }
            s.copy(state=s.state.copy(status=OnboardingStatus.COMPLETED,currentStepKey=null,currentSubstepKey=null,
                completedAt=now,completionOrigin=CompletionOrigin.FLOW,revision=Math.addExact(s.state.revision,1),updatedAt=now)) to p
        }
    }.onboarding
}
