using System;
using System.Collections;
using System.IO;
using Domino.UI;
using UnityEngine;

namespace Domino.Client
{
    public sealed class GameModeSmokeTest : MonoBehaviour
    {
        public static Action<int, int> ResizeGameView;
        static void Check(bool value, string detail)
        { if (!value) throw new InvalidOperationException("GAME_MODE_FAILURE: " + detail); }
        IEnumerator Start()
        {
            yield return null;
            var client = GetComponent<DominoClientController>();
            while (!client.Menu) yield return null;
            Check(client.Menu.Screen == StartScreen.MainMenu && client.State == null && !client.View, "APP_STARTS_IN_MENU");
            yield return new WaitForSeconds(.5f);
            Check(client.State == null && !client.AcceptingInput, "No automatic match");
            int[,] sizes = { {1600,900}, {1950,900}, {2000,900}, {2100,900}, {1200,900} };
            string[] names = { "16x9", "19.5x9", "20x9", "21x9", "tablet" };
            for (int i = 0; i < names.Length; i++)
            {
                ResizeGameView(sizes[i,0], sizes[i,1]);
                yield return new WaitForSecondsRealtime(.3f);
                Check(Screen.width == sizes[i,0] && Screen.height == sizes[i,1], "Resolution applied");
                client.Menu.MainPlay.onClick.Invoke();
                yield return null;
                Check(client.Menu.Screen == StartScreen.ModeSelector && client.Menu.ModePlay.gameObject.activeInHierarchy, "PLAY_OPENS_MODE_SELECTOR / TEAM_MATCH_VISIBLE");
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                client.Menu.Card.GetWorldCorners(corners);
                foreach (var corner in corners) Check(Screen.safeArea.Contains(corner), "Card inside safe area");
                foreach (var text in client.Menu.Card.GetComponentsInChildren<UnityEngine.UI.Text>())
                    Check(text.preferredWidth <= text.rectTransform.rect.width + 1 && text.preferredHeight <= text.rectTransform.rect.height + 1, "Card text fits: " + text.name);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../mode-" + names[i] + ".png")));
                yield return new WaitForSecondsRealtime(.15f);
                client.Menu.Back.onClick.Invoke();
                Check(client.Menu.Screen == StartScreen.MainMenu, "BACK_NAVIGATION");
                Debug.Log("MODE_RESPONSIVE=PASS " + names[i]);
            }
            client.Menu.MainPlay.onClick.Invoke(); client.Menu.ModePlay.onClick.Invoke();
            Check(client.Session.Mode == GameModeDefinition.TeamMatch && client.State != null, "TEAM_MATCH_SELECTED / MATCH_STARTS");
            Check(client.Session.LocalPlayerSeat == 0 && client.Session.Mode.BotCount == 3 && client.Session.Mode.LocalPlayerCount == 1, "Participant ownership");
            for (int p = 0; p < 4; p++) Check(client.Session.IsBot(p) == (p != 0), "Bots unchanged");
            Check(client.State.Configuration.PlayerCount == 4 && client.State.Configuration.GetTeamForPlayer(0) == 0
                && client.State.Configuration.GetTeamForPlayer(2) == 0 && client.State.Configuration.GetTeamForPlayer(1) == 1
                && client.State.Configuration.GetTeamForPlayer(3) == 1, "Teams unchanged");
            var session = client.Session;
            client.RestartClient(); Check(client.Session == session && client.Menu.Screen == StartScreen.Match, "RESTART_SAME_MODE");
            client.ExitMatch();
            yield return null;
            Check(client.State == null && client.Session == null && !client.View && !client.AcceptingInput, "Exit during deal cancels session");
            Check(FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 0, "Exit stops and removes wash audio");
            client.Menu.ModePlay.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!client.AcceptingInput && Time.realtimeSinceStartup < deadline) yield return null;
            Check(client.AcceptingInput && client.View.LocalTiles.Count == 10, "Reentry finishes dealing");
            client.View.transform.Find("Safe area/Landscape composition/Client menu/Exit match").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Check(client.Menu.Screen == StartScreen.ModeSelector && client.State == null, "EXIT_MATCH navigation");
            Debug.Log("DOMINO_MODE_SUCCESS: navigation, participants, restart, exit, reentry and five responsive layouts PASS.");
        }
    }
}
