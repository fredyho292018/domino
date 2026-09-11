using System.IO;
using Domino.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    public static class ClientEditorTools
    {
        public const string ScenePath = "Assets/_Domino/Scenes/DominoClient.unity";
        [InitializeOnLoadMethod]
        static void FirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("DominoOpened", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool("DominoOpened", true);
                var scene = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.path) && !scene.isDirty) Open();
            };
        }
        [MenuItem("Domino/Open client")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Domino/Configure mobile portrait")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Domino Studio";
            PlayerSettings.productName = "DominoGame";
            ConfigurePortraitOrientation();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.dominostudio.dominogame");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.dominostudio.dominogame");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Domino/Presentation/Configure mobile Portrait")]
        public static void ConfigurePortraitOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Domino/Capture Game View (Play Mode)")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode before capturing."); return; }
            Directory.CreateDirectory("../Validation/Generated");
            ScreenCapture.CaptureScreenshot("../Validation/Generated/domino-" + Screen.width + "x" + Screen.height + ".png");
        }
        [MenuItem("Domino/Run interactive smoke test (Play Mode)")]
        public static void SmokeTest()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode before running smoke validation."); return; }
            var controller = Object.FindFirstObjectByType<DominoClientController>();
            if (controller && !controller.GetComponent<ClientSmokeTest>()) controller.gameObject.AddComponent<ClientSmokeTest>();
        }
        [MenuItem("Domino/Ver efecto - Darle agua (Play Mode)")]
        public static void PreviewWash()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("Pulsa Play y espera al turno de Fredy para ver el efecto."); return; }
            var controller = Object.FindFirstObjectByType<DominoClientController>();
            if (controller) controller.PreviewWash();
        }
    }
}
