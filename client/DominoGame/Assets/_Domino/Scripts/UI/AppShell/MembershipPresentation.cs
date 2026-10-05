using System;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;

namespace Domino.UI.AppShell {
 // UI projection of server contracts, not a second entitlement store.
 public interface IMembershipPresentation {
  string Locale {get;}
  MembershipCatalogDto Membership {get;}
  string SelectedMembershipPlan {get;}
  string MembershipBillingPeriod {get;}
  bool Busy {get;}
  bool InteractionBlocked {get;}
  bool MembershipIsFamily {get;}
  EntitlementSummaryDto MembershipEntitlements {get;}
  TrialEligibilityDto MembershipTrialEligibility {get;}
  string TrialFeedback {get;}
  bool TrialRetry {get;}
  bool CanActivateTrial {get;}
  void SelectMembershipPlan(string key);
  void SelectMembershipPeriod(string period);
  Task ActivateMembershipTrialAsync();
 }
 public interface IAppMembershipPresentation:IMembershipPresentation {
  string State {get;}
  event Action StartTrialRequested;
 }
 public sealed class OnboardingMembershipPresentation : IMembershipPresentation {
  readonly OnboardingShellController host;
  public OnboardingMembershipPresentation(OnboardingShellController host){this.host=host;}
  public string Locale=>host.Locale;
  public MembershipCatalogDto Membership=>host.Membership;
  public string SelectedMembershipPlan=>host.SelectedMembershipPlan;
  public string MembershipBillingPeriod=>host.MembershipBillingPeriod;
  public bool Busy=>host.Busy;
  public bool InteractionBlocked=>host.TrialRetry||host.MembershipRetry;
  public bool MembershipIsFamily=>host.MembershipIsFamily;
  public EntitlementSummaryDto MembershipEntitlements=>host.MembershipEntitlements;
  public TrialEligibilityDto MembershipTrialEligibility=>host.MembershipTrialEligibility;
  public string TrialFeedback=>host.TrialFeedback;
  public bool TrialRetry=>host.TrialRetry;
  public bool CanActivateTrial=>host.CanActivateTrial;
  public void SelectMembershipPlan(string key)=>host.SelectMembershipPlan(key);
  public void SelectMembershipPeriod(string period)=>host.SelectMembershipPeriod(period);
  public Task ActivateMembershipTrialAsync()=>host.ActivateMembershipTrialAsync();
 }
 public static class MembershipStatePresentation {
  public static string BillingPeriod(string value,string locale)=>value=="YEARLY"?(locale=="es"?"Anual":"Annual"):value=="MONTHLY"?(locale=="es"?"Mensual":"Monthly"):"—";
  public static bool CapabilityUsable(string status,bool usable)=>status=="IMPLEMENTED"&&usable;
  public static string MembershipIdentity(EntitlementSummaryDto value,string locale){
   var s=value?.availability=="AVAILABLE"?value.snapshot:null;
   if(s?.membershipPlan=="FREE")return locale=="es"?"Tu membresía: Gratis":"Your membership: Free";
   if(s?.membershipPlan=="PREMIUM")return locale=="es"?"Tu membresía: Premium (legacy)":"Your membership: Premium (legacy)";
   return locale=="es"?"Membresía sin confirmar":"Membership unconfirmed";
  }
  public static string TimelineDate(string instant,string locale){
   if(!DateTimeOffset.TryParse(instant,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out var date))return "—";
   // Match Profile's localized date-only convention without changing the authoritative instant.
   date=date.ToUniversalTime();
   return locale=="es"?date.ToString("d 'de' MMMM 'de' yyyy",System.Globalization.CultureInfo.GetCultureInfo("es-ES")):
    date.ToString("MMMM d, yyyy",System.Globalization.CultureInfo.GetCultureInfo("en-US"));
  }
  // Effective entitlement keys have a different authority and vocabulary from catalog features.
  public static string FeatureName(string key,string locale){bool es=locale=="es";switch(key){
   case "PUBLIC_DUEL":return es?"Duelos públicos":"Public duels";
   case "PUBLIC_PARTNERS":return es?"Partidas públicas por parejas":"Public partner games";
   case "FOLLOW_PLAYER":return es?"Seguir jugadores":"Follow players";
   case "FRIENDS":return es?"Amigos":"Friends";
   case "FRIEND_REQUESTS":return es?"Solicitudes de amistad":"Friend requests";
   case "PARTY_CREATE":return es?"Crear grupos":"Create parties";
   case "PARTY_INVITE":return es?"Invitaciones a grupos":"Party invitations";
   case "PRIVATE_DUEL":return es?"Duelos privados":"Private duels";
   case "PRIVATE_PARTNERS":return es?"Partidas privadas por parejas":"Private partner games";
   case "CHOOSE_2V2_PARTNER":return es?"Elegir pareja en 2 contra 2":"Choose a 2v2 partner";
   case "PARTY_MATCHMAKING":return es?"Buscar partida en grupo":"Party matchmaking";
   case "FULL_HISTORY":return es?"Historial completo":"Full history";
   case "FULL_REPLAY":return es?"Repeticiones completas":"Full replays";
   case "ADVANCED_STATS":return es?"Estadísticas avanzadas":"Advanced stats";
   case "PREMIUM_THEMES":return es?"Temas Premium":"Premium themes";
   default:return es?"Beneficio adicional":"Additional benefit";
  }}
  public static string Classify(EntitlementSummaryDto current,TrialEligibilityDto trial){
   var s=current?.snapshot;
   if(current?.availability!="AVAILABLE"||s==null)return "UNAVAILABLE";
   // The new contract separates commercial ownership from promotional access.
   if(s.trial!=null&&!s.trial.legacy){
    if(s.membershipPlan!="FREE"&&s.membershipPlan!="PREMIUM")return "UNAVAILABLE";
    if(s.trial.trialStatus=="ACTIVE"||s.trial.trialStatus=="EXPIRED"){
     if(s.trial.trialPlan!="DIAMOND"&&s.trial.trialPlan!="PLATINUM"&&s.trial.trialPlan!="GOLD")return "UNAVAILABLE";
     if(s.trial.trialBillingPeriod!="YEARLY"&&s.trial.trialBillingPeriod!="MONTHLY")return "UNAVAILABLE";
     return s.trial.trialStatus=="ACTIVE"?"TRIAL_ACTIVE":"TRIAL_EXPIRED";
    }
   }
   if(s.trialActive)return s.plan=="PREMIUM"&&s.status=="ACTIVE"?"TRIAL_ACTIVE":"UNAVAILABLE";
   if(s.plan=="PREMIUM")return s.status=="ACTIVE"?"PREMIUM_LEGACY":"UNAVAILABLE";
   if(s.plan!="FREE")return "UNAVAILABLE";
   if(s.trialConsumed&&s.status=="EXPIRED")return "TRIAL_EXPIRED";
   if(s.status!="FREE"||s.trialConsumed||trial==null)return "UNAVAILABLE";
   if(trial.eligible&&trial.state=="NOT_STARTED")return "FREE_TRIAL_AVAILABLE";
   return !trial.eligible&&trial.state=="INELIGIBLE"?"FREE_NOT_ELIGIBLE":"UNAVAILABLE";
  }
  public static string Copy(string state,string locale){bool es=locale=="es";switch(state){
   case "FREE_TRIAL_AVAILABLE":case "FREE_NOT_ELIGIBLE":return es?"Tu membresía: Gratis":"Your membership: Free";
   case "TRIAL_ACTIVE":return es?"Membresía sin confirmar":"Membership unconfirmed";
   case "TRIAL_EXPIRED":return es?"Tu membresía: Gratis":"Your membership: Free";
   case "PREMIUM_LEGACY":return es?"Tu membresía: Premium (legacy)":"Your membership: Premium (legacy)";
   default:return es?"No se pudo confirmar tu membresía.":"Your membership could not be confirmed.";
  }}
 }
}
