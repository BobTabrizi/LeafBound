using UnityEngine;

namespace LeafBound
{
    /// <summary>
    /// Loot lying in the world. Pops out of a monster, falls onto a foothold (one-way platforms
    /// catch it), bobs in place, blinks before it expires, and flies to the player when picked up.
    /// </summary>
    public sealed class Drop
    {
        public const float Lifetime = 60f;
        public const float BlinkTime = 6f;
        public const float PickupAnimTime = 0.25f;
        public const float Gravity = 30f;
        public const float HalfSize = 0.25f;

        public readonly Loot Loot;
        public Vector2 Position;
        public Vector2 Velocity;
        public bool Resting { get; private set; }
        public float Age { get; private set; }
        public float PickupTimer { get; private set; } = -1f;

        public Transform View { get; private set; }
        SpriteRenderer viewRenderer;
        Vector2 pickupFrom;
        readonly float bobPhase;

        public Drop(Loot loot, Vector2 position, Vector2 velocity, float bobPhase = 0f)
        {
            Loot = loot;
            Position = position;
            Velocity = velocity;
            this.bobPhase = bobPhase;
        }

        public bool IsBeingPickedUp => PickupTimer >= 0f;
        public bool CanBePickedUp => Resting && !IsBeingPickedUp && Age < Lifetime;
        public bool Finished => Age >= Lifetime || PickupTimer >= PickupAnimTime;
        public Rect Hitbox => new Rect(Position.x - HalfSize, Position.y, HalfSize * 2f, HalfSize * 2f);

        public void AttachView(Transform view, SpriteRenderer renderer)
        {
            View = view;
            viewRenderer = renderer;
        }

        public void StartPickup()
        {
            PickupTimer = 0f;
            pickupFrom = Position;
        }

        public void Tick(float dt, MapData map)
        {
            Age += dt;
            if (IsBeingPickedUp)
            {
                PickupTimer += dt;
                return;
            }
            if (Resting) return;

            Velocity.y = Mathf.Max(Velocity.y - Gravity * dt, -PlayerMotor.MaxFallSpeed);
            float prevY = Position.y;
            Position += Velocity * dt;
            Position.x = Mathf.Clamp(Position.x, map.MinX + HalfSize, map.MaxX - HalfSize);

            if (Velocity.y <= 0f)
            {
                int foothold = map.FindLanding(Position.x, prevY, Position.y, -1);
                if (foothold >= 0)
                {
                    Position.y = map.Footholds[foothold].Y;
                    Velocity = Vector2.zero;
                    Resting = true;
                }
            }
            if (Position.y < map.BottomY - 5f) Age = Lifetime; // fell out of the world
        }

        public void UpdateView(Vector2 playerPos)
        {
            if (View == null) return;
            var color = Color.white;
            var scale = Vector3.one;
            Vector2 pos;

            if (IsBeingPickedUp)
            {
                float k = Mathf.Clamp01(PickupTimer / PickupAnimTime);
                pos = Vector2.Lerp(pickupFrom, playerPos + new Vector2(0f, 0.8f), k * k);
                color.a = 1f - k;
            }
            else
            {
                pos = Position;
                if (Resting) pos.y += 0.1f + Mathf.Sin(Age * 3f + bobPhase) * 0.06f;
                if (Loot.IsMesos) scale.x = Mathf.Max(0.25f, Mathf.Abs(Mathf.Cos(Age * 4f + bobPhase)));
                if (Lifetime - Age < BlinkTime && Mathf.FloorToInt(Age * 8f) % 2 == 0) color.a = 0.3f;
            }

            View.position = pos;
            View.localScale = scale;
            viewRenderer.color = color;
        }
    }
}
