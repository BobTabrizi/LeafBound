using UnityEngine;

namespace LeafBound
{
    public enum SkillId { PowerStrike, SlashBlast, Rage }

    /// <summary>A warrior skill and how it scales with level. Pure data and formulas.</summary>
    public sealed class SkillDef
    {
        public readonly SkillId Id;
        public readonly string Name;
        public readonly int MaxLevel;

        SkillDef(SkillId id, string name, int maxLevel)
        {
            Id = id;
            Name = name;
            MaxLevel = maxLevel;
        }

        public static readonly SkillDef PowerStrike = new SkillDef(SkillId.PowerStrike, "Power Strike", 10);
        public static readonly SkillDef SlashBlast = new SkillDef(SkillId.SlashBlast, "Slash Blast", 10);
        public static readonly SkillDef Rage = new SkillDef(SkillId.Rage, "Rage", 10);

        /// <summary>Indexed by SkillId.</summary>
        public static readonly SkillDef[] All = { PowerStrike, SlashBlast, Rage };

        public static SkillDef Get(SkillId id) => All[(int)id];

        public int MpCost(int level)
        {
            if (level <= 0) return 0;
            switch (Id)
            {
                case SkillId.PowerStrike: return 4 + level / 2;
                case SkillId.SlashBlast: return 6 + level / 2;
                default: return 10 + level;
            }
        }

        /// <summary>Damage as a percentage of a normal hit (0 for buffs).</summary>
        public int DamagePercent(int level)
        {
            if (level <= 0) return 0;
            switch (Id)
            {
                case SkillId.PowerStrike: return 155 + 10 * level;
                case SkillId.SlashBlast: return 60 + 5 * level;
                default: return 0;
            }
        }

        public int MaxTargets(int level)
        {
            if (level <= 0) return 0;
            switch (Id)
            {
                case SkillId.PowerStrike: return 1;
                case SkillId.SlashBlast: return Mathf.Min(6, 2 + (level + 2) / 3);
                default: return 0;
            }
        }

        public int AttackBonus(int level) => Id == SkillId.Rage && level > 0 ? 2 * level : 0;
        public float Duration(int level) => Id == SkillId.Rage && level > 0 ? 40f + 8f * level : 0f;

        public string Describe(int level)
        {
            switch (Id)
            {
                case SkillId.PowerStrike:
                    return $"{DamagePercent(level)}% damage to one monster. MP {MpCost(level)}";
                case SkillId.SlashBlast:
                    return $"{DamagePercent(level)}% damage to up to {MaxTargets(level)} monsters. MP {MpCost(level)}";
                default:
                    return $"+{AttackBonus(level)} attack for {Duration(level):0} sec. MP {MpCost(level)}";
            }
        }
    }

    /// <summary>Skill levels and unspent skill points (SP).</summary>
    public sealed class SkillBook
    {
        readonly int[] levels = new int[SkillDef.All.Length];

        public SkillBook(int startingPoints)
        {
            Points = Mathf.Max(0, startingPoints);
        }

        public int Points { get; private set; }

        public int Level(SkillId id) => levels[(int)id];

        public bool CanLearn(SkillId id) => Points > 0 && Level(id) < SkillDef.Get(id).MaxLevel;

        public bool Learn(SkillId id)
        {
            if (!CanLearn(id)) return false;
            levels[(int)id]++;
            Points--;
            return true;
        }

        public void AddPoints(int amount)
        {
            if (amount > 0) Points += amount;
        }
    }
}
