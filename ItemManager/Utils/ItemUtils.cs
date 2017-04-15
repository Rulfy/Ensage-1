﻿namespace ItemManager.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage;
    using Ensage.Items;

    internal static class ItemUtils
    {
        [Flags]
        public enum Stats
        {
            None = 0,

            Any = 1,

            Health = Any | 2,

            Mana = Any | 4,

            All = Health | Mana
        }

        [Flags]
        public enum StoredPlace
        {
            Inventory = 1,

            Backpack = 2,

            Stash = 4,

            Any = Inventory | Backpack | Stash
        }

        private static readonly List<string> BonusAllStats = new List<string>
        {
            "bonus_all_stats",
            "bonus_stats"
        };

        private static readonly List<string> BonusHealth = new List<string>
        {
            "bonus_strength",
            "bonus_str",
            "bonus_health"
        };

        private static readonly List<string> BonusMana = new List<string>
        {
            "bonus_intellect",
            "bonus_int",
            "bonus_mana"
        };

        private static readonly Dictionary<AbilityId, ShopFlags> ItemShops = new Dictionary<AbilityId, ShopFlags>
        {
            { AbilityId.item_blink, ShopFlags.Base | ShopFlags.Side }, //Blink Dagger
            { AbilityId.item_blades_of_attack, ShopFlags.Base | ShopFlags.Side }, //Blades of Attack
            { AbilityId.item_broadsword, ShopFlags.Base | ShopFlags.Side }, //Broadsword
            { AbilityId.item_chainmail, ShopFlags.Base | ShopFlags.Side }, //Chainmail
            { AbilityId.item_helm_of_iron_will, ShopFlags.Base | ShopFlags.Side }, //Helm of Iron Will
            { AbilityId.item_platemail, ShopFlags.Secret }, //Platemail
            { AbilityId.item_quarterstaff, ShopFlags.Base | ShopFlags.Side }, //Quarterstaff
            { AbilityId.item_quelling_blade, ShopFlags.Base | ShopFlags.Side }, //Quelling Blade
            { AbilityId.item_belt_of_strength, ShopFlags.Base | ShopFlags.Side }, //Belt of Strength
            { AbilityId.item_boots_of_elves, ShopFlags.Base | ShopFlags.Side }, //Band of Elvenskin
            { AbilityId.item_robe, ShopFlags.Base | ShopFlags.Side }, //Robe of the Magi
            { AbilityId.item_ultimate_orb, ShopFlags.Secret }, //Ultimate Orb
            { AbilityId.item_gloves, ShopFlags.Base | ShopFlags.Side }, //Gloves of Haste
            { AbilityId.item_lifesteal, ShopFlags.Base | ShopFlags.Side }, //Morbid Mask
            { AbilityId.item_ring_of_regen, ShopFlags.Base | ShopFlags.Side }, //Ring of Regen
            { AbilityId.item_sobi_mask, ShopFlags.Base | ShopFlags.Side }, //Sage's Mask
            { AbilityId.item_boots, ShopFlags.Base | ShopFlags.Side }, //Boots of Speed
            { AbilityId.item_cloak, ShopFlags.Base | ShopFlags.Side }, //Cloak
            { AbilityId.item_talisman_of_evasion, ShopFlags.Secret }, //Talisman of Evasion
            { AbilityId.item_magic_stick, ShopFlags.Base | ShopFlags.Side }, //Magic Stick
            { AbilityId.item_bottle, ShopFlags.Base | ShopFlags.Side }, //Bottle
            { AbilityId.item_tpscroll, ShopFlags.Base | ShopFlags.Side }, //Town Portal Scroll
            { AbilityId.item_demon_edge, ShopFlags.Secret }, //Demon Edge
            { AbilityId.item_eagle, ShopFlags.Secret }, //Eaglesong
            { AbilityId.item_reaver, ShopFlags.Secret }, //Reaver
            { AbilityId.item_relic, ShopFlags.Secret }, //Sacred Relic
            { AbilityId.item_hyperstone, ShopFlags.Secret }, //Hyperstone
            { AbilityId.item_ring_of_health, ShopFlags.Base | ShopFlags.Side }, //Ring of Health
            { AbilityId.item_void_stone, ShopFlags.Base | ShopFlags.Side }, //Void Stone
            { AbilityId.item_mystic_staff, ShopFlags.Secret }, //Mystic Staff
            { AbilityId.item_energy_booster, ShopFlags.Side | ShopFlags.Secret }, //Energy Booster
            { AbilityId.item_point_booster, ShopFlags.Secret }, //Point Booster
            { AbilityId.item_vitality_booster, ShopFlags.Side | ShopFlags.Secret }, //Vitality Booster
            { AbilityId.item_orb_of_venom, ShopFlags.Base | ShopFlags.Side }, //Orb of Venom
            { AbilityId.item_stout_shield, ShopFlags.Base | ShopFlags.Side } //Stout Shield
        };

        private static readonly Dictionary<AbilityId, Stats> SavedStats = new Dictionary<AbilityId, Stats>();

        public static bool CanBeMoved(this Item item)
        {
            switch (item.Id)
            {
                case AbilityId.item_aegis:
                case AbilityId.item_gem:
                case AbilityId.item_rapier:
                {
                    return false;
                }
                case AbilityId.item_bottle:
                {
                    if (((Bottle)item).StoredRune != RuneType.None)
                    {
                        return false;
                    }
                    break;
                }
            }

            return true;
        }

        public static Stats GetItemStats(this Item item)
        {
            Stats stats;
            if (SavedStats.TryGetValue(item.Id, out stats))
            {
                return stats;
            }

            if (item.AbilitySpecialData.Any(x => BonusAllStats.Contains(x.Name)))
            {
                stats = Stats.Health | Stats.Mana;
            }
            else
            {
                if (item.AbilitySpecialData.Any(x => BonusHealth.Contains(x.Name)))
                {
                    stats |= Stats.Health;
                }
                if (item.AbilitySpecialData.Any(x => BonusMana.Contains(x.Name)))
                {
                    stats |= Stats.Mana;
                }
            }

            SavedStats.Add(item.Id, stats);
            return stats;
        }

        public static bool IsEmptyBottle(this Item item)
        {
            return item.Id == AbilityId.item_bottle && item.CurrentCharges == 0;
        }

        public static bool IsPurchasable(this AbilityId id, Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            var stockInfo = Game.StockInfo.FirstOrDefault(x => x.AbilityId == id && x.Team == unit.Team);
            if (stockInfo != null && stockInfo.StockCount <= 0)
            {
                return false;
            }

            var shop = unit.ActiveShop;
            var shopFlags = ShopFlags.None;

            switch (shop)
            {
                case ShopType.Base:
                {
                    shopFlags = ShopFlags.Base;
                    break;
                }
                case ShopType.Side:
                {
                    shopFlags = ShopFlags.Side;
                    break;
                }
                case ShopType.Secret:
                {
                    shopFlags = ShopFlags.Secret;
                    break;
                }
            }

            ShopFlags itemFlags;
            if (!ItemShops.TryGetValue(id, out itemFlags))
            {
                itemFlags = ShopFlags.Base;
            }

            return itemFlags.HasFlag(shopFlags);
        }

        public static bool IsTango(this Item item)
        {
            return item.Id == AbilityId.item_tango_single || item.Id == AbilityId.item_tango;
        }
    }
}