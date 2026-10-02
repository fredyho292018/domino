using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Newtonsoft.Json;

namespace Domino.UI.AppShell
{
    public interface IOnboardingShellSource
    {
        Task<OnboardingStateDto> LoadAsync(CancellationToken token);
        Task<OnboardingCatalogDto> CatalogAsync(string locale, CancellationToken token);
    }
    // Composition is explicit: this adapter is not registered in startup routing.
    public interface IOnboardingFlowSource
    {
        object PrepareStart();
        object PrepareComplete();
        Task<OnboardingStateDto> ExecuteFlowAsync(object operation, CancellationToken token);
    }
    public sealed class OnboardingShellApiSource : IOnboardingShellSource, IOnboardingFlowSource, IBasicProfileSource, IExperienceSource, ICoachSource, IContactsSource, IMembershipSource, Domino.Player.IPlayerPresentationCatalogs
    {
        readonly OnboardingApiClient client;readonly CoachCatalogApiClient coachClient;readonly MembershipCatalogApiClient membershipClient;readonly TrialActivationApiClient trialClient;readonly Func<TrialEligibilityDto> trialEligibility;readonly Func<EntitlementSummaryDto> entitlements;
        public OnboardingShellApiSource(OnboardingApiClient client,CoachCatalogApiClient coachClient=null,MembershipCatalogApiClient membershipClient=null,TrialActivationApiClient trialClient=null,Func<TrialEligibilityDto> trialEligibility=null,Func<EntitlementSummaryDto> entitlements=null) { this.client=client ?? throw new ArgumentNullException(nameof(client));this.coachClient=coachClient;this.membershipClient=membershipClient;this.trialClient=trialClient;this.trialEligibility=trialEligibility;this.entitlements=entitlements; }
        public Task<CoachCatalogDto> LoadCoachesAsync(string locale,int? version,CancellationToken token)=>(coachClient??throw new InvalidOperationException("Coach catalog client required.")).LoadAsync(locale,version,token);
        public Task<OnboardingCatalogDto> ReadPinnedOnboardingCatalogAsync(string locale,int version,CancellationToken token)=>client.PinnedCatalogAsync(locale,version,token);
        public Task<CoachCatalogDto> ReadPinnedCoachCatalogAsync(string locale,int version,CancellationToken token)=>LoadCoachesAsync(locale,version,token);
        public TrialEligibilityDto TrialEligibility=>trialEligibility?.Invoke();
        public EntitlementSummaryDto CurrentEntitlements=>entitlements?.Invoke();
        public Task<MembershipCatalogDto> LoadMembershipAsync(string locale,int? version,CancellationToken token)=>(membershipClient??throw new InvalidOperationException("Membership catalog client required.")).LoadAsync(locale,version,token);
        public object PrepareMembershipSkip()=>client.PrepareSave("MEMBERSHIP_STEP","SKIP",Array.Empty<OnboardingAnswerDto>());
        public object PrepareMembershipBack()=>client.PrepareCursor("CONTACTS_STEP");
        public async Task<OnboardingStateDto> ExecuteMembershipAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public object PrepareTrial(long version)=>(trialClient??throw new InvalidOperationException("Trial client required.")).Prepare(version);
        public Task<TrialActivationResponseDto> ExecuteTrialAsync(object operation,CancellationToken token)=>(trialClient??throw new InvalidOperationException("Trial client required.")).ExecuteAsync((OnboardingOperation)operation,token);
        public object PrepareContactsSkip()=>client.PrepareSave("CONTACTS_STEP","SKIP",Array.Empty<OnboardingAnswerDto>());
        public object PrepareContactsBack()=>client.PrepareCursor("COACH_STEP");
        public async Task<OnboardingStateDto> ExecuteContactsAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public object PrepareCoach(string questionKey,string key)=>client.PrepareSave("COACH_STEP","SAVE",new[]{new OnboardingAnswerDto{questionKey=questionKey,type="COACH_SELECT",optionKey=key}});
        public object PrepareCoachBack()=>client.PrepareCursor("EXPERIENCE_STEP");
        public async Task<OnboardingStateDto> ExecuteCoachAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public object PrepareExperience(string questionKey,string optionKey)=>client.PrepareSave("EXPERIENCE_STEP","SAVE",new[]{OnboardingApiClient.Experience(questionKey,optionKey)});
        public object PrepareExperienceBack()=>client.PrepareCursor("BASIC_PROFILE_STEP");
        public async Task<OnboardingStateDto> ExecuteExperienceAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public object PrepareProfile(OnboardingAnswerDto[] answers,string zone)=>client.PrepareSave("BASIC_PROFILE_STEP","SAVE",answers,zone);
        public async Task<OnboardingStateDto> SaveProfileAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public Task<OnboardingStateDto> LoadAsync(CancellationToken token)=>client.LoadAsync(token);
        public object PrepareStart()=>client.PrepareStart();
        public object PrepareComplete()=>client.PrepareComplete();
        public async Task<OnboardingStateDto> ExecuteFlowAsync(object operation,CancellationToken token)=>(await client.ExecuteAsync((OnboardingOperation)operation,token)).onboarding;
        public Task<OnboardingCatalogDto> CatalogAsync(string locale,CancellationToken token)=>client.CatalogAsync(locale,token);
    }
    public enum OnboardingShellPhase { Idle, Loading, NotStarted, InProgress, Completed, Error, UpdateRequired }
    // Sole presentation owner. Responses are published atomically; views receive defensive snapshots.
    // All steps, start and completion share this owner. No application navigation occurs here.
    public sealed partial class OnboardingShellController : IDisposable
    {
        readonly IOnboardingShellSource source;
        readonly CancellationTokenSource lifetime;
        OnboardingStateDto state; OnboardingCatalogDto catalog;
        bool disposed, busy;
        object pendingFlow;
        bool pendingFlowIsStart;
        public string FlowFeedback { get; private set; }="";
        public bool FlowRetry=>pendingFlow!=null&&!busy;
        public bool CanStart=>!disposed&&!busy&&!lifetime.IsCancellationRequested&&source is IOnboardingFlowSource&&Phase==OnboardingShellPhase.NotStarted;
        public bool CanComplete=>!disposed&&!busy&&!lifetime.IsCancellationRequested&&source is IOnboardingFlowSource&&Phase==OnboardingShellPhase.InProgress&&pendingTrial==null&&pendingMembership==null;
        public Task StartAsync()=>CanStart?RunFlowAsync(true):Task.CompletedTask;
        public Task CompleteAsync()=>CanComplete?RunFlowAsync(false):Task.CompletedTask;
        async Task RunFlowAsync(bool start)
        {
            if(pendingFlow!=null&&pendingFlowIsStart!=start)return;
            busy=true;FlowFeedback="SAVING";Changed?.Invoke();
            try {
                var writer=(IOnboardingFlowSource)source;
                if(pendingFlow==null){pendingFlowIsStart=start;pendingFlow=start?writer.PrepareStart():writer.PrepareComplete();}
                var next=await writer.ExecuteFlowAsync(pendingFlow,lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                var nextCatalog=start?await source.CatalogAsync(Locale,lifetime.Token):catalog;
                lifetime.Token.ThrowIfCancellationRequested();Validate(next,nextCatalog);
                if(next.revision<state.revision||(!start&&next.status!="COMPLETED"))throw new DominoApiException(ApiFailure.Contract);
                state=Copy(next);catalog=Copy(nextCatalog);pendingFlow=null;FlowFeedback="";
                draft=null;InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();await RefreshMembershipAsync();
                Phase=state.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
            }catch(OperationCanceledException){}
            catch(DominoApiException e){
                if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingFlow=null;Phase=OnboardingShellPhase.UpdateRequired;}
                else if(e.HttpStatus==400||e.HttpStatus==403){pendingFlow=null;FlowFeedback="VALIDATION";}
                else if(e.HttpStatus==409){
                    pendingFlow=null;FlowFeedback="CONFLICT";
                    try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);state=Copy(next);catalog=Copy(c);draft=null;InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();await RefreshMembershipAsync();Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:next.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}
                    catch(OperationCanceledException){}catch(Exception){Phase=OnboardingShellPhase.Error;}
                }else FlowFeedback="NETWORK";
            }catch(Exception){FlowFeedback="NETWORK";}
            finally{busy=false;if(!disposed)Changed?.Invoke();}
        }
        public event Action Changed;
        public OnboardingShellPhase Phase { get; private set; }
        public string Locale { get; private set; }="en";
        public bool Busy=>busy;
        public bool CanRetry=>Phase==OnboardingShellPhase.Error;
        public OnboardingStateDto State=>Copy(state);
        public OnboardingCatalogDto Catalog=>Copy(catalog);
        public OnboardingStepDto CurrentStep=>Copy(catalog?.steps?.SingleOrDefault(s=>s.key==state?.currentStepKey));
        static T Copy<T>(T value)=>value==null?default(T):JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        public OnboardingShellController(IOnboardingShellSource source,CancellationToken sessionLifetime=default)
        {this.source=source ?? throw new ArgumentNullException(nameof(source));lifetime=CancellationTokenSource.CreateLinkedTokenSource(sessionLifetime);}
        public Task RetryAsync()=>CanRetry?LoadAsync(Locale):Task.CompletedTask;
        public async Task LoadAsync(string locale)
        {
            if(disposed||busy||lifetime.IsCancellationRequested)return;
            busy=true;Locale=locale=="es"?"es":"en";Phase=OnboardingShellPhase.Loading;Changed?.Invoke();
            try {
                var next=await source.LoadAsync(lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                var nextCatalog=await source.CatalogAsync(Locale,lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                Validate(next,nextCatalog);
                if(state!=null && next.revision<state.revision)throw new InvalidOperationException();
                state=Copy(next);catalog=Copy(nextCatalog);InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();await RefreshMembershipAsync();
                Phase=next.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:next.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
            } catch(OperationCanceledException) { if(!disposed){state=null;catalog=null;Phase=OnboardingShellPhase.Idle;} }
            catch(DominoApiException e){Phase=e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"?OnboardingShellPhase.UpdateRequired:OnboardingShellPhase.Error;}
            catch(Exception){Phase=OnboardingShellPhase.Error;}
            finally {busy=false;if(!disposed)Changed?.Invoke();}
        }
        static void Validate(OnboardingStateDto s,OnboardingCatalogDto c)
        {
            if(s==null||s.revision<0||c==null||c.catalogVersion<1||c.catalogVersion>2||c.steps==null||c.steps.Length==0||
                c.steps.Any(x=>x==null||string.IsNullOrEmpty(x.key))||c.steps.Select(x=>x.key).Distinct().Count()!=c.steps.Length||
                (s.catalogVersion.HasValue&&s.catalogVersion!=c.catalogVersion)||
                (s.status!="NOT_STARTED"&&s.status!="IN_PROGRESS"&&s.status!="COMPLETED")||
                (s.status!="NOT_STARTED"&&!s.catalogVersion.HasValue))throw new InvalidOperationException();
            var allowed=new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"};
            if(c.steps.Any(x=>!allowed.Contains(x.key))||(c.catalogVersion==1&&c.steps.Any(x=>x.key=="BASIC_PROFILE_STEP"))||
                (s.status=="IN_PROGRESS"&&!c.steps.Any(x=>x.key==s.currentStepKey)))throw new InvalidOperationException();
        }
        public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();state=null;catalog=null;draft=null;pendingFlow=null;pendingProfile=null;pendingExperience=null;experienceSelection=null;pendingCoach=null;pendingContacts=null;pendingMembership=null;pendingTrial=null;membership=null;coachSelection=null;coaches=null;Changed=null;}
    }
}
