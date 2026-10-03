using UnityEngine;

namespace LeafBound
{
    public enum MotorState { Ground, Air, Climb }

    /// <summary>
    /// MapleStory-style kinematic movement over footholds and ropes. Pure logic with no
    /// GameObjects, so it can be unit tested. Position is the point between the feet.
    /// </summary>
    public sealed class PlayerMotor
    {
        public const float WalkSpeed = 5f;
        public const float GroundAccel = 60f;
        public const float GroundFriction = 45f;
        public const float AirAccel = 10f;
        public const float Gravity = 30f;
        public const float JumpSpeed = 13f;
        public const float DropHopSpeed = 3f;
        public const float MaxFallSpeed = 20f;
        public const float ClimbSpeed = 3.5f;
        public const float HalfWidth = 0.3f;
        public const float Height = 1.5f;

        const float DropIgnoreTime = 0.35f;
        const float RopeRegrabDelay = 0.3f;

        public Vector2 Position;
        public Vector2 Velocity;
        public int Facing = 1;
        public MotorState State = MotorState.Air;
        public int Foothold = -1;
        public int Rope = -1;

        /// <summary>True while climbing and actually moving, for the climb animation.</summary>
        public bool ClimbMoving { get; private set; }
        public bool JumpedThisTick { get; private set; }
        public bool LandedThisTick { get; private set; }

        int ignoredFoothold = -1;
        float ignoreTimer;
        float ropeCooldown;

        public Rect Hitbox => new Rect(Position.x - HalfWidth, Position.y, HalfWidth * 2f, Height);

        public void Teleport(Vector2 position)
        {
            Position = position;
            Velocity = Vector2.zero;
            State = MotorState.Air;
            Foothold = -1;
            Rope = -1;
            ignoredFoothold = -1;
            ignoreTimer = 0f;
            ropeCooldown = 0f;
        }

        /// <summary>Thrown back and up, away from fromX, when something hurts the player.</summary>
        public void Knockback(float fromX)
        {
            float dir = Position.x >= fromX ? 1f : -1f;
            State = MotorState.Air;
            Foothold = -1;
            Rope = -1;
            ropeCooldown = 0.5f;
            Velocity = new Vector2(dir * 4f, 6f);
        }

        /// <param name="attacking">Attacking roots you on the ground and locks facing.</param>
        public void Tick(float dt, IGameInput input, MapData map, bool attacking)
        {
            JumpedThisTick = false;
            LandedThisTick = false;
            ClimbMoving = false;
            if (ignoreTimer > 0f)
            {
                ignoreTimer -= dt;
                if (ignoreTimer <= 0f) ignoredFoothold = -1;
            }
            if (ropeCooldown > 0f) ropeCooldown -= dt;

            int h = (input.Held(GameAction.Right) ? 1 : 0) - (input.Held(GameAction.Left) ? 1 : 0);
            switch (State)
            {
                case MotorState.Ground: TickGround(dt, input, map, h, attacking); break;
                case MotorState.Air: TickAir(dt, input, map, h, attacking); break;
                case MotorState.Climb: TickClimb(dt, input, map, h); break;
            }
        }

        void TickGround(float dt, IGameInput input, MapData map, int h, bool attacking)
        {
            if (!attacking)
            {
                if (input.Held(GameAction.Up) && TryGrabRope(map, map.FindGrabbableRope(Position.x, Position.y))) return;
                if (input.Held(GameAction.Down) && TryGrabRope(map, map.FindRopeHangingFrom(Position.x, Position.y))) return;
                if (input.Held(GameAction.Jump))
                {
                    if (!input.Held(GameAction.Down))
                    {
                        LeaveGround(JumpSpeed);
                        JumpedThisTick = true;
                        return;
                    }
                    if (!map.Footholds[Foothold].Solid)
                    {
                        ignoredFoothold = Foothold;
                        ignoreTimer = DropIgnoreTime;
                        LeaveGround(DropHopSpeed);
                        return;
                    }
                    // Down + jump on solid ground does nothing, as in MapleStory.
                }
                if (h != 0) Facing = h;
            }

            bool walking = !attacking && h != 0;
            float target = walking ? h * WalkSpeed : 0f;
            Velocity.x = Mathf.MoveTowards(Velocity.x, target, (walking ? GroundAccel : GroundFriction) * dt);
            Position.x += Velocity.x * dt;
            ClampToMap(map);

            if (!map.Footholds[Foothold].Covers(Position.x))
            {
                int next = map.FindFootholdNear(Position.x, Position.y, 0.05f);
                if (next >= 0)
                {
                    Foothold = next;
                    Position.y = map.Footholds[next].Y;
                }
                else
                {
                    State = MotorState.Air;
                    Foothold = -1;
                    Velocity.y = 0f;
                }
            }
        }

