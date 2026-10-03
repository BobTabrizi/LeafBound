using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>The hero: movement, stats and attack/hurt timers. Game resolves combat.</summary>
    public sealed class Player
    {
        public const float AttackDuration = 0.45f;
        public const float AttackHitTime = 0.18f;
        public const float InvincibleTime = 1.5f;

        public readonly PlayerMotor Motor = new PlayerMotor();
        public readonly PlayerStats Stats = new PlayerStats();
        public float AttackTimer;
        public bool AttackHitDone;
        public float Invincible;
        public float ReviveTimer;

        public bool IsDead => Stats.IsDead;
        public bool IsAttacking => AttackTimer > 0f;
        public float AttackProgress => IsAttacking ? 1f - AttackTimer / AttackDuration : 0f;
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

        readonly Transform root, rig, legBack, legFront, body, head, armBack, armFront, sword, tomb;
        readonly SpriteRenderer headRenderer, armBackRenderer, slashRenderer;
        readonly List<SpriteRenderer> rigRenderers = new List<SpriteRenderer>();
        readonly Sprite headFront, headBack;
        float walkPhase, climbPhase, tombHeight;

        public PlayerView(ArtLibrary art, Transform parent)
        {
            root = new GameObject("Player").transform;
            root.SetParent(parent, false);
            rig = new GameObject("Rig").transform;
            rig.SetParent(root, false);

            armBack = Part("ArmBack", art.Arm, ShoulderBack, -1, rig);
            legBack = Part("LegBack", art.Leg, HipBack, 0, rig);
            legFront = Part("LegFront", art.Leg, HipFront, 1, rig);
            body = Part("Body", art.Body, BodyPos, 2, rig);
            head = Part("Head", art.Head, HeadPos, 3, rig);
            armFront = Part("ArmFront", art.Arm, ShoulderFront, 5, rig);
            sword = Part("Sword", art.Sword, Hand, 4, armFront);
            var slash = Part("Slash", art.Slash, SlashPos, 6, rig);

            headRenderer = head.GetComponent<SpriteRenderer>();
            armBackRenderer = armBack.GetComponent<SpriteRenderer>();
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
            armBackRenderer.sortingOrder = SortBase + (climbing ? 6 : -1);
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
            if (player.IsAttacking)
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
                    slashRenderer.enabled = true;
                    slashRenderer.color = new Color(1f, 1f, 1f, 1f - (p - 0.38f) / 0.37f);
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

            // Blink while invincible after being hit.
            float alpha = player.Invincible > 0f && Mathf.FloorToInt(time * 14f) % 2 == 0 ? 0.35f : 1f;
            foreach (var r in rigRenderers)
            {
                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
