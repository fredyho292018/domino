using System;
using System.Collections.Generic;
using System.Linq;
using Domino.Client;
using Domino.Configuration;
using Domino.Catalog;
using Domino.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public enum StartScreen { MainMenu, ModeSelector, Match }
    public sealed class StartMenuView : MonoBehaviour
    {
        RectTransform safe, composition;
        GameObject main, selector;
        GameCatalogSnapshot shownCatalog;
        RectTransform template,modeList,viewport;
        Scrollbar modeScrollbar;
        Button moreModes;
        Text choose;
        readonly Dictionary<string,RectTransform> cards=new Dictionary<string,RectTransform>();
        public ScrollRect ModeScroll {get;private set;}
        public IReadOnlyCollection<string> VisibleModeKeys => cards.Keys;
        public Button ButtonFor(string key)=>cards.TryGetValue(key,out var card)?card.Find("Start match").GetComponent<Button>():null;
        public Button DuelPlay { get; private set; }
        public StartScreen Screen { get; private set; }
        public Button MainPlay { get; private set; }
        public Button ModePlay { get; private set; }
        public Button Back { get; private set; }
        public RectTransform Card { get; private set; }
        public LanguageSettingsPanel Settings { get; private set; }
        public PlayerProfileView Profile { get; private set; }
        public event Action<GameModeDefinition> StartRequested;
        public event Action HistoryRequested;
        public event Action SocialRequested;
        void OnEnable() { _ = Domino.Infrastructure.ApplicationServices.RefreshGameCatalogAsync(); }

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
            UiKit.LButton("History",main.transform,"history.title",new Vector2(300,64),new Vector2(-170,-205),UiKit.Hex("254B47"),()=>HistoryRequested?.Invoke());
            UiKit.LButton("Social",main.transform,"social.title",new Vector2(300,64),new Vector2(170,-205),UiKit.Hex("254B47"),()=>SocialRequested?.Invoke());
            selector = UiKit.Rect("Mode selector", composition, new Vector2(1100,590), Vector2.zero).gameObject;
            Back = UiKit.LButton("Back", selector.transform, "menu.back", new Vector2(140,42), new Vector2(-460,245), Color.clear, () => Show(StartScreen.MainMenu));
            choose=UiKit.LLabel("Choose", selector.transform, "menu.choose", new Vector2(800,38), new Vector2(0,240), 23, UiKit.Cream);
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
            template=Card;template.gameObject.SetActive(false);
            viewport=UiKit.Rect("Mode list viewport",selector.transform,new Vector2(950,700),new Vector2(0,-80));
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            modeList=UiKit.Rect("Mode list content",viewport,new Vector2(950,420),Vector2.zero);
            modeList.anchorMin=modeList.anchorMax=new Vector2(.5f,1);modeList.pivot=new Vector2(.5f,1);
            ModeScroll=viewport.gameObject.AddComponent<ScrollRect>();ModeScroll.viewport=viewport;ModeScroll.content=modeList;
            ModeScroll.horizontal=false;ModeScroll.vertical=true;ModeScroll.movementType=ScrollRect.MovementType.Clamped;ModeScroll.scrollSensitivity=40;
            var track=UiKit.Panel("Mode list scrollbar",selector.transform,new Vector2(38,700),new Vector2(477,-80),new Color(1,1,1,.04f));
            track.raycastTarget=true;
            var handleArea=UiKit.Rect("Handle area",track.transform,Vector2.zero,Vector2.zero);
            handleArea.anchorMin=new Vector2(.34f,0);handleArea.anchorMax=new Vector2(.66f,1);handleArea.sizeDelta=Vector2.zero;
            var handle=UiKit.Panel("Handle",handleArea,new Vector2(10,0),Vector2.zero,UiKit.Gold);handle.raycastTarget=true;
            handle.rectTransform.anchorMin=new Vector2(.5f,0);handle.rectTransform.anchorMax=new Vector2(.5f,1);
            modeScrollbar=track.gameObject.AddComponent<Scrollbar>();modeScrollbar.handleRect=handle.rectTransform;modeScrollbar.targetGraphic=handle;
            modeScrollbar.direction=Scrollbar.Direction.BottomToTop;ModeScroll.verticalScrollbar=modeScrollbar;
            moreModes=UiKit.LButton("More modes",selector.transform,"menu.more_modes",new Vector2(330,44),new Vector2(0,-495),UiKit.Hex("254B47"),ScrollToNextCard);
            Show(StartScreen.MainMenu);
        }
        void ScrollToNextCard()
        {
            float overflow=modeList.rect.height-viewport.rect.height;
            if(overflow<=0)return;
            ModeScroll.StopMovement();
            float next=Mathf.Min(overflow,modeList.anchoredPosition.y+viewport.rect.height*.9f);
            ModeScroll.verticalNormalizedPosition=1-next/overflow;
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
        void RefreshCatalogCards()
        {
            var catalog=ApplicationServices.GameCatalog?.Current;
            if(catalog==null||ReferenceEquals(catalog,shownCatalog))return;
            float scrollOffset=modeList.anchoredPosition.y;
            shownCatalog=catalog;
            var compatible=catalog.Modes.Where(m=>m.Active).OrderBy(m=>m.SortOrder).ThenBy(m=>m.Id).ToArray();
            foreach(var key in cards.Keys.Except(compatible.Select(m=>m.Key)).ToArray()){Destroy(cards[key].gameObject);cards.Remove(key);}
            int index=0;
            foreach(var mode in compatible) {
                if(!cards.TryGetValue(mode.Key,out var card)) {
                    card=Instantiate(template,modeList);card.name="Mode card "+mode.Key;cards.Add(mode.Key,card);
                    foreach(Transform child in card)if(child.name.EndsWith(" avatar")){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                    for(int seat=0;seat<mode.PlayerCount;seat++) {
                        float x=mode.PlayerCount==2?(seat==0?-240:240):new[]{-295f,-185f,185f,295f}[seat];
                        Avatar(card,mode.BotsAllowed?(seat==0?"player.you":"player.bot"):"player.human",x,seat<mode.PlayerCount/2?DominoVisualTheme.TeamA:DominoVisualTheme.TeamB);
                    }
                }
                card.gameObject.SetActive(true);card.anchorMin=card.anchorMax=new Vector2(.5f,1);card.pivot=new Vector2(.5f,.5f);
                card.sizeDelta=new Vector2(880,420);card.localScale=Vector3.one;card.anchoredPosition=new Vector2(0,-210-index++*450);
                var button=card.Find("Start match").GetComponent<Button>();BindCard(card,button,mode);
                if(mode.Key==GameCatalogConfigurationAdapter.SupportedModeKey){Card=card;ModePlay=button;}
                if(mode.Key==GameCatalogConfigurationAdapter.DuelModeKey)DuelPlay=button;
            }
            modeList.sizeDelta=new Vector2(950,Mathf.Max(420,index*450-30));
            // Refresh future mode metadata without pulling the player's current selection out of view.
            modeList.anchoredPosition=new Vector2(0,Mathf.Clamp(scrollOffset,0,Mathf.Max(0,modeList.rect.height-viewport.rect.height)));
            DominoLocalization.Set(choose,"menu.choose_modes",index);
        }
        void BindCard(RectTransform card,Button button,GameModeSnapshot snapshot)
        {
            var mode=new GameModeDefinition(snapshot);var c=snapshot.RuleSet.Configuration;
            DominoLocalization.Set(card.Find("Mode name").GetComponent<Text>(),mode.DisplayNameKey);
            DominoLocalization.Set(card.Find("Subtitle").GetComponent<Text>(),snapshot.Key==GameCatalogConfigurationAdapter.DuelModeKey?"online.duel_subtitle":mode.SubtitleKey);
            DominoLocalization.Set(button.GetComponentInChildren<Text>(),mode.Online?"online.play":"menu.play");
            DominoLocalization.Set(card.Find("Teams").GetComponent<Text>(),"mode.teams",c.PlayerCount==2?1:2,c.PlayerCount==2?1:2);
            DominoLocalization.Bind(card.Find("Rules").GetComponent<Text>(),()=>DominoLocalization.Get("rules.summary",DominoLocalization.Get("rules.double_nine",c.MaxPip),DominoLocalization.Get("rules.tiles",c.TilesPerPlayer),DominoLocalization.Get("rules.no_draw"),DominoLocalization.Get("rules.target_score",c.TargetScore)));
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>StartRequested?.Invoke(mode));
        }
        void LateUpdate()
        {
            RefreshCatalogCards();
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
            ((RectTransform)composition.Find("Settings")).anchoredPosition = portrait ? new Vector2(0,-555) : new Vector2(450,310);
            Back.GetComponent<RectTransform>().anchoredPosition = portrait ? new Vector2(-365,435) : new Vector2(-460,245);
            ((RectTransform)selector.transform.Find("Choose")).anchoredPosition = new Vector2(0, portrait ? 350 : 240);
            viewport.sizeDelta=new Vector2(950,portrait?780:445);
            viewport.anchoredPosition=new Vector2(0,portrait?-75:-60);
            var scrollbarRect=(RectTransform)modeScrollbar.transform;
            scrollbarRect.sizeDelta=new Vector2(38,viewport.rect.height);
            scrollbarRect.anchoredPosition=new Vector2(477,viewport.anchoredPosition.y);
            bool overflow=modeList.rect.height>viewport.rect.height+.5f;
            modeScrollbar.gameObject.SetActive(overflow);moreModes.gameObject.SetActive(overflow);
            moreModes.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,portrait?-495:-317);
            moreModes.interactable=overflow&&ModeScroll.verticalNormalizedPosition>.001f;
            Settings.GetComponent<RectTransform>().sizeDelta = new Vector2(2400, portrait ? 3200 : 1400);
        }
    }
}
