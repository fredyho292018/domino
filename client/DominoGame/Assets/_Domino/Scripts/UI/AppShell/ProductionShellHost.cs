using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    // Explicitly mounted by migration tooling; no automatic startup hook.
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionShellHost : MonoBehaviour
    {
        ProductionAppShell shell;
        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.style.flexGrow=1; shell=new ProductionAppShell(); root.Add(shell);
        }
        void Update()
        {
            if(shell==null || Screen.width<=0 || Screen.height<=0)return;
            var root=GetComponent<UIDocument>().rootVisualElement;
            if(root.layout.width<=0 || root.layout.height<=0)return;
            var safe=Screen.safeArea;float x=root.layout.width/Screen.width,y=root.layout.height/Screen.height;
            shell.SetSafeArea(safe.xMin*x,(Screen.height-safe.yMax)*y,(Screen.width-safe.xMax)*x,safe.yMin*y);
        }
    }
}
