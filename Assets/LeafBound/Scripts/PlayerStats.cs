using UnityEngine;

namespace LeafBound
{
    /// <summary>Level, EXP, HP and damage. Pure logic.</summary>
    public sealed class PlayerStats
    {
        public const int MaxLevel = 200;
        public const float CritChance = 0.08f;
        public const float CritMultiplier = 1.5f;

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int MaxHp { get; private set; } = 50;
        public int Hp { get; private set; } = 50;
        public int Str { get; private set; } = 12;
        public int Dex { get; private set; } = 5;
        public int WeaponAttack { get; private set; } = 17;

        public bool IsDead => Hp <= 0;
        public int ExpNeeded => ExpToNext(Level);
        public float ExpFraction => Level >= MaxLevel ? 1f : (float)Exp / ExpNeeded;

        /// <summary>MapleStory-like damage range: STR is the main stat, DEX the secondary.</summary>
        public int MaxDamage => Mathf.Max(1, (Str * 4 + Dex) * WeaponAttack / 100);
        public int MinDamage => Mathf.Max(1, Mathf.RoundToInt(MaxDamage * 0.6f));

        public static int ExpToNext(int level) => Mathf.Max(1, Mathf.RoundToInt(15f * Mathf.Pow(level, 1.6f)));

        /// <summary>Adds EXP and applies every level-up it earns. Returns how many levels were gained.</summary>
        public int GainExp(int amount)
        {
            if (amount <= 0 || Level >= MaxLevel) return 0;
            Exp += amount;
            int gained = 0;
            while (Level < MaxLevel && Exp >= ExpNeeded)
            {
                Exp -= ExpNeeded;
                Level++;
                gained++;
                MaxHp += 16;
                Str += 3;
                Dex += 1;
            }
            if (Level >= MaxLevel) Exp = 0;
            if (gained > 0) Hp = MaxHp;
            return gained;
        }

        public int RollDamage(System.Random rng, out bool critical)
        {
            int damage = rng.Next(MinDamage, MaxDamage + 1);
            critical = rng.NextDouble() < CritChance;
            if (critical) damage = Mathf.RoundToInt(damage * CritMultiplier);
            return damage;
        }

        /// <summary>Returns true if this damage knocked the player out.</summary>
        public bool TakeDamage(int amount)
        {
            Hp = Mathf.Max(0, Hp - Mathf.Max(1, amount));
            return Hp == 0;
        }

        public void HealFull() => Hp = MaxHp;
    }
}
