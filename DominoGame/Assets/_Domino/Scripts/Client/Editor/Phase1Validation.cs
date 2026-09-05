using System;
using System.IO;
using System.Reflection;
using Domino.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Opt-in batch runner. It never starts on an ordinary editor launch.
    public static class Phase1Validation
    {
        const string Running = "Domino.Phase1.Running";
        static double deadline;
        public static void RunDealing()
        {
            SessionState.SetBool("Domino.ValidateDealing", true);
            Run();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            if (SessionState.GetBool(Running, false)) deadline = EditorApplication.timeSinceStartup + 240;
        }
        public static void Run()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Domino/Config/double-nine-partners-v1.json");
                var config = LocalGameConfiguration.Load(asset);
                if (config.TotalTiles != 55 || config.ReserveCount != 15 || config.TargetScore != 200)
                    throw new Exception("Bundled values differ");
                Reject(() => LocalGameConfiguration.Load(null));
                Reject(() => LocalGameConfiguration.Parse(""));
                Reject(() => LocalGameConfiguration.Parse("{ broken JSON"));
                Reject(() => LocalGameConfiguration.Parse("{}"));
                Reject(() => LocalGameConfiguration.Parse(asset.text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99")));
                Reject(() => LocalGameConfiguration.Parse(asset.text.Replace("\"bonus\": 0", "\"omittedBonus\": 0")));
                Reject(() => LocalGameConfiguration.Parse(asset.text.Replace("\"seat\": 0", "\"omittedSeat\": 0")));
                var scene = EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
                var controller = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                if (!scene.IsValid() || !controller || new SerializedObject(controller).FindProperty("configurationJson").objectReferenceValue != asset)
                    throw new Exception("Scene configuration reference is missing");
                Debug.Log("UNITY_JSON_CONFIGURATION_TESTS=SUCCESS");
                SessionState.SetBool(Running, true);
                deadline = EditorApplication.timeSinceStartup + 240;
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Complete(false, error.ToString()); }
        }
        static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new Exception("Invalid JSON/configuration was accepted");
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            var controller = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
            if (!controller) { Complete(false, "Controller missing in Play Mode"); return; }
            // Accelerates the automated run only; serialized animation timings are untouched.
            Time.timeScale = 4;
            if (SessionState.GetBool("Domino.ValidateDealing", false))
            {
                DealingSmokeTest.ResizeGameView = ResizeGameView;
                controller.gameObject.AddComponent<DealingSmokeTest>();
            }
            else
            {
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) ResizeGameView(1600, 900);
                controller.gameObject.AddComponent<ClientSmokeTest>();
            }
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Complete(false, message + "\n" + stack);
            else if (message.StartsWith("DOMINO_SMOKE_SUCCESS", StringComparison.Ordinal) || message.StartsWith("DOMINO_DEALING_SUCCESS", StringComparison.Ordinal)) Complete(true, message);
        }
        static void Tick()
        {
            if (SessionState.GetBool(Running, false) && EditorApplication.timeSinceStartup > deadline)
                Complete(false, "Play Mode smoke timeout");
        }
        static void Complete(bool success, string detail)
        {
            SessionState.SetBool(Running, false);
            Time.timeScale = 1;
            string result = SessionState.GetBool("Domino.ValidateDealing", false) ? "../dealing-unity-result.txt" : "../phase1-unity-result.txt";
            File.WriteAllText(Path.GetFullPath(result), (success ? "SUCCESS\nCONSOLE_ERRORS=0\n" : "FAILURE\n") + detail);
            EditorApplication.Exit(success ? 0 : 1);
        }
        static void ResizeGameView(int width, int height)
        {
            // Editor-only adapter. Runtime layout still uses Screen.safeArea and actual Canvas dimensions.
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            var kind = assembly.GetType("UnityEditor.GameViewSizeType", true);
            var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(kind, "FixedResolution"), (object)width, height, "Domino validation" });
            group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
            int count = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView", true);
            var window = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(window, count - 1);
            window.Show(); window.Repaint();
        }
    }
}
