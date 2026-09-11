using System;
using System.Collections;
using System.IO;
using System.Linq;
using Domino.Core;
using Domino.Game;
using Domino.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Domino.Client
{
    // Opt-in validation only. Invoked by Phase1Validation in an isolated project.
    public sealed class PortraitSmokeTest : MonoBehaviour
    {
        public static Action<int,int> ResizeGameView;
        DominoClientController controller;
        int checks;
        void Check(bool value, string detail)
        { checks++; if (!value) throw new InvalidOperationException("PORTRAIT_FAILURE: " + detail); }
        IEnumerator Start()
        {
            controller = GetComponent<DominoClientController>();
            while (!controller.Menu) yield return null;
            int[,] sizes = { {1080,1920}, {1080,2160}, {1080,2340}, {1080,2400}, {1080,2520}, {1170,2532}, {1284,2778}, {1600,2560}, {1536,2048} };
            for (int test = 0; test < sizes.GetLength(0); test++)
            {
                int local = test % 4;
                ResizeGameView(sizes[test,0],sizes[test,1]);
                float deadline = Time.realtimeSinceStartup + 15;
                while (Screen.width != sizes[test,0] && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null; yield return null;
                Check(Screen.width == sizes[test,0] && Screen.height == sizes[test,1], "Actual portrait resolution");
                controller.Menu.Show(StartScreen.ModeSelector);
                DominoLocalization.Select(test % 2 == 0 ? "es" : "en");
                yield return new WaitForSecondsRealtime(.1f);
                CheckInside(controller.Menu.Card, "Mode card");
                if (test == 0) { Capture("menu"); yield return new WaitForSecondsRealtime(.2f); }
                controller.StartMatch(GameModeDefinition.TeamMatch,local);
                var board = controller.View;
                var engine = controller.State;
                int[] received = new int[4];
                board.TileDealt += p => received[p]++;
                bool ready = false;
                board.DealPhaseChanged += phase =>
                {
                    if (phase != DealPresentationPhase.Ready) return;
                    ready = true;
                    Check(board.VisuallyDealt == 40 && received.All(n => n == 10), "40 deals with logical identities");
                    for (int p = 0; p < 4; p++)
                    {
                        Check(board.HandViews(p).Count == 10, "Ten per logical seat");
                        for (int i = 0; i < 10; i++)
                        {
                            var tile = board.HandViews(p)[i];
                            Check(tile.Tile.Equals(engine.Hand(p)[i]), "Hand order unchanged");
                            Check(tile.IsFaceUp == (p == local), "Only local face revealed");
                            CheckInside(tile.Rect,"Hand seat " + p);
                        }
                    }
                };
                deadline = Time.realtimeSinceStartup + 40;
                while (!controller.AcceptingInput && Time.realtimeSinceStartup < deadline)
                {
                    if (board.IsPreparingRound)
                    {
                        Check(!controller.AcceptingInput && engine.Chain.Count == 0, "Preparation blocks gameplay/bots");
                        Check(board.LocalTiles.All(t => !t.Selectable), "Preparation blocks selection");
                    }
                    yield return null;
                }
                Check(ready && controller.AcceptingInput && engine.CurrentPlayer == local, "Local turn follows complete deal");
                Check(board.IsPortrait && board.LocalPlayerSeat == local, "Local perspective accepted");
                Check(board.PlayerRect(local).anchoredPosition.y < 0, "Local bottom");
                Check(board.PlayerRect(board.Perspective.TopPlayer).anchoredPosition.y > 0, "Partner top");
                Check(board.PlayerRect(board.Perspective.LeftPlayer).anchoredPosition.x < 0 && board.PlayerRect(board.Perspective.RightPlayer).anchoredPosition.x > 0, "Opponents left/right");
                Check(engine.Configuration.TurnOrder.SequenceEqual(new[] {0,3,2,1}), "Original turn order");
                Check(board.ReserveViews.Count == 15 && board.ReserveViews.All(t => !t.IsFaceUp), "Reserved fifteen concealed");
                CheckPanels(board);
                var first = board.LocalTiles.First(t => engine.CanPlay(t.Tile));
                controller.Select(first);
                yield return new WaitForSecondsRealtime(.2f);
                Check(first.Selected && first.Rect.anchoredPosition.y > first.Home.y + 20 && first.Rect.localScale.x > 2, "Selection lift/zoom");
                Check(first.transform.GetSiblingIndex() == first.transform.parent.childCount - 1, "Selection in front");
                CheckInside(first.Rect,"Selected tile");
                Capture("seat-" + local + "-" + sizes[test,0] + "x" + sizes[test,1]);
                yield return new WaitForSecondsRealtime(.2f);
                int eventSeat = -1;
                engine.Changed += e => { if(e.Type == GameEventType.TILE_PLAYED) eventSeat = e.Player; };
                var pointer = new PointerEventData(EventSystem.current) { pointerId=-1, button=PointerEventData.InputButton.Left,
                    position=RectTransformUtility.WorldToScreenPoint(null,first.Rect.position) };
                int before = engine.Chain.Count;
                first.OnBeginDrag(pointer); first.OnEndDrag(pointer);
                Check(engine.Chain.Count == before, "Outside drop rejected");
                first.OnBeginDrag(pointer);
                var end = engine.CanPlay(first.Tile,ChainEnd.Left) ? ChainEnd.Left : ChainEnd.Right;
                var target = board.ChainCenter;
                if (before > 0)
                {
                    var active = board.GetComponentsInChildren<DominoTileView>().Where(v => !v.Selectable && v.IsFaceUp).ToArray();
                    // Match the actual endpoint through its ordered domain tile.
                    var endpoint = end == ChainEnd.Left ? engine.Chain[0] : engine.Chain[engine.Chain.Count-1];
                    var view = active.First(v => v.Tile.Equals(endpoint));
                    target = view.Rect.anchoredPosition;
                    if (before == 1) target += (Vector2)(view.Rect.localRotation * Vector3.right) * (end == ChainEnd.Left ? -30 : 30);
                }
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, first.Rect.parent.TransformPoint(target));
                first.OnDrag(pointer); first.OnEndDrag(pointer);
                Check(engine.Chain.Count == before+1 && eventSeat == local, "Drop retains logical seat in event");
                Check(engine.Hand(local).Count == 9, "Only local logical hand removed");
                yield return new WaitForSeconds(.85f);
                Check(board.PlayedCount >= before+1, "Played tile visible");
                CheckLayouts(board);
                controller.ExitMatch();
                yield return null;
                Debug.Log("PORTRAIT_LAYOUT=PASS " + sizes[test,0] + "x" + sizes[test,1] + " LOCAL_SEAT=" + local);
            }
            ResizeGameView(1080,2340);
            yield return null; yield return null;
            controller.StartMatch(GameModeDefinition.TeamMatch);
            float limit = Time.realtimeSinceStartup + 100;
            bool captured = false;
            while (!controller.View.RoundPresentationFinished && Time.realtimeSinceStartup < limit)
            {
                if (controller.AcceptingInput)
                {
                    if (!captured && controller.View.PlayedCount >= 20)
                    {
                        Capture("live-chain"); captured = true;
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                    var tile = controller.View.LocalTiles.First(t => controller.State.CanPlay(t.Tile));
                    controller.Select(tile); controller.PlaySelected();
                }
                yield return null;
            }
            Check(controller.View.RoundPresentationFinished, "Complete portrait round including result/wash");
            Check(controller.State.Result != null, "Domain result retained");
            Capture("round-result");
            yield return new WaitForSecondsRealtime(.2f);
            Debug.Log("DOMINO_PORTRAIT_SUCCESS: " + checks + " checks; nine portrait sizes, all four local seats, dealing, drag, logical events, selection, board bounds and complete round.");
        }
        void CheckPanels(BoardView board)
        {
            var canvas = board.GetComponent<Canvas>();
            Check(canvas.GetComponent<CanvasScaler>().uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,"Responsive scaler");
            var surface = board.BoardSurface;
            foreach (var slot in new[] { VisualSeat.Left,VisualSeat.Right })
            {
                var player = board.PlayerRect(board.Perspective.PlayerAt(slot));
                Check(Mathf.Abs(player.anchoredPosition.y-surface.anchoredPosition.y)<.1f,"Floating rival centered on table");
                CheckInside(player.GetComponent<PlayerView>().PanelRect,"Floating player panel");
            }
            Check(board.ChainSize.x/surface.rect.width >= .60f,"Protected chain width at least sixty percent");
            var title = board.TurnNotice.Find("Turn title").GetComponent<Text>();
            Check(title.text == DominoLocalization.Get("game.your_turn_named",board.PlayerRect(board.LocalPlayerSeat).GetComponent<PlayerView>().DisplayName)
                || title.text == DominoLocalization.Get("game.turn_named",board.PlayerRect(controller.State.CurrentPlayer).GetComponent<PlayerView>().DisplayName),"Named localized turn panel");
        }
        Rect RelativeBounds(RectTransform target,Transform parent)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var min = Vector2.one*float.MaxValue; var max = Vector2.one*float.MinValue;
            foreach(var c in corners) {var p=(Vector2)parent.InverseTransformPoint(c);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        void CheckLayouts(BoardView board)
        {
            for (int count = 1; count <= 40; count++)
            {
                var layout = BoardLayout.Arrange(count, i => i%6==0,board.ChainSize);
                foreach(var pose in layout)
                {
                    var half = BoardLayout.HalfSize(pose);
                    var tileBounds = new Rect(pose.Position+board.ChainCenter-half-Vector2.one*20,half*2+Vector2.one*40);
                    foreach(var slot in new[] {VisualSeat.Left,VisualSeat.Top,VisualSeat.Right})
                    {
                        var player = board.PlayerRect(board.Perspective.PlayerAt(slot));
                        Check(!tileBounds.Overlaps(RelativeBounds(player.GetComponent<PlayerView>().PanelRect,surfaceParent(board))),"Floating panel cannot obscure chain or endpoint zoom");
                    }
                    Check(!tileBounds.Overlaps(RelativeBounds(board.TurnNotice,surfaceParent(board))),"Turn notice outside chain");
                    Check(Mathf.Abs(pose.Position.x)+half.x <= board.ChainSize.x/2+.1f && Mathf.Abs(pose.Position.y)+half.y <= board.ChainSize.y/2+.1f,"Chain stays in actual board");
                }
                for (int i=0;i<layout.Length;i++) for (int j=i+1;j<layout.Length;j++)
                {
                    var half = BoardLayout.HalfSize(layout[i])+BoardLayout.HalfSize(layout[j]);
                    var delta = layout[i].Position-layout[j].Position;
                    Check(Mathf.Abs(delta.x)>=half.x-.1f || Mathf.Abs(delta.y)>=half.y-.1f,"No overlapping chain tiles");
                }
            }
        }
        Transform surfaceParent(BoardView board) => board.BoardSurface.parent;
        void CheckInside(RectTransform rect,string label)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach(var corner in corners) Check(Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null,corner)),label+" inside safe area");
        }
        void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../portrait-"+name+".png")));
    }
}
