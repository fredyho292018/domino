#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Development;
using Domino.Infrastructure;
using Domino.UI;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Domino.Online.Development
{
    // Explicit opt-in validation of the normal default-app session. Never enabled on normal launch.
    public static class AnonymousIdentityValidation
    {
        static string role, directory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start() {
#if UNITY_EDITOR
            if (!SessionState.GetBool("DevAuth.Validate", false)) return;
            SessionState.SetBool("DevAuth.Validate", false); role = "A";
#else
            if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("--dev-auth-check-b")) return;
            role = "B";
#endif
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-devAuthEvidence");
            if (index < 0) return;
            directory = args[index + 1]; Directory.CreateDirectory(directory);
            Application.runInBackground = true; _ = Run();
        }
        static async Task Until(Func<bool> condition) {
            var deadline = DateTime.UtcNow.AddSeconds(100);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(100);
            if (!condition()) throw new Exception("Validation timeout");
        }
        static async Task Run() {
            try {
                await Until(() => ApplicationServices.Player?.CanEdit == true && DevelopmentAuthentication.CanReset);
                var initial = ApplicationServices.Identity.Current.Uid;
                if (role == "B") {
                    await Until(() => File.Exists(Path.Combine(directory, "A-match.txt")));
                    var first = DevelopmentAuthentication.ResetAnonymousIdentityAsync();
                    var second = DevelopmentAuthentication.ResetAnonymousIdentityAsync();
                    if (!ReferenceEquals(first, second)) throw new Exception("Single flight failed");
                    await first;
                    if (ApplicationServices.Identity.Current.Uid == initial) throw new Exception("UID unchanged");
                }
                await ApplicationServices.Player.UpdateDisplayNameAsync("FHO-" + role);
                if (ApplicationServices.Player.Player?.DisplayName != "FHO-" + role) throw new Exception("Name not confirmed");
                var uid = ApplicationServices.Identity.Current.Uid;
                File.WriteAllText(Path.Combine(directory, role + "-identity.txt"), DevelopmentAuthentication.Fingerprint(uid));
                var local = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                local.Menu.Show(StartScreen.ModeSelector);
                await Until(() => local.Menu.DuelPlay && local.Menu.DuelPlay.interactable);
                local.OpenDevelopmentOnlineEntry();
                var entry = UnityEngine.Object.FindFirstObjectByType<OnlineEntryView>();
                if (!entry) throw new Exception("Online entry missing");
                if (role == "A") entry.CreateButton.onClick.Invoke();
                else {
                    entry.MatchId.text = File.ReadAllText(Path.Combine(directory, "A-match.txt"));
                    entry.JoinButton.onClick.Invoke();
                }
                await Until(() => entry.Match);
                if (role == "A") File.WriteAllText(Path.Combine(directory, "A-match.txt"), entry.Match.Client.Snapshot.MatchId);
                await Until(() => entry.Match.Client.Snapshot.Phase != "WAITING_FOR_PLAYER");
                await Until(() => File.Exists(Path.Combine(directory, (role == "A" ? "B" : "A") + "-identity.txt")));
                if (File.ReadAllText(Path.Combine(directory, "A-identity.txt")) == File.ReadAllText(Path.Combine(directory, "B-identity.txt")))
                    throw new Exception("Same UID");
                if (ApplicationServices.Identity.Current.Uid != uid) throw new Exception("Other process changed identity");
                if (DevelopmentAuthentication.CanReset) throw new Exception("Reset available in match");
                entry.Close();
                await Task.Delay(500);
                File.WriteAllText(Path.Combine(directory, role + "-result.txt"), "PASS\nBOOTSTRAP=PASS\nDISPLAY_NAME=FHO-" + role + "\nDUEL_CREATE_JOIN=PASS\nUID=" + DevelopmentAuthentication.Fingerprint(uid));
                await Task.Delay(1500);
                Exit(0);
            } catch (Exception error) {
                File.WriteAllText(Path.Combine(directory, role + "-result.txt"), "FAIL " + error.GetType().Name);
                Exit(1);
            }
        }
        static void Exit(int code) {
#if UNITY_EDITOR
            EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
#endif
