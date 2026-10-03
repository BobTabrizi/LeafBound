using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>What the current swing is: a basic attack, an attack skill, or a buff cast.</summary>
    public enum AttackKind { Basic, PowerStrike, SlashBlast, Rage, Dash }

    /// <summary>The hero: movement, stats, skills, inventory and timers. Game resolves combat.</summary>
    public sealed class Player
    {
        public const float InvincibleTime = 1.5f;
        public const int StartingSkillPoints = 3;
        public const int SkillPointsPerLevel = 3;

        public readonly PlayerMotor Motor = new PlayerMotor();
        public readonly PlayerStats Stats = new PlayerStats();
        public readonly SkillBook Skills = new SkillBook(StartingSkillPoints);
        public readonly Inventory Inventory = new Inventory();

        public AttackKind Attack;
        public float AttackTimer;
        public bool AttackHitDone;
        public float Invincible;
        public float ReviveTimer;
        public float RageTimer;
        public float PotionCooldown;
        public float PickupCooldown;
        public float RegenTimer;
        public float AuraTimer;
        public float DashCooldown;
        /// <summary>Monsters already cut by the current dash, so each is hit once.</summary>
        public readonly List<Mob> DashHits = new List<Mob>();

        public Player()
        {
            Inventory.Add(ItemDef.RedPotion, 5);
            Inventory.Add(ItemDef.BluePotion, 5);
        }

        public bool IsDead => Stats.IsDead;
        public bool IsAttacking => AttackTimer > 0f;
        public float AttackProgress => IsAttacking ? 1f - AttackTimer / DurationOf(Attack) : 0f;

        public static float DurationOf(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.PowerStrike: return 0.5f;
                case AttackKind.SlashBlast: return 0.55f;
                case AttackKind.Rage: return 0.5f;
                case AttackKind.Dash: return SkillDef.DashDuration;
                default: return 0.45f;
            }
        }

        /// <summary>When, after the swing starts, its hit lands (or the buff takes effect).</summary>
        public static float HitTimeOf(AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.PowerStrike: return 0.2f;
                case AttackKind.SlashBlast: return 0.22f;
                case AttackKind.Rage: return 0.25f;
                case AttackKind.Dash: return 0f; // damage is dealt along the path instead
                default: return 0.18f;
            }
        }

        public void StartAttack(AttackKind kind)
        {
            Attack = kind;
            AttackTimer = DurationOf(kind);
            AttackHitDone = false;
        }
    }

    /// <summary>
    /// Draws the hero from separate body parts and animates them in code, the way MapleStory
    /// composes characters. Part offsets are in sprite pixels from the feet.
    /// </summary>
    public sealed class PlayerView
    {
        const float Px = 1f / ArtLibrary.PixelsPerUnit;
        const int SortBase = 30;

        static readonly Vector2 HipBack = new Vector2(-1.5f, 7f);
        static readonly Vector2 HipFront = new Vector2(1.5f, 7f);
        static readonly Vector2 BodyPos = new Vector2(0f, 6f);
        static readonly Vector2 HeadPos = new Vector2(0.5f, 13f);
        static readonly Vector2 ShoulderBack = new Vector2(-1.5f, 13f);
        static readonly Vector2 ShoulderFront = new Vector2(1f, 13f);
        static readonly Vector2 Hand = new Vector2(0f, -5.5f);
        static readonly Vector2 SlashPos = new Vector2(4f, 10f);
        // Where the ponytail is tied, relative to the head pivot: back of the head from the side, centre from behind.
        static readonly Vector2 TieSide = new Vector2(-4f, 11f);
        static readonly Vector2 TieBack = new Vector2(0f, 11f);

        readonly Transform root, rig, legBack, legFront, body, head, armBack, armFront, sword, ponytail, tomb;
        readonly SpriteRenderer headRenderer, armBackRenderer, ponytailRenderer, slashRenderer;
        readonly Transform slash;
        readonly List<SpriteRenderer> rigRenderers = new List<SpriteRenderer>();
        readonly Sprite headFront, headBack;
        float walkPhase, climbPhase, tombHeight;

        public PlayerView(ArtLibrary art, Transform parent)
        {
            root = new GameObject("Player").transform;
            root.SetParent(parent, false);
            rig = new GameObject("Rig").transform;
            rig.SetParent(root, false);

            armBack = Part("ArmBack", art.Arm, ShoulderBack, -2, rig);
            legBack = Part("LegBack", art.Leg, HipBack, 0, rig);
            legFront = Part("LegFront", art.Leg, HipFront, 1, rig);
            body = Part("Body", art.Body, BodyPos, 2, rig);
            head = Part("Head", art.Head, HeadPos, 3, rig);
            armFront = Part("ArmFront", art.ArmFront, ShoulderFront, 5, rig);
            ponytail = Part("Ponytail", art.Ponytail, TieSide, -1, head);
            sword = Part("Sword", art.Sword, Hand, 4, armFront);
            slash = Part("Slash", art.Slash, SlashPos, 6, rig);

            headRenderer = head.GetComponent<SpriteRenderer>();
            armBackRenderer = armBack.GetComponent<SpriteRenderer>();
            ponytailRenderer = ponytail.GetComponent<SpriteRenderer>();
            slashRenderer = slash.GetComponent<SpriteRenderer>();
            rigRenderers.Remove(slashRenderer); // the slash fades on its own schedule
            slashRenderer.enabled = false;

            tomb = new GameObject("Tombstone").transform;
            tomb.SetParent(root, false);
            var tombRenderer = tomb.gameObject.AddComponent<SpriteRenderer>();
            tombRenderer.sprite = art.Tombstone;
            tombRenderer.sortingOrder = SortBase;
            tomb.gameObject.SetActive(false);

            headFront = art.Head;
            headBack = art.HeadBack;
        }

        Transform Part(string name, Sprite sprite, Vector2 pixelPos, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pixelPos * Px;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = SortBase + order;
            rigRenderers.Add(renderer);
            return go.transform;
        }

        public void Apply(Player player, float dt, float time)
        {
            var m = player.Motor;
            root.position = new Vector3(m.Position.x, m.Position.y, 0f);

            if (player.IsDead)
            {
                rig.gameObject.SetActive(false);
                tomb.gameObject.SetActive(true);
                tombHeight = Mathf.Max(0f, tombHeight - dt * 12f);
                tomb.localPosition = new Vector3(0f, tombHeight, 0f);
                return;
            }
            rig.gameObject.SetActive(true);
            tomb.gameObject.SetActive(false);
            tombHeight = 4f; // where the tombstone drops from next time

            rig.localScale = new Vector3(m.Facing, 1f, 1f);
            bool climbing = m.State == MotorState.Climb;
            headRenderer.sprite = climbing ? headBack : headFront;
            // Seen from behind on a rope, both hands reach up over the head.
            armBackRenderer.sortingOrder = SortBase + (climbing ? 6 : -2);
            // From behind, the ponytail hangs over the back of the head instead of behind it.
            ponytailRenderer.sortingOrder = SortBase + (climbing ? 4 : -1);
            ponytail.localPosition = (climbing ? TieBack : TieSide) * Px;
            sword.gameObject.SetActive(!climbing);

            // Angles in degrees; 0 hangs straight down, positive swings forward.
            float bob = 0f, legF = 0f, legB = 0f, armF = 0f, armB = 0f, swordAngle = -70f;
            if (climbing)
            {
                if (m.ClimbMoving) climbPhase += dt * 9f;
                float s = Mathf.Sin(climbPhase);
                armF = 160f + s * 15f;
                armB = 200f + s * 15f;
                legF = s * 18f;
                legB = -s * 18f;
            }
            else if (m.State == MotorState.Air)
            {
                legF = 35f;
                legB = -25f;
                armF = 60f;
                armB = -45f;
            }
            else if (Mathf.Abs(m.Velocity.x) > 0.3f)
            {
                walkPhase += dt * 11f * Mathf.Abs(m.Velocity.x) / PlayerMotor.WalkSpeed;
                float s = Mathf.Sin(walkPhase);
                legF = s * 32f;
                legB = -s * 32f;
                armF = -s * 28f;
                armB = s * 28f;
                bob = Mathf.Abs(Mathf.Cos(walkPhase)) > 0.6f ? 1f : 0f;
            }
            else
            {
                walkPhase = 0f;
                float s = Mathf.Sin(time * 3.5f);
                bob = s > 0f ? 1f : 0f;
                armF = s * 4f;
                armB = -s * 4f;
            }

            slashRenderer.enabled = false;
            if (player.IsAttacking && player.Attack == AttackKind.Dash)
            {
                // Low lunge with the blade trailing behind.
                legF = 50f;
                legB = -45f;
                armF = -55f;
                armB = -70f;
                swordAngle = -165f;
                bob = -1f;
            }
            else if (player.IsAttacking && player.Attack == AttackKind.Rage)
            {
                // Buff cast: thrust the sword straight up and hold it.
                float k = Smooth(Mathf.Clamp01(player.AttackProgress / 0.3f));
                armF = Mathf.Lerp(armF, 175f, k);
                armB = Mathf.Lerp(armB, 190f, k);
                swordAngle = Mathf.Lerp(-70f, 180f, k);
            }
            else if (player.IsAttacking)
            {
                // Overhead swing: raise the sword behind the head, then bring it down in front.
                float p = player.AttackProgress;
                if (p < 0.35f)
                {
                    float k = Smooth(p / 0.35f);
                    armF = Mathf.Lerp(20f, 215f, k);
                    swordAngle = Mathf.Lerp(-70f, -120f, k);
                }
                else if (p < 0.6f)
                {
                    armF = Mathf.Lerp(215f, 35f, Smooth((p - 0.35f) / 0.25f));
                    swordAngle = -150f;
                }
                else
                {
                    armF = 35f;
                    swordAngle = -150f;
                }
                if (p >= 0.38f && p < 0.75f)
                {
                    // Skills tint and enlarge the slash: gold for Power Strike, wide and blue for Slash Blast.
                    Color tint;
                    Vector3 size;
                    switch (player.Attack)
                    {
                        case AttackKind.PowerStrike:
                            tint = new Color(1f, 0.82f, 0.3f);
                            size = new Vector3(1.3f, 1.3f, 1f);
                            break;
                        case AttackKind.SlashBlast:
                            tint = new Color(0.6f, 0.85f, 1f);
                            size = new Vector3(1.8f, 1.5f, 1f);
                            break;
                        default:
                            tint = new Color(0.85f, 0.97f, 1f); // a breath of wind
                            size = Vector3.one;
                            break;
                    }
                    tint.a = 1f - (p - 0.38f) / 0.37f;
                    slashRenderer.enabled = true;
                    slashRenderer.color = tint;
                    slash.localScale = size;
                }
            }

            var lift = new Vector2(0f, bob * Px);
            body.localPosition = BodyPos * Px + lift;
            head.localPosition = HeadPos * Px + lift;
            armFront.localPosition = ShoulderFront * Px + lift;
            armBack.localPosition = ShoulderBack * Px + lift;
            legFront.localRotation = Quaternion.Euler(0f, 0f, legF);
            legBack.localRotation = Quaternion.Euler(0f, 0f, legB);
            armFront.localRotation = Quaternion.Euler(0f, 0f, armF);
            armBack.localRotation = Quaternion.Euler(0f, 0f, armB);
            sword.localRotation = Quaternion.Euler(0f, 0f, swordAngle);
            ponytail.localRotation = Quaternion.Euler(0f, 0f, PonytailAngle(m, climbing, time));

            // Blink while invincible after being hit.
            float alpha = player.Invincible > 0f && Mathf.FloorToInt(time * 14f) % 2 == 0 ? 0.35f : 1f;
            foreach (var r in rigRenderers)
            {
                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }

        /// <summary>The ponytail streams back when running and lifts when falling. Negative swings it back and up.</summary>
        float PonytailAngle(PlayerMotor m, bool climbing, float time)
        {
            if (climbing) return Mathf.Sin(climbPhase) * 6f;
            if (m.IsDashing) return -70f + Mathf.Sin(time * 40f) * 4f; // streams straight back
            if (m.State == MotorState.Air) return Mathf.Clamp(m.Velocity.y * 2f, -35f, 5f);
            float speed = Mathf.Min(1f, Mathf.Abs(m.Velocity.x) / PlayerMotor.WalkSpeed);
            if (speed > 0.06f) return -speed * 35f + Mathf.Sin(walkPhase * 2f) * 5f;
            return Mathf.Sin(time * 2f) * 3f;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
