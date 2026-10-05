using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.Player;

namespace Domino.UI.AppShell {
 public interface IAppMembershipSource:IDisposable {
  bool IsCurrent {get;}
  EntitlementSummaryDto Entitlements {get;}
  TrialEligibilityDto TrialEligibility {get;}
  event Action Changed;
  Task<MembershipCatalogDto> LoadAsync(string locale,CancellationToken token);
 }
 // Reads existing contracts only; PlayerService remains the sole session commercial-state owner.
 public sealed class AppMembershipApiSource:IAppMembershipSource {
  readonly OnboardingApiSession session; readonly PlayerService player;
  readonly MembershipCatalogApiClient catalog; readonly TrialActivationApiClient trial;
  bool disposed;
  public AppMembershipApiSource(OnboardingApiSession session,PlayerService player){
   this.session=session;this.player=player;catalog=new MembershipCatalogApiClient(session);trial=new TrialActivationApiClient(session,player);
   player.EntitlementsChanged+=Notify;player.SyncStateChanged+=Sync;player.SnapshotChanged+=Snapshot;
  }
  public bool IsCurrent {get {if(disposed||!player.IsCurrentSession||!player.IsFresh)return false;try{session.EnsureCurrent();return true;}catch(OperationCanceledException){return false;}}}
  public EntitlementSummaryDto Entitlements=>IsCurrent?player.Entitlements:null;
  public TrialEligibilityDto TrialEligibility=>IsCurrent?player.TrialEligibility:null;
  public event Action Changed;
  void Notify()=>Changed?.Invoke();void Sync(PlayerSyncState ignored)=>Notify();void Snapshot(PlayerSnapshot ignored)=>Notify();
  public async Task<MembershipCatalogDto> LoadAsync(string locale,CancellationToken token){
   session.EnsureCurrent();var result=await catalog.LoadAsync(locale,null,token);
   await trial.RefreshEntitlementsAsync(token);session.EnsureCurrent();return result;
  }
  public void Dispose(){if(disposed)return;disposed=true;player.EntitlementsChanged-=Notify;player.SyncStateChanged-=Sync;player.SnapshotChanged-=Snapshot;Changed=null;session.Dispose();}
 }
 public sealed class AppMembershipController:IAppMembershipPresentation,IDisposable {
  readonly IAppMembershipSource source;readonly CancellationTokenSource lifetime=new CancellationTokenSource();
  MembershipCatalogDto catalog;bool disposed,loading;int loadGeneration;
  public AppMembershipController(IAppMembershipSource source){this.source=source;if(source!=null)source.Changed+=Notify;}
  public event Action Changed;
  public string Locale{get;private set;}="en";
  public MembershipCatalogDto Membership=>source?.IsCurrent==true?catalog:null;
  public string SelectedMembershipPlan{get;private set;}
  public string MembershipBillingPeriod{get;private set;}="MONTHLY";
  public string Error{get;private set;}="";
  public bool Busy=>loading;
  public bool InteractionBlocked=>false;
  public bool MembershipIsFamily=>Membership?.plans.FirstOrDefault(p=>p.key==SelectedMembershipPlan)?.productKind=="MULTI_PLAYER";
  public EntitlementSummaryDto MembershipEntitlements=>source?.IsCurrent==true?source.Entitlements:null;
  public TrialEligibilityDto MembershipTrialEligibility=>source?.IsCurrent==true?source.TrialEligibility:null;
  public string State=>disposed||loading||catalog==null||Error.Length>0?"UNAVAILABLE":MembershipStatePresentation.Classify(MembershipEntitlements,MembershipTrialEligibility);
  public string TrialFeedback{get;private set;}="";
  public bool TrialRetry=>false;
  public event Action StartTrialRequested;
  public bool CanActivateTrial=>!disposed&&!Busy&&source?.IsCurrent==true&&!MembershipIsFamily&&
   (SelectedMembershipPlan=="DIAMOND"||SelectedMembershipPlan=="PLATINUM"||SelectedMembershipPlan=="GOLD")&&
   (MembershipTrialEligibility?.trial!=null||Membership?.trialPresentation?.product=="PREMIUM_LEGACY")&&
   State=="FREE_TRIAL_AVAILABLE"&&MembershipTrialEligibility?.activationMode=="EXPLICIT"&&MembershipTrialEligibility.policyVersion>0&&MembershipTrialEligibility.periodDays>0;
  void Notify(){if(!disposed)Changed?.Invoke();}
  public async Task LoadAsync(string locale){
   if(disposed)return;Locale=OnboardingApiSession.Locale(locale);int generation=++loadGeneration;loading=true;catalog=null;Error="";Notify();
   try{
    if(source?.IsCurrent!=true)throw new OperationCanceledException();
    var next=await source.LoadAsync(Locale,lifetime.Token);
    if(disposed||generation!=loadGeneration)return;
    if(!source.IsCurrent)throw new OperationCanceledException();
    if(next==null||next.schemaVersion!=1||next.plans==null||next.features==null||next.plans.Any(p=>p==null||p.features==null||string.IsNullOrWhiteSpace(p.key))||next.features.Any(f=>f==null||string.IsNullOrWhiteSpace(f.key)))throw new DominoApiException(ApiFailure.Contract);
    catalog=next;
    if(!catalog.plans.Any(p=>p.active&&p.key==SelectedMembershipPlan))SelectedMembershipPlan=catalog.plans.Where(p=>p.active&&p.key!="FREE").OrderBy(p=>p.sortOrder).ThenBy(p=>p.key,StringComparer.Ordinal).FirstOrDefault()?.key;
   }catch(OperationCanceledException){if(!disposed&&generation==loadGeneration)Error="SESSION_UNAVAILABLE";}
   catch(Exception){if(!disposed&&generation==loadGeneration)Error="LOAD_FAILED";}
   finally{if(!disposed&&generation==loadGeneration){loading=false;Notify();}}
  }
  public void SelectMembershipPlan(string key){if(disposed||Busy||InteractionBlocked)return;if(Membership?.plans.Any(p=>p.active&&p.key==key)==true){SelectedMembershipPlan=key;Notify();}}
  public void SelectMembershipPeriod(string period){if(disposed||Busy)return;if(period=="MONTHLY"||period=="YEARLY"){MembershipBillingPeriod=period;Notify();}}
  // Presentation intent only. A future explicitly authorized trial host may subscribe.
  // No production activation transport is connected by MEMBERSHIP-01B.
  public Task ActivateMembershipTrialAsync(){
   if(!CanActivateTrial)return Task.CompletedTask;
   TrialFeedback="START_TRIAL_REQUESTED";StartTrialRequested?.Invoke();Notify();return Task.CompletedTask;
  }
  public void Dispose(){if(disposed)return;disposed=true;++loadGeneration;lifetime.Cancel();if(source!=null){source.Changed-=Notify;source.Dispose();}catalog=null;Changed=null;StartTrialRequested=null;lifetime.Dispose();}
 }
}
