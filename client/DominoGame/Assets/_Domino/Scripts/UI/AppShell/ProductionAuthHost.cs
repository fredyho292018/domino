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
        ProductionWelcomeView welcome;
        ProductionAppShell shell;
        ProductionEmailView email;
        void Start(){ApplicationServices.SessionReplaced+=Rebind;Bind();}
        void Rebind(){if(logout!=null)logout.Changed-=RenderLogout;logout=null;if(router!=null)router.Changed-=Render;welcome=null;shell=null;email=null;Bind();}
        void Bind(){router=ApplicationServices.AuthRouter;if(router==null)return;router.Changed+=Render;Render();_ = router.RestoreAsync();}
        void Render(){
            if(logout!=null && (logout.State==LogoutState.Confirming||logout.State==LogoutState.LoggingOut||logout.State==LogoutState.Error)){RenderLogout();return;}
            var root=GetComponent<UIDocument>().rootVisualElement;root.style.flexGrow=1;
            if(router.Route==ProductionAuthRoute.AppShell){if(shell==null){root.Clear();shell=new ProductionAppShell(signOut:RequestLogout);root.Add(shell);welcome=null;email=null;}}
            else if(router.Route==ProductionAuthRoute.EmailEntry||router.Route==ProductionAuthRoute.Register||router.Route==ProductionAuthRoute.VerificationPending||router.Route==ProductionAuthRoute.EmailPlaceholder||router.Route==ProductionAuthRoute.EmailSignIn||router.Route==ProductionAuthRoute.ForgotPassword){
                if(email==null||email.Route!=router.Route){root.Clear();welcome=null;shell=null;email=new ProductionEmailView(router.Route,router.NavigateEmail,(e,p,c)=>{_=router.RegisterAsync(e,p,c);},()=>{_=router.CheckVerificationAsync();},()=>{_=router.ResendVerificationAsync();},c=>{if(c)RequestLogout();},(e,p)=>{_=router.SignInEmailAsync(e,p);},e=>{_=router.ResetPasswordAsync(e);});root.Add(email);}
                email.SetState(router.Busy,router.EmailState,router.Message,router.DisplayEmail,true,router.EmailError);
            }
            else {if(welcome==null){root.Clear();shell=null;email=null;welcome=new ProductionWelcomeView(()=>{_ = router.ContinueAsGuestAsync();},()=>router.NavigateEmail(ProductionAuthRoute.EmailEntry),()=>router.NavigateEmail(ProductionAuthRoute.EmailSignIn));root.Add(welcome);}welcome.SetState(router.Busy,router.Message);}
        }
        ProductionLogoutService logout;
        void RequestLogout()
        {
            if(logout!=null)logout.Changed-=RenderLogout;
            logout=ApplicationServices.Logout;if(logout==null)return;
            logout.Changed+=RenderLogout;logout.Request();
        }
        void RenderLogout()
        {
            if(logout.State==LogoutState.Success){logout.Changed-=RenderLogout;return;}
            if(logout.State==LogoutState.Idle){welcome=null;shell=null;email=null;Render();shell?.Select(ShellTab.Menu);return;}
            var root=GetComponent<UIDocument>().rootVisualElement;root.Clear();welcome=null;shell=null;email=null;
            root.Add(new ProductionLogoutConfirmation(logout.GuestWarning,logout.Cancel,()=>{
                if(logout.State==LogoutState.Error)logout.Request();else _=logout.ConfirmAsync();
            },logout.State==LogoutState.LoggingOut,logout.Message));
        }
        void Update(){var root=GetComponent<UIDocument>().rootVisualElement;if(root.layout.width<=0||Screen.width<=0||Screen.height<=0)return;var safe=Screen.safeArea;float x=root.layout.width/Screen.width,y=root.layout.height/Screen.height;root.style.paddingLeft=safe.xMin*x;root.style.paddingRight=(Screen.width-safe.xMax)*x;root.style.paddingTop=(Screen.height-safe.yMax)*y;root.style.paddingBottom=safe.yMin*y;}
        void OnDestroy(){if(router!=null)router.Changed-=Render;ApplicationServices.SessionReplaced-=Rebind;if(logout!=null)logout.Changed-=RenderLogout;}
    }
}
