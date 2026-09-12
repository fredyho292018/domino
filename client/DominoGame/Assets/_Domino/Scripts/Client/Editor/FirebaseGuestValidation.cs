using System;
using System.IO;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Firebase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Opt-in integration test. Uses real Auth; never signs out or clears persisted state.
    public static class FirebaseGuestValidation
    {
        const string Prefix = "Domino.GuestValidation.";
        static double deadline;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/Generated/firebase-guest-editor.txt"));
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += Changed;
            Application.logMessageReceived += OnLog;
            deadline = EditorApplication.timeSinceStartup + 120;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, "RUNNING\n");
            SessionState.SetBool(Prefix + "Running", true);
            SessionState.SetInt(Prefix + "Run", 1);
            SessionState.SetBool(Prefix + "Captured", false);
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.isPlaying = true;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Prefix + "Running", false) &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) Finish(false, message);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Prefix + "Running", false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Authentication timeout"); return; }
            if (!EditorApplication.isPlaying || SessionState.GetBool(Prefix + "Captured", false)) return;
            var service = ApplicationServices.Identity;
            if (service == null || service.State != IdentityState.Ready) return;
            if (service.Current == null || !service.Current.IsAnonymous) { Finish(false, "Anonymous identity missing"); return; }
            int run = SessionState.GetInt(Prefix + "Run", 1);
            string uid = service.Current.Uid;
            bool reused = ((FirebaseAuthService)service).ReusedExistingUser;
            File.AppendAllText(Output, $"RUN_{run}_UID={uid}\nRUN_{run}_ANONYMOUS=true\nRUN_{run}_REUSED={reused}\nDEPENDENCIES={ApplicationServices.Firebase.DependencyStatus}\n");
            if (run == 1) SessionState.SetString(Prefix + "Uid", uid);
            else if (uid != SessionState.GetString(Prefix + "Uid", "") || !reused)
            { Finish(false, "UID/session did not persist"); return; }
            SessionState.SetBool(Prefix + "Captured", true);
            EditorApplication.isPlaying = false;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Running", false) || state != PlayModeStateChange.EnteredEditMode) return;
            if (!SessionState.GetBool(Prefix + "Captured", false)) { Finish(false, "Play stopped before identity ready"); return; }
            if (SessionState.GetInt(Prefix + "Run", 1) == 2) { Finish(true, "UID_PERSISTED_EDITOR=PASS"); return; }
            SessionState.SetInt(Prefix + "Run", 2);
            SessionState.SetBool(Prefix + "Captured", false);
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
        static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Prefix + "Running", false);
            File.AppendAllText(Output, (success ? "SUCCESS\nCONSOLE_ERRORS=0\n" : "FAILURE\n") + detail + "\n");
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
