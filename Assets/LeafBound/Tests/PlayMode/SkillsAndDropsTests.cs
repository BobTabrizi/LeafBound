using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LeafBound.Tests
{
    /// <summary>Skills, potions and loot, run through the real Game with scripted input.</summary>
    public class SkillsAndDropsTests
    {
        const float Dt = 1f / 60f;

        Game game;
        ScriptedInput input;

        static MobDef Target(int hp, DropTable drops = null, int exp = 0) => new MobDef
        {
            Name = "Target", Level = 1, MaxHp = hp, TouchDamage = 1, Exp = exp,
            Speed = 0f, HalfWidth = 0.5f, Height = 0.8f, Look = MobLook.Slime,
            Drops = drops ?? DropTable.None,
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            game = new GameObject("Game").AddComponent<Game>();
            input = new ScriptedInput();
            game.Controls = input;
            game.ClearMobs();
            Run(0.5f); // land on the ground
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(game.gameObject);
            yield return null;
        }

        void Run(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) game.Tick(Dt);
        }

        float PlayerX => game.Player.Motor.Position.x;

        Mob Spawn(MobDef def, float dx) => game.SpawnMob(new MobSpawn(def, 0, PlayerX + dx));

        /// <summary>Holds an action for one swing and runs until its hit has landed.</summary>
        void Swing(GameAction action)
        {
            input.Set(action, true);
            game.Tick(Dt);
            input.Clear();
            for (int i = 0; i < 60 && !game.Player.AttackHitDone; i++) game.Tick(Dt);
            Assert.IsTrue(game.Player.AttackHitDone, "the swing should have landed");
        }

        [UnityTest]
        public IEnumerator PowerStrikeCostsMpAndHitsHarder()
        {
            var stats = game.Player.Stats;
            Assert.IsTrue(game.LearnSkill(SkillId.PowerStrike));
            var mob = Spawn(Target(9999), 1.2f);
            int mpBefore = stats.Mp;

            Swing(GameAction.Skill1);

            Assert.AreEqual(mpBefore - SkillDef.PowerStrike.MpCost(1), stats.Mp);
            int dealt = mob.Def.MaxHp - mob.Hp;
            int percent = SkillDef.PowerStrike.DamagePercent(1);
            int lowest = Mathf.RoundToInt(stats.MinDamage * percent / 100f);
            int highest = Mathf.RoundToInt(Mathf.RoundToInt(stats.MaxDamage * PlayerStats.CritMultiplier) * percent / 100f);
            Assert.That(dealt, Is.InRange(lowest, highest));
            Assert.Greater(lowest, stats.MinDamage, "Power Strike should out-hit a basic attack");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlashBlastHitsSeveralMonsters()
        {
            game.LearnSkill(SkillId.SlashBlast);
            var a = Spawn(Target(9999), 0.9f);
            var b = Spawn(Target(9999), 1.5f);
            var c = Spawn(Target(9999), 2.1f);

            Swing(GameAction.Skill2);

            Assert.Less(a.Hp, a.Def.MaxHp);
            Assert.Less(b.Hp, b.Def.MaxHp);
            Assert.Less(c.Hp, c.Def.MaxHp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BasicAttackHitsOnlyOneMonster()
        {
            var a = Spawn(Target(9999), 0.9f);
            var b = Spawn(Target(9999), 1.3f);
            Swing(GameAction.Attack);
            Assert.Less(a.Hp, a.Def.MaxHp, "nearest monster is hit");
            Assert.AreEqual(b.Def.MaxHp, b.Hp, "the one behind it is not");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkillsNeedToBeLearnedAndNeedMp()
        {
            var stats = game.Player.Stats;
            var mob = Spawn(Target(9999), 1.2f);

            input.Set(GameAction.Skill1, true);
            Run(0.5f);
            Assert.AreEqual(mob.Def.MaxHp, mob.Hp, "unlearned skill does nothing");

            game.LearnSkill(SkillId.PowerStrike);
            stats.SpendMp(stats.Mp);
            Run(0.5f);
            input.Clear();
            Assert.AreEqual(mob.Def.MaxHp, mob.Hp, "no MP, no Power Strike");
            Assert.AreEqual(0, stats.Mp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RageRaisesAttackThenWearsOff()
        {
            var stats = game.Player.Stats;
            game.LearnSkill(SkillId.Rage);
            int maxBefore = stats.MaxDamage;

            Swing(GameAction.Skill3);

            Assert.AreEqual(SkillDef.Rage.AttackBonus(1), stats.BonusAttack);
            Assert.Greater(stats.MaxDamage, maxBefore);
            Run(SkillDef.Rage.Duration(1) + 0.5f);
            Assert.AreEqual(0, stats.BonusAttack);
            Assert.AreEqual(maxBefore, stats.MaxDamage);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PotionsRestoreAndAreUsedUp()
        {
            var p = game.Player;
            int reds = p.Inventory.Count(ItemDef.RedPotion);
            p.Stats.TakeDamage(30);

            input.Set(GameAction.HpPotion, true);
            Run(0.2f); // shorter than the potion delay: exactly one potion
            input.Clear();

            Assert.AreEqual(p.Stats.MaxHp, p.Stats.Hp);
            Assert.AreEqual(reds - 1, p.Inventory.Count(ItemDef.RedPotion));

            int blues = p.Inventory.Count(ItemDef.BluePotion);
            p.Stats.SpendMp(15);
            Run(0.5f);
            Assert.IsTrue(game.UseItem(ItemDef.BluePotion));
            Assert.AreEqual(p.Stats.MaxMp, p.Stats.Mp);
            Assert.AreEqual(blues - 1, p.Inventory.Count(ItemDef.BluePotion));
            yield return null;
        }

        [UnityTest]
        public IEnumerator KillsDropLootThatZPicksUp()
        {
            var table = new DropTable(1f, 10, 10, new DropEntry(ItemDef.SlimeGel, 1f));
            var mob = Spawn(Target(1, table, exp: 20), 1.2f);
            Swing(GameAction.Attack);
            Assert.IsTrue(mob.IsDead);
            Assert.AreEqual(2, game.Drops.Count, "mesos and one Slime Gel");
            Assert.AreEqual(Player.StartingSkillPoints + Player.SkillPointsPerLevel, game.Player.Skills.Points,
                "the level-up grants skill points");

            Run(1f); // let the loot land
            foreach (var drop in game.Drops) Assert.IsTrue(drop.CanBePickedUp);

            for (int i = 0; i < 60 * 5 && game.Drops.Count > 0; i++)
            {
                input.Clear();
                Drop nearest = null;
                foreach (var drop in game.Drops)
                    if (drop.CanBePickedUp && (nearest == null || Mathf.Abs(drop.Position.x - PlayerX) < Mathf.Abs(nearest.Position.x - PlayerX)))
                        nearest = drop;
                if (nearest != null)
                {
                    float dx = nearest.Position.x - PlayerX;
                    if (Mathf.Abs(dx) > 0.3f) input.Set(dx > 0f ? GameAction.Right : GameAction.Left, true);
                    else input.Set(GameAction.Pickup, true);
                }
                game.Tick(Dt);
            }
            input.Clear();

            Assert.AreEqual(0, game.Drops.Count);
            Assert.AreEqual(10, game.Player.Inventory.Mesos);
            Assert.AreEqual(1, game.Player.Inventory.Count(ItemDef.SlimeGel));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WindowsRenderWithoutErrors()
        {
            game.Hud.ShowInventory = true;
            game.Hud.ShowSkills = true;
            game.Player.Inventory.Add(ItemDef.SlimeGel, 3);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(game.Hud.ShowInventory && game.Hud.ShowSkills);
        }
    }
}
