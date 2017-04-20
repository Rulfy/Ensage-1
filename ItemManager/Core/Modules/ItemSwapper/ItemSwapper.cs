﻿namespace ItemManager.Core.Modules.ItemSwapper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Attributes;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Objects.UtilityObjects;

    using EventArgs;

    using Menus;
    using Menus.Modules.ItemSwap;

    using Utils;

    using Courier = Ensage.Courier;

    [Module]
    internal class ItemSwapper : IDisposable
    {
        private readonly List<Courier> couriers = new List<Courier>();

        private readonly Manager manager;

        private readonly ItemSwapMenu menu;

        private readonly Sleeper sleeper;

        private bool courierFollowing;

        private bool courierSwapActive;

        private bool itemsMovedToCourier;

        private List<Item> swappedItems = new List<Item>();

        public ItemSwapper(Manager manager, MenuManager menu)
        {
            this.manager = manager;
            this.menu = menu.ItemSwapMenu;
            sleeper = new Sleeper();

            this.menu.Backpack.OnSwap += BackpackOnSwap;
            this.menu.Stash.OnSwap += StashOnSwap;
            this.menu.Courier.OnSwap += CourierOnSwap;
            Unit.OnModifierAdded += OnModifierAdded;
            Entity.OnInt32PropertyChange += OnInt32PropertyChange;
            manager.OnItemAdd += OnItemAdd;
            manager.OnItemRemove += OnItemRemove;
            manager.OnUnitAdd += OnUnitAdd;
        }

        public void Dispose()
        {
            menu.Backpack.OnSwap -= BackpackOnSwap;
            menu.Stash.OnSwap -= StashOnSwap;
            menu.Courier.OnSwap -= CourierOnSwap;
            Unit.OnModifierAdded -= OnModifierAdded;
            Entity.OnInt32PropertyChange -= OnInt32PropertyChange;
            manager.OnItemAdd -= OnItemAdd;
            manager.OnItemRemove -= OnItemRemove;
            manager.OnUnitAdd -= OnUnitAdd;

            couriers.Clear();
        }

        private void BackpackOnSwap(object sender, EventArgs eventArgs)
        {
            var inventoryItems = manager.GetMyItems(ItemUtils.StoredPlace.Inventory)
                .Where(x => menu.Backpack.ItemEnabled(x.Name) && x.CanBeMoved())
                .ToList();

            var backpackItems = manager.GetMyItems(ItemUtils.StoredPlace.Backpack)
                .Where(x => menu.Backpack.ItemEnabled(x.Name))
                .ToList();

            MoveItems(inventoryItems, backpackItems, ItemUtils.StoredPlace.Backpack);
            MoveItems(backpackItems, inventoryItems, ItemUtils.StoredPlace.Inventory);
        }

        private void CourierOnSwap(object sender, EventArgs eventArgs)
        {
            if (courierSwapActive)
            {
                courierFollowing = false;
                itemsMovedToCourier = false;
                return;
            }

            courierSwapActive = true;
            Game.OnUpdate += OnUpdate;
        }

        private void DisableCourierItemSwap()
        {
            courierFollowing = false;
            courierSwapActive = false;
            itemsMovedToCourier = false;
            Game.OnUpdate -= OnUpdate;
        }

        private void MoveItem(
            Item item,
            ItemSlot slot,
            ICollection<ItemSlot> freeSlots,
            ICollection<ItemSlot> allSlots,
            ItemUtils.StoredPlace direction)
        {
            if (direction != ItemUtils.StoredPlace.Inventory)
            {
                manager.SaveItemSlot(item);
            }
            else
            {
                var savedSlot = manager.GetSavedSlot(item);
                if (savedSlot != null)
                {
                    slot = savedSlot.Value;
                }
            }

            item.MoveItem(slot);
            freeSlots.Remove(slot);
            allSlots.Remove(slot);
        }

        private void MoveItems(Unit source, Unit target, IReadOnlyCollection<Item> itemsToMove)
        {
            if (!itemsToMove.Any())
            {
                return;
            }

            var count = Math.Min(target.Inventory.FreeInventorySlots.Count(), itemsToMove.Count);
            for (var i = 0; i < count; i++)
            {
                source.GiveItem(itemsToMove.ElementAt(i), target, i != 0);
            }
        }

        private void MoveItems(IEnumerable<Item> from, ICollection<Item> to, ItemUtils.StoredPlace direction)
        {
            var freeSlots = new List<ItemSlot>();
            var allSlots = new List<ItemSlot>();

            switch (direction)
            {
                case ItemUtils.StoredPlace.Inventory:
                {
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var itemSlot in Enum.GetValues(typeof(InventorySlot)).Cast<ItemSlot>())
                    {
                        allSlots.Add(itemSlot);
                    }
                    freeSlots.AddRange(manager.MyHero.Inventory.FreeInventorySlots);
                    break;
                }
                case ItemUtils.StoredPlace.Backpack:
                {
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var itemSlot in Enum.GetValues(typeof(BackpackSlot)).Cast<ItemSlot>())
                    {
                        allSlots.Add(itemSlot);
                    }
                    freeSlots.AddRange(manager.MyHero.Inventory.FreeBackpackSlots);
                    break;
                }
                case ItemUtils.StoredPlace.Stash:
                {
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var itemSlot in Enum.GetValues(typeof(StashSlot)).Cast<ItemSlot>())
                    {
                        allSlots.Add(itemSlot);
                    }
                    freeSlots.AddRange(manager.MyHero.Inventory.FreeStashSlots);
                    break;
                }
            }

            foreach (var item in from)
            {
                var swapItem = to.FirstOrDefault();

                if (swapItem != null)
                {
                    var slot = manager.GetSlot(swapItem.Handle, direction);
                    if (slot != null)
                    {
                        item.MoveItem(slot.Value);
                    }

                    to.Remove(swapItem);
                }
                else
                {
                    if (freeSlots.Any())
                    {
                        MoveItem(item, freeSlots.First(), freeSlots, allSlots, direction);
                    }
                    else if (allSlots.Any() && menu.ForceItemSwap)
                    {
                        MoveItem(item, allSlots.First(), freeSlots, allSlots, direction);
                    }
                }
            }
        }

        private void OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            if (!courierSwapActive || args.OldValue == args.NewValue || args.PropertyName != "m_nCourierState")
            {
                return;
            }

            var courier = sender as Courier;
            if (courier == null || !courier.IsAlive)
            {
                return;
            }

            DisableCourierItemSwap();
        }

        private void OnItemAdd(object sender, ItemEventArgs itemEventArgs)
        {
            if (!itemEventArgs.IsMine || itemEventArgs.Item.IsRecipe)
            {
                return;
            }

            menu.AddItem(itemEventArgs.Item.Name);
        }

        private void OnItemRemove(object sender, ItemEventArgs itemEventArgs)
        {
            var item = itemEventArgs.Item;

            if (!itemEventArgs.IsMine || item.IsRecipe || manager.GetMyItems(ItemUtils.StoredPlace.Any)
                    .Any(x => x.Id == item.Id))
            {
                return;
            }

            menu.RemoveItem(itemEventArgs.Item.Name);

            if (!menu.Auto.SwapCheeseAegis || item.Id != AbilityId.item_cheese && item.Id != AbilityId.item_aegis)
            {
                return;
            }

            var freeSlots = manager.MyHero.Inventory.FreeInventorySlots.ToList();
            if (!freeSlots.Any())
            {
                return;
            }

            var backpackItem = manager.GetBestBackpackItem();
            backpackItem?.MoveItem(freeSlots.First());
        }

        private void OnModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            if (!menu.Auto.SwapTpScroll || sender.Handle != manager.MyHandle
                || args.Modifier.TextureName != "item_tpscroll")
            {
                return;
            }

            var backpackItem = manager.GetBestBackpackItem();
            if (backpackItem == null)
            {
                return;
            }

            var scrollSlot = manager.GetSlot(AbilityId.item_tpscroll, ItemUtils.StoredPlace.Inventory);
            if (scrollSlot != null)
            {
                backpackItem.MoveItem(scrollSlot.Value);
            }
        }

        private void OnUnitAdd(object sender, UnitEventArgs unitEventArgs)
        {
            var courier = unitEventArgs.Unit as Courier;
            if (courier != null && courier.Team == manager.MyTeam)
            {
                couriers.Add(courier);
            }
        }

        private void OnUpdate(EventArgs args)
        {
            if (sleeper.Sleeping)
            {
                return;
            }

            var courier = couriers.FirstOrDefault(x => x.IsValid && x.IsAlive);
            if (courier == null || !manager.MyHero.IsAlive)
            {
                DisableCourierItemSwap();
                return;
            }

            var inventoryItems = manager.GetMyItems(ItemUtils.StoredPlace.Inventory)
                .Where(x => menu.Courier.ItemEnabled(x.Name) && x.CanBeMoved())
                .ToList();

            var courierItems = courier.Inventory.Items
                .Where(x => x.Purchaser?.Hero?.Handle == manager.MyHandle && menu.Courier.ItemEnabled(x.Name))
                .ToList();

            if (!inventoryItems.Any() && !courierItems.Any())
            {
                DisableCourierItemSwap();
                return;
            }

            if (!courierFollowing)
            {
                courier.Follow(manager.MyHero);
                courierFollowing = true;
            }

            if (courier.Distance2D(manager.MyHero) > 250)
            {
                sleeper.Sleep(200);
                return;
            }

            if (!itemsMovedToCourier)
            {
                swappedItems = inventoryItems;
                itemsMovedToCourier = true;

                if (inventoryItems.Any())
                {
                    MoveItems(manager.MyHero, courier, inventoryItems);
                    sleeper.Sleep(300);
                    return;
                }
            }

            MoveItems(courier, manager.MyHero, courierItems.Where(x => !swappedItems.Contains(x)).ToList());
            DisableCourierItemSwap();
        }

        private void StashOnSwap(object sender, EventArgs eventArgs)
        {
            if (manager.MyHero.ActiveShop != ShopType.Base)
            {
                return;
            }

            var inventoryItems = manager.GetMyItems(ItemUtils.StoredPlace.Inventory)
                .Where(x => menu.Stash.ItemEnabled(x.Name) && x.CanBeMoved())
                .ToList();

            var stashItems = manager.GetMyItems(ItemUtils.StoredPlace.Stash)
                .Where(x => menu.Stash.ItemEnabled(x.Name))
                .ToList();

            MoveItems(inventoryItems, stashItems, ItemUtils.StoredPlace.Stash);
            MoveItems(stashItems, inventoryItems, ItemUtils.StoredPlace.Inventory);
        }
    }
}