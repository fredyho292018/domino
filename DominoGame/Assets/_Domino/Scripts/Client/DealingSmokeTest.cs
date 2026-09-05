using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Domino.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Client
{
    // Opt-in presentation regression, driven by the isolated editor runner.
    public sealed class DealingSmokeTest : MonoBehaviour
    {
        public static Action<int, int> ResizeGameView;
        DominoClientController controller;
        readonly List<DealPresentationPhase> phases = new();
        readonly int[] received = new int[4];
        int dealt, checks;
        float washStarted, washFinished, organizeStarted, organizeFinished;
        bool capturePhases, resizeDuringDeal;
        void Check(bool condition, string detail)
        { checks++; if (!condition) throw new InvalidOperationException("DOMINO_DEALING_FAILURE: " + detail); }
        IEnumerator Start()
        {
            controller = GetComponent<DominoClientController>();
            yield return null;
            Check(controller && controller.View && controller.State != null, "Controller initialized");
            controller.View.DealPhaseChanged += OnPhase;
            controller.View.TileDealt += OnTile;
            int[,] sizes = { {1600,900}, {1950,900}, {2000,900}, {2100,900}, {1200,900}, {1800,900} };
            string[] names = { "16x9", "19.5x9", "20x9", "21x9", "tablet-4x3", "18x9" };
            for (int test = 0; test < names.Length; test++)
            {
                capturePhases = test == 0;
                resizeDuringDeal = test == 3;
                Check(ResizeGameView != null, "Editor resize adapter assigned");
                ResizeGameView(sizes[test,0], sizes[test,1]);
                float timeout = Time.realtimeSinceStartup + 10;
                while ((Screen.width != sizes[test,0] || Screen.height != sizes[test,1]) && Time.realtimeSinceStartup < timeout) yield return null;
                Check(Screen.width == sizes[test,0] && Screen.height == sizes[test,1], "Actual Game View size " + names[test]);
                controller.RestartClient();
                yield return CheckPreparation();
                Check(Screen.width == sizes[test,0] && Screen.height == sizes[test,1], "Layout size after dealing");
                Check(phases.Count == 7 && phases[0] == DealPresentationPhase.Idle && phases[1] == DealPresentationPhase.Washing
                    && phases[2] == DealPresentationPhase.Dealing && phases[3] == DealPresentationPhase.RevealingHand
                    && phases[4] == DealPresentationPhase.OrganizingHands && phases[5] == DealPresentationPhase.Ready
                    && phases[6] == DealPresentationPhase.Playing, "Phase order");
                Check(washFinished - washStarted >= 1.19f && washFinished - washStarted < 1.5f, "Wash duration");
                Check(organizeFinished - organizeStarted >= .74f && organizeFinished - organizeStarted < 1.0f, "Organize duration");
                Check(dealt == 40 && controller.View.VisuallyDealt == 40, "40 tiles visually dealt");
                for (int p = 0; p < 4; p++)
                {
                    Check(received[p] == 10, "Ten tiles received by seat " + p);
                    var views = controller.View.HandViews(p);
                    for (int i = 0; i < views.Count; i++)
                    {
                        var tile = views[i];
                        Check(tile.IsFaceUp == (p == 0), "Only Fredy reveals");
                        Check(tile.Tile.Equals(controller.State.Hand(p)[i]), "Hand order unchanged");
                        Check(Vector2.Distance(tile.Rect.anchoredPosition, tile.Home) < .1f, "Final hand layout");
                        var corners = new Vector3[4]; tile.Rect.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                            Check(Screen.safeArea.Contains(screen), "Visible tile inside safe area " + names[test]);
                        }
                    }
                }
                Check(controller.State.Chain.Count == 0 && controller.State.CurrentPlayer == 0 && controller.AcceptingInput, "Round begins only after ready");
                Check(controller.View.CreatedTileViews == 55, "Views reused across restarts");
                Check(controller.View.ReserveViews.Count == 15, "Fifteen undealt tiles remain visible");
                foreach (var tile in controller.View.ReserveViews)
                {
                    Check(tile.gameObject.activeInHierarchy && !tile.IsFaceUp && !tile.Selectable, "Reserve visible, concealed and unavailable");
                    var corners = new Vector3[4]; tile.Rect.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Check(Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, corner)), "Reserve inside safe area");
                }
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../dealing-" + names[test] + ".png")));
                    yield return new WaitForSeconds(.3f);
                }
                Debug.Log("DEALING_LAYOUT=PASS " + names[test]);
            }
            capturePhases = resizeDuringDeal = false;
            // Restart during organization, not just between rounds.
            controller.RestartClient();
            float limit = Time.realtimeSinceStartup + 30;
            while (controller.View.DealPhase != DealPresentationPhase.OrganizingHands && Time.realtimeSinceStartup < limit) yield return null;
            Check(controller.View.DealPhase == DealPresentationPhase.OrganizingHands, "Reached organization before cancellation");
            controller.RestartClient();
            yield return CheckPreparation();
            Check(controller.View.CreatedTileViews == 55 && dealt == 40, "Restart cancels and reuses all views");
            var feedback = controller.View.Feedback;
            Check(feedback && feedback.GetComponentsInChildren<ParticleSystem>().Length == 3, "Three persistent native particle systems");
            feedback.Cue(FeedbackCue.GameWin, Vector2.zero);
            Check(feedback.LiveParticles > 0 && feedback.LiveParticles <= 64, "Bounded celebration burst");
            yield return new WaitForSecondsRealtime(.18f);
            Check(feedback.LiveParticles > 0, "Native particles remain alive while simulating");
            foreach (var particles in feedback.GetComponentsInChildren<FeedbackParticles>())
                if (particles.LiveCount > 0)
                    Check(particles.canvasRenderer && particles.canvasRenderer.GetMesh() && particles.canvasRenderer.GetMesh().vertexCount > 0, "Active particles submit visible UI geometry");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) Capture("FeedbackParticles");
            yield return new WaitForSecondsRealtime(.12f);
            controller.RestartClient();
            Check(feedback.LiveParticles == 0, "Restart clears active particles immediately");
            yield return CheckPreparation();
            feedback.Animations = FeedbackAnimationMode.Off;
            feedback.Cue(FeedbackCue.GameWin, Vector2.zero);
            Check(feedback.LiveParticles == 0, "Effects off prevents particles");
            feedback.Animations = FeedbackAnimationMode.Reduced;
            feedback.Cue(FeedbackCue.GameWin, Vector2.zero);
            Check(feedback.LiveParticles > 0 && feedback.LiveParticles <= 20, "Reduced effects limits particles");
            feedback.Clear(); feedback.Animations = FeedbackAnimationMode.Full;
            Debug.Log($"DOMINO_DEALING_SUCCESS: {checks} checks; wash, 40 deals, reveal, organization, hidden opponents, input/bot gates, pool reuse and six responsive layouts.");
            Destroy(this);
        }
        IEnumerator CheckPreparation()
        {
            float limit = Time.realtimeSinceStartup + 30;
            var play = controller.View.transform.Find("Safe area/Landscape composition/Play").GetComponent<Button>();
            while (controller.View.IsPreparingRound && Time.realtimeSinceStartup < limit)
            {
                Check(!controller.AcceptingInput && !play.interactable, "Input blocked while preparing");
                Check(controller.State.Chain.Count == 0 && controller.State.CurrentPlayer == 0, "No bot or local play during preparation");
                for (int p = 0; p < 4; p++) foreach (var tile in controller.View.HandViews(p))
                {
                    Check(!tile.Selectable, "No selectable tile during preparation");
                    if (p != 0 || controller.View.DealPhase == DealPresentationPhase.Dealing)
                        Check(!tile.IsFaceUp, "Faces concealed while arriving");
                }
                if (controller.View.LocalTiles.Count > 0)
                {
                    controller.Select(controller.View.LocalTiles[0]); controller.PlaySelected();
                    Check(!controller.View.LocalTiles[0].Selected, "Selection attempts ignored");
                }
                yield return null;
            }
            Check(controller.View.DealPhase == DealPresentationPhase.Playing, "Ready then playing before timeout");
        }
        void OnPhase(DealPresentationPhase phase)
        {
            var washAudio = controller.View.GetComponent<AudioSource>();
            Check(washAudio && washAudio.clip && !washAudio.loop && washAudio.spatialBlend == 0, "Wash has reusable 2D audio");
            Check(washAudio.isPlaying == (phase == DealPresentationPhase.Washing), "Wash audio starts and stops with visual phase");
            if (phase == DealPresentationPhase.Washing)
            {
                var samples = new float[washAudio.clip.samples];
                Check(washAudio.clip.GetData(samples, 0), "Wash audio samples accessible");
                float peak = 0;
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Check(peak > .05f && peak < 1, "Wash audio non-silent and unclipped");
                Check(Mathf.Abs(washAudio.clip.length - BoardView.InitialWashDuration) < .001f, "Wash audio matches animation duration");
                Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "Exactly one audio listener");
            }
            if (phase == DealPresentationPhase.Idle) { phases.Clear(); Array.Clear(received,0,received.Length); dealt=0; }
            phases.Add(phase);
            if (phase == DealPresentationPhase.Washing) washStarted = Time.time;
            if (phase == DealPresentationPhase.Dealing) washFinished = Time.time;
            if (phase == DealPresentationPhase.OrganizingHands) organizeStarted = Time.time;
            if (phase == DealPresentationPhase.Ready) organizeFinished = Time.time;
            if (capturePhases && (phase == DealPresentationPhase.Washing || phase == DealPresentationPhase.OrganizingHands)) Capture(phase.ToString());
        }
        void OnTile(int player)
        {
            var reserveLabel = controller.View.transform.Find("Safe area/Landscape composition/Tiles/Reserve count");
            Check(!reserveLabel.gameObject.activeSelf, "Reserve parking starts only after all arrivals");
            foreach (var tile in controller.View.ReserveViews)
                Check(Mathf.Abs(tile.Rect.localScale.x - .5f) < .001f, "Reserve remains in wash position during deal");
            Check(player == controller.State.Configuration.Deal.SeatOrder[dealt % 4], "Visual deal follows engine order");
            Check(!controller.View.HandViews(player)[received[player]].IsFaceUp, "Tile arrives face down");
            received[player]++; dealt++;
            if (capturePhases && dealt == 20) Capture("HalfDealt");
            if (resizeDuringDeal && dealt == 8) ResizeGameView(1600,900);
            if (resizeDuringDeal && dealt == 24) ResizeGameView(2100,900);
        }
        static void Capture(string phase)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../../dealing-phase-" + phase + ".png")));
        }
        void OnDestroy()
        {
            if (controller && controller.View)
            { controller.View.DealPhaseChanged -= OnPhase; controller.View.TileDealt -= OnTile; }
        }
    }
}
