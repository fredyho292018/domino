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
        void Start(){Bind();}
        void Bind(){router=ApplicationServices.AuthRouter;if(router==null)return;router.Changed+=Render;Render();_ = router.RestoreAsync();}
        void Render(){
            var root=GetComponent<UIDocument>().rootVisualElement;root.style.flexGrow=1;
            if(router.Route==ProductionAuthRoute.AppShell){if(shell==null){root.Clear();shell=new ProductionAppShell();root.Add(shell);welcome=null;}}
            else {if(welcome==null){root.Clear();shell=null;welcome=new ProductionWelcomeView(()=>{_ = router.ContinueAsGuestAsync();});root.Add(welcome);}welcome.SetState(router.Busy,router.Message);}
        }
        // Not exposed by the UI: a future confirmation dialog must explicitly authorize loss of Guest access.
        public async Task LogoutGuestAsync(bool confirmedLossOfAccess){if(!confirmedLossOfAccess)throw new InvalidOperationException("Confirmation required.");router.Changed-=Render;await ApplicationServices.LogoutGuestAsync(true);welcome=null;shell=null;Bind();}
        void Update(){var root=GetComponent<UIDocument>().rootVisualElement;if(root.layout.width<=0||Screen.width<=0||Screen.height<=0)return;var safe=Screen.safeArea;float x=root.layout.width/Screen.width,y=root.layout.height/Screen.height;root.style.paddingLeft=safe.xMin*x;root.style.paddingRight=(Screen.width-safe.xMax)*x;root.style.paddingTop=(Screen.height-safe.yMax)*y;root.style.paddingBottom=safe.yMin*y;}
        void OnDestroy(){if(router!=null)router.Changed-=Render;}
    }
}
