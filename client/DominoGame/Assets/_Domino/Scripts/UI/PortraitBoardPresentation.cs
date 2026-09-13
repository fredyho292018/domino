using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    public sealed partial class BoardView
    {
        public SeatPerspectiveMapper Perspective { get; private set; }
        int? activeHumanSeat;
        public int LocalPlayerSeat => activeHumanSeat ?? Perspective.BottomPlayer;
        public bool IsPortrait => safe && safe.rect.height > safe.rect.width;
        public RectTransform BoardSurface => dropSurface.rectTransform;
        public RectTransform PlayerRect(int logicalSeat) => (RectTransform)players[logicalSeat].transform;
        Vector2 previousLayout;
        bool previousPortrait;
        RectTransform localHandLayer, opponentHandLayer, turnNotice;
        Text turnNoticeTitle;
        int noticePlayer = -1, displayedRound = 1;
        public RectTransform TurnNotice => turnNotice;
        float OpponentScale => IsPortrait ? .52f : .42f;
        float HandAngle(int seat) => seat == LocalPlayerSeat || (IsPortrait && (Perspective.PositionFor(seat) == VisualSeat.Left || Perspective.PositionFor(seat) == VisualSeat.Right)) ? 90 : 0;
        public string LocalHandLayout => IsPortrait ? "SINGLE_ROW_CONTROLLED_OVERLAP" : "SINGLE_ROW";
        public Vector2 ChainCenter => IsPortrait ? BoardSurface.anchoredPosition + Vector2.down * 25 : new Vector2(0, 25);
        public Vector2 ChainSize => IsPortrait ? new Vector2(BoardSurface.rect.width * .64f, BoardSurface.rect.height - (SharedDevice ? 640 : 330)) : BoardSurface.rect.size - new Vector2(160, 180);

        internal static void ConfigureCanvas(GameObject owner)
        {
            var scaler = owner.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
        }
        TilePose[] ChainLayout() => BoardLayout.Arrange(played.Count, i => played[i].Tile.SideA == played[i].Tile.SideB, ChainSize);

        void Fit()
        {
            if (!content || safe.rect.width <= 0 || safe.rect.height <= 0) return;
            bool portrait = IsPortrait;
            float scale = portrait ? Mathf.Min(safe.rect.width / 1080, safe.rect.height / 1440)
                : Mathf.Min(safe.rect.width / 1600, safe.rect.height / 900);
            var size = portrait ? new Vector2(1080, safe.rect.height / scale)
                : new Vector2(Mathf.Min(2100, safe.rect.width / scale), 900);
            content.sizeDelta = size;
            content.localScale = Vector3.one * scale;
            bool resized = (previousLayout - size).sqrMagnitude > .01f || previousPortrait != portrait;
            if (resized)
            {
                previousLayout = size; previousPortrait = portrait;
                float halfHeight = size.y / 2;
                for (int i = 0; i < tableLayers.Count; i++)
                {
                    tableLayers[i].sizeDelta = (portrait ? new Vector2(1000, size.y - 620) : new Vector2(size.x - 260, 710)) - Vector2.one * (portrait ? new[] { 0f,0f,16f,20f,44f,48f }[i] : TableInsets[i]);
                    tableLayers[i].GetComponent<Image>().pixelsPerUnitMultiplier = portrait ? .65f : 1f;
                    tableLayers[i].anchoredPosition = new Vector2(0, (portrait ? 120 : 25) - (i == 0 ? 8 : 0));
                }
                for (int p = 0; p < players.Length; p++)
                {
                    var slot = Perspective.PositionFor(p);
                    PlayerRect(p).anchoredPosition = portrait ? slot switch
                    {
                        VisualSeat.Bottom => new Vector2(0, -halfHeight + 385),
                        VisualSeat.Top => new Vector2(0, BoardSurface.anchoredPosition.y + BoardSurface.rect.height / 2 + 15),
                        VisualSeat.Left => new Vector2(-426, BoardSurface.anchoredPosition.y),
                        _ => new Vector2(426, BoardSurface.anchoredPosition.y)
                    } : slot switch
                    {
                        VisualSeat.Bottom => new Vector2(-size.x / 2 + 126, -397),
                        VisualSeat.Left => new Vector2(-size.x / 2 + 65, 80),
                        VisualSeat.Top => new Vector2(0, 402),
                        _ => new Vector2(size.x / 2 - 65, 80)
                    };
                    players[p].SetPresentation(slot, portrait,
                        SharedDevice ? "player.human" : slot == VisualSeat.Bottom ? "player.you" : slot == VisualSeat.Top && configuration.TeamMode == Domino.Configuration.TeamMode.FixedTeams ? "player.partner" : "player.opponent");
                }
                Place("Brand", portrait ? new Vector2(-280, halfHeight - 45) : new Vector2(-size.x / 2 + 190, 415));
                Place("Score surface", portrait ? new Vector2(175, halfHeight - 52) : new Vector2(size.x / 2 - 275, 414));
                Place("Menu", portrait ? new Vector2(470, halfHeight - 48) : new Vector2(size.x / 2 - 64, 414));
                Place("Client menu", portrait ? new Vector2(300, halfHeight - 285) : new Vector2(size.x / 2 - 205, 190));
                Place("Play", portrait ? new Vector2(370, -halfHeight + 385) : new Vector2(size.x / 2 - 116, -377));
                Place("Restart", portrait ? new Vector2(-370, -halfHeight + 385) : new Vector2(size.x / 2 - 116, -423));
                SetSize("Menu", portrait ? new Vector2(72,72) : new Vector2(48,48));
                SetSize("Play", portrait ? new Vector2(240,70) : new Vector2(158,48));
                SetSize("Restart", portrait ? new Vector2(200,70) : new Vector2(158,32));
                prompt.rectTransform.sizeDelta = new Vector2(portrait ? 880 : 950, portrait ? 36 : 28);
                turnNotice.gameObject.SetActive(portrait);
                turnNotice.sizeDelta = new Vector2(600,94);
                turnNotice.anchoredPosition = BoardSurface.anchoredPosition + Vector2.down * (BoardSurface.rect.height / 2 - 65);
                prompt.rectTransform.anchoredPosition = portrait ? turnNotice.anchoredPosition + Vector2.down * 19 : new Vector2(0,-294);
                turnNoticeTitle.fontSize = 24;
                prompt.rectTransform.sizeDelta = new Vector2(portrait ? 560 : 950, portrait ? 38 : 28);
                ((RectTransform)turnBanner.transform).gameObject.SetActive(!portrait);
                boardHint.rectTransform.anchoredPosition = BoardSurface.anchoredPosition;
                ((RectTransform)turnBanner.transform).anchoredPosition = new Vector2(12, portrait ? -58 : -35);
                if (Settings) Settings.GetComponent<RectTransform>().sizeDelta = size + new Vector2(300, 300);
                ArrangeLayers();
                if (!IsPreparingRound) RefreshHandLayout();
                if (!IsPreparingRound && !IsDragging) ApplyChainLayout();
            }
            reserveLabel.rectTransform.anchoredPosition = ReservePosition(2) + Vector2.up * 28;
            if (reserveParked)
                for (int i = 0; i < washReserve.Count; i++) washReserve[i].Rect.anchoredPosition = ReservePosition(i);
        }
        void CreatePresentationLayers()
        {
            opponentHandLayer = UiKit.Rect("Opponent backs",content,Vector2.zero,Vector2.zero);
            localHandLayer = UiKit.Rect("Local hand",content,Vector2.zero,Vector2.zero);
            turnNotice = UiKit.Panel("Turn notice",content,new Vector2(600,94),Vector2.zero,new Color(.035f,.105f,.115f,.86f)).rectTransform;
            UiKit.Panel("Turn accent",turnNotice,new Vector2(28,2),new Vector2(0,40),new Color(UiKit.Gold.r,UiKit.Gold.g,UiKit.Gold.b,.45f));
            turnNoticeTitle = UiKit.Label("Turn title",turnNotice,"",new Vector2(550,32),new Vector2(0,15),24,UiKit.Cream);
            UpdateTurnNotice(-1);
        }
        void UpdateTurnNotice(int logicalSeat)
        {
            noticePlayer = logicalSeat;
            if (!turnNoticeTitle) return;
            if (noticePlayer < 0) DominoLocalization.Set(turnNoticeTitle,"game.round",displayedRound);
            else DominoLocalization.Set(turnNoticeTitle, noticePlayer == LocalPlayerSeat ? "game.your_turn_named" : "game.turn_named",players[noticePlayer].DisplayName);
        }
        void ArrangeLayers()
        {
            // Every tile layer has the same origin: parenting changes depth, never seat coordinates.
            tiles.SetAsLastSibling();
            foreach (var player in players) player.transform.SetAsLastSibling();
            opponentHandLayer.SetAsLastSibling();
            foreach (string name in new[] { "Brand","Score surface","Menu","Turn notice","Prompt","Play","Restart" }) content.Find(name).SetAsLastSibling();
            localHandLayer.SetAsLastSibling();
            if (content.Find("Table effects")) content.Find("Table effects").SetAsLastSibling();
            if (content.Find("Feedback particles")) content.Find("Feedback particles").SetAsLastSibling();
            if (RoundRewardPanel) RoundRewardPanel.transform.SetAsLastSibling();
            menu.transform.SetAsLastSibling();
            Settings.transform.SetAsLastSibling();
        }
        void Place(string name, Vector2 position)
        {
            var rect = (RectTransform)content.Find(name);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position;
        }
        void SetSize(string name, Vector2 size) => ((RectTransform)content.Find(name)).sizeDelta = size;
        Vector2 PortraitHandPosition(int player, int index, int count)
        {
            var slot = Perspective.PositionFor(player);
            if(SharedDevice) {
                var sharedOrigin=PlayerRect(player).anchoredPosition;
                if(player!=LocalPlayerSeat) return sharedOrigin+new Vector2((index-(count-1)*.5f)*4.5f,-110);
                float spacing=Mathf.Min(94,(content.rect.width-234)/Mathf.Max(1,count-1));
                return new Vector2((index-(count-1)*.5f)*spacing,slot==VisualSeat.Bottom?-content.rect.height/2+165:sharedOrigin.y-205);
            }
            if (slot == VisualSeat.Bottom)
            {
                // Keep both pip fields visible: the overlap is confined to the ivory margins.
                float spacing = Mathf.Min(94, (content.rect.width - 234) / Mathf.Max(1,count-1));
                return new Vector2((index - (count-1)*.5f)*spacing,-content.rect.height/2+165);
            }
            var origin = PlayerRect(player).anchoredPosition;
            return slot == VisualSeat.Top ? origin + new Vector2((index-(count-1)*.5f)*4.5f,-110)
                : origin + new Vector2(0,-111+index*4);
        }
        public void HideHands()
        {
            SetInteraction(false,false);
            foreach(var hand in hands) foreach(var tile in hand) {tile.Selectable=false;tile.Select(false);tile.Conceal();}
        }
        public void RevealActiveSeat(int seat)
        {
            HideHands();
            activeHumanSeat=seat;
            previousLayout=Vector2.zero;Fit();
            for(int p=0;p<hands.Length;p++) foreach(var tile in hands[p]) {
                tile.transform.SetParent(p==seat?localHandLayer:opponentHandLayer,false);
                tile.SetCompactBack(p!=seat);
                if(p==seat)tile.Reveal();
            }
            RefreshHandLayout();
        }
        void RefreshHandLayout()
        {
            for (int p = 0; p < hands.Length; p++)
                for (int i = 0; i < hands[p].Count; i++)
                {
                    var view = hands[p][i];
                    view.Home = HandPosition(p, i, hands[p].Count);
                    if (!view.IsDragging)
                    {
                        view.Rect.anchoredPosition = view.Home + (view.Selected ? Vector2.up * 26 : Vector2.zero);
                        view.Rect.localScale = Vector3.one * (p == LocalPlayerSeat ? LocalScale : OpponentScale);
                        view.Rect.localRotation = Quaternion.Euler(0,0,HandAngle(p));
                    }
                }
        }
        void ApplyChainLayout()
        {
            ResetEndpointZoom();
            var layout = ChainLayout();
            for (int i = 0; i < played.Count; i++)
            {
                played[i].Rect.anchoredPosition = layout[i].Position + ChainCenter;
                played[i].Rect.localRotation = Quaternion.Euler(0,0,layout[i].Angle);
                played[i].Rect.localScale = Vector3.one * layout[i].Scale;
            }
        }
        Vector2 KnockPosition(int player)
        {
            var slot = Perspective.PositionFor(player);
            var center = BoardSurface.anchoredPosition;
            var half = BoardSurface.rect.size * .5f - new Vector2(95, 100);
            return center + (slot switch
            {
                VisualSeat.Bottom => Vector2.down * half.y,
                VisualSeat.Top => Vector2.up * half.y,
                VisualSeat.Left => Vector2.left * half.x,
                _ => Vector2.right * half.x
            });
        }
    }
}
