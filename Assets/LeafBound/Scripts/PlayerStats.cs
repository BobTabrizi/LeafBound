using UnityEngine;

namespace LeafBound
{
    /// <summary>Level, EXP, HP, MP and damage. Pure logic.</summary>
    public sealed class PlayerStats
    {
        public const int MaxLevel = 200;
        public const float CritChance = 0.08f;
        public const float CritMultiplier = 1.5f;

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int MaxHp { get; private set; } = 50;
        public int Hp { get; private set; } = 50;
        public int MaxMp { get; private set; } = 20;
        public int Mp { get; private set; } = 20;
        public int Str { get; private set; } = 12;
        public int Dex { get; private set; } = 5;
        public int WeaponAttack { get; private set; } = 17;

        /// <summary>Extra weapon attack from buffs such as Rage.</summary>
        public int BonusAttack { get; set; }

        public bool IsDead => Hp <= 0;
        public int TotalAttack => WeaponAttack + BonusAttack;
        public int ExpNeeded => ExpToNext(Level);
        public float ExpFraction => Level >= MaxLevel ? 1f : (float)Exp / ExpNeeded;

        /// <summary>MapleStory-like damage range: STR is the main stat, DEX the secondary.</summary>
        public int MaxDamage => Mathf.Max(1, (Str * 4 + Dex) * TotalAttack / 100);
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
                MaxMp += 8;
                Str += 3;
                Dex += 1;
            }
            if (Level >= MaxLevel) Exp = 0;
            if (gained > 0) HealFull();
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

        /// <summary>Spends MP if there is enough. Returns false (spending nothing) otherwise.</summary>
        public bool SpendMp(int amount)
        {
            if (amount <= 0) return true;
            if (Mp < amount) return false;
            Mp -= amount;
            return true;
        }

        /// <summary>Restores HP up to the maximum. Returns how much was actually restored.</summary>
        public int Heal(int amount)
        {
            int before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + Mathf.Max(0, amount));
            return Hp - before;
        }

        /// <summary>Restores MP up to the maximum. Returns how much was actually restored.</summary>
        public int RestoreMp(int amount)
        {
            int before = Mp;
            Mp = Mathf.Min(MaxMp, Mp + Mathf.Max(0, amount));
            return Mp - before;
        }

        public void HealFull()
        {
            Hp = MaxHp;
            Mp = MaxMp;
        }
    }
}
