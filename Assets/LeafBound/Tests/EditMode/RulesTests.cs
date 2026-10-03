using NUnit.Framework;
using UnityEngine;

namespace LeafBound.Tests
{
    public class StatsTests
    {
        [Test]
        public void ExpRequirementGrowsWithLevel()
        {
            Assert.AreEqual(15, PlayerStats.ExpToNext(1));
            for (int level = 1; level < 50; level++)
                Assert.Greater(PlayerStats.ExpToNext(level + 1), PlayerStats.ExpToNext(level));
        }

        [Test]
        public void LevelUpCarriesOverExpAndRefillsHp()
        {
            var stats = new PlayerStats();
            stats.TakeDamage(20);
            int levels = stats.GainExp(15 + 5);
            Assert.AreEqual(1, levels);
            Assert.AreEqual(2, stats.Level);
            Assert.AreEqual(5, stats.Exp);
            Assert.AreEqual(stats.MaxHp, stats.Hp);
            Assert.AreEqual(66, stats.MaxHp);
        }

        [Test]
        public void BigExpGainCanGrantSeveralLevels()
        {
            var stats = new PlayerStats();
            int levels = stats.GainExp(PlayerStats.ExpToNext(1) + PlayerStats.ExpToNext(2) + 10);
            Assert.AreEqual(2, levels);
            Assert.AreEqual(3, stats.Level);
            Assert.AreEqual(10, stats.Exp);
        }

        [Test]
        public void IgnoresNonPositiveExp()
        {
            var stats = new PlayerStats();
            Assert.AreEqual(0, stats.GainExp(0));
            Assert.AreEqual(0, stats.GainExp(-5));
            Assert.AreEqual(0, stats.Exp);
        }

        [Test]
        public void DamageRollsStayInRange()
        {
            var stats = new PlayerStats();
            var rng = new System.Random(1234);
            int crits = 0;
            for (int i = 0; i < 5000; i++)
            {
                int damage = stats.RollDamage(rng, out bool critical);
                if (critical)
                {
                    crits++;
                    Assert.That(damage, Is.InRange(Mathf.RoundToInt(stats.MinDamage * PlayerStats.CritMultiplier),
                        Mathf.RoundToInt(stats.MaxDamage * PlayerStats.CritMultiplier)));
                }
                else
                {
                    Assert.That(damage, Is.InRange(stats.MinDamage, stats.MaxDamage));
                }
            }
            Assert.That(crits, Is.InRange(250, 600), "crit rate should be near 8%");
        }

        [Test]
        public void HpNeverGoesNegative()
        {
            var stats = new PlayerStats();
            Assert.IsTrue(stats.TakeDamage(9999));
            Assert.AreEqual(0, stats.Hp);
            Assert.IsTrue(stats.IsDead);
            stats.HealFull();
            Assert.AreEqual(stats.MaxHp, stats.Hp);
        }
    }

    public class MobTests
    {
        MapData map;

        [SetUp]
        public void SetUp()
        {
            map = new MapData { MinX = 0f, MaxX = 30f, BottomY = -3f, TopY = 12f };
            map.Footholds.Add(new Foothold(0f, 30f, 0f, solid: true));
            map.Footholds.Add(new Foothold(5f, 12f, 2.5f));
        }

        [Test]
        public void WanderingNeverLeavesItsPlatform()
        {
            var mob = new Mob(MobDef.SproutSlime, 1, new Vector2(8f, 2.5f));
            var rng = new System.Random(42);
            float min = 5f + mob.Def.HalfWidth, max = 12f - mob.Def.HalfWidth;
            for (int i = 0; i < 60 * 120; i++)
            {
                mob.Tick(1f / 60f, map, 8f, rng);
                Assert.That(mob.Position.x, Is.InRange(min, max));
                Assert.AreEqual(2.5f, mob.Position.y);
            }
        }

        [Test]
        public void HitsReduceHpAndKill()
        {
            var mob = new Mob(MobDef.SproutSlime, 0, new Vector2(10f, 0f));
            Assert.IsFalse(mob.TakeHit(10, 9f));
            Assert.AreEqual(MobDef.SproutSlime.MaxHp - 10, mob.Hp);
            Assert.IsTrue(mob.TakeHit(100, 9f));
            Assert.IsTrue(mob.IsDead);
            Assert.AreEqual(0, mob.Hp);
            Assert.IsFalse(mob.TakeHit(10, 9f), "a dead mob cannot be killed twice");
        }

        [Test]
        public void ChasesThePlayerAfterBeingHit()
        {
            var mob = new Mob(MobDef.SproutSlime, 0, new Vector2(10f, 0f));
            var rng = new System.Random(1);
            mob.TakeHit(1, 9f);
            Assert.IsTrue(mob.IsAggro);
            for (int i = 0; i < 60 * 3; i++) mob.Tick(1f / 60f, map, 20f, rng);
            Assert.Greater(mob.Position.x, 11f);
            Assert.AreEqual(1, mob.Facing);
        }
    }

    public class MapDataTests
    {
        [Test]
        public void LandingPicksTheHighestFootholdCrossed()
        {
            var map = new MapData();
            map.Footholds.Add(new Foothold(0f, 10f, 0f, solid: true));
            map.Footholds.Add(new Foothold(0f, 10f, 3f));
            Assert.AreEqual(1, map.FindLanding(5f, 4f, -1f, -1));
            Assert.AreEqual(0, map.FindLanding(5f, 4f, -1f, ignore: 1));
            Assert.AreEqual(-1, map.FindLanding(5f, 2f, 1f, -1), "nothing between 2 and 1");
            Assert.AreEqual(-1, map.FindLanding(11f, 4f, -1f, -1), "off the end of every foothold");
        }

        [Test]
        public void MossyMeadowLayoutIsConsistent()
        {
            var map = MapData.CreateMossyMeadow();
            Assert.IsTrue(map.Footholds[0].Solid);
            Assert.AreEqual(map.MinX, map.Footholds[0].X1);
            Assert.AreEqual(map.MaxX, map.Footholds[0].X2);
            Assert.AreEqual(0, map.FindFootholdNear(map.PlayerSpawn.x, map.PlayerSpawn.y, 0.01f), "spawn is on the ground");

            for (int i = 0; i < map.Ropes.Count; i++)
            {
                var rope = map.Ropes[i];
                Assert.Less(rope.Bottom, rope.Top, $"rope {i}");
                Assert.GreaterOrEqual(map.FindFootholdNear(rope.X, rope.Top, 0.01f), 0, $"rope {i} top must sit on a foothold");
                Assert.GreaterOrEqual(map.FindFootholdBelow(rope.X, rope.Bottom, 0.7f), 0, $"rope {i} bottom must be reachable");
            }

            foreach (var spawn in map.Spawns)
            {
                var f = map.Footholds[spawn.Foothold];
                Assert.That(spawn.X, Is.InRange(f.X1 + spawn.Def.HalfWidth, f.X2 - spawn.Def.HalfWidth), spawn.Def.Name);
            }

            foreach (var f in map.Footholds)
            {
                Assert.That(f.X1, Is.GreaterThanOrEqualTo(map.MinX));
                Assert.That(f.X2, Is.LessThanOrEqualTo(map.MaxX));
                Assert.That(f.Y, Is.InRange(map.BottomY, map.TopY - PlayerMotor.Height));
            }
        }
    }
}
