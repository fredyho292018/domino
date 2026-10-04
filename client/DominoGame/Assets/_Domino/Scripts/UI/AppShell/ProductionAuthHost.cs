using System;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionAuthHost : MonoBehaviour
    {
        ProductionAuthRouter router;
        ProductionRoutingComposition composition;
        // One host-scoped identity/catalog projection shared by Menu and Home.
        public PlayerPresentationSource PlayerPresentation { get; private set; }
        VisualElement isolatedRoot;
        VisualElement Root=>isolatedRoot??GetComponent<UIDocument>().rootVisualElement;
#if UNITY_EDITOR
        public void BindIsolated(VisualElement root,ProductionAuthRouter forms,ProductionRoutingComposition routing,PlayerPresentationSource presentation=null)
        {isolatedRoot=root;router=forms;composition=routing;PlayerPresentation=presentation;router.Changed+=Render;Render();}
#endif
        ProductionWelcomeView welcome;
        ProductionAppShell shell;
        ProductionEmailView email;
        ProductionOnboardingRoot onboarding;
        VisualElement routingStatus;
        void Start(){ApplicationServices.SessionReplaced+=Rebind;Bind();}
        void Rebind(){linking?.Dispose();linking=null;protectionOverlay?.Dispose();protection?.Dispose();protectionOverlay=null;protection=null;PlayerPresentation?.Dispose();PlayerPresentation=null;logout=null;if(router!=null)router.Changed-=Render;onboarding?.Dispose();onboarding=null;routingStatus=null;welcome=null;shell=null;email=null;Bind();}
        void Bind(){router=ApplicationServices.AuthRouter;composition=ApplicationServices.Routing;if(router==null)return;if(composition!=null&&ApplicationServices.Player!=null)PlayerPresentation=new PlayerPresentationSource(ApplicationServices.Player,composition.Router,()=>composition.Onboarding);router.Changed+=Render;Render();_ = router.RestoreAsync();}
        void Render(){
            if(protectionOverlay?.Visible==true)return;
            var root=Root;root.style.flexGrow=1;
            if(router.Route!=ProductionAuthRoute.Onboarding&&onboarding!=null){onboarding.Dispose();onboarding=null;root.Clear();}
            if(router.Route==ProductionAuthRoute.Onboarding){
                if(onboarding==null){root.Clear();welcome=null;shell=null;email=null;routingStatus=null;onboarding=new ProductionOnboardingRoot(composition.Onboarding);root.Add(onboarding);_=composition.LoadOnboardingAsync();}return;
            }
            if(router.Route==ProductionAuthRoute.Loading||router.Route==ProductionAuthRoute.Error||router.Route==ProductionAuthRoute.UpdateRequired){
                root.Clear();welcome=null;shell=null;email=null;routingStatus=new ProductionRoutingStatusView(router.Route,router.Message,()=>{_=router.RetryRoutingAsync();});root.Add(routingStatus);return;
            }
            if(router.Route==ProductionAuthRoute.AppShell){if(shell==null){root.Clear();shell=new ProductionAppShell(profileDataSource:new PlayerProfileDataSource(PlayerPresentation),signOut:RequestLogout,menuDataSource:new PlayerMenuDataSource(PlayerPresentation),homeDataSource:new PlayerHomeDataSource(PlayerPresentation));root.Add(shell);welcome=null;email=null;}}
            else if(router.Route==ProductionAuthRoute.EmailEntry||router.Route==ProductionAuthRoute.Register||router.Route==ProductionAuthRoute.VerificationPending||router.Route==ProductionAuthRoute.EmailPlaceholder||router.Route==ProductionAuthRoute.EmailSignIn||router.Route==ProductionAuthRoute.ForgotPassword){
                if(email==null||email.Route!=router.Route){root.Clear();welcome=null;shell=null;email=new ProductionEmailView(router.Route,router.NavigateEmail,(e,p,c)=>{_=router.RegisterAsync(e,p,c);},()=>{_=router.CheckVerificationAsync();},()=>{_=router.ResendVerificationAsync();},c=>{if(c)RequestLogout();},(e,p)=>{_=router.SignInEmailAsync(e,p);},e=>{_=router.ResetPasswordAsync(e);});root.Add(email);}
                email.SetState(router.Busy,router.EmailState,router.Message,router.DisplayEmail,true,router.EmailError);
                if(router.IsLinkedVerification)email.PresentLinkedVerification(Domino.UI.DominoLocalization.Language,router.EmailError,router.Busy,router.Message);
            }
            else {if(welcome==null){root.Clear();shell=null;email=null;welcome=new ProductionWelcomeView(()=>{_ = router.ContinueAsGuestAsync();},()=>router.NavigateEmail(ProductionAuthRoute.EmailEntry),()=>router.NavigateEmail(ProductionAuthRoute.EmailSignIn));root.Add(welcome);}welcome.SetState(router.Busy,router.Message);}
        }
        ProductionLogoutService logout;
        AccountProtectionController protection;
        AccountLinkController linking;
        ProductionAccountProtectionOverlay protectionOverlay;
        void RequestLogout()
        {
            if(protection==null){
                logout=ApplicationServices.Logout;var auth=ApplicationServices.Identity as Domino.Infrastructure.Firebase.FirebaseAuthService;
                if(logout==null||auth==null)return;
                protection=new AccountProtectionController(()=>auth.Session,()=>auth.Current,logout);
                protectionOverlay=new ProductionAccountProtectionOverlay(Root,protection,logout,()=>Domino.UI.DominoLocalization.Language,()=>{
                    linking?.Dispose();
                    AccountLinkSnapshot Capture(){
                        var player=ApplicationServices.Player;
                        if(player==null||!player.IsFresh||player.Player.Uid!=auth.Session?.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
                        var state=composition?.Router.Onboarding;
                        string Json(object value)=>value==null?null:Newtonsoft.Json.JsonConvert.SerializeObject(value);
                        return new AccountLinkSnapshot().Add("FirebaseUid",auth.Session?.Uid).Add("PlayerId",player.Player?.Uid)
                            .Add("Alias",player.Player?.DisplayName).Add("Country",player.Player?.CountryCode).Add("CreatedAt",player.Player?.CreatedAt?.ToString("O"))
                            .Add("OnboardingAndProfile",Json(state)).Add("MembershipEntitlements",Json(player.Entitlements)).Add("Trial",Json(player.TrialEligibility)).Add("Wallet",Json(player.Wallet));
                    }
                    linking=new AccountLinkController(auth,Capture,async()=>{
                        protection.Cancel();await router.AcceptLinkedAccountAsync();
                        if(router.EmailError!=EmailAuthError.None)throw new EmailAuthException(router.EmailError);
                    });
                    return new ProductionProtectAccountView(linking,()=>{if(linking.CanCancel){linking.Dispose();protection.Cancel();}},Domino.UI.DominoLocalization.Language);
                },()=>linking?.CanCancel??true);
            }
            _=protection.RequestAsync();
        }
        void Update(){var root=GetComponent<UIDocument>().rootVisualElement;if(root.layout.width<=0||Screen.width<=0||Screen.height<=0)return;var safe=Screen.safeArea;float x=root.layout.width/Screen.width,y=root.layout.height/Screen.height;root.style.paddingLeft=safe.xMin*x;root.style.paddingRight=(Screen.width-safe.xMax)*x;root.style.paddingTop=(Screen.height-safe.yMax)*y;root.style.paddingBottom=safe.yMin*y;}
        void OnDestroy(){linking?.Dispose();protectionOverlay?.Dispose();protection?.Dispose();PlayerPresentation?.Dispose();onboarding?.Dispose();if(router!=null)router.Changed-=Render;ApplicationServices.SessionReplaced-=Rebind;}
    }
}
