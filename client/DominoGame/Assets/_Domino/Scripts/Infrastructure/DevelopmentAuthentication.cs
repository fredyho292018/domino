#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.UI;
using UnityEngine;

namespace Domino.Development
{
    // Local development controls; no UID entry and no Firebase account deletion.
    public sealed class DevelopmentAuthentication : MonoBehaviour
    {
        static Task reset;
        static bool open;
        string displayName = "FHO-B", status = "";
        bool saving;
        public static bool Allowed
        {
            get {
                var settings = Resources.Load<DominoApiSettings>("ApiSettings");
                return (Application.isEditor || Debug.isDebugBuild) && settings &&
                    (settings.Environment == "LOCAL" || settings.Environment == "DEVELOPMENT" || settings.Environment == "TEST");
            }
        }
        public static bool Busy => reset != null && !reset.IsCompleted;
        public static bool CanReset {
            get {
                var client = FindFirstObjectByType<DominoClientController>();
                return Allowed && client && client.Menu && client.Menu.Screen != StartScreen.Match &&
                    !FindFirstObjectByType<Domino.Online.OnlineEntryView>() &&
                    !FindFirstObjectByType<Domino.Online.MatchmakingView>() &&
                    !FindFirstObjectByType<Domino.Online.OnlineMatchController>() &&
                    ApplicationServices.Identity?.Current?.IsAnonymous == true;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear() { reset = null; open = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install() {
            if (!Allowed) return;
            var host = new GameObject("Development authentication");
            DontDestroyOnLoad(host); host.AddComponent<DevelopmentAuthentication>();
        }
        public static void Open() { if (Allowed) open = true; }
        public static Task ResetAnonymousIdentityAsync() {
            if (Busy) return reset;
            return reset = ApplicationServices.ResetDevelopmentAnonymousIdentityAsync();
        }
        public static string Fingerprint(string uid) {
            if (string.IsNullOrEmpty(uid)) return "none";
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(uid))).Replace("-", "").Substring(0, 16);
        }
        async void ResetIdentity() {
            status = "Creating anonymous identity...";
            try { await ResetAnonymousIdentityAsync(); status = "New identity ready; player bootstrapped."; }
            catch { status = "Reset/bootstrap failed. Check connection and retry profile sync."; Debug.LogWarning("[DEV AUTH] reset unavailable"); }
        }
        async void SaveName() {
            saving = true;
            try {
                await ApplicationServices.Player.UpdateDisplayNameAsync(displayName);
                status = ApplicationServices.Player.Player?.DisplayName == displayName ? "Name saved by backend." : "Name update failed.";
            } catch { status = "Name update failed."; }
            finally { saving = false; }
        }
        void OnGUI() {
            if (!Allowed) return;
            var area = Screen.safeArea;
            GUILayout.BeginArea(new Rect(area.x + 12, Screen.height - area.yMax + 12, Mathf.Min(400, area.width - 24), open ? 310 : 38), GUI.skin.box);
            if (GUILayout.Button(open ? "Close Development Authentication" : "DEV • Authentication")) open = !open;
            if (open) {
                GUILayout.Label("DEVELOPMENT ONLY • Anonymous Authentication");
                GUILayout.Label("Identity fingerprint: " + Fingerprint(ApplicationServices.Identity?.Current?.Uid));
                GUILayout.Label("Profile: " + (ApplicationServices.Player?.Player?.DisplayName ?? "unavailable"));
                GUILayout.Label("Reset signs out locally and creates a new guest.\nNo remote account is deleted.");
                GUI.enabled = CanReset && !Busy && !saving;
                if (GUILayout.Button("Reset Anonymous Identity")) ResetIdentity();
                if (GUILayout.Button("Manual DUEL Create / Join")) {FindFirstObjectByType<DominoClientController>().OpenDevelopmentOnlineEntry();open=false;}
                GUI.enabled = !Busy && !saving;
                displayName = GUILayout.TextField(displayName, 24);
                GUI.enabled = CanReset && !Busy && !saving && ApplicationServices.Player?.CanEdit == true;
                if (GUILayout.Button("Save Display Name")) SaveName();
                GUI.enabled = true;
                GUILayout.Label(status);
                if (!CanReset && !Busy) GUILayout.Label("Return to the menu; anonymous session required.");
            }
            GUILayout.EndArea();
        }
    }
}
#endif
