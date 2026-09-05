using System;
using System.Collections;
using System.Collections.Generic;
using Domino.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Domino.UI
{
    public sealed class BoardView : MonoBehaviour
    {
        readonly List<DominoTileView>[] hands = { new(), new(), new(), new() };
        readonly List<DominoTileView> played = new();
        readonly List<DominoTileView> washReserve = new();
        readonly PlayerView[] players = new PlayerView[4];
        static readonly Vector2[] PlayerPositions = { new(-645, -270), new(-665, 65), new(0, 329), new(665, 65) };
        RectTransform content, safe, tiles;
        DominoTileView tilePrefab;
        Text prompt, boardHint, round;
        Text scoreA, scoreB;
        TableEffects effects;
        RectTransform washPreview;
        bool roundEnded, matchEnded;
        CanvasGroup turnBanner;
        Button playButton;
        GameObject menu;
        float bannerTarget;
        Image dropSurface;
        DominoTileView dragging;
        public bool IsDragging => dragging != null;
        public event Action<DominoTileView> TileSelected;
        public event Action<DominoTileView, ChainEnd> TileDropped;
        public Func<DominoTile, ChainEnd, bool> CanPlace;
        public event Action PlayRequested;
        public event Action RestartRequested;
        public event Action NextRoundRequested;
        public IReadOnlyList<DominoTileView> LocalTiles => hands[0];
        public int PlayedCount => played.Count;

        public void Initialize(DominoTileView dominoPrefab, PlayerView playerPrefab)
        {
            tilePrefab = dominoPrefab;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>();
            var bg = UiKit.Rect("Backdrop", transform, Vector2.zero, Vector2.zero);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.sizeDelta = Vector2.zero;
            bg.gameObject.AddComponent<SoftBackdrop>().raycastTarget = false;
            safe = UiKit.Rect("Safe area", transform, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            content = UiKit.Rect("Landscape composition", safe, new Vector2(1600, 900), Vector2.zero);

            UiKit.Label("Brand", content, "D O M I N O", new Vector2(280, 40), new Vector2(-600, 383), 29, UiKit.Cream, TextAnchor.MiddleLeft);
            UiKit.Label("Subtitle", content, "CLUB  /  MESA DE CUATRO", new Vector2(300, 28), new Vector2(-590, 348), 14, UiKit.Muted, TextAnchor.MiddleLeft);
            round = UiKit.Label("Round", content, "RONDA 01", new Vector2(150, 25), new Vector2(525, 395), 14, UiKit.Gold);
            UiKit.Panel("Score surface", content, new Vector2(268, 68), new Vector2(525, 342), UiKit.Hex("193A37"));
            scoreA = UiKit.Label("Team A", content, "EQUIPO A   0", new Vector2(128, 50), new Vector2(458, 342), 18, UiKit.Cream);
            UiKit.Label("Score separator", content, "|", new Vector2(20, 40), new Vector2(525, 342), 18, UiKit.Muted);
            scoreB = UiKit.Label("Team B", content, "0   EQUIPO B", new Vector2(128, 50), new Vector2(592, 342), 18, UiKit.Muted);

            UiKit.Panel("Table shadow", content, new Vector2(1050, 468), new Vector2(0, -16), new Color(0, 0, 0, .24f));
            UiKit.Panel("Table outer rail", content, new Vector2(1040, 458), Vector2.zero, UiKit.Hex("102C2B"));
            UiKit.Panel("Brass inlay", content, new Vector2(1018, 436), Vector2.zero, UiKit.Hex("738474"));
            UiKit.Panel("Felt", content, new Vector2(1014, 432), Vector2.zero, UiKit.Hex("23594F"));
            UiKit.Panel("Inner stitch", content, new Vector2(977, 395), Vector2.zero, UiKit.Hex("397366"));
            var table = UiKit.Panel("Playing surface", content, new Vector2(974, 392), Vector2.zero, UiKit.Hex("245A50"));
            dropSurface = table;
            table.raycastTarget = true;
            table.gameObject.AddComponent<Button>().onClick.AddListener(() => PlayRequested?.Invoke());
            boardHint = UiKit.Label("Empty board", content, "D  /  C\n\nTu próxima partida empieza aquí", new Vector2(450, 120), Vector2.zero, 20, UiKit.Hex("76A095"));

            string[] names = { "Fredy", "Alex", "Maria", "John" };
            string[] initials = { "F", "A", "M", "J" };
            string[] colors = { "52786D", "866952", "8B6873", "526B87" };
            for (int p = 0; p < 4; p++)
            {
                players[p] = Instantiate(playerPrefab, content);
                players[p].name = "Player " + (p + 1) + " - " + names[p];
                ((RectTransform)players[p].transform).anchoredPosition = PlayerPositions[p];
                players[p].Initialize(names[p], initials[p], UiKit.Hex(colors[p]));
            }
            tiles = UiKit.Rect("Tiles", content, Vector2.zero, Vector2.zero);
            var banner = UiKit.Rect("Turn banner", content, new Vector2(400, 32), new Vector2(0, -246));
            turnBanner = banner.gameObject.AddComponent<CanvasGroup>();
            UiKit.Label("Your turn", banner, "T U  T U R N O", new Vector2(400, 30), Vector2.zero, 18, UiKit.Gold);
            prompt = UiKit.Label("Prompt", content, "Repartiendo…", new Vector2(760, 28), new Vector2(0, -409), 17, UiKit.Muted);
            playButton = UiKit.Button("Play", content, "JUGAR  →", new Vector2(165, 58), new Vector2(591, -339), UiKit.Hex("63826A"), () =>
            {
                if (matchEnded) RestartRequested?.Invoke();
                else if (roundEnded) NextRoundRequested?.Invoke();
                else PlayRequested?.Invoke();
            });
            UiKit.Button("Restart", content, "Reiniciar", new Vector2(148, 46), new Vector2(682, -407), UiKit.Hex("24443E"), () => RestartRequested?.Invoke());
            UiKit.Label("Client note", content, "DOBLE NUEVE · 10 FICHAS · SIN ROBO", new Vector2(395, 26), new Vector2(-550, -410), 12, UiKit.Muted, TextAnchor.MiddleLeft);
            var menuButton = UiKit.Button("Menu", content, "", new Vector2(48, 48), new Vector2(736, 381), UiKit.Hex("294841"), ToggleMenu);
            for (int i = -1; i <= 1; i++) UiKit.Panel("Menu line", menuButton.transform, new Vector2(20, 2), new Vector2(0, i * 6), UiKit.Cream);
            menu = UiKit.Panel("Client menu", content, new Vector2(330, 275), new Vector2(595, 190), UiKit.Hex("102D2A")).gameObject;
            menu.GetComponent<Image>().raycastTarget = true;
            UiKit.Label("Styles heading", menu.transform, "ESTILO DE FICHAS", new Vector2(310,30), new Vector2(0,108), 18, UiKit.Gold);
            var ivoryButton = UiKit.Button("TEAMFHO ivory",menu.transform,"",new Vector2(294,48),new Vector2(0,54),UiKit.Hex("365448"),()=>{});
            var blueButton = UiKit.Button("Cuba blue",menu.transform,"",new Vector2(294,48),new Vector2(0,-4),UiKit.Hex("294B58"),()=>{});
            var whiteButton = UiKit.Button("TeamFHO white",menu.transform,"",new Vector2(294,48),new Vector2(0,-62),UiKit.Hex("3D3F6B"),()=>{});
            void UpdateStyleLabels()
            {
                ivoryButton.GetComponentInChildren<Text>().text = "TEAMFHO · Marfil" + (TileStyles.Current == TileStyle.TeamFhoIvory ? "  •" : "");
                blueButton.GetComponentInChildren<Text>().text = "Cuba · Azul" + (TileStyles.Current == TileStyle.CubaBlue ? "  •" : "");
                whiteButton.GetComponentInChildren<Text>().text = "TeamFHO · Blanco" + (TileStyles.Current == TileStyle.TeamFhoWhite ? "  •" : "");
            }
            ivoryButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.TeamFhoIvory); UpdateStyleLabels(); });
            blueButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.CubaBlue); UpdateStyleLabels(); });
            whiteButton.onClick.AddListener(()=> { TileStyles.Set(TileStyle.TeamFhoWhite); UpdateStyleLabels(); });
            UpdateStyleLabels();
            UiKit.Label("Saved style",menu.transform,"Se aplica al instante y se guarda",new Vector2(310,25),new Vector2(0,-112),14,UiKit.Muted);
            menu.SetActive(false);
            effects = gameObject.AddComponent<TableEffects>();
            effects.Initialize(content);
            SetInteraction(false, false);
            Fit();
        }
        void ToggleMenu() => menu.SetActive(!menu.activeSelf);
        void Fit()
        {
            if (!content) return;
            float scale = Mathf.Min(safe.rect.width / 1600, safe.rect.height / 900);
            content.localScale = Vector3.one * scale;
        }
        void Update()
        {
            Fit();
            if (turnBanner) turnBanner.alpha = Mathf.MoveTowards(turnBanner.alpha, bannerTarget, Time.unscaledDeltaTime * 4);
            foreach (var view in hands[0])
            {
                if (!view.Selectable || view.IsDragging) continue;
                var target = view.Home + (view.Selected ? Vector2.up * 20 : Vector2.zero);
                view.Rect.anchoredPosition = Vector2.Lerp(view.Rect.anchoredPosition, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
                view.Rect.localScale = Vector3.Lerp(view.Rect.localScale, Vector3.one * (view.Selected ? 1.13f : 1.05f), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
        }
        public void Clear()
        {
            effects.Clear();
            if (washPreview) { washPreview.gameObject.SetActive(false); Destroy(washPreview.gameObject); washPreview = null; }
            tiles.gameObject.SetActive(true);
            foreach (var tile in washReserve) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
            washReserve.Clear();
            roundEnded = matchEnded = false;
            playButton.GetComponentInChildren<Text>().text = "JUGAR  →";
            CancelDrag();
            foreach (var hand in hands) { foreach (var tile in hand) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); } hand.Clear(); }
            foreach (var tile in played) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); }
            played.Clear(); boardHint.gameObject.SetActive(true); menu.SetActive(false);
            foreach (var player in players) { player.SetCount(0); player.SetTurn(false); }
            bannerTarget = 0; round.text = "RONDA 01";
            SetInteraction(false, false);
        }
        Vector2 HandPosition(int player, int index, int count)
        {
            float offset = index - (count - 1) * .5f;
            return player switch
            {
                0 => new Vector2(offset * 88, -339),
                1 => new Vector2(-665 + (index % 2 == 0 ? -39 : 39), -54 - index / 2 * 36),
                2 => new Vector2(190 + index % 5 * 73, 287 - index / 5 * 36),
                _ => new Vector2(665 + (index % 2 == 0 ? -39 : 39), -54 - index / 2 * 36)
            };
        }
        public IEnumerator Deal(IReadOnlyList<DominoTile>[] modelHands)
        {
            prompt.text = "Repartiendo 10 fichas por jugador…";
            for (int n = 0; n < modelHands[0].Count; n++)
            for (int p = 0; p < 4; p++)
            {
                var view = Instantiate(tilePrefab, tiles);
                view.name = p == 0 ? $"Local {modelHands[p][n]}" : $"Player {p + 1} hidden tile";
                view.Initialize(modelHands[p][n], false, p == 0);
                view.Clicked = t => TileSelected?.Invoke(t);
                view.DragRequested = BeginDrag;
                view.DragMoved = HoverDrag;
                view.DragReleased = Drop;
                view.Rect.anchoredPosition = Vector2.zero;
                view.Rect.localScale = Vector3.one * .6f;
                hands[p].Add(view);
                view.Home = HandPosition(p, n, modelHands[p].Count);
                yield return Move(view, view.Home, p == 0 ? 90 : 0, p == 0 ? 1.05f : .68f, .095f);
                if (p == 0) view.Reveal();
                players[p].SetCount(n + 1);
            }
        }
        public void SetInteraction(bool canChoose, bool canPlay)
        {
            foreach (var tile in hands[0]) tile.Selectable = canChoose;
            playButton.interactable = canPlay;
        }
        bool BeginDrag(DominoTileView tile)
        {
            if (dragging || !tile.Selectable || !hands[0].Contains(tile)) return false;
            dragging = tile;
            prompt.text = "Arrastra a la mesa y suelta para jugar";
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
            var preferred = played.Count == 1 ? (point.x < first.x ? ChainEnd.Left : ChainEnd.Right)
                : (Vector2.Distance(point, first) < Vector2.Distance(point, last) ? ChainEnd.Left : ChainEnd.Right);
            if (CanPlace != null && !CanPlace(tile.Tile, preferred)) return ChainEnd.Auto;
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
            bool accepted = tile.Selectable && OverTable(pointer);
            dragging = null;
            dropSurface.color = UiKit.Hex("245A50");
            if (accepted) TileDropped?.Invoke(tile, DropEnd(tile, pointer));
            else RestoreDragPrompt(); // Update smoothly returns the tile to its hand position.
        }
        void RestoreDragPrompt()
        {
            playButton.interactable = hands[0].Exists(t => t.Selected && t.Selectable);
            prompt.text = "Arrastra una ficha a la mesa o selecciónala para jugar";
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
            for (int p = 0; p < 4; p++) players[p].SetTurn(p == player);
            bannerTarget = player == 0 ? 1 : 0;
            prompt.text = player == 0 ? "Arrastra una ficha a la mesa o selecciónala para jugar" : new[] { "", "Alex está pensando…", "Maria está pensando…", "John está pensando…" }[player];
        }
        public void SetSelected(DominoTileView selection)
        {
            foreach (var tile in hands[0]) tile.Select(tile == selection);
            playButton.interactable = selection != null;
            prompt.text = selection ? "Toca la mesa o JUGAR para colocar tu ficha" : "Selecciona una ficha y toca la mesa para jugar";
        }
        public void ShowMessage(string message) => prompt.text = message;
        public IEnumerator ShowPass(int player) => effects.Knock(player);
        public IEnumerator ShowWinner(string winner, bool match) => effects.Celebrate(winner, match);
        public IEnumerator PreviewWash(bool isPreview = true)
        {
            washPreview = UiKit.Rect("Wash preview tiles", content, Vector2.zero, Vector2.zero);
            // Keep the hands and chain intact; only preview copies are shuffled.
            washPreview.SetSiblingIndex(tiles.GetSiblingIndex() + 1);
            var clientTiles = new List<DominoTileView>();
            var set = DominoTile.CreateSet();
            for (int i = 0; i < set.Count; i++)
            {
                var tile = Instantiate(tilePrefab, washPreview);
                tile.Initialize(set[i], isPreview, false);
                tile.Rect.anchoredPosition = new Vector2((i % 11 - 5) * 52, (i / 11 - 2) * 38);
                tile.Rect.localScale = Vector3.one * .5f;
                clientTiles.Add(tile);
            }
            tiles.gameObject.SetActive(false);
            ShowMessage(isPreview ? "Vista previa: dándole agua al dominó…" : "Dándole agua al dominó antes de repartir…");
            yield return effects.Wash(clientTiles);
            yield return new WaitForSeconds(.7f);
            washPreview.gameObject.SetActive(false); Destroy(washPreview.gameObject); washPreview = null;
            tiles.gameObject.SetActive(true);
        }
        public IEnumerator WashDominoes(IReadOnlyList<DominoTile> reserve)
        {
            SetInteraction(false, false);
            var all = new List<DominoTileView>(55);
            all.AddRange(played);
            foreach (var hand in hands) all.AddRange(hand);
            foreach (var data in reserve)
            {
                var tile = Instantiate(tilePrefab, tiles);
                tile.name = "Reserve - washing dominoes";
                tile.Initialize(data, false, false);
                tile.Rect.anchoredPosition = new Vector2(0,170);
                tile.Rect.localScale = Vector3.one*.5f;
                washReserve.Add(tile); all.Add(tile);
            }
            foreach (var tile in all) { tile.Selectable = false; tile.Select(false); }
            ShowMessage("Dándole agua al dominó…");
            yield return effects.Wash(all);
        }
        public IEnumerator Play(GameEvent e)
        {
            boardHint.gameObject.SetActive(false);
            var tile = hands[e.Player].Find(t => t.Tile.Equals(e.Tile));
            if (!tile) throw new InvalidOperationException("Played tile is missing from presentation.");
            hands[e.Player].Remove(tile); played.Insert(e.ChainIndex, tile); tile.Orient(e.Tile); tile.Select(false); tile.Selectable = false;
            tile.transform.SetAsLastSibling();
            yield return MoveChain(.42f, tile);
            tile.Reveal();
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
            var layout = BoardLayout.Arrange(played.Count, i => played[i].Tile.SideA == played[i].Tile.SideB);
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < played.Count; i++)
                {
                    var pose = layout[i];
                    var r = played[i].Rect;
                    r.anchoredPosition = Vector2.Lerp(starts[i], pose.Position, t);
                    if (played[i] == incoming) r.anchoredPosition += Vector2.up * (Mathf.Sin(t * Mathf.PI) * 15);
                    r.localRotation = Quaternion.Slerp(rotations[i], Quaternion.Euler(0, 0, pose.Angle), t);
                    r.localScale = Vector3.Lerp(scales[i], Vector3.one * pose.Scale, t);
                }
                yield return null;
            }
        }
        static IEnumerator Move(DominoTileView tile, Vector2 end, float angle, float scale, float duration)
        {
            Vector2 start = tile.Rect.anchoredPosition;
            var startRotation = tile.Rect.localRotation; var startScale = tile.Rect.localScale;
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
                tile.Rect.anchoredPosition = Vector2.Lerp(start, end, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 15);
                tile.Rect.localRotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0, 0, angle), t);
                tile.Rect.localScale = Vector3.Lerp(startScale, Vector3.one * scale, t);
                yield return null;
            }
            tile.Rect.anchoredPosition = end;
        }
        public void UpdateScore(Domino.Game.GameRules rules, Domino.Game.MatchState match)
        {
            scoreA.fontSize = scoreB.fontSize = rules.Teams ? 18 : 15;
            scoreA.text = rules.Teams ? $"EQUIPO A  {match.Score(0)}" : $"Fredy {match.Score(0)}\nMaria {match.Score(2)}";
            scoreB.text = rules.Teams ? $"{match.Score(1)}  EQUIPO B" : $"Alex {match.Score(1)}\nJohn {match.Score(3)}";
            round.text = $"R{match.RoundNumber} · META {rules.TargetScore} · ×{match.Multiplier}";
            round.rectTransform.sizeDelta = new Vector2(290, 25);
        }
        public IEnumerator RevealHands(int[] points)
        {
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
            for (int p = 0; p < 4; p++) players[p].ShowPoints(hands[p].Count, points[p]);
        }
        public void Finish(string message, bool matchFinished = false)
        {
            foreach (var player in players) player.SetTurn(false);
            bannerTarget = 0;
            prompt.text = message;
            SetInteraction(false, false);
            roundEnded = true; matchEnded = matchFinished;
            playButton.GetComponentInChildren<Text>().text = matchFinished ? "NUEVA PARTIDA" : "SIGUIENTE →";
            playButton.GetComponentInChildren<Text>().fontSize = 16;
            playButton.interactable = true;
        }
    }
}
