using Domino.Client;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    // Editor-only, session-only configuration; builds retain the legacy entry.
    public static class ProductionShellDevelopmentEntry
    {
        static GameObject shellObject, legacy;
        static PanelSettings panel;
        [MenuItem("Domino/Production App Shell/Play Mode entry/PRODUCTION_SHELL")]
        static void Enter()
        {
            if(!EditorApplication.isPlaying || shellObject)return;
            var client=Object.FindFirstObjectByType<DominoClientController>();
            if(!client || !client.Menu || client.Session!=null || client.Menu.Screen!=Domino.UI.StartScreen.MainMenu) {
                Debug.LogWarning("Production shell entry requires the idle legacy main menu.");return;
            }
            legacy=client.Menu.gameObject;legacy.SetActive(false);
            shellObject=new GameObject("Production App Shell");shellObject.SetActive(false);shellObject.transform.SetParent(client.transform,false);
            panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(393,852);
            var document=shellObject.AddComponent<UIDocument>();document.panelSettings=panel;
            shellObject.AddComponent<ProductionShellHost>();shellObject.SetActive(true);
        }
        [MenuItem("Domino/Production App Shell/Play Mode entry/LEGACY")]
        static void Exit()
        {
            if(shellObject)Object.Destroy(shellObject);if(panel)Object.Destroy(panel);if(legacy)legacy.SetActive(true);
            shellObject=null;panel=null;legacy=null;
        }
    }
}
