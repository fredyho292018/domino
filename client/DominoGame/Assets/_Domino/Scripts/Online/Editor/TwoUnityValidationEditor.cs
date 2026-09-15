#if UNITY_EDITOR
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
namespace Domino.Online.Development { public static class TwoUnityValidationEditor {

        public static void Run(){if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_EDITOR_REQUIRED");SessionState.SetBool("I21.TwoUnity",true);EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;}
        public static void Build(){Domino.Editor.LocalizationAssets.Import();var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Domino.Editor.ClientEditorTools.ScenePath},locationPathName=Path.GetFullPath(Path.Combine(Application.dataPath,"../../I21/Build/DominoValidation.exe")),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});EditorApplication.Exit(result.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);}

}}
#endif
