using System;
using System.Collections;
using Domino.UI;
using UnityEngine;
using UnityEngine.EventSystems;

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
            controller.RestartClient();
            yield return new WaitForSeconds(.35f);
            controller.RestartClient();
            yield return WaitForTurn();
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
            float deadline = Time.realtimeSinceStartup + 180;
            while (!controller.State.Finished && Time.realtimeSinceStartup < deadline)
            {
                if (controller.AcceptingInput)
                {
                    foreach (var tile in controller.View.LocalTiles)
                    {
                        if (!controller.State.CanPlay(tile.Tile)) continue;
                        controller.Select(tile); controller.PlaySelected(); controller.PlaySelected();
                        break;
                    }
                }
                yield return null;
            }
            yield return new WaitForSeconds(1);
            Check(controller.State.Finished, "Round completed or blocked without hanging");
            Check(controller.View.PlayedCount == controller.State.Chain.Count, "All legal plays visible");
            Check(errors == 0, "No errors during smoke validation");
            Debug.Log("DOMINO_SMOKE_SUCCESS: Double Nine, ten tiles, drag, legal plays, passes, turns, restart, round end. Visual inspection still required.");
            Destroy(this);
        }
        IEnumerator WaitForTurn()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!controller.AcceptingInput && Time.realtimeSinceStartup < deadline) yield return null;
            Check(controller.AcceptingInput, "Local turn before timeout");
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("DOMINO_SMOKE_FAILURE: " + message); }
    }
}
