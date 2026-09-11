using System;
using System.Collections;
using Domino.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Domino.Client
{
    /// <summary>Opt-in smoke validation using the real presentation and input handlers.</summary>
    public sealed class ClientSmokeTest : MonoBehaviour
    {
        DominoClientController controller;
        int errors;
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        IEnumerator Start()
        {
            controller = GetComponent<DominoClientController>();
            yield return null;
            while (!controller.Menu) yield return null;
            controller.Menu.MainPlay.onClick.Invoke();
            controller.Menu.ModePlay.onClick.Invoke();
            controller.RestartClient();
            yield return new WaitForSeconds(.35f);
            controller.RestartClient();
            yield return WaitForTurn();
            var snapshot = controller.State.Configuration;
            Check(snapshot.FinishScoring.Source == Domino.Configuration.PointsSource.OpponentsOnly && snapshot.BlockedScoring.Source == Domino.Configuration.PointsSource.OpponentsOnly, "Bundled 2v2 scores opponents only for both outcomes");
            Check(snapshot.Id == "double-nine-partners" && snapshot.TargetScore == 200, "Bundled JSON loaded");
            Check(controller.State.Reserve.Count == 15 && controller.State.CurrentPlayer == 0, "Reserve and first seat");
            Check(snapshot.GetTeamForPlayer(0) == snapshot.GetTeamForPlayer(2) && snapshot.GetTeamForPlayer(1) == snapshot.GetTeamForPlayer(3), "Original teams");
            Check(controller.View.LocalTiles.Count == 10, "Ten local tiles after restart during deal");
            Canvas.ForceUpdateCanvases();
            foreach (var tile in FindObjectsByType<DominoTileView>(FindObjectsSortMode.None))
            {
                var renderer = tile.GetComponent<CanvasRenderer>();
                Check(renderer != null && renderer.GetMesh() != null && renderer.GetMesh().vertexCount > 0, "Every tile submits geometry");
                Check(tile.IsFaceUp == controller.View.LocalTiles.ContainsReference(tile), "Only local hand revealed");
            }
            var first = controller.View.LocalTiles[0];
            controller.Select(first); Check(first.Selected, "Selection");
            controller.Select(first); Check(!first.Selected, "Deselection");
            var pointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -1, button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, first.Rect.position)
            };
            first.OnBeginDrag(pointer); first.OnEndDrag(pointer);
            Check(controller.State.Chain.Count == 0, "Outside drop rejected");
            first.OnBeginDrag(pointer);
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, first.Rect.parent.position);
            first.OnDrag(pointer); first.OnEndDrag(pointer);
            Check(controller.State.Chain.Count == 1, "Legal drag accepted");
            yield return new WaitForSeconds(.1f);
            controller.RestartClient();
            yield return WaitForTurn();
            float deadline = Time.realtimeSinceStartup + 360 * Mathf.Max(1, 4 / Mathf.Max(1, Time.timeScale));
            var playButton = controller.View.transform.Find("Safe area/Landscape composition/Play").GetComponent<Button>();
            int roundsCompleted = 0;
            bool capturedChain = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (controller.State.Finished)
                {
                    // Wait for the actual presentation, including reveal/celebration/wash.
                    if (!playButton.interactable || !controller.View.RoundPresentationFinished)
                    { yield return null; continue; }
                    Check(controller.View.PlayedCount == controller.State.Chain.Count, "All legal plays visible");
                    var roundResult = controller.State.Result;
                    if (!roundResult.Tie)
                    {
                        int opponentPips = 0;
                        for (int seat = 0; seat < snapshot.PlayerCount; seat++)
                            if (snapshot.GetTeamForPlayer(seat) != roundResult.WinnerSide) opponentPips += roundResult.HandPoints[seat];
                        int bonus = roundResult.Blocked ? snapshot.BlockedScoring.Bonus : snapshot.FinishScoring.Bonus;
                        Check(roundResult.BasePoints == opponentPips && roundResult.Award == (opponentPips + bonus) * roundResult.Multiplier, "Real round excludes winning team and preserves bonus/multiplier");
                        var resultText = controller.View.transform.Find("Safe area/Landscape composition/Prompt").GetComponent<Text>().text;
                        Check(resultText.Contains($"+{roundResult.Award} pts ({opponentPips} + {bonus}) ×{roundResult.Multiplier}"), "Result UI displays domain award without partner points");
                    }
                    roundsCompleted++;
                    Debug.Log($"MATCH_TEST_ROUND={roundsCompleted} SCORE={controller.State.Match.Score(0)}:{controller.State.Match.Score(1)}");
                    if (controller.State.Match.Finished) break;
                    int oldRound = controller.State.Match.RoundNumber;
                    int scoreA = controller.State.Match.Score(0), scoreB = controller.State.Match.Score(1);
                    playButton.onClick.Invoke();
                    Check(controller.State.Match.RoundNumber == oldRound + 1 && controller.State.CurrentPlayer == 0, "Next round starts with Fredy");
                    Check(controller.State.Match.Score(0) == scoreA && controller.State.Match.Score(1) == scoreB, "Next round retains scores");
                    Check(ReferenceEquals(snapshot, controller.State.Match.Configuration), "Same snapshot across rounds and restart");
                    Check(controller.State.Hand(0).Count == 10 && controller.State.Reserve.Count == 15, "Next round deal");
                }
                if (controller.AcceptingInput)
                {
                    if (!capturedChain && controller.View.PlayedCount >= 20 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        capturedChain = true;
                        Debug.Log("CHAIN_WITH_20_TILES=PASS count=" + controller.View.PlayedCount);
                        ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../visual-v2-chain.png")));
                        yield return new WaitForSeconds(.3f);
                    }
                    foreach (var tile in controller.View.LocalTiles)
                    {
                        if (!controller.State.CanPlay(tile.Tile)) continue;
                        controller.Select(tile); controller.PlaySelected(); controller.PlaySelected();
                        break;
                    }
                }
                yield return null;
            }
            Check(controller.State.Match.Finished && roundsCompleted > 0, $"Full match completed without hanging; rounds={roundsCompleted}, score={controller.State.Match.Score(0)}:{controller.State.Match.Score(1)}, turn={controller.State.CurrentPlayer}, phase={controller.View.DealPhase}, input={controller.AcceptingInput}");
            Check(controller.State.Match.Score(controller.State.Match.WinnerSide) >= snapshot.TargetScore, "Match ends at configured target");
            Check(controller.View.PlayedCount == controller.State.Chain.Count, "All legal plays visible");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Check(capturedChain, "Observed a live chain with at least twenty tiles");
            Check(errors == 0, "No errors during smoke validation");
            Debug.Log($"DOMINO_SMOKE_SUCCESS: JSON configuration, Double Nine, ten tiles, reserve, teams, drag, legal plays, passes, turns, restart, {roundsCompleted} rounds, immutable snapshot and target 200. Visual inspection still required.");
            Destroy(this);
        }
        IEnumerator WaitForTurn()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!controller.AcceptingInput && Time.realtimeSinceStartup < deadline) yield return null;
            Check(controller.AcceptingInput, "Local turn before timeout");
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("DOMINO_SMOKE_FAILURE: " + message); }
    }
}
