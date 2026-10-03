using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>Floating damage numbers and text (drawn by the HUD) plus pooled particle bursts.</summary>
    public sealed class Effects
    {
        public static readonly Color DamageDealt = new Color(1f, 0.62f, 0.15f);
        public static readonly Color DamageCrit = new Color(1f, 0.32f, 0.45f);
        public static readonly Color DamageTaken = new Color(0.78f, 0.5f, 1f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.25f);

        public struct Popup
        {
            public Vector2 Position;
            public string Text;
            public Color Color;
            public int Size;
            public float Age, Life;
        }

        sealed class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public Color Color;
            public float Age, Life, Gravity;
        }

        public readonly List<Popup> Popups = new List<Popup>();
        readonly List<Particle> active = new List<Particle>();
        readonly List<Particle> pool = new List<Particle>();
        readonly ArtLibrary art;
        readonly Transform parent;
        readonly System.Random rng;

        public Effects(ArtLibrary art, Transform parent, System.Random rng)
        {
            this.art = art;
            this.parent = parent;
            this.rng = rng;
        }

        public void DamageNumber(Vector2 position, int amount, bool critical, bool onPlayer)
        {
            var color = onPlayer ? DamageTaken : critical ? DamageCrit : DamageDealt;
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.3f;
            Text(position + new Vector2(jitter, 0f), amount.ToString(), color, critical ? 34 : 28, 0.9f);
        }

        public void Text(Vector2 position, string text, Color color, int size, float life)
        {
            Popups.Add(new Popup { Position = position, Text = text, Color = color, Size = size, Life = life });
        }

        public void Burst(Vector2 position, Color color, int count, float speed, float life, float gravity)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Rent();
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float s = speed * (0.4f + (float)rng.NextDouble() * 0.6f);
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * s;
                p.Age = 0f;
                p.Life = life * (0.6f + (float)rng.NextDouble() * 0.4f);
                p.Gravity = gravity;
                p.Color = color;
                p.Renderer.color = color;
                p.Transform.position = position;
                p.Transform.gameObject.SetActive(true);
                active.Add(p);
            }
        }

        public void Tick(float dt)
        {
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                var popup = Popups[i];
                popup.Age += dt;
                if (popup.Age >= popup.Life) Popups.RemoveAt(i);
                else Popups[i] = popup;
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    Release(p);
                    active.RemoveAt(i);
                    continue;
                }
                p.Velocity.y -= p.Gravity * dt;
                p.Transform.position += (Vector3)(p.Velocity * dt);
                var color = p.Color;
                color.a = 1f - p.Age / p.Life;
                p.Renderer.color = color;
            }
        }

        public void Clear()
        {
            Popups.Clear();
            foreach (var p in active) Release(p);
            active.Clear();
        }

        Particle Rent()
        {
            if (pool.Count > 0)
            {
                var reused = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return reused;
            }
            var go = new GameObject("Particle");
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = art.Particle;
            renderer.sortingOrder = 40;
            return new Particle { Transform = go.transform, Renderer = renderer };
        }

        void Release(Particle p)
        {
            p.Transform.gameObject.SetActive(false);
            pool.Add(p);
        }
    }
}
