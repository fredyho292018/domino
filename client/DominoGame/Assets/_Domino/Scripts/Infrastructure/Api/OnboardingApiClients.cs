using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Infrastructure.Api
{
    public sealed class OnboardingApiClient
    {
        readonly OnboardingApiSession session;
        readonly SemaphoreSlim writes=new SemaphoreSlim(1,1);
        OnboardingStateDto state;
        public OnboardingApiClient(OnboardingApiSession session){this.session=session;}
        public OnboardingStateDto State { get { session.EnsureCurrent(); return state==null?null:OnboardingApiSession.Decode<OnboardingStateDto>(OnboardingApiSession.Serialize(state)); } }
        void Apply(OnboardingStateDto value) {
            session.EnsureCurrent();
            if(value==null || value.revision<0 || (value.status!="NOT_STARTED" && value.status!="IN_PROGRESS" && value.status!="COMPLETED") ||
                value.domainRevisions==null || value.answers==null || (value.status!="NOT_STARTED" && !(value.catalogVersion>0)))throw new DominoApiException(ApiFailure.Contract);
            // Concurrent reads and lost-response retries cannot roll back a newer server revision.
            if(state==null || value.revision>=state.revision)state=OnboardingApiSession.Decode<OnboardingStateDto>(OnboardingApiSession.Serialize(value));
        }
        public async Task<OnboardingStateDto> LoadAsync(CancellationToken token) { Apply(await session.Send<OnboardingStateDto>("GET","player/onboarding",null,token)); return State; }
        public async Task<OnboardingCatalogDto> CatalogAsync(string locale,CancellationToken token) {
            var version=Ready().catalogVersion;
            var result=await session.Send<OnboardingCatalogDto>("GET","onboarding/catalog"+OnboardingApiSession.Query(locale,version),null,token);
            if(result.catalogVersion<=0 || result.steps==null || (version.HasValue&&result.catalogVersion!=version) || (state.catalogVersion.HasValue && result.catalogVersion!=state.catalogVersion.Value))throw new DominoApiException(ApiFailure.Contract);
            return result;
        }
        OnboardingStateDto Ready(){session.EnsureCurrent();return state ?? throw new InvalidOperationException("Load onboarding state first.");}
        static string Id()=>Guid.NewGuid().ToString("D");
        public OnboardingOperation PrepareStart() {
            var s=Ready();var id=Id();return new OnboardingOperation(this,"POST","player/onboarding/start",new OnboardingStartDto{operationId=id,expectedRevision=s.revision},id);
        }
        public OnboardingOperation PrepareSave(string stepKey,string action,OnboardingAnswerDto[] answers,string detectedTimeZone=null) {
            var s=Ready();if(!s.catalogVersion.HasValue || !System.Text.RegularExpressions.Regex.IsMatch(stepKey??"",@"^[A-Z][A-Z0-9_]{0,63}$") || (action!="SAVE"&&action!="SKIP"))throw new ArgumentException("Invalid step operation.");
            var id=Id();return new OnboardingOperation(this,"PUT","player/onboarding/steps/"+stepKey,new OnboardingSaveDto {
                operationId=id,expectedRevision=s.revision,catalogVersion=s.catalogVersion.Value,action=action,answers=answers ?? Array.Empty<OnboardingAnswerDto>(),detectedTimeZone=detectedTimeZone,
                domainRevisions=action=="SKIP" ? new Dictionary<string,long>() : stepKey=="BASIC_PROFILE_STEP" ? new Dictionary<string,long>{{"profile",s.domainRevisions.profile},{"preferences",s.domainRevisions.preferences}} : new Dictionary<string,long>{{"domino",s.domainRevisions.domino}}
            },id);
        }
        public OnboardingOperation PrepareCursor(string stepKey,string substepKey=null) {
            var s=Ready();if(!s.catalogVersion.HasValue)throw new InvalidOperationException();var id=Id();
            return new OnboardingOperation(this,"PUT","player/onboarding/cursor",new OnboardingCursorDto{operationId=id,expectedRevision=s.revision,catalogVersion=s.catalogVersion.Value,targetStepKey=stepKey,targetSubstepKey=substepKey},id);
        }
        public OnboardingOperation PrepareComplete() {
            var s=Ready();if(!s.catalogVersion.HasValue)throw new InvalidOperationException();var id=Id();
            return new OnboardingOperation(this,"POST","player/onboarding/complete",new OnboardingCompleteDto{operationId=id,expectedRevision=s.revision,catalogVersion=s.catalogVersion.Value},id);
        }
        public async Task<OnboardingMutationDto> ExecuteAsync(OnboardingOperation operation,CancellationToken token) {
            if(operation==null || !ReferenceEquals(operation.Owner,this))throw new ArgumentException("Operation belongs to another client.");
            session.EnsureCurrent();await writes.WaitAsync(token);
            try { session.EnsureCurrent();var result=operation.Path.Contains("/steps/") ? await session.Send<OnboardingMutationDto>(operation.Method,operation.Path,operation.Body,token) : new OnboardingMutationDto{onboarding=await session.Send<OnboardingStateDto>(operation.Method,operation.Path,operation.Body,token)};Apply(result.onboarding);return result; }
            finally{writes.Release();}
        }
        public static OnboardingAnswerDto Experience(string questionKey,string stableKey) {
            if(stableKey!="BEGINNER"&&stableKey!="RULES_KNOWN"&&stableKey!="STRATEGY"&&stableKey!="COMPETITIVE")throw new ArgumentException("Use a stable experience key.");
            return new OnboardingAnswerDto{questionKey=questionKey,type="SINGLE_SELECT",optionKey=stableKey};
        }
    }
    public sealed class CoachCatalogApiClient
    {
        readonly OnboardingApiSession session;
        public CoachCatalogApiClient(OnboardingApiSession session){this.session=session;}
        public async Task<CoachCatalogDto> LoadAsync(string locale,int? version,CancellationToken token) {
            var dto=await session.Send<CoachCatalogDto>("GET","coaches"+OnboardingApiSession.Query(locale,version),null,token);
            if(dto.catalogVersion<=0||dto.items==null||(version.HasValue&&dto.catalogVersion!=version))throw new DominoApiException(ApiFailure.Contract);return dto;
        }
    }
    public sealed class MembershipCatalogApiClient
    {
        readonly OnboardingApiSession session;
        public MembershipCatalogApiClient(OnboardingApiSession session){this.session=session;}
        public async Task<MembershipCatalogDto> LoadAsync(string locale,int? version,CancellationToken token) {
            var dto=await session.Send<MembershipCatalogDto>("GET","membership/catalog"+OnboardingApiSession.Query(locale,version),null,token);
            if(dto.schemaVersion!=1||dto.catalogVersion<=0||dto.plans==null||dto.features==null||(version.HasValue&&dto.catalogVersion!=version))throw new DominoApiException(ApiFailure.Contract);return dto;
        }
        public static bool Purchasable(MembershipPlanDto plan,BillingProductDto product)=>plan!=null && plan.active && product!=null && product.planKey==plan.key && product.IsConfiguredForPurchase;
    }
    public sealed class TrialActivationApiClient
    {
        readonly OnboardingApiSession session;
        readonly Domino.Player.PlayerService player;
        public TrialActivationApiClient(OnboardingApiSession session,Domino.Player.PlayerService player){this.session=session;this.player=player;}
        public OnboardingOperation Prepare(long expectedPolicyVersion) {
            session.EnsureCurrent();if(expectedPolicyVersion<=0)throw new ArgumentOutOfRangeException(nameof(expectedPolicyVersion));var id=Guid.NewGuid().ToString("D");
            return new OnboardingOperation(this,"POST","player/trial/activate",new TrialActivationRequestDto{operationId=id,expectedPolicyVersion=expectedPolicyVersion},id);
        }
        public async Task<TrialActivationResponseDto> ExecuteAsync(OnboardingOperation operation,CancellationToken token) {
            if(operation==null || !ReferenceEquals(operation.Owner,this))throw new ArgumentException("Operation belongs to another client.");
            var result=await session.Send<TrialActivationResponseDto>(operation.Method,operation.Path,operation.Body,token);
            if(result.operationId!=operation.OperationId || (result.outcome!="ACTIVATED"&&result.outcome!="ALREADY_ACTIVE") || result.entitlements==null)throw new DominoApiException(ApiFailure.Contract);
            // Receipt may replay a historical snapshot. Refresh current authority; never apply receipt snapshot as current access.
            await RefreshEntitlementsAsync(token);return result;
        }
        public async Task RefreshEntitlementsAsync(CancellationToken token) {
            var current=await session.Send<EntitlementSummaryDto>("GET","player/entitlements",null,token);
            if(current.availability!="AVAILABLE"&&current.availability!="UNAVAILABLE")throw new DominoApiException(ApiFailure.Contract);
            session.EnsureCurrent();player?.ReceiveEntitlements(session.Owner,current);
        }
    }
}



