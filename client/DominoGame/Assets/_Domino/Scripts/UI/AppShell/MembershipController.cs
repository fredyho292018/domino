using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
namespace Domino.UI.AppShell {
 public interface IMembershipSource {
  Task<MembershipCatalogDto> LoadMembershipAsync(string locale,int? version,CancellationToken token);
  TrialEligibilityDto TrialEligibility {get;}
  EntitlementSummaryDto CurrentEntitlements {get;}
  object PrepareMembershipSkip(); object PrepareMembershipBack();
  Task<OnboardingStateDto> ExecuteMembershipAsync(object operation,CancellationToken token);
  object PrepareTrial(long policyVersion);
  Task<TrialActivationResponseDto> ExecuteTrialAsync(object operation,CancellationToken token);
 }
 public sealed partial class OnboardingShellController {
  MembershipCatalogDto membership; object pendingMembership,pendingTrial;
  string membershipPlan,billingPeriod="MONTHLY"; bool trialBlocked;
  public MembershipCatalogDto Membership=>Copy(membership);
  public string SelectedMembershipPlan=>membershipPlan;
  public string MembershipBillingPeriod=>billingPeriod;
  public string MembershipFeedback {get;private set;}="";
  public string TrialFeedback {get;private set;}="";
  public EntitlementSummaryDto MembershipEntitlements=>Copy((source as IMembershipSource)?.CurrentEntitlements);
  public TrialEligibilityDto MembershipTrialEligibility=>Copy((source as IMembershipSource)?.TrialEligibility);
  bool AtMembership=>Phase==OnboardingShellPhase.InProgress&&state?.currentStepKey=="MEMBERSHIP_STEP";
  public bool MembershipIsFamily=>membership?.plans?.FirstOrDefault(p=>p.key==membershipPlan)?.productKind=="MULTI_PLAYER";
  public bool MembershipSkipped=>state?.skippedStepKeys?.Contains("MEMBERSHIP_STEP")==true;
  public bool TrialRetry=>pendingTrial!=null&&!busy;
  public bool MembershipRetry=>(pendingMembership!=null||pendingFlow!=null)&&!busy;
  public bool CanSkipMembership=>AtMembership&&!busy&&pendingTrial==null&&(pendingMembership!=null||(!MembershipSkipped||source is IOnboardingFlowSource)&&CurrentStep?.skippable==true&&!CurrentStep.required);
  public bool CanBackMembership=>AtMembership&&!busy&&pendingTrial==null&&pendingMembership==null&&pendingFlow==null;
  public bool CanActivateTrial=>AtMembership&&!busy&&pendingFlow==null&&pendingMembership==null&&!MembershipIsFamily&&(pendingTrial!=null||!trialBlocked&&MembershipTrialEligibility?.eligible==true&&MembershipTrialEligibility.activationMode=="EXPLICIT"&&MembershipTrialEligibility.policyVersion>0&&MembershipTrialEligibility.periodDays>0&&membership?.trialPresentation?.product=="PREMIUM_LEGACY");
  public void SelectMembershipPlan(string key){if(disposed||busy||pendingTrial!=null||pendingMembership!=null||!AtMembership)return;if(membership?.plans?.Any(p=>p.active&&p.key==key)==true){membershipPlan=key;Changed?.Invoke();}}
  public void SelectMembershipPeriod(string period){if(disposed||busy||!AtMembership)return;if(period=="MONTHLY"||period=="YEARLY"){billingPeriod=period;Changed?.Invoke();}}
  async Task RefreshMembershipAsync(){
   if(state?.currentStepKey!="MEMBERSHIP_STEP"||!(source is IMembershipSource provider))return;
   var next=await provider.LoadMembershipAsync(Locale,catalog.membershipCatalogVersion,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();ValidateMembership(next,catalog.membershipCatalogVersion);membership=Copy(next);
   if(!membership.plans.Any(p=>p.active&&p.key==membershipPlan))membershipPlan=membership.plans.Where(p=>p.active).OrderBy(p=>p.sortOrder).ThenBy(p=>p.key,StringComparer.Ordinal).FirstOrDefault()?.key;
  }
  static void ValidateMembership(MembershipCatalogDto c,int? version){
   if(c==null||c.schemaVersion!=1||c.catalogVersion<=0||(version.HasValue&&version!=c.catalogVersion)||c.plans==null||c.features==null||c.plans.Any(p=>p==null||string.IsNullOrWhiteSpace(p.key)||string.IsNullOrWhiteSpace(p.name)||p.features==null)||c.features.Any(f=>f==null||string.IsNullOrWhiteSpace(f.key)||string.IsNullOrWhiteSpace(f.name))||c.plans.Select(p=>p.key).Distinct().Count()!=c.plans.Length||c.features.Select(f=>f.key).Distinct().Count()!=c.features.Length||c.plans.Any(p=>p.features.Any(f=>f==null||!c.features.Any(x=>x.key==f.featureKey))||p.features.Select(f=>f.featureKey).Distinct().Count()!=p.features.Length))throw new DominoApiException(ApiFailure.Contract);
  }
  public async Task ChangeMembershipLocaleAsync(string locale){
   if(disposed||busy||!AtMembership||(locale!="en"&&locale!="es")||!(source is IMembershipSource provider))return;
   busy=true;Changed?.Invoke();try{var c=await source.CatalogAsync(locale,lifetime.Token);var m=await provider.LoadMembershipAsync(locale,c.membershipCatalogVersion,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(state,c);ValidateMembership(m,c.membershipCatalogVersion);catalog=Copy(c);membership=Copy(m);Locale=locale;if(!membership.plans.Any(p=>p.active&&p.key==membershipPlan))membershipPlan=membership.plans.Where(p=>p.active).OrderBy(p=>p.sortOrder).ThenBy(p=>p.key,StringComparer.Ordinal).FirstOrDefault()?.key;}
   catch(OperationCanceledException){}catch(Exception){MembershipFeedback="LOCALE_ERROR";}finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public async Task SkipMembershipAsync(){
   if(!CanSkipMembership)return;
   if(!MembershipSkipped)await RunMembership(false);
   if(MembershipSkipped&&AtMembership&&pendingMembership==null)await CompleteAsync();
  }
  public Task BackMembershipAsync()=>CanBackMembership?RunMembership(true):Task.CompletedTask;
  async Task RunMembership(bool back){
   if(disposed||!(source is IMembershipSource provider))return;busy=true;MembershipFeedback="SAVING";Changed?.Invoke();
   try{if(pendingMembership==null)pendingMembership=back?provider.PrepareMembershipBack():provider.PrepareMembershipSkip();var next=await provider.ExecuteMembershipAsync(pendingMembership,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,catalog);if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);state=Copy(next);pendingMembership=null;MembershipFeedback="SAVED";Phase=state.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;}
   catch(OperationCanceledException){}
   catch(DominoApiException e){if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingMembership=null;Phase=OnboardingShellPhase.UpdateRequired;}else if((e.ServerErrorCode??"").Contains("REVISION")||(e.ServerErrorCode??"").Contains("CATALOG_VERSION")){pendingMembership=null;MembershipFeedback="CONFLICT";try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);state=Copy(next);catalog=Copy(c);await RefreshMembershipAsync();Phase=state.status=="COMPLETED"?OnboardingShellPhase.Completed:state.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}catch{Phase=OnboardingShellPhase.Error;}}else if(e.HttpStatus==400||e.HttpStatus==403){pendingMembership=null;MembershipFeedback="VALIDATION";}else MembershipFeedback="NETWORK";}
   catch(Exception){MembershipFeedback="NETWORK";}finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public async Task ActivateMembershipTrialAsync(){
   if(disposed||!CanActivateTrial||!(source is IMembershipSource provider))return;busy=true;TrialFeedback="LOADING";Changed?.Invoke();
   try{if(pendingTrial==null)pendingTrial=provider.PrepareTrial(provider.TrialEligibility.policyVersion.Value);var result=await provider.ExecuteTrialAsync(pendingTrial,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();if(result==null||(result.outcome!="ACTIVATED"&&result.outcome!="ALREADY_ACTIVE")||result.entitlements==null)throw new DominoApiException(ApiFailure.Contract);pendingTrial=null;trialBlocked=true;TrialFeedback=result.outcome;}
   catch(OperationCanceledException){}
   catch(DominoApiException e){var code=e.ServerErrorCode;var terminal=new[]{"TRIAL_NOT_ELIGIBLE","TRIAL_ALREADY_CONSUMED","TRIAL_POLICY_VERSION_MISMATCH","TRIAL_DISABLED","IDEMPOTENCY_CONFLICT","CLIENT_UPDATE_REQUIRED"};if(terminal.Contains(code)){pendingTrial=null;trialBlocked=true;TrialFeedback=code;}else TrialFeedback="DEPENDENCY_UNAVAILABLE";}
   catch(Exception){TrialFeedback="DEPENDENCY_UNAVAILABLE";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
 }
}
