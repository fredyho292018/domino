using System;
using System.Collections;
using UnityEngine;

namespace Domino.UI
{
    public enum FeedbackAnimationMode { Full, Reduced, Off }
    public enum FeedbackQuality { Low, Medium, High }
    public enum FeedbackCue { TilePick, TilePlay, TileImpact, RoundWin, GameWin }

    public sealed class FeedbackPresenter : MonoBehaviour
    {
        public FeedbackAnimationMode Animations = FeedbackAnimationMode.Full;
        public FeedbackQuality Quality = FeedbackQuality.Medium;
        public bool ParticlesEnabled = true;
        [Range(.25f, 3)] public float Speed = 1;
        public event Action<FeedbackCue> AudioRequested;
        FeedbackParticles impact, round, game;
        RectTransform turnText, score;
        Coroutine turnPulse, scorePulse;
        public int LiveParticles => (impact ? impact.LiveCount : 0) + (round ? round.LiveCount : 0) + (game ? game.LiveCount : 0);
        public void Initialize(RectTransform parent, RectTransform turn, RectTransform scoreTransform)
        {
            turnText = turn; score = scoreTransform;
            var root = UiKit.Rect("Feedback particles", parent, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.gameObject.AddComponent<Canvas>(); // Isolate animated particle geometry from the static HUD.
            impact = Create("TileImpactParticles", root);
            round = Create("RoundWinParticles", root);
            game = Create("GameWinConfetti", root);
        }
        static FeedbackParticles Create(string name, RectTransform root)
        {
            var rect = UiKit.Rect(name, root, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            var view = rect.gameObject.AddComponent<FeedbackParticles>(); view.Initialize(); return view;
        }
        public void Cue(FeedbackCue cue, Vector2 position)
        {
            AudioRequested?.Invoke(cue);
            if (cue == FeedbackCue.RoundWin || cue == FeedbackCue.GameWin)
            {
                if (scorePulse != null) StopCoroutine(scorePulse);
                scorePulse = StartCoroutine(Pulse(score, .05f));
            }
            if (!ParticlesEnabled || Animations == FeedbackAnimationMode.Off) return;
            float density = Animations == FeedbackAnimationMode.Reduced || Quality == FeedbackQuality.Low ? .35f : Quality == FeedbackQuality.High ? 1 : .7f;
            float speed = Mathf.Max(.25f, Speed);
            if (cue == FeedbackCue.TileImpact && Quality != FeedbackQuality.Low) impact.Burst(position, 4, .24f / speed, 55 * speed, false);
            else if (cue == FeedbackCue.RoundWin) round.Burst(Vector2.up * 60, Mathf.CeilToInt(28 * density), 1.4f / speed, 120 * speed, true);
            else if (cue == FeedbackCue.GameWin) game.Burst(Vector2.up * 60, Mathf.CeilToInt(56 * density), 1.8f / speed, 145 * speed, true);
        }
        public void Turn(bool local)
        {
            if (turnPulse != null) StopCoroutine(turnPulse);
            turnText.localScale = Vector3.one;
            if (local) turnPulse = StartCoroutine(Pulse(turnText, .05f));
        }
        IEnumerator Pulse(RectTransform target, float amplitude)
        {
            if (Animations == FeedbackAnimationMode.Off) { target.localScale = Vector3.one; yield break; }
            if (Animations == FeedbackAnimationMode.Reduced) amplitude *= .4f;
            float duration = .24f / Mathf.Max(.25f, Speed);
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1 + amplitude * Mathf.Sin(Mathf.PI * time / duration));
                yield return null;
            }
            target.localScale = Vector3.one;
        }
        public void Clear()
        {
            StopAllCoroutines(); turnPulse = scorePulse = null;
            if (turnText) turnText.localScale = Vector3.one;
            if (score) score.localScale = Vector3.one;
            if (impact) impact.Clear(); if (round) round.Clear(); if (game) game.Clear();
        }
        void Update()
        {
            if ((!ParticlesEnabled || Animations == FeedbackAnimationMode.Off) && LiveParticles > 0)
            { impact.Clear(); round.Clear(); game.Clear(); }
        }
        void OnDisable() => Clear();
    }
}
