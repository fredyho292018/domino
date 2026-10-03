package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import java.time.Instant
import java.util.UUID

class OnboardingProgressService(private val repository: OnboardingProgressRepository,
    private val coach: OnboardingCoachValidation = UnavailableOnboardingCoach, private val clock: Clock = Clock.systemUTC()) {
    private data class Snapshot(val player:Player,val preferences:PlayerPreferences,val domino:DominoProfile,val state:PlayerOnboarding,val rawPlayer:Map<String,Any>)
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
            read("dominoProfile",DominoProfile::class.java),read("onboarding",PlayerOnboarding::class.java),player)
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
            val key=when(q.binding){OnboardingBinding.EXPERIENCE_LEVEL->s.domino.experienceLevel?.name;OnboardingBinding.PREFERRED_COACH->s.domino.preferredCoachKey
                else->OnboardingProfileBinding.value(q.binding,s.player,s.preferences)}
            key?.let{if(q.type==OnboardingQuestionType.TEXT)OnboardingAnswer(q.key,q.type,textValue=it)else OnboardingAnswer(q.key,q.type,it)}
        } ?: emptyList()
        val missing=if(s.state.status==OnboardingStatus.COMPLETED) emptyList() else p?.questions?.filter { q ->
            q.required && q.active && p.steps.any{it.active && q.key in it.questionKeys} && answers.none{it.questionKey==q.key}
        }?.map{it.binding.name} ?: emptyList()
        val o=s.state
        return OnboardingResponse(o.status,o.catalogVersion,o.currentStepKey,o.currentSubstepKey,o.lastCompletedStepKey,o.completedStepKeys,
            o.skippedStepKeys,o.startedAt,o.completedAt,o.updatedAt,o.revision,o.completionOrigin,missing,
            OnboardingDomainRevisions(s.player.profileRevision,s.preferences.revision,s.domino.revision),answers,
            if(p?.questions?.any{it.binding in OnboardingProfileBinding.bindings}==true)
                OnboardingBasicProfile(s.player.firstName,s.player.lastName,s.player.displayName,s.player.countryCode,s.preferences.preferredLocale,s.preferences.timeZone) else null)
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
            if(next.player!=s.player) {
                val stamp=com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,now.nano)
                // Preserve unknown/root metadata, lifecycle timestamps and account identity.
                val changes=mapOf("displayName" to next.player.displayName,"language" to next.player.language,
                    "profileRevision" to next.player.profileRevision,"updatedAt" to stamp)+listOfNotNull(
                    next.player.firstName?.let{"firstName" to it},next.player.lastName?.let{"lastName" to it},next.player.countryCode?.let{"countryCode" to it}).toMap()
                if(next.player.displayName!=s.player.displayName) {
                    tx.read("$root/publicIdentity/current")?.let { identity ->
                        val publicId=identity["publicPlayerId"] as? String
                        onboardingCheck(publicId!=null && Regex("[A-Za-z0-9_-]{1,128}").matches(publicId),"DEPENDENCY_UNAVAILABLE",503)
                        val path="publicPlayerProfiles/$publicId"
                        val public=tx.read(path) ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
                        tx.write(path,public+mapOf("displayName" to next.player.displayName,
                            "normalizedDisplayName" to com.teamfho.domino.social.SocialNames.normalize(next.player.displayName),"updatedAt" to stamp))
                    }
                }
                tx.write(root,s.rawPlayer+changes)
            }
            if(next.preferences!=s.preferences) tx.write("$root/preferences/current",FoundationDocumentCodec.encode(next.preferences))
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
            var player=s.player
            var preferences=s.preferences
            onboardingCheck(r.detectedTimeZone==null || key=="BASIC_PROFILE_STEP","REQUEST_INVALID",400)
            if(r.action==OnboardingStepAction.SKIP) onboardingCheck(r.answers.isEmpty() && r.domainRevisions.isEmpty(),"ONBOARDING_INVALID_ANSWER",400)
            else {
                onboardingCheck(step.kind==OnboardingStepKind.QUESTION,"DEPENDENCY_UNAVAILABLE",503)
                val questions=p.questions.filter{it.active && it.key in step.questionKeys}
                onboardingCheck(r.answers.map{it.questionKey}.distinct().size==r.answers.size,"ONBOARDING_INVALID_ANSWER",400)
                onboardingCheck(r.answers.all{a->questions.any{it.key==a.questionKey && it.type==a.type}},"ONBOARDING_INVALID_ANSWER",400)
                onboardingCheck(questions.filter{it.required}.all{q->r.answers.any{it.questionKey==q.key}},"ONBOARDING_REQUIRED_FIELD_MISSING",400)
                val basic=questions.any{it.binding in OnboardingProfileBinding.bindings}
                onboardingCheck(r.domainRevisions.keys==if(basic)setOf("profile","preferences")else setOf("domino"),"REQUEST_INVALID",400)
                if(basic) {
                    onboardingCheck(r.domainRevisions["profile"]==s.player.profileRevision && r.domainRevisions["preferences"]==s.preferences.revision,"REVISION_MISMATCH")
                    preferences=preferences.copy(timeZone=OnboardingProfileBinding.initializeTimeZone(preferences.timeZone,r.detectedTimeZone))
                } else onboardingCheck(r.domainRevisions["domino"]==s.domino.revision,"REVISION_MISMATCH")
                r.answers.forEach { a ->
                    val q=questions.single{it.key==a.questionKey}
                    onboardingCheck(q.binding in OnboardingProfileBinding.bindings || (a.textValue==null && a.optionKey!=null),"ONBOARDING_INVALID_ANSWER",400)
                    when(q.binding) {
                        OnboardingBinding.EXPERIENCE_LEVEL -> {
                            onboardingCheck(p.options.any{it.key==a.optionKey && it.questionKey==q.key && it.active && it.key in q.optionKeys},"ONBOARDING_INVALID_ANSWER",400)
                            domino=domino.copy(experienceLevel=ExperienceLevel.valueOf(requireNotNull(a.optionKey)))
                        }
                        OnboardingBinding.PREFERRED_COACH -> {
                            if(s.domino.preferredCoachKey==a.optionKey && s.domino.selectedCoachCatalogVersion==p.coachCatalogVersion)
                                coach.validateExisting(tx,p.coachCatalogVersion,a.optionKey)
                            else coach.validate(tx,p.coachCatalogVersion,a.optionKey)
                            onboardingCheck(p.coachCatalogVersion!=null,"DEPENDENCY_UNAVAILABLE",503)
                            domino=domino.copy(preferredCoachKey=a.optionKey,selectedCoachCatalogVersion=p.coachCatalogVersion)
                        }
                        else -> {
                            val value=OnboardingProfileBinding.answer(q.binding,q.type,a)
                            when(q.binding) {
                                OnboardingBinding.FIRST_NAME->player=player.copy(firstName=value)
                                OnboardingBinding.LAST_NAME->player=player.copy(lastName=value)
                                OnboardingBinding.DISPLAY_NAME->player=player.copy(displayName=value)
                                OnboardingBinding.COUNTRY_CODE->player=player.copy(countryCode=value)
                                OnboardingBinding.PREFERRED_LOCALE->{preferences=preferences.copy(preferredLocale=value);player=player.copy(language=value)}
                                else->error("UNSUPPORTED_PROFILE_BINDING")
                            }
                        }
                    }
                }
                if (basic) {
                    try {
                        PlayerAliasReservations.prepare(tx::read, identity.uid, s.player.displayName, player.displayName)
                            .forEach { (path, value) -> tx.write(path, value) }
                    } catch (e: PlayerFoundationException) { throw OnboardingFailure(e.code.name, 409) }
                }
                if(player!=s.player) player=player.copy(profileRevision=Math.addExact(player.profileRevision,1),updatedAt=FoundationTimestamp.Recorded(now))
                if(preferences!=s.preferences) preferences=preferences.copy(revision=Math.addExact(preferences.revision,1),updatedAt=now)
                if(domino!=s.domino) domino=domino.copy(revision=Math.addExact(domino.revision,1),updatedAt=now)
            }
            val completed=if(r.action==OnboardingStepAction.SAVE)(s.state.completedStepKeys+key).distinct()else s.state.completedStepKeys-key
            val skipped=if(r.action==OnboardingStepAction.SKIP)(s.state.skippedStepKeys+key).distinct()else s.state.skippedStepKeys-key
            val steps=ordered(p)
            val next=steps.firstOrNull{it.key !in completed && it.key !in skipped}?.key ?: steps.last().key
            val last=if(r.action==OnboardingStepAction.SAVE)key else s.state.lastCompletedStepKey
            s.copy(player=player,preferences=preferences,domino=domino,state=s.state.copy(completedStepKeys=completed,skippedStepKeys=skipped,lastCompletedStepKey=last,
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
                    else -> {
                        try { OnboardingProfileBinding.validated(q.binding,OnboardingProfileBinding.value(q.binding,s.player,s.preferences)) }
                        catch(e:OnboardingFailure){throw OnboardingFailure("ONBOARDING_INCOMPLETE",400)}
                    }
                }
            } }
            s.copy(state=s.state.copy(status=OnboardingStatus.COMPLETED,currentStepKey=null,currentSubstepKey=null,
                completedAt=now,completionOrigin=CompletionOrigin.FLOW,revision=Math.addExact(s.state.revision,1),updatedAt=now)) to p
        }
    }.onboarding
}
