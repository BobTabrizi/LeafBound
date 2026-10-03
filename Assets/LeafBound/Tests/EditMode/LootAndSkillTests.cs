using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LeafBound.Tests
{
    public class SkillTests
    {
        [Test]
        public void FormulasMatchTheirDescriptions()
        {
            Assert.AreEqual(165, SkillDef.PowerStrike.DamagePercent(1));
            Assert.AreEqual(255, SkillDef.PowerStrike.DamagePercent(10));
            Assert.AreEqual(4, SkillDef.PowerStrike.MpCost(1));
            Assert.AreEqual(1, SkillDef.PowerStrike.MaxTargets(10));

            Assert.AreEqual(3, SkillDef.SlashBlast.MaxTargets(1));
            Assert.AreEqual(6, SkillDef.SlashBlast.MaxTargets(10));
            Assert.AreEqual(6, SkillDef.SlashBlast.MpCost(1));

            Assert.AreEqual(2, SkillDef.Rage.AttackBonus(1));
            Assert.AreEqual(20, SkillDef.Rage.AttackBonus(10));
            Assert.AreEqual(48f, SkillDef.Rage.Duration(1));
            Assert.AreEqual(0, SkillDef.Rage.DamagePercent(5));

            Assert.AreEqual(5, SkillDef.Dash.MpCost(1));
            Assert.AreEqual(120, SkillDef.Dash.DamagePercent(1));
            Assert.AreEqual(3, SkillDef.Dash.MaxTargets(1));
            Assert.AreEqual(6, SkillDef.Dash.MaxTargets(10));
            Assert.AreEqual(4.2f, SkillDef.Dash.DashDistance(1), 1e-4f);
            Assert.AreEqual(0f, SkillDef.PowerStrike.DashDistance(5));

            foreach (var def in SkillDef.All)
            {
                Assert.AreEqual(0, def.MpCost(0), def.Name);
                Assert.IsNotEmpty(def.Describe(1), def.Name);
                for (int level = 1; level < def.MaxLevel; level++)
                    Assert.GreaterOrEqual(def.MpCost(level + 1), def.MpCost(level), def.Name);
            }
        }

        [Test]
        public void AllIsIndexedBySkillId()
        {
            for (int i = 0; i < SkillDef.All.Length; i++)
                Assert.AreEqual(i, (int)SkillDef.All[i].Id);
        }

        [Test]
        public void LearningSpendsPointsAndStopsAtMaxLevel()
        {
            var book = new SkillBook(3);
            Assert.IsTrue(book.Learn(SkillId.PowerStrike));
            Assert.AreEqual(1, book.Level(SkillId.PowerStrike));
            Assert.AreEqual(2, book.Points);

            book.AddPoints(20);
            while (book.Learn(SkillId.PowerStrike)) { }
            Assert.AreEqual(SkillDef.PowerStrike.MaxLevel, book.Level(SkillId.PowerStrike));
            Assert.IsFalse(book.CanLearn(SkillId.PowerStrike));
            Assert.AreEqual(22 - (SkillDef.PowerStrike.MaxLevel - 1), book.Points);
        }

        [Test]
        public void CannotLearnWithoutPoints()
        {
            var book = new SkillBook(0);
            Assert.IsFalse(book.Learn(SkillId.Rage));
            Assert.AreEqual(0, book.Level(SkillId.Rage));
        }
    }

    public class InventoryTests
    {
        [Test]
        public void AddsRemovesAndCounts()
        {
            var inv = new Inventory();
            Assert.IsTrue(inv.Add(ItemDef.RedPotion, 3));
            Assert.AreEqual(3, inv.Count(ItemDef.RedPotion));
            Assert.IsTrue(inv.Remove(ItemDef.RedPotion));
            Assert.AreEqual(2, inv.Count(ItemDef.RedPotion));
            Assert.IsFalse(inv.Remove(ItemDef.RedPotion, 5), "can't remove more than you have");
            Assert.AreEqual(2, inv.Count(ItemDef.RedPotion));
            Assert.IsFalse(inv.Remove(ItemDef.BluePotion));
        }

        [Test]
        public void StacksAreCapped()
        {
            var inv = new Inventory();
            Assert.IsTrue(inv.Add(ItemDef.SlimeGel, Inventory.MaxStack));
            Assert.IsFalse(inv.Add(ItemDef.SlimeGel));
            Assert.AreEqual(Inventory.MaxStack, inv.Count(ItemDef.SlimeGel));
        }

        [Test]
        public void TabsListItemsOfTheirKindInPickupOrder()
        {
            var inv = new Inventory();
            inv.Add(ItemDef.CapshroomCap);
            inv.Add(ItemDef.BluePotion);
            inv.Add(ItemDef.SlimeGel);
            inv.Add(ItemDef.RedPotion);
            CollectionAssert.AreEqual(new[] { ItemDef.BluePotion, ItemDef.RedPotion }, inv.Items(ItemKind.Use).ToArray());
            CollectionAssert.AreEqual(new[] { ItemDef.CapshroomCap, ItemDef.SlimeGel }, inv.Items(ItemKind.Etc).ToArray());

            inv.Remove(ItemDef.BluePotion);
            CollectionAssert.AreEqual(new[] { ItemDef.RedPotion }, inv.Items(ItemKind.Use).ToArray(), "empty stacks are hidden");
        }

        [Test]
        public void MesosNeverOverflow()
        {
            var inv = new Inventory();
            inv.AddMesos(int.MaxValue - 5);
            inv.AddMesos(100);
            Assert.AreEqual(int.MaxValue, inv.Mesos);
            inv.AddMesos(-50);
            Assert.AreEqual(int.MaxValue, inv.Mesos);
        }
    }

    public class DropTableTests
    {
        [Test]
        public void CertainDropsAlwaysAppearAndImpossibleOnesNever()
        {
            var table = new DropTable(1f, 10, 10,
                new DropEntry(ItemDef.SlimeGel, 1f),
                new DropEntry(ItemDef.RedPotion, 0f));
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var loot = table.Roll(rng);
                Assert.AreEqual(2, loot.Count);
                Assert.IsTrue(loot[0].IsMesos);
                Assert.AreEqual(10, loot[0].Mesos);
                Assert.AreSame(ItemDef.SlimeGel, loot[1].Item);
            }
        }

        [Test]
        public void MesoAmountsStayInRangeAndChancesAreRoughlyRight()
        {
            var table = MobDef.SproutSlime.Drops;
            var rng = new System.Random(11);
            int mesoDrops = 0, gels = 0;
            const int rolls = 5000;
            for (int i = 0; i < rolls; i++)
            {
                foreach (var loot in table.Roll(rng))
                {
                    if (loot.IsMesos)
                    {
                        mesoDrops++;
                        Assert.That(loot.Mesos, Is.InRange(table.MinMesos, table.MaxMesos));
                    }
                    else if (loot.Item == ItemDef.SlimeGel)
                    {
                        gels++;
                    }
                }
            }
            Assert.That(mesoDrops / (float)rolls, Is.InRange(0.55f, 0.65f));
            Assert.That(gels / (float)rolls, Is.InRange(0.40f, 0.50f));
        }

        [Test]
        public void EmptyTableDropsNothing()
        {
            Assert.IsEmpty(DropTable.None.Roll(new System.Random(1)));
        }
    }

    public class DropTests
    {
        MapData map;

        [SetUp]
        public void SetUp()
        {
            map = new MapData { MinX = 0f, MaxX = 30f, BottomY = -2f, TopY = 12f };
            map.Footholds.Add(new Foothold(0f, 30f, 0f, solid: true));
            map.Footholds.Add(new Foothold(5f, 12f, 2.5f));
        }

        static void Run(Drop drop, MapData map, float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++) drop.Tick(1f / 60f, map);
        }

        [Test]
        public void PopsUpThenRestsOnThePlatformItCameFrom()
        {
            var drop = new Drop(Loot.OfItem(ItemDef.SlimeGel), new Vector2(8f, 3f), new Vector2(0.5f, 7f));
            Assert.IsFalse(drop.CanBePickedUp, "can't grab it mid-air");
            Run(drop, map, 1.5f);
            Assert.IsTrue(drop.Resting);
            Assert.AreEqual(2.5f, drop.Position.y);
            Assert.IsTrue(drop.CanBePickedUp);
        }

        [Test]
        public void FallsOffPlatformEdgesToTheGround()
        {
            var drop = new Drop(Loot.OfMesos(5), new Vector2(11.8f, 3f), new Vector2(3f, 2f));
            Run(drop, map, 2f);
            Assert.IsTrue(drop.Resting);
            Assert.AreEqual(0f, drop.Position.y);
        }

        [Test]
        public void ExpiresAfterItsLifetime()
        {
            var drop = new Drop(Loot.OfMesos(5), new Vector2(3f, 0.5f), Vector2.zero);
            Run(drop, map, Drop.Lifetime - 1f);
            Assert.IsFalse(drop.Finished);
            Run(drop, map, 1.5f);
            Assert.IsTrue(drop.Finished);
            Assert.IsFalse(drop.CanBePickedUp);
        }

        [Test]
        public void PickupAnimationFinishesTheDrop()
        {
            var drop = new Drop(Loot.OfMesos(5), new Vector2(3f, 0.5f), Vector2.zero);
            Run(drop, map, 0.5f);
            drop.StartPickup();
            Assert.IsFalse(drop.CanBePickedUp, "can't be picked up twice");
            Run(drop, map, Drop.PickupAnimTime + 0.05f);
            Assert.IsTrue(drop.Finished);
        }
    }

    public class ManaTests
    {
        [Test]
        public void SpendingMpFailsWithoutEnough()
        {
            var stats = new PlayerStats();
            Assert.IsTrue(stats.SpendMp(15));
            Assert.AreEqual(5, stats.Mp);
            Assert.IsFalse(stats.SpendMp(6));
            Assert.AreEqual(5, stats.Mp, "a failed cast costs nothing");
        }

        [Test]
        public void HealingIsCappedAndReportsTheRealAmount()
        {
            var stats = new PlayerStats();
            stats.TakeDamage(30);
            Assert.AreEqual(30, stats.Heal(50));
            Assert.AreEqual(stats.MaxHp, stats.Hp);
            stats.SpendMp(5);
            Assert.AreEqual(5, stats.RestoreMp(50));
        }

        [Test]
        public void LevelUpRaisesAndRefillsMp()
        {
            var stats = new PlayerStats();
            stats.SpendMp(20);
            stats.GainExp(PlayerStats.ExpToNext(1));
            Assert.AreEqual(28, stats.MaxMp);
            Assert.AreEqual(28, stats.Mp);
        }

        [Test]
        public void BonusAttackRaisesDamage()
        {
            var stats = new PlayerStats();
            int before = stats.MaxDamage;
            stats.BonusAttack = 10;
            Assert.Greater(stats.MaxDamage, before);
            stats.BonusAttack = 0;
            Assert.AreEqual(before, stats.MaxDamage);
        }
    }
}
