using UnityEngine;

namespace LeafBound
{
    public enum MobLook { Slime, Mushroom }

    public sealed class MobDef
    {
        public string Name = "";
        public int Level;
        public int MaxHp;
        public int TouchDamage;
        public int Exp;
        public float Speed;
        public float HalfWidth;
        public float Height;
        public MobLook Look;

        public static readonly MobDef SproutSlime = new MobDef
        {
            Name = "Sprout Slime", Level = 1, MaxHp = 18, TouchDamage = 6, Exp = 4,
            Speed = 1.2f, HalfWidth = 0.5f, Height = 0.8f, Look = MobLook.Slime,
        };

        public static readonly MobDef Capshroom = new MobDef
        {
            Name = "Capshroom", Level = 4, MaxHp = 60, TouchDamage = 12, Exp = 11,
            Speed = 1.6f, HalfWidth = 0.5f, Height = 0.95f, Look = MobLook.Mushroom,
        };
    }

    /// <summary>
    /// A monster that wanders its own foothold and chases the player for a while after being hit.
    /// Logic runs without a view, so it can be unit tested; the view is optional.
    /// </summary>
    public sealed class Mob
    {
        public const float DeathDuration = 0.6f;
        const float HitStunTime = 0.3f;
        const float KnockbackSpeed = 3.5f;
        const float AggroTime = 8f;

        public readonly MobDef Def;
        public readonly int Foothold;
        public Vector2 Position;
        public int Hp;
        public int Facing = 1;
        public int SlotIndex = -1;
        public float DeathTimer;
        public float HpBarTimer;
        public bool Moving { get; private set; }

        public Transform View { get; private set; }
        SpriteRenderer viewRenderer;

        float hitFlash, stun, knockVelocity, aggro, decisionTimer, animTime;
        int moveDir;

        public Mob(MobDef def, int foothold, Vector2 position)
        {
            Def = def;
            Foothold = foothold;
            Position = position;
            Hp = def.MaxHp;
        }

        public bool IsDead => Hp <= 0;
        public bool IsAggro => aggro > 0f;
        public Rect Hitbox => new Rect(Position.x - Def.HalfWidth, Position.y, Def.HalfWidth * 2f, Def.Height);

        public void AttachView(Transform view, SpriteRenderer renderer)
        {
            View = view;
            viewRenderer = renderer;
        }

        public void Tick(float dt, MapData map, float playerX, System.Random rng)
        {
            animTime += dt;
            if (HpBarTimer > 0f) HpBarTimer -= dt;
            if (hitFlash > 0f) hitFlash -= dt;
            if (IsDead)
            {
                DeathTimer -= dt;
                Moving = false;
                return;
            }

            var fh = map.Footholds[Foothold];
            float minX = Mathf.Max(fh.X1, map.MinX) + Def.HalfWidth;
            float maxX = Mathf.Min(fh.X2, map.MaxX) - Def.HalfWidth;
            if (minX > maxX) minX = maxX = (fh.X1 + fh.X2) * 0.5f;

            if (stun > 0f)
            {
                stun -= dt;
                Position.x = Mathf.Clamp(Position.x + knockVelocity * dt, minX, maxX);
                knockVelocity = Mathf.MoveTowards(knockVelocity, 0f, 12f * dt);
                Moving = false;
                return;
            }

            if (aggro > 0f)
            {
                aggro -= dt;
                float dx = playerX - Position.x;
                moveDir = Mathf.Abs(dx) < 0.3f ? 0 : (dx > 0f ? 1 : -1);
            }
            else
            {
                decisionTimer -= dt;
                if (decisionTimer <= 0f)
                {
                    decisionTimer = 1.2f + (float)rng.NextDouble() * 2.3f;
                    moveDir = rng.NextDouble() < 0.35 ? 0 : (rng.NextDouble() < 0.5 ? -1 : 1);
                }
            }

            if (moveDir != 0) Facing = moveDir;
            float speed = Def.Speed * (aggro > 0f ? 1.4f : 1f);
            Position.x += moveDir * speed * dt;
            if (Position.x <= minX)
            {
                Position.x = minX;
                if (aggro <= 0f) moveDir = 1;
            }
            else if (Position.x >= maxX)
            {
                Position.x = maxX;
                if (aggro <= 0f) moveDir = -1;
            }
            Moving = moveDir != 0;
        }

        /// <summary>Applies damage from an attacker at fromX. Returns true if this hit killed it.</summary>
        public bool TakeHit(int damage, float fromX)
        {
            if (IsDead) return false;
            Hp = Mathf.Max(0, Hp - damage);
            HpBarTimer = 6f;
            hitFlash = 0.12f;
            aggro = AggroTime;
            stun = HitStunTime;
            knockVelocity = (Position.x >= fromX ? 1f : -1f) * KnockbackSpeed;
            if (Hp > 0) return false;
            DeathTimer = DeathDuration;
            return true;
        }

        public void UpdateView()
        {
            if (View == null) return;

            float squash, hop = 0f;
            if (Moving)
            {
                squash = Mathf.Sin(animTime * 10f);
                if (Def.Look == MobLook.Slime) hop = Mathf.Abs(Mathf.Sin(animTime * 5f)) * 0.18f;
            }
            else
            {
                squash = Mathf.Sin(animTime * 3f) * 0.6f;
            }

            View.position = new Vector3(Position.x, Position.y + hop, 0f);
            var scale = new Vector3(1f + squash * 0.05f, 1f - squash * 0.05f, 1f);
            var color = hitFlash > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;
            if (IsDead)
            {
                float k = Mathf.Clamp01(DeathTimer / DeathDuration);
                color.a = k;
                scale *= 1f + (1f - k) * 0.3f;
            }
            View.localScale = scale;
            viewRenderer.flipX = Facing < 0;
            viewRenderer.color = color;
        }
    }
}