        void TickAir(float dt, IGameInput input, MapData map, int h, bool attacking)
        {
            if (!attacking)
            {
                if (h != 0) Facing = h;
                if (ropeCooldown <= 0f && input.Held(GameAction.Up)
                    && TryGrabRope(map, map.FindGrabbableRope(Position.x, Position.y))) return;
            }

            if (h != 0) Velocity.x = Mathf.MoveTowards(Velocity.x, h * WalkSpeed, AirAccel * dt);
            Velocity.y = Mathf.Max(Velocity.y - Gravity * dt, -MaxFallSpeed);
            float prevY = Position.y;
            Position += Velocity * dt;
            ClampToMap(map);

            if (Velocity.y <= 0f)
            {
                int landing = map.FindLanding(Position.x, prevY, Position.y, ignoredFoothold);
                if (landing >= 0) Land(map, landing);
            }
        }

        void TickClimb(float dt, IGameInput input, MapData map, int h)
        {
            var rope = map.Ropes[Rope];
            Position.x = rope.X;
            Velocity = Vector2.zero;

            if (h != 0 && input.Held(GameAction.Jump))
            {
                Facing = h;
                State = MotorState.Air;
                Rope = -1;
                ropeCooldown = RopeRegrabDelay;
                Velocity = new Vector2(h * WalkSpeed * 0.7f, JumpSpeed * 0.55f);
                JumpedThisTick = true;
                return;
            }

            int v = (input.Held(GameAction.Up) ? 1 : 0) - (input.Held(GameAction.Down) ? 1 : 0);
            ClimbMoving = v != 0;
            Position.y += v * ClimbSpeed * dt;

            if (Position.y >= rope.Top)
            {
                int top = map.FindFootholdNear(rope.X, rope.Top, 0.3f);
                if (top >= 0) Land(map, top);
                else Position.y = rope.Top;
            }
            else if (Position.y <= rope.Bottom)
            {
                int below = map.FindFootholdBelow(rope.X, rope.Bottom, 0.7f);
                if (below >= 0)
                {
                    Land(map, below);
                }
                else
                {
                    // Nothing under the end of the rope: let go and fall.
                    Position.y = rope.Bottom;
                    State = MotorState.Air;
                    Rope = -1;
                    ropeCooldown = RopeRegrabDelay;
                }
            }
        }

        bool TryGrabRope(MapData map, int index)
        {
            if (index < 0) return false;
            var rope = map.Ropes[index];
            State = MotorState.Climb;
            Rope = index;
            Foothold = -1;
            Velocity = Vector2.zero;
            Position = new Vector2(rope.X, Mathf.Clamp(Position.y, rope.Bottom, rope.Top - 0.25f));
            return true;
        }

        void LeaveGround(float upSpeed)
        {
            State = MotorState.Air;
            Foothold = -1;
            Velocity.y = upSpeed;
        }

        void Land(MapData map, int foothold)
        {
            State = MotorState.Ground;
            Foothold = foothold;
            Rope = -1;
            Position.y = map.Footholds[foothold].Y;
            Velocity.y = 0f;
            LandedThisTick = true;
        }

        void ClampToMap(MapData map)
        {
            float min = map.MinX + HalfWidth, max = map.MaxX - HalfWidth;
            if (Position.x < min)
            {
                Position.x = min;
                Velocity.x = 0f;
            }
            else if (Position.x > max)
            {
                Position.x = max;
                Velocity.x = 0f;
            }
        }
    }
}
