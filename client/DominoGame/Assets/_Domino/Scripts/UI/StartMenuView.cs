using System;
using Domino.Client;
using Domino.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public enum StartScreen { MainMenu, ModeSelector, Match }
    public sealed class StartMenuView : MonoBehaviour
    {
        RectTransform safe, composition;
        GameObject main, selector;
        public StartScreen Screen { get; private set; }
        public Button MainPlay { get; private set; }
        public Button ModePlay { get; private set; }
        public Button Back { get; private set; }
        public RectTransform Card { get; private set; }
        public LanguageSettingsPanel Settings { get; private set; }
        public PlayerProfileView Profile { get; private set; }
        public event Action<GameModeDefinition> StartRequested;

        public void Initialize(GameModeDefinition mode, GameConfigurationSnapshot configuration)
        {
            gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            BoardView.ConfigureCanvas(gameObject);
            gameObject.AddComponent<GraphicRaycaster>();
            var background = UiKit.Rect("Background", transform, Vector2.zero, Vector2.zero);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
            background.gameObject.AddComponent<SoftBackdrop>().raycastTarget = false;
            safe = UiKit.Rect("Safe area", transform, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            composition = UiKit.Rect("Menu composition", safe, new Vector2(1120, 700), Vector2.zero);
            UiKit.Label("Brand", composition, "TEAMFHO  /  DOMINO", new Vector2(500,40), new Vector2(0,310), 22, UiKit.Gold);
            main = UiKit.Rect("Main menu", composition, new Vector2(1000,570), Vector2.zero).gameObject;
            UiKit.LLabel("Eyebrow", main.transform, "menu.eyebrow", new Vector2(700,30), new Vector2(0,180), 16, UiKit.Muted);
            UiKit.Label("Title", main.transform, "DOMINO", new Vector2(800,110), new Vector2(0,90), 76, UiKit.Cream);
            UiKit.LLabel("Welcome", main.transform, "menu.welcome", new Vector2(850,45), new Vector2(0,-5), 23, UiKit.Muted);
            MainPlay = UiKit.LButton("Play", main.transform, "menu.play", new Vector2(300,64), new Vector2(0,-125), UiKit.Hex("397566"), () => Show(StartScreen.ModeSelector));
            UiKit.LLabel("Offline", main.transform, "menu.offline", new Vector2(700,35), new Vector2(0,-210), 15, UiKit.Gold);
            selector = UiKit.Rect("Mode selector", composition, new Vector2(1100,590), Vector2.zero).gameObject;
            Back = UiKit.LButton("Back", selector.transform, "menu.back", new Vector2(140,42), new Vector2(-460,245), Color.clear, () => Show(StartScreen.MainMenu));
            UiKit.LLabel("Choose", selector.transform, "menu.choose", new Vector2(650,38), new Vector2(0,240), 23, UiKit.Cream);
            Card = UiKit.Panel("Team match card", selector.transform, new Vector2(880,420), new Vector2(0,-10), UiKit.Hex("1D403E")).rectTransform;
            UiKit.LLabel("Mode name", Card, mode.DisplayNameKey, new Vector2(740,48), new Vector2(0,155), 34, UiKit.Cream);
            UiKit.LLabel("Subtitle", Card, mode.SubtitleKey, new Vector2(740,32), new Vector2(0,113), 20, UiKit.Muted);
            DominoLocalization.Set(UiKit.Label("Teams", Card, "", new Vector2(160,38), new Vector2(0,33), 23, UiKit.Gold), "mode.teams", mode.TeamA.Count, mode.TeamB.Count);
            Avatar(Card, "player.you", -295, DominoVisualTheme.TeamA);
            Avatar(Card, "player.bot", -185, DominoVisualTheme.TeamA);
            Avatar(Card, "player.bot", 185, DominoVisualTheme.TeamB);
            Avatar(Card, "player.bot", 295, DominoVisualTheme.TeamB);
            var rules = UiKit.Label("Rules", Card, "", new Vector2(820,38), new Vector2(0,-62), 21, UiKit.Muted);
            DominoLocalization.Bind(rules, () => DominoLocalization.Get("rules.summary", DominoLocalization.Get("rules.double_nine", configuration.MaxPip), DominoLocalization.Get("rules.tiles", configuration.TilesPerPlayer), DominoLocalization.Get("rules.no_draw"), DominoLocalization.Get("rules.target_score", configuration.TargetScore)));
            ModePlay = UiKit.LButton("Start match", Card, "menu.play", new Vector2(320,58), new Vector2(0,-145), UiKit.Hex("397566"), () => StartRequested?.Invoke(mode));
            Settings = LanguageSettingsPanel.Create(composition);
            Profile = gameObject.AddComponent<PlayerProfileView>();
            Profile.Initialize(Domino.Infrastructure.ApplicationServices.Player, composition);
            gameObject.AddComponent<RealtimeStatusView>().Initialize(Domino.Infrastructure.ApplicationServices.Realtime, main.transform);
            UiKit.LButton("Settings", composition, "menu.settings", new Vector2(180,44), new Vector2(450,310), Color.clear, Settings.Open);
            UiKit.LButton("Exit", main.transform, "menu.exit", new Vector2(150,38), new Vector2(0,-266), Color.clear, Application.Quit);
            Show(StartScreen.MainMenu);
        }
        static void Avatar(Transform parent, string label, float x, Color color)
        {
            var disc = UiKit.Disc(label + " avatar", parent, 68, new Vector2(x,30), color);
            UiKit.LLabel("Role", disc.transform, label, new Vector2(65,25), Vector2.zero, 16, UiKit.Cream);
        }
        public void Show(StartScreen screen)
        {
            Screen = screen;
            if (Profile) Profile.Close();
            if (Settings) Settings.gameObject.SetActive(false);
            gameObject.SetActive(screen != StartScreen.Match);
            main.SetActive(screen == StartScreen.MainMenu);
            selector.SetActive(screen == StartScreen.ModeSelector);
        }
        void LateUpdate()
        {
            bool portrait = safe.rect.height > safe.rect.width;
            float scale = portrait ? Mathf.Min(safe.rect.width / 1040, safe.rect.height / 1280) : Mathf.Min(safe.rect.width / 1160, safe.rect.height / 740);
            composition.localScale = Vector3.one * Mathf.Max(.01f, scale);
            composition.sizeDelta = portrait ? new Vector2(1000, 1240) : new Vector2(1120, 700);
            Profile.OpenButton.gameObject.SetActive(Screen == StartScreen.MainMenu);
            var profileRect = Profile.OpenButton.GetComponent<RectTransform>();
            profileRect.anchoredPosition = portrait ? new Vector2(0,400) : new Vector2(-395,285);
            profileRect.sizeDelta = portrait ? new Vector2(720,110) : new Vector2(320,95);
            Profile.OpenButton.GetComponentInChildren<Text>().rectTransform.sizeDelta = profileRect.sizeDelta - new Vector2(30,12);
            ((RectTransform)composition.Find("Brand")).anchoredPosition = new Vector2(0, portrait ? 530 : 310);
            ((RectTransform)composition.Find("Settings")).anchoredPosition = portrait ? new Vector2(0,-540) : new Vector2(450,310);
            Back.GetComponent<RectTransform>().anchoredPosition = portrait ? new Vector2(-365,435) : new Vector2(-460,245);
            ((RectTransform)selector.transform.Find("Choose")).anchoredPosition = new Vector2(0, portrait ? 350 : 240);
            Card.sizeDelta = new Vector2(880, portrait ? 620 : 420);
            ((RectTransform)Card.Find("Mode name")).anchoredPosition = new Vector2(0, portrait ? 250 : 155);
            ((RectTransform)Card.Find("Subtitle")).anchoredPosition = new Vector2(0, portrait ? 198 : 113);
            var rules = (RectTransform)Card.Find("Rules");
            rules.sizeDelta = new Vector2(820, portrait ? 85 : 38);
            rules.anchoredPosition = new Vector2(0, portrait ? -110 : -62);
            ModePlay.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, portrait ? -230 : -145);
            Settings.GetComponent<RectTransform>().sizeDelta = new Vector2(2400, portrait ? 3200 : 1400);
        }
    }
}
