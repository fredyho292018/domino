using System;
using System.Collections;
using System.IO;
using Domino.UI;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Domino.Client
{
    public sealed class LocalizationSmokeTest : MonoBehaviour
    {
        public static Action<int,int> ResizeGameView;
        bool hadPreference;
        string oldPreference;
        int checks;
        void Awake() { hadPreference = PlayerPrefs.HasKey(DominoLocalization.PreferenceKey); oldPreference = PlayerPrefs.GetString(DominoLocalization.PreferenceKey); }
        void OnDestroy() => RestorePreference();
        void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetString(DominoLocalization.PreferenceKey, oldPreference);
            else PlayerPrefs.DeleteKey(DominoLocalization.PreferenceKey);
            PlayerPrefs.Save();
        }
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException("LOCALIZATION_FAILURE: " + reason); }
        Text FindText(Transform root, string path) => root.Find(path).GetComponent<Text>();
        IEnumerator Start()
        {
            var client = GetComponent<DominoClientController>();
            while (!client.Menu) yield return null;
            foreach (string language in new[] { "en", "es" })
            {
                DominoLocalization.Select(language);
                yield return new WaitForSecondsRealtime(.2f);
                Check(PlayerPrefs.GetString(DominoLocalization.PreferenceKey) == language, "Preference persisted");
                var saved = new PlayerPrefLocaleSelector { PlayerPreferenceKey = DominoLocalization.PreferenceKey }.GetStartupLocale(LocalizationSettings.AvailableLocales);
                Check(saved.Identifier.Code == language, "Saved locale selected at startup");
                var table = LocalizationSettings.StringDatabase.GetTable(DominoLocalization.TableName);
                Check(table.Count == 83, "Complete catalog");
                foreach (var pair in table)
                {
                    Check(!string.IsNullOrEmpty(pair.Value.Value), "Translation populated: " + pair.Value.Key);
                    string formatted = DominoLocalization.Get(pair.Value.Key, 2, 200, 10, 2, 1);
                    Check(!string.IsNullOrEmpty(formatted) && !formatted.Contains("{0"), "Smart String formatted: " + pair.Value.Key);
                }
                Check(client.Menu.MainPlay.GetComponentInChildren<Text>().text == (language == "es" ? "Jugar" : "Play"), "Menu localized: " + language + " / " + client.Menu.MainPlay.GetComponentInChildren<Text>().text + " / " + DominoLocalization.Get("menu.play"));
                Check(DominoLocalization.Get("rules.tiles", 1) == (language == "es" ? "1 ficha" : "1 tile"), "Singular");
                Check(DominoLocalization.Get("rules.tiles", 10) == (language == "es" ? "10 fichas" : "10 tiles"), "Plural");
                Check(DominoLocalization.Get("rules.target_score", 200) == (language == "es" ? "Meta 200" : "Target 200"), "Dynamic target");
                Debug.Log("LOCALIZATION_LANGUAGE=PASS " + language);
            }
            var spanish = LocalizationSettings.StringDatabase.GetTable(DominoLocalization.TableName);
            var fallbackEntry = spanish.GetEntry("menu.play"); string translated = fallbackEntry.Value;
            try { fallbackEntry.Value = ""; Check(DominoLocalization.Get("menu.play") == "Play", "English fallback for absent translation"); }
            finally { fallbackEntry.Value = translated; }
            var font = UiKit.Font;
            font.RequestCharactersInTexture("áéíóúñ¿¡", 24);
            foreach (char character in "áéíóúñ¿¡") Check(font.HasCharacter(character), "Spanish glyph " + character);
            int[,] sizes = { {1600,900}, {1950,900}, {2000,900}, {2100,900}, {1200,900}, {1080,1920}, {1080,2340}, {1536,2048} };
            string[] names = { "16x9", "19.5x9", "20x9", "21x9", "tablet", "portrait-16x9", "portrait-19.5x9", "portrait-tablet" };
            for (int i=0;i<names.Length;i++)
            {
                ResizeGameView(sizes[i,0],sizes[i,1]); yield return new WaitForSecondsRealtime(.25f);
                Check(Screen.width == sizes[i,0] && Screen.height == sizes[i,1], "Game View resolution");
                ValidateVisible(client.Menu.transform);
                client.Menu.MainPlay.onClick.Invoke(); yield return null;
                Check(FindText(client.Menu.Card, "Mode name").text == "Partida por equipos", "Mode title localized");
                ValidateVisible(client.Menu.transform);
                Capture("selector-es-" + names[i]); yield return new WaitForSecondsRealtime(.1f);
                client.Menu.Settings.Open(); yield return null; ValidateVisible(client.Menu.Settings.transform);
                Capture("settings-es-" + names[i]); yield return new WaitForSecondsRealtime(.1f);
                client.Menu.Settings.gameObject.SetActive(false); client.Menu.Back.onClick.Invoke();
            }
            client.Menu.MainPlay.onClick.Invoke(); client.Menu.ModePlay.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!client.AcceptingInput && Time.realtimeSinceStartup < deadline) yield return null;
            Check(client.AcceptingInput, "Playable hand");
            var state = client.State; var session = client.Session; var config = state.Configuration;
            var first = client.View.LocalTiles[0]; client.Select(first);
            int turn=state.CurrentPlayer, chain=state.Chain.Count, count=state.Hand(0).Count;
            foreach (string code in new[] {"en","es","en","es"})
            {
                client.View.Settings.Open();
                if (code == "es") client.View.Settings.Spanish.onClick.Invoke(); else client.View.Settings.English.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                Check(ReferenceEquals(state,client.State) && ReferenceEquals(session,client.Session) && ReferenceEquals(config,client.State.Configuration), "Engine/session/config preserved");
                Check(state.CurrentPlayer == turn && state.Chain.Count == chain && state.Hand(0).Count == count && first.Selected, "Turn/tiles/selection preserved");
                Check(FindText(client.View.transform,"Safe area/Landscape composition/Play/Label").text == DominoLocalization.Get("game.play"), "HUD localized");
                client.View.Settings.gameObject.SetActive(false);
                Check(FindText(client.View.transform,"Safe area/Landscape composition/Prompt").text == DominoLocalization.Get("game.selected_hint"), "Dynamic prompt localized");
            }
            for (int i=0;i<names.Length;i++)
            {
                ResizeGameView(sizes[i,0],sizes[i,1]); yield return new WaitForSecondsRealtime(.25f);
                ValidateVisible(client.View.transform);
                Capture("hud-es-"+names[i]); yield return new WaitForSecondsRealtime(.1f);
            }
            Time.timeScale = 1; // Allow the normal result duration while locales finish their asynchronous callbacks.
            var celebration = StartCoroutine(client.View.ShowWinner("Fredy–Maria", false, true));
            yield return null;
            var title=FindText(client.View.transform,"Safe area/Landscape composition/Table effects/Winner celebration/Winner card/Title");
            Check(title.text == "Ronda ganada", "Round result ES");
            DominoLocalization.Select("en"); yield return new WaitForSecondsRealtime(.2f); Check(title.text == "Round Won", "Result switches live EN: " + title.text);
            DominoLocalization.Select("es"); yield return new WaitForSecondsRealtime(.2f); Check(title.text == "Ronda ganada", "Result switches live ES: " + title.text);
            yield return celebration;
            Time.timeScale = 4;
            client.View.Finish(() => DominoLocalization.Get("result.tie_summary",2));
            for (int i=0;i<names.Length;i++)
            {
                ResizeGameView(sizes[i,0],sizes[i,1]); yield return new WaitForSecondsRealtime(.25f);
                ValidateVisible(client.View.transform);
                Capture("result-es-"+names[i]); yield return new WaitForSecondsRealtime(.1f);
            }
            DominoLocalization.Select("en"); yield return new WaitForSecondsRealtime(.2f);
            Check(FindText(client.View.transform,"Safe area/Landscape composition/Play/Label").text == "Next Round →", "Result action switches");
            client.ExitMatch();
            RestorePreference();
            Debug.Log("DOMINO_LOCALIZATION_SUCCESS: " + checks + " checks; EN, ES, runtime switching, state preserved, menus, HUD, results, dynamic values, plurals, fallback, persistence, glyphs and eight layouts PASS; 83 keys.");
            Destroy(this);
        }
        void ValidateVisible(Transform root)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var text in root.GetComponentsInChildren<Text>())
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                if (text.GetComponentInParent<DominoTileView>()) continue;
                var generation = text.GetGenerationSettings(text.rectTransform.rect.size);
                var generator = new TextGenerator(); generator.Populate(text.text, generation);
                Check(generator.characterCountVisible >= text.text.Replace("\n","").Length - 1, "No clipped characters: " + text.name + " / " + text.text);
                if (text.GetComponentInParent<Button>())
                {
                    var corners=new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                    foreach(var corner in corners) Check(Screen.safeArea.Contains(corner), "Control inside safe area: " + text.name);
                }
            }
        }
        static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../localization-"+name+".png")));
    }
}
