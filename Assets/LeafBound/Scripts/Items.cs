using System.Collections.Generic;
using UnityEngine;

namespace LeafBound
{
    /// <summary>Use items are consumed from the Use tab; Etc items are loot to collect.</summary>
    public enum ItemKind { Use, Etc }

    public enum ItemIcon { RedPotion, BluePotion, SlimeGel, CapshroomCap }

    public sealed class ItemDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public ItemKind Kind;
        public ItemIcon Icon;
        public int HealHp;
        public int HealMp;

        public static readonly ItemDef RedPotion = new ItemDef
        {
            Id = "red_potion", Name = "Red Potion", Description = "Restores 50 HP.",
            Kind = ItemKind.Use, Icon = ItemIcon.RedPotion, HealHp = 50,
        };

        public static readonly ItemDef BluePotion = new ItemDef
        {
            Id = "blue_potion", Name = "Blue Potion", Description = "Restores 50 MP.",
            Kind = ItemKind.Use, Icon = ItemIcon.BluePotion, HealMp = 50,
        };

        public static readonly ItemDef SlimeGel = new ItemDef
        {
            Id = "slime_gel", Name = "Slime Gel", Description = "Sticky. Dropped by Sprout Slimes.",
            Kind = ItemKind.Etc, Icon = ItemIcon.SlimeGel,
        };

        public static readonly ItemDef CapshroomCap = new ItemDef
        {
            Id = "capshroom_cap", Name = "Capshroom Cap", Description = "Springy. Dropped by Capshrooms.",
            Kind = ItemKind.Etc, Icon = ItemIcon.CapshroomCap,
        };
    }

    /// <summary>One thing a monster dropped: either mesos (Item is null) or one item.</summary>
    public readonly struct Loot
    {
        public readonly ItemDef Item;
        public readonly int Mesos;

        Loot(ItemDef item, int mesos)
        {
            Item = item;
            Mesos = mesos;
        }

        public bool IsMesos => Item == null;
        public static Loot OfMesos(int amount) => new Loot(null, amount);
        public static Loot OfItem(ItemDef item) => new Loot(item, 0);
    }

    public readonly struct DropEntry
    {
        public readonly ItemDef Item;
        public readonly float Chance;

        public DropEntry(ItemDef item, float chance)
        {
            Item = item;
            Chance = chance;
        }
    }

    /// <summary>What a monster can drop. Each entry is rolled independently, as in MapleStory.</summary>
    public sealed class DropTable
    {
        public static readonly DropTable None = new DropTable(0f, 0, 0);

        public readonly float MesoChance;
        public readonly int MinMesos, MaxMesos;
        public readonly DropEntry[] Items;

        public DropTable(float mesoChance, int minMesos, int maxMesos, params DropEntry[] items)
        {
            MesoChance = mesoChance;
            MinMesos = minMesos;
            MaxMesos = Mathf.Max(minMesos, maxMesos);
            Items = items;
        }

        public List<Loot> Roll(System.Random rng)
        {
            var loot = new List<Loot>();
            if (MesoChance > 0f && rng.NextDouble() < MesoChance)
                loot.Add(Loot.OfMesos(Mathf.Max(1, rng.Next(MinMesos, MaxMesos + 1))));
            foreach (var entry in Items)
                if (rng.NextDouble() < entry.Chance) loot.Add(Loot.OfItem(entry.Item));
            return loot;
        }
    }

    /// <summary>Item stacks and mesos. Items keep the order they were first picked up in.</summary>
    public sealed class Inventory
    {
        public const int MaxStack = 9999;

        readonly Dictionary<ItemDef, int> counts = new Dictionary<ItemDef, int>();
        // Never shrinks, so the UI can iterate it while an item is being used up.
        readonly List<ItemDef> order = new List<ItemDef>();

        public int Mesos { get; private set; }

        public int Count(ItemDef item) => counts.TryGetValue(item, out int n) ? n : 0;

        public void AddMesos(int amount)
        {
            if (amount <= 0) return;
            Mesos = (int)System.Math.Min(int.MaxValue, (long)Mesos + amount);
        }

        /// <summary>Adds items; false (and nothing added) if that would overflow the stack.</summary>
        public bool Add(ItemDef item, int amount = 1)
        {
            if (amount <= 0) return true;
            int have = Count(item);
            if (have + amount > MaxStack) return false;
            if (!counts.ContainsKey(item)) order.Add(item);
            counts[item] = have + amount;
            return true;
        }

        /// <summary>Removes items; false (and nothing removed) if there are not enough.</summary>
        public bool Remove(ItemDef item, int amount = 1)
        {
            int have = Count(item);
            if (amount <= 0 || have < amount) return false;
            counts[item] = have - amount;
            return true;
        }

        public IEnumerable<ItemDef> Items(ItemKind kind)
        {
            foreach (var item in order)
                if (item.Kind == kind && Count(item) > 0) yield return item;
        }
    }
}
