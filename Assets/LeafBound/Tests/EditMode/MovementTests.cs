using NUnit.Framework;
using UnityEngine;

namespace LeafBound.Tests
{
    public class MovementTests
    {
        const float Dt = 1f / 60f;

        MapData map;
        PlayerMotor motor;
        ScriptedInput input;

        /// <summary>Ground 0..30, a jumpable platform at 2.5, a high platform at 6 reached by a rope at x=18.</summary>
        [SetUp]
        public void SetUp()
        {
            map = new MapData { Name = "Test", MinX = 0f, MaxX = 30f, BottomY = -3f, TopY = 12f };
            map.Footholds.Add(new Foothold(0f, 30f, 0f, solid: true));
            map.Footholds.Add(new Foothold(5f, 12f, 2.5f));
            map.Footholds.Add(new Foothold(14f, 24f, 6f));
            map.Ropes.Add(new Rope(18f, 0.4f, 6f));
            motor = new PlayerMotor();
            input = new ScriptedInput();
        }

        void Run(float seconds, bool attacking = false)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) motor.Tick(Dt, input, map, attacking);
        }

        void PlaceOn(float x, float y)
        {
            motor.Teleport(new Vector2(x, y + 0.05f));
            Run(0.2f);
            Assert.AreEqual(MotorState.Ground, motor.State, "setup: should be standing");
            Assert.AreEqual(y, motor.Position.y, 1e-4f, "setup: wrong foothold");
        }

        [Test]
        public void FallsAndLandsOnGround()
        {
            motor.Teleport(new Vector2(3f, 4f));
            Run(1f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(0, motor.Foothold);
            Assert.AreEqual(0f, motor.Position.y);
        }

        [Test]
        public void WalksAtWalkSpeed()
        {
            PlaceOn(3f, 0f);
            input.Set(GameAction.Right, true);
            Run(1f);
            Assert.That(motor.Position.x, Is.InRange(7.6f, 8.01f));
            Assert.AreEqual(1, motor.Facing);
        }

        [Test]
        public void JumpsUpThroughOneWayPlatformAndLandsOnIt()
        {
            PlaceOn(8f, 0f);
            input.Set(GameAction.Jump, true);
            Run(Dt);
            input.Clear();
            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                motor.Tick(Dt, input, map, false);
                peak = Mathf.Max(peak, motor.Position.y);
            }
            Assert.That(peak, Is.InRange(2.6f, 2.9f), "jump height");
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(1, motor.Foothold);
        }

        [Test]
        public void DownJumpDropsThroughPlatform()
        {
            PlaceOn(8f, 2.5f);
            input.Set(GameAction.Down, true);
            input.Set(GameAction.Jump, true);
            Run(Dt);
            input.Clear();
            Run(1.5f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(0, motor.Foothold);
        }

        [Test]
        public void DownJumpOnSolidGroundDoesNothing()
        {
            PlaceOn(3f, 0f);
            input.Set(GameAction.Down, true);
            input.Set(GameAction.Jump, true);
            Run(0.5f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(0f, motor.Position.y);
        }

        [Test]
        public void WalkingOffAnEdgeFallsToTheGround()
        {
            PlaceOn(11.5f, 2.5f);
            input.Set(GameAction.Right, true);
            Run(1f);
            input.Clear();
            Run(1f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(0, motor.Foothold);
        }

        [Test]
        public void ClimbsRopeOntoPlatformAbove()
        {
            PlaceOn(18.2f, 0f);
            input.Set(GameAction.Up, true);
            Run(Dt);
            Assert.AreEqual(MotorState.Climb, motor.State);
            Assert.AreEqual(18f, motor.Position.x);
            Run(3f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(2, motor.Foothold);
            Assert.AreEqual(6f, motor.Position.y);
        }

        [Test]
        public void ClimbsDownRopeFromPlatform()
        {
            PlaceOn(18f, 6f);
            input.Set(GameAction.Down, true);
            Run(Dt);
            Assert.AreEqual(MotorState.Climb, motor.State);
            Run(3f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.AreEqual(0, motor.Foothold);
        }

        [Test]
        public void JumpsOffRopeSideways()
        {
            PlaceOn(18f, 0f);
            input.Set(GameAction.Up, true);
            Run(0.5f);
            input.Clear();
            input.Set(GameAction.Right, true);
            input.Set(GameAction.Jump, true);
            Run(Dt);
            Assert.AreEqual(MotorState.Air, motor.State);
            Assert.Greater(motor.Velocity.x, 0f);
            Assert.Greater(motor.Velocity.y, 0f);
        }

        [Test]
        public void StaysInsideMapBounds()
        {
            PlaceOn(1f, 0f);
            input.Set(GameAction.Left, true);
            Run(1f);
            Assert.AreEqual(map.MinX + PlayerMotor.HalfWidth, motor.Position.x);
        }

        [Test]
        public void AttackingOnGroundRootsThePlayer()
        {
            PlaceOn(3f, 0f);
            input.Set(GameAction.Right, true);
            input.Set(GameAction.Jump, true);
            Run(0.5f, attacking: true);
            Assert.AreEqual(3f, motor.Position.x, 1e-4f);
            Assert.AreEqual(MotorState.Ground, motor.State);
        }

        [Test]
        public void KnockbackPushesAwayFromAttacker()
        {
            PlaceOn(10f, 0f);
            motor.Knockback(fromX: 11f);
            Assert.AreEqual(MotorState.Air, motor.State);
            Assert.Less(motor.Velocity.x, 0f);
            Run(2f);
            Assert.AreEqual(MotorState.Ground, motor.State);
            Assert.Less(motor.Position.x, 10f);
        }
    }
}
