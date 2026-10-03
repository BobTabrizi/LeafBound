using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeafBound.Tests
{
    /// <summary>Runs the real game object. Uses Game.Tick to fast-forward instead of waiting in real time.</summary>
    public class GameTests
    {
        const float Dt = 1f / 60f;

        Game game;
        ScriptedInput input;

        static readonly MobDef Dummy = new MobDef
        {
            Name = "Dummy", Level = 1, MaxHp = 30, TouchDamage = 1, Exp = 20,
            Speed = 0f, HalfWidth = 0.5f, Height = 0.8f, Look = MobLook.Slime,
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            game = new GameObject("Game").AddComponent<Game>();
            input = new ScriptedInput();
            game.Controls = input;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(game.gameObject);
            yield return null;
            Assert.IsNull(Game.Instance);
        }

        void Run(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) game.Tick(Dt);
        }

        [UnityTest]
        public IEnumerator BootsWithPlayerOnTheGroundAndAllMobs()
        {
            Run(1f);
            Assert.AreEqual(MotorState.Ground, game.Player.Motor.State);
            Assert.AreEqual(0, game.Player.Motor.Foothold);
            Assert.AreEqual(game.Map.Spawns.Count, game.Mobs.Count);
            Assert.IsNotNull(game.Camera);
            // Let a few real frames render so view, camera and HUD code all run.
            for (int i = 0; i < 5; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator AttackingKillsAMobAndGrantsExp()
        {
            game.ClearMobs();
            Run(0.5f);
            var player = game.Player.Motor;
            var mob = game.SpawnMob(new MobSpawn(Dummy, 0, player.Position.x + 1.2f));

            // Attacking roots you in place, so step closer whenever hits knock the dummy out of reach.
            for (int i = 0; i < 60 * 15 && !mob.IsDead; i++)
            {
                bool inReach = mob.Position.x - player.Position.x <= 1.3f;
                input.Set(GameAction.Attack, inReach);
                input.Set(GameAction.Right, !inReach);
                game.Tick(Dt);
            }
            input.Clear();

            Assert.IsTrue(mob.IsDead, "the dummy should die to repeated attacks");
            Assert.AreEqual(2, game.Player.Stats.Level, "20 EXP is enough for level 2");
            Assert.AreEqual(5, game.Player.Stats.Exp);

            Run(Mob.DeathDuration + 0.1f);
            Assert.AreEqual(0, game.Mobs.Count, "one-off mobs are removed and not respawned");
            yield return null;
        }

        [UnityTest]
        public IEnumerator KilledMobsRespawn()
        {
            Run(0.1f);
            int total = game.Mobs.Count;
            var mob = game.Mobs[0];
            Assert.IsTrue(mob.TakeHit(9999, mob.Position.x - 1f));
            Run(Mob.DeathDuration + 0.1f);
            Assert.AreEqual(total - 1, game.Mobs.Count);
            Run(Game.MobRespawnTime);
            Assert.AreEqual(total, game.Mobs.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FaintingRevivesAtSpawnWithFullHp()
        {
            game.ClearMobs();
            Run(0.5f);
            var deadly = new MobDef
            {
                Name = "Deadly", Level = 99, MaxHp = 999, TouchDamage = 9999, Exp = 0,
                Speed = 0f, HalfWidth = 0.5f, Height = 0.8f, Look = MobLook.Mushroom,
            };
            game.SpawnMob(new MobSpawn(deadly, 0, game.Player.Motor.Position.x));
            game.Tick(Dt);
            Assert.IsTrue(game.Player.IsDead);
            yield return null; // render the fainted state once

            Run(Game.ReviveTime + 0.1f);
            Assert.IsFalse(game.Player.IsDead);
            Assert.AreEqual(game.Player.Stats.MaxHp, game.Player.Stats.Hp);
            Assert.Greater(game.Player.Invincible, 0f);
            Assert.AreEqual(game.Map.PlayerSpawn.x, game.Player.Motor.Position.x, 0.01f);
        }
    }
}
