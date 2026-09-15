#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Domino.Online.Editor
{
    public static class MatchmakingNetworkValidationEditor
    {
        public static void Build(){Domino.Editor.LocalizationAssets.Import();Domino.Editor.DominoDevelopmentBuild.BuildWindowsDuelClient();}
        public static void RestartProbe(){SessionState.SetBool("I3.Restart",true);Run();}
        public static void Run(){
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new InvalidOperationException("Isolated validation only");
            SessionState.SetBool("I3.Validate",true);EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
    }
}
#endif
