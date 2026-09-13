using System;
using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using Domino.Configuration;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Domino.UI
{
    public enum DealPresentationPhase { Idle, Washing, Dealing, RevealingHand, OrganizingHands, Ready, Playing }

    public sealed partial class BoardView : MonoBehaviour
    {
        public const float InitialWashDuration = 1.2f;
        public const float HandOrganizeDuration = .75f;
        readonly Stack<DominoTileView> tilePool = new();
        readonly List<DominoTileView> dealStock = new();
        readonly List<DominoTileView> previewTiles = new();
        public DealPresentationPhase DealPhase { get; private set; }
        public bool IsPreparingRound => DealPhase != DealPresentationPhase.Idle && DealPhase != DealPresentationPhase.Playing;
        public int VisuallyDealt { get; private set; }
        public int CreatedTileViews { get; private set; }
        public event Action<DealPresentationPhase> DealPhaseChanged;
        public event Action<int> TileDealt;
        public IReadOnlyList<DominoTileView> HandViews(int player) => hands[player];
        List<DominoTileView>[] hands;
        readonly List<DominoTileView> played = new();
        readonly Dictionary<DominoTileView, (Vector3 scale, Vector2 position, float started)> endpointZoom = new();
        ChainEnd? opponentPlacement;
        readonly List<DominoTileView> washReserve = new();
        public IReadOnlyList<DominoTileView> ReserveViews => washReserve;
        Text reserveLabel;
        bool reserveParked;
        PlayerView[] players;
        public bool SharedDevice { get; private set; }
        static readonly Vector2[] PlayerPositions = { new(-674, -397), new(-735, 80), new(0, 402), new(735, 80) };
        readonly List<RectTransform> tableLayers = new();
        static readonly float[] TableInsets = { 0, 0, 4, 6, 8, 10 };
        float LocalScale => IsPortrait ? 2.1f : 1.16f;
        RectTransform content, safe, tiles;
        DominoTileView tilePrefab;
        GameConfigurationSnapshot configuration;
        Text prompt, boardHint, round;
        Text scoreA, scoreB;
        TableEffects effects;
        FeedbackPresenter feedback;
        public FeedbackPresenter Feedback => feedback;
        RectTransform washPreview;
        bool roundEnded, matchEnded;
        CanvasGroup turnBanner;
        Button playButton;
        GameObject menu;
        public LanguageSettingsPanel Settings { get; private set; }
        float bannerTarget;
        Image dropSurface;
        DominoTileView dragging;
        public bool IsDragging => dragging != null;
        public event Action<DominoTileView> TileSelected;
        public event Action<DominoTileView, ChainEnd> TileDropped;
        public Func<DominoTile, ChainEnd, bool> CanPlace;
        public event Action PlayRequested;
        public event Action RestartRequested;
        public event Action ExitRequested;
        public bool RoundPresentationFinished => roundEnded;
        public event Action NextRoundRequested;
        public RoundRewardView RoundRewardPanel { get; private set; }
        // Optional audio adapter can subscribe without changing gameplay or adding an audio dependency.
        public event Action TileLanded;
        public IReadOnlyList<DominoTileView> LocalTiles => hands[LocalPlayerSeat];
        public int PlayedCount => played.Count;

        public void Initialize(DominoTileView dominoPrefab, PlayerView playerPrefab, GameConfigurationSnapshot configuration, int localPlayerSeat = 0, bool sharedDevice=false)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            SharedDevice=sharedDevice;
            hands=new List<DominoTileView>[configuration.PlayerCount];players=new PlayerView[configuration.PlayerCount];
            for(int p=0;p<hands.Length;p++)hands[p]=new List<DominoTileView>();
            Perspective = new SeatPerspectiveMapper(localPlayerSeat, configuration);
            tilePrefab = dominoPrefab;
            gameObject.AddComponent<DominoWashAudio>().Initialize(this);
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(gameObject);
            gameObject.AddComponent<GraphicRaycaster>();
            var bg = UiKit.Rect("Backdrop", transform, Vector2.zero, Vector2.zero);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.sizeDelta = Vector2.zero;
            bg.gameObject.AddComponent<SoftBackdrop>().raycastTarget = false;
            safe = UiKit.Rect("Safe area", transform, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            content = UiKit.Rect("Landscape composition", safe, new Vector2(1600, 900), Vector2.zero);

            UiKit.Label("Brand", content, "TEAMFHO  /  DOMINO", new Vector2(320, 40), new Vector2(-610, 415), 22, UiKit.Cream, TextAnchor.MiddleLeft);
            var scoreboard = UiKit.Panel("Score surface", content, new Vector2(310, 58), new Vector2(525, 414), Color.clear).transform;
            round = UiKit.Label("Round", scoreboard, "", new Vector2(290, 20), new Vector2(0, -17), 12, UiKit.Muted);
            scoreA = UiKit.Label("Team A", scoreboard, "A  0", new Vector2(140, 32), new Vector2(-75, 9), 20, UiKit.Cream);
            UiKit.Label("Score separator", scoreboard, "·", new Vector2(20, 30), new Vector2(0, 9), 18, UiKit.Muted);
            scoreB = UiKit.Label("Team B", scoreboard, "0  B", new Vector2(140, 32), new Vector2(75, 9), 20, UiKit.Cream);

            UiKit.Panel("Table shadow", content, new Vector2(1050, 468), new Vector2(0, -16), new Color(0, 0, 0, .24f));
            UiKit.Panel("Table outer rail", content, new Vector2(1040, 458), Vector2.zero, UiKit.Hex("102C2B"));
            UiKit.Panel("Brass inlay", content, new Vector2(1018, 436), Vector2.zero, UiKit.Hex("738474"));
            UiKit.Panel("Felt", content, new Vector2(1014, 432), Vector2.zero, UiKit.Hex("23594F"));
            UiKit.Panel("Inner stitch", content, new Vector2(977, 395), Vector2.zero, UiKit.Hex("397366"));
            var table = UiKit.Panel("Playing surface", content, new Vector2(974, 392), Vector2.zero, UiKit.Hex("245A50"));
            dropSurface = table;
            table.color = DominoVisualTheme.Table;
            table.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var feltLight = UiKit.Rect("Felt lighting", table.transform, Vector2.zero, Vector2.zero);
            feltLight.anchorMin = Vector2.zero; feltLight.anchorMax = Vector2.one;
            var lighting = feltLight.gameObject.AddComponent<SoftBackdrop>();
            lighting.TableSurface = true; lighting.raycastTarget = false;
            string[] layers = { "Table shadow", "Table outer rail", "Brass inlay", "Felt", "Inner stitch", "Playing surface" };
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = (RectTransform)content.Find(layers[i]);
                float inset = TableInsets[i];
                layer.sizeDelta = new Vector2(1340 - inset, 710 - inset);
                layer.anchoredPosition = new Vector2(0, i == 0 ? 17 : 25);
                tableLayers.Add(layer);
            }
            content.Find("Table outer rail").GetComponent<Image>().color = DominoVisualTheme.Rail;
            content.Find("Brass inlay").GetComponent<Image>().color = Color.Lerp(DominoVisualTheme.Rail, UiKit.Gold, .22f);
            content.Find("Felt").GetComponent<Image>().color = DominoVisualTheme.Table;
            content.Find("Inner stitch").GetComponent<Image>().color = DominoVisualTheme.Table;
            table.raycastTarget = true;
            table.gameObject.AddComponent<Button>().onClick.AddListener(() => PlayRequested?.Invoke());
            boardHint = UiKit.Label("Empty board", content, "T E A M F H O", new Vector2(450, 60), new Vector2(0, 25), 24, UiKit.Hex("548679"));

            string[] names = { "Fredy", "Alex", "Maria", "John" };
            string[] initials = { "F", "A", "M", "J" };
            string[] colors = { "52786D", "866952", "8B6873", "526B87" };
            for (int p = 0; p < players.Length; p++)
            {
                players[p] = Instantiate(playerPrefab, content);
                players[p].name = "Player " + (p + 1) + " - " + names[p];
                ((RectTransform)players[p].transform).anchoredPosition = PlayerPositions[(int)Perspective.PositionFor(p)];
                players[p].Initialize(names[p], initials[p], UiKit.Hex(colors[p]), p == LocalPlayerSeat || p == Perspective.TopPlayer);
            }
            tiles = UiKit.Rect("Tiles", content, Vector2.zero, Vector2.zero);
            reserveLabel = UiKit.Label("Reserve count", tiles, "", new Vector2(210, 22), Vector2.zero, 14, UiKit.Muted);
            reserveLabel.gameObject.SetActive(false);
            var banner = UiKit.Rect("Turn banner", players[LocalPlayerSeat].transform, new Vector2(180, 22), new Vector2(12, -35));
            turnBanner = banner.gameObject.AddComponent<CanvasGroup>();
            turnBanner.alpha = 0;
            UiKit.LLabel("Your turn", banner, "game.your_turn", new Vector2(180, 22), Vector2.zero, 12, UiKit.Gold);
            prompt = UiKit.Label("Prompt", content, "", new Vector2(950, 28), new Vector2(0, -294), 15, UiKit.Muted);
            playButton = UiKit.LButton("Play", content, "game.play", new Vector2(158, 48), new Vector2(684, -377), UiKit.Hex("63826A"), () =>
            {
                if (matchEnded) RestartRequested?.Invoke();
                else if (roundEnded) NextRoundRequested?.Invoke();
                else PlayRequested?.Invoke();
            });
            UiKit.LButton("Restart", content, "game.restart", new Vector2(158, 32), new Vector2(684, -423), Color.clear, () => RestartRequested?.Invoke());
            var menuButton = UiKit.Button("Menu", content, "", new Vector2(48, 48), new Vector2(736, 414), Color.clear, ToggleMenu);
            for (int i = -1; i <= 1; i++) UiKit.Panel("Menu line", menuButton.transform, new Vector2(20, 2), new Vector2(0, i * 6), UiKit.Cream);
            menu = UiKit.Panel("Client menu", content, new Vector2(330, 350), new Vector2(595, 190), UiKit.Hex("102D2A")).gameObject;
            menu.GetComponent<Image>().raycastTarget = true;
            UiKit.LLabel("Styles heading", menu.transform, "style.heading", new Vector2(310,30), new Vector2(0,108), 18, UiKit.Gold);
            var ivoryButton = UiKit.Button("TEAMFHO ivory",menu.transform,"",new Vector2(294,48),new Vector2(0,54),UiKit.Hex("365448"),()=>{});
            var blueButton = UiKit.Button("Cuba blue",menu.transform,"",new Vector2(294,48),new Vector2(0,-4),UiKit.Hex("294B58"),()=>{});
            var whiteButton = UiKit.Button("TeamFHO white",menu.transform,"",new Vector2(294,48),new Vector2(0,-62),UiKit.Hex("3D3F6B"),()=>{});
            void UpdateStyleLabels()
            {
                DominoLocalization.Bind(ivoryButton.GetComponentInChildren<Text>(), () => DominoLocalization.Get("style.choice", DominoLocalization.Get("style.ivory"), TileStyles.Current == TileStyle.TeamFhoIvory ? "  •" : ""));
                DominoLocalization.Bind(blueButton.GetComponentInChildren<Text>(), () => DominoLocalization.Get("style.choice", DominoLocalization.Get("style.blue"), TileStyles.Current == TileStyle.CubaBlue ? "  •" : ""));
                DominoLocalization.Bind(whiteButton.GetComponentInChildren<Text>(), () => DominoLocalization.Get("style.choice", DominoLocalization.Get("style.white"), TileStyles.Current == TileStyle.TeamFhoWhite ? "  •" : ""));
            }
            ivoryButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.TeamFhoIvory); UpdateStyleLabels(); });
            blueButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.CubaBlue); UpdateStyleLabels(); });
            whiteButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.TeamFhoWhite); UpdateStyleLabels(); });
            UpdateStyleLabels();
            UiKit.LLabel("Saved style",menu.transform,"style.saved",new Vector2(310,25),new Vector2(0,-112),14,UiKit.Muted);
            UiKit.LButton("Exit match", menu.transform, "game.exit_match", new Vector2(294,38), new Vector2(0,-148), Color.clear, () => ExitRequested?.Invoke());
            menu.SetActive(false);
            Settings = LanguageSettingsPanel.Create(content);
            UiKit.LButton("Settings", menu.transform, "menu.settings", new Vector2(294,36), new Vector2(0,149), Color.clear, Settings.Open);
            foreach (string name in new[] { "Score surface", "Menu", "Client menu", "Play", "Restart" }) AnchorEdge(name, true);
            AnchorEdge("Brand", false);
            menu.transform.SetAsLastSibling();
            effects = gameObject.AddComponent<TableEffects>();
            effects.Initialize(content);
            feedback = gameObject.AddComponent<FeedbackPresenter>();
            feedback.Initialize(content, (RectTransform)turnBanner.transform, (RectTransform)scoreboard);
            CreatePresentationLayers();
            // Allocate once, before animation. All rounds reuse these views.
            for (int i = 0; i < configuration.TotalTiles; i++) ReleaseTile(CreateTile());
            SetInteraction(false, false);
            Fit();
        }
        void ToggleMenu() => menu.SetActive(!menu.activeSelf);
        void AnchorEdge(string name, bool right)
        {
            var rect = (RectTransform)content.Find(name);
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1 : 0, .5f);
            rect.anchoredPosition -= new Vector2(right ? 800 : -800, 0);
        }
        void Update()
        {
            Fit();
            UpdatePlacementTargets();
            if (turnBanner) turnBanner.alpha = Mathf.MoveTowards(turnBanner.alpha, bannerTarget, Time.unscaledDeltaTime * 4);
            foreach (var view in hands[LocalPlayerSeat])
            {
                if (!view.Selectable || view.IsDragging) continue;
                var target = view.Home + (view.Selected ? Vector2.up * 26 : Vector2.zero);
                view.Rect.anchoredPosition = Vector2.Lerp(view.Rect.anchoredPosition, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
                view.Rect.localScale = Vector3.Lerp(view.Rect.localScale, Vector3.one * (view.Selected ? LocalScale * 1.07f : LocalScale), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
        }
        public void Clear()
        {
            Domino.Infrastructure.ApplicationServices.RoundRewards?.LeaveRound();
            if (RoundRewardPanel) { RoundRewardPanel.gameObject.SetActive(false); Destroy(RoundRewardPanel.gameObject); RoundRewardPanel = null; }
            prompt.gameObject.SetActive(true);
            ResetEndpointZoom();
            effects.Clear();
            feedback.Clear();
            SetDealPhase(DealPresentationPhase.Idle);
            VisuallyDealt = 0;
            foreach (var view in dealStock) ReleaseTile(view);
            dealStock.Clear();
            foreach (var view in previewTiles) ReleaseTile(view);
            previewTiles.Clear();
            if (washPreview) { washPreview.gameObject.SetActive(false); Destroy(washPreview.gameObject); washPreview = null; }
            tiles.gameObject.SetActive(true);
            if (localHandLayer) localHandLayer.gameObject.SetActive(true);
            if (opponentHandLayer) opponentHandLayer.gameObject.SetActive(true);
            foreach (var tile in washReserve) ReleaseTile(tile);
            washReserve.Clear();
            reserveParked = false;
            reserveLabel.gameObject.SetActive(false);
            roundEnded = matchEnded = false;
            DominoLocalization.Set(playButton.GetComponentInChildren<Text>(), "game.play");
            CancelDrag();
            foreach (var hand in hands) { foreach (var tile in hand) ReleaseTile(tile); hand.Clear(); }
            foreach (var tile in played) ReleaseTile(tile);
            played.Clear(); boardHint.gameObject.SetActive(true); menu.SetActive(false);
            foreach (var player in players) { player.SetCount(0); player.SetTurn(false); }
            bannerTarget = 0; DominoLocalization.Set(round, "game.round", 1);
            turnBanner.alpha = 0;
            SetInteraction(false, false);
        }
        Vector2 HandPosition(int player, int index, int count)
        {
            float offset = index - (count - 1) * .5f;
            float available = Mathf.Min(750, content.rect.width - 560);
            float spacing = Mathf.Min(65, (available - 96 * LocalScale) / Mathf.Max(1, count - 1));
            if (IsPortrait) return PortraitHandPosition(player, index, count);
            return (int)Perspective.PositionFor(player) switch
            {
                0 => new Vector2(offset * spacing, -content.rect.height * (388f / 900)),
                1 => new Vector2(-content.sizeDelta.x / 2 + 65, -32 - index * 7),
                2 => new Vector2(144 + index * 7, 415),
                _ => new Vector2(content.sizeDelta.x / 2 - 65, -32 - index * 7)
            };
        }
        Vector2 ReservePosition(int index)
        {
            var surface = dropSurface.rectTransform;
            return surface.anchoredPosition + new Vector2(surface.rect.width / 2 - 126 + (index % 5 - 2) * 40,
                surface.rect.height / 2 - 44 - index / 5 * 22 - (SharedDevice && IsPortrait ? 300 : 0));
        }
        public IEnumerator Deal(IReadOnlyList<DominoTile>[] modelHands)
        {
            SetDealPhase(DealPresentationPhase.Washing);
            SetInteraction(false, false);
            foreach (var player in players) player.SetTurn(false);
            bannerTarget = 0;
            VisuallyDealt = 0;
            boardHint.gameObject.SetActive(false);
            DominoLocalization.Set(prompt, "game.washing");
            for (int i = 0; i < configuration.TotalTiles; i++)
            {
                var view = AcquireTile();
                // Values stay private until a hand is assigned; all wash tiles are backs.
                view.Initialize(default, false, false);
                view.Rect.localScale = Vector3.one * .5f;
                dealStock.Add(view);
            }
            for (float elapsed = 0; elapsed < InitialWashDuration; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / InitialWashDuration);
                float strength = Mathf.Sin(progress * Mathf.PI);
                var size = dropSurface.rectTransform.rect.size;
                for (int i = 0; i < dealStock.Count; i++)
                {
                    float angle = i * 2.39996f;
                    float radius = Mathf.Sqrt((i + .5f) / dealStock.Count);
                    var anchor = new Vector2(Mathf.Cos(angle) * size.x * .22f * radius, Mathf.Sin(angle) * size.y * .2f * radius);
                    var swirl = new Vector2(Mathf.Sin(angle + progress * 8) * 35, Mathf.Cos(angle + progress * 8) * 24) * strength;
                    dealStock[i].Rect.anchoredPosition = dropSurface.rectTransform.anchoredPosition + anchor + swirl;
                    dealStock[i].Rect.localRotation = Quaternion.Euler(0, 0, i * 137.5f + strength * 35);
                }
                yield return null;
            }
            // Set aside the fifteen undealt views; no draw operation is introduced.
            int toDeal = configuration.PlayerCount * configuration.TilesPerPlayer;
            while (dealStock.Count > toDeal)
            {
                int last = dealStock.Count - 1;
                var reserveTile = dealStock[last];
                reserveTile.name = "Undealt reserve tile";
                washReserve.Add(reserveTile); dealStock.RemoveAt(last);
            }
            SetDealPhase(DealPresentationPhase.Dealing);
            DominoLocalization.Set(prompt, "game.deal_count", configuration.TilesPerPlayer);
            for (int n = 0; n < modelHands[0].Count; n++)
            foreach (int p in configuration.Deal.SeatOrder)
            {
                var view = dealStock[dealStock.Count - 1];
                dealStock.RemoveAt(dealStock.Count - 1);
                var startRotation = view.Rect.localRotation;
                view.name = "Player " + (p + 1) + " tile";
                view.Initialize(modelHands[p][n], false, false);
                view.Rect.localRotation = startRotation;
                view.SetCompactBack(p != LocalPlayerSeat);
                view.transform.SetParent(p == LocalPlayerSeat ? localHandLayer : opponentHandLayer,false);
                hands[p].Add(view);
                view.transform.SetAsLastSibling();
                int seat = p, index = n, count = modelHands[p].Count;
                float duration = .245f + .04f * (.5f + .5f * Mathf.Sin(VisuallyDealt * 2.4f));
                yield return Move(view, () => ProvisionalHandPosition(seat, index, count), HandAngle(p) + (n % 2 == 0 ? -3 : 3), p == LocalPlayerSeat ? LocalScale : OpponentScale, duration);
                view.Home = HandPosition(p, n, count);
                players[p].SetCount(n + 1);
                VisuallyDealt++;
                TileDealt?.Invoke(p);
            }
            // Only move the reserve once all forty arrivals have completed.
            DominoLocalization.Set(reserveLabel, "game.reserve", washReserve.Count);
            reserveLabel.gameObject.SetActive(washReserve.Count > 0);
            var reserveStarts = new Vector2[washReserve.Count];
            var reserveRotations = new Quaternion[washReserve.Count];
            for (int i = 0; i < washReserve.Count; i++)
            { reserveStarts[i] = washReserve[i].Rect.anchoredPosition; reserveRotations[i] = washReserve[i].Rect.localRotation; }
            for (float elapsed = 0; elapsed < .35f; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / .35f);
                for (int i = 0; i < washReserve.Count; i++)
                {
                    var rect = washReserve[i].Rect;
                    rect.anchoredPosition = Vector2.Lerp(reserveStarts[i], ReservePosition(i), t);
                    rect.localRotation = Quaternion.Slerp(reserveRotations[i], Quaternion.identity, t);
                    rect.localScale = Vector3.one * Mathf.Lerp(.5f, .36f, t);
                }
                yield return null;
            }
            reserveParked = true;
            foreach (var tile in washReserve) { tile.Rect.localRotation = Quaternion.identity; tile.Rect.localScale = Vector3.one * .36f; }
            yield return new WaitForSeconds(.12f);
            SetDealPhase(DealPresentationPhase.RevealingHand);
            DominoLocalization.Set(prompt, "game.preparing_hand");
            for (int phase = 0; phase < 2; phase++)
            {
                for (float elapsed = 0; elapsed < .16f; elapsed += Time.deltaTime)
                {
                    float t = Mathf.SmoothStep(0, 1, elapsed / .16f);
                    float width = phase == 0 ? Mathf.Lerp(1, .03f, t) : Mathf.Lerp(.03f, 1, t);
                    foreach (var view in hands[LocalPlayerSeat]) view.Rect.localScale = new Vector3(LocalScale, LocalScale * width, LocalScale);
                    yield return null;
                }
                if (phase == 0 && !SharedDevice) foreach (var view in hands[LocalPlayerSeat]) view.Reveal();
            }
            SetDealPhase(DealPresentationPhase.OrganizingHands);
            var starts = new Vector2[toDeal]; var rotations = new Quaternion[toDeal];
            int cursor = 0;
            foreach (var hand in hands) foreach (var view in hand)
            { starts[cursor] = view.Rect.anchoredPosition; rotations[cursor++] = view.Rect.localRotation; }
            for (float elapsed = 0; elapsed < HandOrganizeDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / HandOrganizeDuration);
                cursor = 0;
                for (int p = 0; p < hands.Length; p++) for (int n = 0; n < hands[p].Count; n++)
                {
                    var view = hands[p][n];
                    view.Home = HandPosition(p, n, hands[p].Count);
                    view.Rect.anchoredPosition = Vector2.Lerp(starts[cursor], view.Home, t);
                    view.Rect.localRotation = Quaternion.Slerp(rotations[cursor++], Quaternion.Euler(0, 0, HandAngle(p)), t);
                    view.Rect.localScale = Vector3.one * (p == LocalPlayerSeat ? LocalScale : OpponentScale);
                }
                yield return null;
            }
            for (int p = 0; p < hands.Length; p++) for (int n = 0; n < hands[p].Count; n++)
            {
                var view = hands[p][n];
                view.Home = HandPosition(p, n, hands[p].Count);
                view.Rect.anchoredPosition = view.Home;
                view.Rect.localRotation = Quaternion.Euler(0, 0, HandAngle(p));
                view.Rect.localScale = Vector3.one * (p == LocalPlayerSeat ? LocalScale : OpponentScale);
            }
            SetDealPhase(DealPresentationPhase.Ready);
            DominoLocalization.Set(prompt, "game.hands_ready");
            yield return new WaitForSeconds(.08f);
        }
        Vector2 ProvisionalHandPosition(int player, int index, int count)
        {
            var target = HandPosition(player, index, count);
            if (player == LocalPlayerSeat) return new Vector2(target.x * .78f, target.y + (index % 2 == 0 ? 2 : -2));
            return target + new Vector2(index % 2 == 0 ? -5 : 5, 3);
        }
        void SetDealPhase(DealPresentationPhase phase) { DealPhase = phase; if (phase != DealPresentationPhase.Playing) UpdateTurnNotice(-1); DealPhaseChanged?.Invoke(phase); }
        DominoTileView CreateTile()
        {
            var view = Instantiate(tilePrefab, tiles);
            CreatedTileViews++;
            view.Initialize(default, false, false);
            view.Clicked = t => TileSelected?.Invoke(t);
            view.DragRequested = BeginDrag; view.DragMoved = HoverDrag; view.DragReleased = Drop;
            return view;
        }
        DominoTileView AcquireTile()
        {
            var view = tilePool.Count > 0 ? tilePool.Pop() : CreateTile();
            view.transform.SetParent(tiles, false); view.SetCompactBack(true); view.gameObject.SetActive(true);
            return view;
        }
        void ReleaseTile(DominoTileView view)
        {
            view.CancelDrag(); view.Selectable = false; view.Select(false); view.Conceal();
            view.gameObject.SetActive(false); view.transform.SetParent(tiles, false);
            tilePool.Push(view);
        }
        public void SetInteraction(bool canChoose, bool canPlay)
        {
            if (IsPreparingRound) canChoose = canPlay = false;
            if (!canChoose) ResetEndpointZoom();
            foreach (var tile in hands[LocalPlayerSeat]) tile.Selectable = canChoose;
            playButton.interactable = canPlay;
            dropSurface.GetComponent<Button>().interactable = canChoose;
            UpdatePlacementTargets();
        }
        bool BeginDrag(DominoTileView tile)
        {
            if (dragging || !tile.Selectable || !hands[LocalPlayerSeat].Contains(tile)) return false;
            dragging = tile;
            DominoLocalization.Set(prompt, "game.drag_drop");
            playButton.interactable = false;
            return true;
        }
        bool OverTable(PointerEventData pointer) => RectTransformUtility.RectangleContainsScreenPoint(dropSurface.rectTransform, pointer.position, pointer.pressEventCamera);
        ChainEnd DropEnd(DominoTileView tile, PointerEventData pointer)
        {
            if (played.Count == 0) return ChainEnd.Auto;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tiles, pointer.position, pointer.pressEventCamera, out var point);
            var first = played[0].Rect.anchoredPosition;
            var last = played[played.Count - 1].Rect.anchoredPosition;
            var axis = (Vector2)(played[0].Rect.localRotation * Vector3.right);
            var preferred = played.Count == 1 ? (Vector2.Dot(point - first, axis) < 0 ? ChainEnd.Left : ChainEnd.Right)
                : (Vector2.Distance(point, first) < Vector2.Distance(point, last) ? ChainEnd.Left : ChainEnd.Right);
            return preferred;
        }
        void HoverDrag(DominoTileView tile, PointerEventData pointer)
        {
            if (dragging != tile) return;
            bool valid = CanPlace == null || CanPlace(tile.Tile, DropEnd(tile, pointer));
            dropSurface.color = UiKit.Hex(!OverTable(pointer) ? "245A50" : valid ? "327565" : "754D43");
        }
        void Drop(DominoTileView tile, PointerEventData pointer)
        {
            if (dragging != tile) return;
            var end = DropEnd(tile, pointer);
            bool overTable = OverTable(pointer);
            bool valid = CanPlace == null || CanPlace(tile.Tile, end);
            bool accepted = tile.Selectable && overTable && valid;
            dragging = null;
            dropSurface.color = UiKit.Hex("245A50");
            if (accepted) TileDropped?.Invoke(tile, end);
            else
            {
                RestoreDragPrompt(); // Return to the hand without silently switching ends.
                if (overTable && !valid) DominoLocalization.Set(prompt, "game.invalid_end");
            }
            UpdatePlacementTargets();
        }
        void RestoreDragPrompt()
        {
            playButton.interactable = hands[LocalPlayerSeat].Exists(t => t.Selected && t.Selectable);
            DominoLocalization.Set(prompt, "game.drag_select");
        }
        void CancelDrag()
        {
            if (dragging) dragging.CancelDrag();
            dragging = null;
            if (dropSurface) dropSurface.color = UiKit.Hex("245A50");
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused && dragging) { CancelDrag(); RestoreDragPrompt(); }
        }
        public void SetTurn(int player)
        {
            if (DealPhase == DealPresentationPhase.Ready) SetDealPhase(DealPresentationPhase.Playing);
            if (IsPreparingRound) return;
            for (int p = 0; p < players.Length; p++) players[p].SetTurn(p == player);
            bannerTarget = player == LocalPlayerSeat ? 1 : 0;
            feedback.Turn(player == LocalPlayerSeat);
            UpdateTurnNotice(player);
            DominoLocalization.Set(prompt, player == LocalPlayerSeat ? "game.drag_select" : "game.thinking", new[] { "Fredy", "Alex", "Maria", "John" }[player]);
        }
        public void SetSelected(DominoTileView selection)
        {
            if (IsPreparingRound) return;
            foreach (var tile in hands[LocalPlayerSeat]) tile.Select(tile == selection);
            if (selection) selection.transform.SetAsLastSibling();
            if (selection) feedback.Cue(FeedbackCue.TilePick, selection.Rect.anchoredPosition);
            playButton.interactable = selection != null;
            DominoLocalization.Set(prompt, selection ? "game.selected_hint" : "game.select_hint");
            UpdatePlacementTargets();
        }
        void UpdatePlacementTargets()
        {
            var selection = dragging ? dragging : hands[LocalPlayerSeat].Find(t => t.Selected && t.Selectable);
            bool canHighlight = selection && selection.Selectable && !roundEnded && tiles.gameObject.activeSelf;
            bool left = canHighlight && (CanPlace == null || CanPlace(selection.Tile, ChainEnd.Left));
            bool right = canHighlight && (CanPlace == null || CanPlace(selection.Tile, ChainEnd.Right));
            if (opponentPlacement.HasValue)
            {
                left = opponentPlacement.Value == ChainEnd.Left;
                right = opponentPlacement.Value == ChainEnd.Right;
            }
            for (int i = 0; i < played.Count; i++)
            {
                // A one-tile chain has both logical ends on the same visual tile.
                bool highlight = (i == 0 && left) || (i == played.Count - 1 && right);
                if (played[i].Selected != highlight) played[i].Select(highlight);
                var tile = played[i];
                if (highlight && !endpointZoom.ContainsKey(tile))
                {
                    endpointZoom.Add(tile, (tile.Rect.localScale, tile.Rect.anchoredPosition, Time.unscaledTime));
                    tile.transform.SetAsLastSibling();
                    // The dragged tile remains above the destination highlight.
                    if (selection) selection.transform.SetAsLastSibling();
                }
                if (!endpointZoom.TryGetValue(tile, out var basis)) continue;
                float pulse = .5f - .5f * Mathf.Cos((Time.unscaledTime - basis.started) * Mathf.PI * 2 / 1.5f);
                float zoom = highlight ? 1.16f + .04f * pulse : 1;
                float blend = 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime);
                tile.Rect.localScale = Vector3.Lerp(tile.Rect.localScale, basis.scale * zoom, blend);
                // Grow towards the open end to keep the adjacent tile readable.
                Vector2 outward = Vector2.zero;
                if (played.Count > 1)
                {
                    int neighbor = i == 0 ? 1 : i - 1;
                    outward = (basis.position - played[neighbor].Rect.anchoredPosition).normalized;
                }
                var offset = outward * (highlight ? 8 : 0);
                tile.Rect.anchoredPosition = Vector2.Lerp(tile.Rect.anchoredPosition, basis.position + offset, blend);
                if (!highlight && Vector3.Distance(tile.Rect.localScale, basis.scale) < .001f
                    && Vector2.Distance(tile.Rect.anchoredPosition, basis.position) < .05f)
                {
                    tile.Rect.localScale = basis.scale;
                    tile.Rect.anchoredPosition = basis.position;
                    endpointZoom.Remove(tile);
                }
            }
        }
        void ResetEndpointZoom()
        {
            opponentPlacement = null;
            foreach (var entry in endpointZoom)
            {
                if (!entry.Key) continue;
                entry.Key.Rect.localScale = entry.Value.scale;
                entry.Key.Rect.anchoredPosition = entry.Value.position;
                entry.Key.Select(false);
            }
            endpointZoom.Clear();
        }
        public void ShowMessage(string key, params object[] args) => DominoLocalization.Set(prompt, key, args);
        public IEnumerator ShowPass(int player) => effects.Knock((int)Perspective.PositionFor(player), KnockPosition(player));
        public IEnumerator ShowWinner(string winner, bool match, bool localWon)
        {
            feedback.Cue(match ? FeedbackCue.GameWin : FeedbackCue.RoundWin, Vector2.zero);
            yield return effects.Celebrate(winner, match, localWon);
            feedback.Clear();
        }
        public IEnumerator PreviewWash(bool isPreview = true)
        {
            UpdateTurnNotice(-1);
            washPreview = UiKit.Rect("Wash preview tiles", content, Vector2.zero, Vector2.zero);
            // Keep the hands and chain intact; only preview copies are shuffled.
            washPreview.SetSiblingIndex(tiles.GetSiblingIndex() + 1);
            var set = DominoTile.CreateSet(configuration.MaxPip);
            for (int i = 0; i < set.Count; i++)
            {
                var tile = AcquireTile();
                tile.transform.SetParent(washPreview, false);
                tile.Initialize(set[i], isPreview, false);
                tile.Rect.anchoredPosition = new Vector2((i % 11 - 5) * 52, (i / 11 - 2) * 38);
                tile.Rect.localScale = Vector3.one * .5f;
                previewTiles.Add(tile);
            }
            tiles.gameObject.SetActive(false);
            localHandLayer.gameObject.SetActive(false); opponentHandLayer.gameObject.SetActive(false);
            ShowMessage(isPreview ? "game.wash_preview" : "game.wash_before_deal");
            yield return effects.Wash(previewTiles);
            yield return new WaitForSeconds(.7f);
            foreach (var tile in previewTiles) ReleaseTile(tile);
            previewTiles.Clear();
            washPreview.gameObject.SetActive(false); Destroy(washPreview.gameObject); washPreview = null;
            tiles.gameObject.SetActive(true);
            if (localHandLayer) localHandLayer.gameObject.SetActive(true);
            if (opponentHandLayer) opponentHandLayer.gameObject.SetActive(true);
        }
        public IEnumerator WashDominoes(IReadOnlyList<DominoTile> reserve)
        {
            SetInteraction(false, false);
            reserveParked = false;
            reserveLabel.gameObject.SetActive(false);
            var all = new List<DominoTileView>(configuration.TotalTiles);
            all.AddRange(played);
            foreach (var hand in hands) all.AddRange(hand);
            for (int i = 0; i < reserve.Count; i++)
            {
                var tile = i < washReserve.Count ? washReserve[i] : AcquireTile();
                tile.name = "Reserve - washing dominoes";
                tile.Orient(reserve[i]); tile.Conceal();
                if (i >= washReserve.Count) washReserve.Add(tile);
                all.Add(tile);
            }
            foreach (var tile in all) { tile.transform.SetParent(tiles,false); tile.Selectable = false; tile.Select(false); }
            ShowMessage("game.washing");
            yield return effects.Wash(all);
        }
        public IEnumerator Play(GameEvent e)
        {
            boardHint.gameObject.SetActive(false);
            var tile = hands[e.Player].Find(t => t.Tile.Equals(e.Tile));
            if (!tile) throw new InvalidOperationException("Played tile is missing from presentation.");
            if (e.Player != LocalPlayerSeat && played.Count > 0)
            {
                // Preview the confirmed destination without exposing the opponent's hand.
                opponentPlacement = e.ChainIndex == 0 ? ChainEnd.Left : ChainEnd.Right;
                UpdatePlacementTargets();
                yield return new WaitForSeconds(.4f);
            }
            ResetEndpointZoom();
            hands[e.Player].Remove(tile); played.Insert(e.ChainIndex, tile); tile.Orient(e.Tile); tile.Select(false); tile.Selectable = false;
            tile.transform.SetParent(tiles,false);
            tile.transform.SetAsLastSibling();
            feedback.Cue(FeedbackCue.TilePlay, tile.Rect.anchoredPosition);
            yield return MoveChain(e.Player == LocalPlayerSeat ? .42f : .75f, tile);
            tile.Reveal();
            TileLanded?.Invoke();
            feedback.Cue(FeedbackCue.TileImpact, tile.Rect.anchoredPosition);
            var landingScale = tile.Rect.localScale;
            for (float elapsed = 0; elapsed < .14f; elapsed += Time.deltaTime)
            {
                tile.Rect.localScale = landingScale * (1 + .03f * Mathf.Sin(Mathf.Clamp01(elapsed / .14f) * Mathf.PI));
                yield return null;
            }
            tile.Rect.localScale = landingScale;
            players[e.Player].SetCount(hands[e.Player].Count);
            for (int i = 0; i < hands[e.Player].Count; i++) hands[e.Player][i].Home = HandPosition(e.Player, i, hands[e.Player].Count);
            // Reflow also runs while inputs are locked.
            float time = 0;
            while (time < .15f)
            {
                time += Time.deltaTime;
                foreach (var remaining in hands[e.Player]) remaining.Rect.anchoredPosition = Vector2.Lerp(remaining.Rect.anchoredPosition, remaining.Home, time / .15f);
                yield return null;
            }
        }
        IEnumerator MoveChain(float duration, DominoTileView incoming)
        {
            var starts = new Vector2[played.Count];
            var rotations = new Quaternion[played.Count];
            var scales = new Vector3[played.Count];
            for (int i = 0; i < played.Count; i++)
            {
                starts[i] = played[i].Rect.anchoredPosition;
                rotations[i] = played[i].Rect.localRotation;
                scales[i] = played[i].Rect.localScale;
            }
            var layout = ChainLayout();
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < played.Count; i++)
                {
                    var pose = layout[i];
                    var r = played[i].Rect;
                    r.anchoredPosition = Vector2.Lerp(starts[i], pose.Position + ChainCenter, t);
                    if (played[i] == incoming) r.anchoredPosition += Vector2.up * (Mathf.Sin(t * Mathf.PI) * 15);
                    r.localRotation = Quaternion.Slerp(rotations[i], Quaternion.Euler(0, 0, pose.Angle), t);
                    r.localScale = Vector3.Lerp(scales[i], Vector3.one * pose.Scale, t);
                }
                yield return null;
            }
        }
        static IEnumerator Move(DominoTileView tile, Func<Vector2> destination, float angle, float scale, float duration)
        {
            Vector2 start = tile.Rect.anchoredPosition;
            var startRotation = tile.Rect.localRotation; var startScale = tile.Rect.localScale;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                // Smootherstep starts and ends with zero velocity and acceleration.
                float t = progress * progress * progress * (progress * (progress * 6 - 15) + 10);
                tile.Rect.anchoredPosition = Vector2.Lerp(start, destination(), t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 8);
                tile.Rect.localRotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0, 0, angle), t);
                tile.Rect.localScale = Vector3.Lerp(startScale, Vector3.one * scale, t);
                yield return null;
            }
            tile.Rect.anchoredPosition = destination();
            tile.Rect.localRotation = Quaternion.Euler(0, 0, angle);
            tile.Rect.localScale = Vector3.one * scale;
        }
        public void UpdateScore(Domino.Game.GameRules rules, Domino.Game.MatchState match)
        {
            displayedRound = match.RoundNumber;
            scoreA.fontSize = scoreB.fontSize = IsPortrait ? 24 : rules.Teams ? 18 : 15;
            round.fontSize = IsPortrait ? 16 : 12;
            if(configuration.PlayerCount==2) { DominoLocalization.Set(scoreA,"game.player_score",1,match.Score(0));DominoLocalization.Set(scoreB,"game.player_score",2,match.Score(1)); }
            else {
            DominoLocalization.Set(scoreA, rules.Teams ? "game.score_a" : "game.individual_a", match.Score(rules.Teams ? configuration.GetTeamForPlayer(LocalPlayerSeat) : 0), rules.Teams ? 0 : match.Score(2));
            DominoLocalization.Set(scoreB, rules.Teams ? "game.score_b" : "game.individual_b", match.Score(rules.Teams ? 1 - configuration.GetTeamForPlayer(LocalPlayerSeat) : 1), rules.Teams ? 0 : match.Score(3));
            }
            DominoLocalization.Set(round, "game.score_round", match.RoundNumber, rules.TargetScore, match.Multiplier);
            round.rectTransform.sizeDelta = new Vector2(290, 25);
        }
        public IEnumerator RevealHands(int[] points)
        {
            UpdateTurnNotice(-1);
            var reveal = new List<DominoTileView>();
            foreach (var hand in hands) reveal.AddRange(hand);
            var scales = new Vector3[reveal.Count];
            for (int i = 0; i < reveal.Count; i++) scales[i] = reveal[i].Rect.localScale;
            for (int phase = 0; phase < 2; phase++)
            {
                float elapsed = 0;
                while (elapsed < .16f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / .16f);
                    float width = phase == 0 ? Mathf.Lerp(1, .03f, t) : Mathf.Lerp(.03f, 1, t);
                    for (int i = 0; i < reveal.Count; i++) reveal[i].Rect.localScale = new Vector3(scales[i].x * width, scales[i].y, scales[i].z);
                    yield return null;
                }
                if (phase == 0) foreach (var tile in reveal) tile.Reveal();
            }
            for (int p = 0; p < players.Length; p++) players[p].ShowPoints(hands[p].Count, points[p]);
        }
        public void Finish(Func<string> message, bool matchFinished = false)
        {
            foreach (var player in players) player.SetTurn(false);
            bannerTarget = 0;
            UpdateTurnNotice(-1);
            DominoLocalization.Bind(prompt, message);
            SetInteraction(false, false);
            roundEnded = true; matchEnded = matchFinished;
            DominoLocalization.Set(playButton.GetComponentInChildren<Text>(), matchFinished ? "game.new_match" : "game.next_round");
            playButton.GetComponentInChildren<Text>().fontSize = 16;
            playButton.interactable = true;
            prompt.gameObject.SetActive(false);
            if (RoundRewardPanel) Destroy(RoundRewardPanel.gameObject);
            var resultPanel = UiKit.Rect("Round result and optional reward", content, new Vector2(800,590), Vector2.zero);
            RoundRewardPanel = resultPanel.gameObject.AddComponent<RoundRewardView>();
            RoundRewardPanel.Initialize(SharedDevice?null:Domino.Infrastructure.ApplicationServices.RoundRewards,
                Domino.Infrastructure.ApplicationServices.Player, message, () => scoreA.text + "     ·     " + scoreB.text,
                () => { if (matchEnded) RestartRequested?.Invoke(); else NextRoundRequested?.Invoke(); });
        }
    }
}
