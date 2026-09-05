using UnityEngine;
using UnityEngine.UI;

namespace Domino.UI
{
    // Native ParticleSystem simulation with a small UGUI mesh adapter for the existing overlay Canvas.
    // No camera or world-space conversion; the three views are created once and reused.
    [RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
    public sealed class FeedbackParticles : MaskableGraphic
    {
        ParticleSystem simulation;
        readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];
        readonly Vector3[] corners = new Vector3[4];
        int count;
        public int LiveCount => count;
        public void Initialize()
        {
            raycastTarget = false;
            simulation = gameObject.AddComponent<ParticleSystem>();
            simulation.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = simulation.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = buffer.Length;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0;
            var emission = simulation.emission; emission.enabled = false;
            var shape = simulation.shape; shape.enabled = false;
            GetComponent<ParticleSystemRenderer>().enabled = false;
        }
        public void Burst(Vector2 origin, int amount, float lifetime, float speed, bool confetti)
        {
            Clear();
            for (int i = 0; i < Mathf.Min(amount, buffer.Length); i++)
            {
                float angle = i * 2.39996f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var emit = new ParticleSystem.EmitParams
                {
                    position = origin,
                    velocity = direction * speed * (.6f + (i % 5) * .1f) + (confetti ? Vector2.up * 35 : Vector2.zero),
                    startLifetime = lifetime * (.7f + i % 4 * .1f),
                    startSize = confetti ? 4 + i % 3 * 2 : 2.5f,
                    startColor = i % 3 == 0 ? UiKit.Cream : i % 3 == 1 ? UiKit.Gold : DominoVisualTheme.TeamA,
                    rotation = i * 37,
                    angularVelocity = confetti ? 100 : 0
                };
                simulation.Emit(emit, 1);
            }
            count = simulation.GetParticles(buffer);
            SetVerticesDirty();
        }
        void LateUpdate()
        {
            if (!simulation || count == 0) return;
            simulation.Simulate(Time.unscaledDeltaTime, false, false, false);
            count = simulation.GetParticles(buffer);
            SetVerticesDirty();
        }
        public void Clear()
        {
            if (simulation) simulation.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            count = 0; SetVerticesDirty();
        }
        protected override void OnDisable() { Clear(); base.OnDisable(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < count; i++)
            {
                var particle = buffer[i];
                float half = particle.GetCurrentSize(simulation) * .5f;
                var tint = particle.GetCurrentColor(simulation);
                tint.a = (byte)(tint.a * Mathf.Clamp01(particle.remainingLifetime / particle.startLifetime * 2));
                var rotation = Quaternion.Euler(0, 0, particle.rotation);
                corners[0] = new Vector3(-half, -half); corners[1] = new Vector3(-half, half);
                corners[2] = new Vector3(half, half); corners[3] = new Vector3(half, -half);
                int start = vh.currentVertCount;
                for (int c = 0; c < 4; c++) vh.AddVert(particle.position + rotation * corners[c], tint, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
