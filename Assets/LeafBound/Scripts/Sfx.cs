using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>Retro sound effects synthesized at startup, so the project needs no audio files.</summary>
    public sealed class Sfx : IDisposable
    {
        const int Rate = 44100;
        const float Tau = Mathf.PI * 2f;

        public float Volume = 0.35f;
        public readonly AudioClip Jump, Swing, Hit, Kill, LevelUp, Hurt, Pickup, Potion, Skill, Buff, Dash;

        /// <summary>Voice line for Wind Dash, from Resources/Voice/Hasagi.wav (null if the file is missing).</summary>
        public readonly AudioClip DashVoice;

        readonly AudioSource source;
        readonly List<AudioClip> owned = new List<AudioClip>();

        public Sfx(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            var rng = new System.Random(7);
            float Noise() => (float)rng.NextDouble() * 2f - 1f;

            Jump = Make("Jump", 0.14f, (t, T) => Square(Sweep(t, T, 300f, 620f)) * 0.35f * (1f - t / T));

            float lowPass = 0f;
            Swing = Make("Swing", 0.16f, (t, T) =>
            {
                lowPass += (Noise() - lowPass) * 0.25f;
                return lowPass * 1.4f * Mathf.Sin(Mathf.PI * t / T);
            });

            Hit = Make("Hit", 0.13f, (t, T) =>
            {
                float env = (1f - t / T) * (1f - t / T);
                return (Mathf.Sin(Sweep(t, T, 200f, 60f)) * 0.8f + Noise() * 0.35f) * env;
            });

            Kill = Make("Kill", 0.2f, (t, T) => Square(Tau * (t < 0.07f ? 660f : 990f) * t) * 0.25f * (1f - t / T));

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            LevelUp = Make("LevelUp", 0.6f, (t, T) =>
            {
                const float step = 0.09f;
                int index = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / step));
                float lastStart = step * (notes.Length - 1);
                float env = index < notes.Length - 1 ? 1f : 1f - (t - lastStart) / (T - lastStart);
                return Triangle(Tau * notes[index] * t) * 0.35f * env;
            });

            Hurt = Make("Hurt", 0.22f, (t, T) =>
            {
                float cycles = Sweep(t, T, 320f, 110f) / Tau;
                return (cycles - Mathf.Floor(cycles) - 0.5f) * 0.6f * (1f - t / T);
            });

            Pickup = Make("Pickup", 0.12f, (t, T) => Mathf.Sin(Tau * (t < 0.05f ? 880f : 1320f) * t) * 0.3f * (1f - t / T));

            // A wobbling tone, like a gulp.
            Potion = Make("Potion", 0.25f, (t, T) =>
                Mathf.Sin(Tau * (500f * t - 200f / (Tau * 18f) * Mathf.Cos(Tau * 18f * t))) * 0.3f * (1f - t / T));

            Skill = Make("Skill", 0.3f, (t, T) => Triangle(Sweep(t, T, 400f, 1400f)) * 0.3f * (1f - t / T));

            float dashLowPass = 0f;
            Dash = Make("Dash", 0.28f, (t, T) =>
            {
                // Rising rush of air.
                dashLowPass += (Noise() - dashLowPass) * Mathf.Lerp(0.08f, 0.5f, t / T);
                return dashLowPass * 1.6f * Mathf.Sin(Mathf.PI * t / T);
            });

            DashVoice = Resources.Load<AudioClip>("Voice/Hasagi"); // an asset, so not in owned

            float[] buffNotes = { 392f, 523.25f, 659.25f, 783.99f };
            Buff = Make("Buff", 0.45f, (t, T) =>
            {
                const float step = 0.06f;
                int index = Mathf.Min(buffNotes.Length - 1, Mathf.FloorToInt(t / step));
                float lastStart = step * (buffNotes.Length - 1);
                float env = index < buffNotes.Length - 1 ? 1f : 1f - (t - lastStart) / (T - lastStart);
                return Square(Tau * buffNotes[index] * t) * 0.18f * env;
            });
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null && source != null) source.PlayOneShot(clip, Volume * volume);
        }

        public void Dispose()
        {
            foreach (var clip in owned) Util.SafeDestroy(clip);
            owned.Clear();
        }

        /// <summary>Phase of a tone sliding linearly from f0 to f1 over duration T.</summary>
        static float Sweep(float t, float T, float f0, float f1) => Tau * (f0 * t + (f1 - f0) * t * t / (2f * T));
        static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;
        static float Triangle(float phase) => Mathf.Asin(Mathf.Sin(phase)) * (2f / Mathf.PI);

        AudioClip Make(string name, float seconds, Func<float, float, float> sample)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, seconds), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            owned.Add(clip);
            return clip;
        }
    }
}
