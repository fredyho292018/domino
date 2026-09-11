using System;
using UnityEngine;

namespace Domino.UI
{
    // Presentation only: a reusable, original dry tile-rattle sound.
    public sealed class DominoWashAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip clip;
        BoardView board;

        public void Initialize(BoardView owner)
        {
            board = owner;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.volume = .45f;
            clip = CreateClip(BoardView.InitialWashDuration);
            source.clip = clip;
            if (!FindFirstObjectByType<AudioListener>())
                gameObject.AddComponent<AudioListener>();
            board.DealPhaseChanged += OnPhase;
        }

        void OnPhase(DealPresentationPhase phase)
        {
            source.Stop();
            if (phase == DealPresentationPhase.Washing && isActiveAndEnabled)
                source.Play();
        }

        void OnDisable() { if (source) source.Stop(); }
        void OnDestroy()
        {
            if (board) board.DealPhaseChanged -= OnPhase;
            if (clip) Destroy(clip);
        }

        static AudioClip CreateClip(float duration)
        {
            const int rate = 44100;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var random = new System.Random(7319);
            float friction = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                friction = friction * .72f + ((float)random.NextDouble() * 2 - 1) * .28f;
                float t = (float)i / rate;
                samples[i] = friction * .14f * (.65f + .35f * Mathf.Sin(t * 23));
            }
            // Irregular clusters of short, damped contacts, without a tonal loop.
            for (float at = .025f; at < duration - .09f; at += .018f + (float)random.NextDouble() * .026f)
            {
                float frequency = 950 + (float)random.NextDouble() * 1800;
                float strength = .14f + (float)random.NextDouble() * .19f;
                int start = (int)(at * rate);
                for (int j = 0; j < rate * .055f && start + j < samples.Length; j++)
                {
                    float t = (float)j / rate;
                    float noise = (float)random.NextDouble() * 2 - 1;
                    float contact = .7f * noise + .3f * Mathf.Sin(2 * Mathf.PI * frequency * t);
                    samples[start + j] += contact * strength * Mathf.Exp(-t * 115);
                }
            }
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float fade = Mathf.Clamp01(t / .06f) * Mathf.Clamp01((duration - t) / .15f);
                samples[i] = Mathf.Clamp(samples[i] * fade, -.85f, .85f);
            }
            var result = AudioClip.Create("Domino · lavado", samples.Length, 1, rate, false);
            result.SetData(samples, 0);
            return result;
        }
    }
}
