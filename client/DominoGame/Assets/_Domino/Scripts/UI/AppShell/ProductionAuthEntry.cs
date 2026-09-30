using Domino.Client;
using Domino.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell
{
    public static class ProductionAuthEntry
    {
        static GameObject host;
        static PanelSettings panel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){SceneManager.sceneLoaded-=Mount;host=null;if(panel)Object.Destroy(panel);panel=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register(){SceneManager.sceneLoaded+=Mount;}
        static void Mount(Scene scene,LoadSceneMode mode)
        {
            if(!ApplicationServices.UsesProductionAuth)return;
            // Retain legacy objects/source for their existing isolated validation entry.
            foreach(var legacy in Object.FindObjectsByType<DominoClientController>(FindObjectsSortMode.None))legacy.enabled=false;
            if(host)return;
            host=new GameObject("Production Auth");host.SetActive(false);Object.DontDestroyOnLoad(host);
            panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ConstantPixelSize;
            var document=host.AddComponent<UIDocument>();document.panelSettings=panel;
            host.AddComponent<ProductionAuthHost>();host.SetActive(true);
        }
    }
}
