using Domino.UI.Theming;
namespace Domino.UI.AppShell {
 // Onboarding-only footer/navigation. Shared content never invokes the onboarding state machine.
 public sealed class ProductionMembershipView:SharedMembershipExperience {
  readonly OnboardingShellController host;
  public ProductionMembershipView(OnboardingShellController controller):base(new OnboardingMembershipPresentation(controller)){host=controller;name="MEMBERSHIP_STEP";HostContent();}
  public override void Refresh(){base.Refresh();HostContent();}
  void HostContent(){
   bool es=host.Locale=="es";
   var optional=Text(this,es?"OPCIONAL · No necesitas comprar para continuar.":"OPTIONAL · No purchase is needed to continue.","MembershipOptional",TextRole.Secondary);
   optional.style.marginBottom=ThemeProvider.Current.Spacing.MD;Insert(0,optional);
   if(host.Membership==null)return;
   Action(this,host.MembershipRetry?(es?"Reintentar progreso":"Retry progress"):(es?"Ahora no":"Not now"),"MembershipNotNow",()=>{_=host.SkipMembershipAsync();},host.CanSkipMembership);
   Feedback(this,string.IsNullOrEmpty(host.FlowFeedback)?host.MembershipFeedback:host.FlowFeedback,false);
  }
 }
}
