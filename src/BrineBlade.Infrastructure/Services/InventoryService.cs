// src/BrineBlade.Infrastructure/Services/InventoryService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services
{
    /// <summary>
    /// Inventory + equipment logic with safe, content-driven checks.
    /// Pure mutations happen on GameState.Inventory and GameState.Equipment.
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private readonly ItemCatalog _items;
        private readonly ClassCatalog _classes;

        public InventoryService(ItemCatalog items, ClassCatalog classes)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _classes = classes ?? throw new ArgumentNullException(nameof(classes));
        }

        // ---------------------------
        // Inventory
        // ---------------------------
        public InvOpResult TryAdd(GameState state, string itemId, int qty = 1)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(itemId) || qty <= 0) return new(false, "invalid args");
            if (!_items.TryGet(itemId, out var def)) return new(false, $"unknown item '{itemId}'");

            int remaining = qty;
            if (def.Stackable)
            {
                foreach (var stack in state.Inventory.Where(s => s.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    if (remaining <= 0) break;
                    int canAdd = Math.Max(0, def.MaxStack - stack.Quantity);
                    if (canAdd <= 0) continue;

                    int add = Math.Min(canAdd, remaining);
                    ReplaceStackQuantity(state, stack, stack.Quantity + add);
                    remaining -= add;
                }
                while (remaining > 0)
                {
                    int add = Math.Min(def.MaxStack, remaining);
                    state.Inventory.Add(new ItemStack(itemId, add));
                    remaining -= add;
                }
            }
            else
            {
                for (int i = 0; i < remaining; i++)
                    state.Inventory.Add(new ItemStack(itemId, 1));
            }

            return new(true);
        }

        public InvOpResult TryRemove(GameState state, string itemId, int qty = 1)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(itemId) || qty <= 0) return new(false, "invalid args");

            int have = state.Inventory.Where(s => s.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                                      .Sum(s => s.Quantity);
            if (have < qty) return new(false, "not enough");

            int remaining = qty;
            foreach (var stack in state.Inventory.Where(s => s.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (remaining <= 0) break;
                int take = Math.Min(stack.Quantity, remaining);
                int newQty = stack.Quantity - take;
                if (newQty <= 0)
                    state.Inventory.Remove(stack);
                else
                    ReplaceStackQuantity(state, stack, newQty);
                remaining -= take;
            }
            return new(true);
        }

        public IReadOnlyList<(string ItemId, string Name, int Qty, bool Equipable)> BuildInventoryView(GameState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));

            return state.Inventory
                .GroupBy(s => s.ItemId, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var id = g.Key;
                    var qty = g.Sum(x => x.Quantity);
                    var name = _items.TryGet(id, out var def) ? def.Name : id;
                    var equipable = _items.TryGet(id, out var d2) && d2.Slot.HasValue;
                    return (ItemId: id, Name: name, Qty: qty, Equipable: equipable);
                })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ---------------------------
        // Equipment
        // ---------------------------
        public InvOpResult TryEquip(GameState state, string itemId, EquipmentSlot? targetSlot = null)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!_items.TryGet(itemId, out var def)) return new(false, "unknown item");
            if (def.Slot is null) return new(false, "not equipable");

            var slot = targetSlot ?? def.Slot.Value;

            var classId = GetCurrentClassId(state);
            if (def.AllowedClasses is { Count: > 0 } && classId is not null &&
                !def.AllowedClasses.Contains(classId, StringComparer.OrdinalIgnoreCase))
                return new(false, "class cannot equip");

            if (classId is not null && _classes.All.TryGetValue(classId, out var klass))
            {
                if (def.ArmorType.HasValue &&
                    klass.AllowedArmorTypes is { Count: > 0 } &&
                    !klass.AllowedArmorTypes.Contains(def.ArmorType.Value))
                    return new(false, "armor type not allowed");

                if (def.WeaponType.HasValue &&
                    klass.AllowedWeaponTypes is { Count: > 0 } &&
                    !klass.AllowedWeaponTypes.Contains(def.WeaponType.Value))
                    return new(false, "weapon type not allowed");
            }

            int have = state.Inventory.Where(s => s.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase)).Sum(s => s.Quantity);
            if (have <= 0) return new(false, "not in inventory");

            if (slot == EquipmentSlot.Weapon && def.WeaponType == WeaponType.TwoHanded)
            {
                if (state.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offId))
                {
                    state.Inventory.Add(new ItemStack(offId, 1));
                    state.Equipment.Remove(EquipmentSlot.Offhand);
                }
            }

            if (state.Equipment.TryGetValue(slot, out var oldId))
                state.Inventory.Add(new ItemStack(oldId, 1));

            TryRemove(state, itemId, 1);
            state.Equipment[slot] = itemId;

            return new(true);
        }

        public InvOpResult TryUnequip(GameState state, EquipmentSlot slot)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!state.Equipment.TryGetValue(slot, out var itemId))
                return new(false, "empty");

            state.Inventory.Add(new ItemStack(itemId, 1));
            state.Equipment.Remove(slot);
            return new(true);
        }

        public StatDelta ComputeEquipmentBonuses(GameState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));

            var bonus = new StatDelta();
            foreach (var kv in state.Equipment)
            {
                if (_items.TryGet(kv.Value, out var def) && def.Bonuses is not null)
                    bonus = bonus + def.Bonuses;
            }
            return bonus;
        }

        // Helpers
        private static void ReplaceStackQuantity(GameState s, ItemStack stack, int newQty)
        {
            var idx = s.Inventory.IndexOf(stack);
            if (idx >= 0)
                s.Inventory[idx] = new ItemStack(stack.ItemId, newQty);
        }

        private static string? GetCurrentClassId(GameState s)
        {
            var flag = s.Flags.FirstOrDefault(f => f.StartsWith("class.", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(flag))
                return "CLASS_" + flag["class.".Length..].ToUpperInvariant();

            return s.Player.Archetype?.ToUpperInvariant() switch
            {
                "WARRIOR" => "CLASS_WARRIOR",
                "MAGE" => "CLASS_MAGE",
                "ROGUE" => "CLASS_ROGUE",
                _ => null
            };
        }
    }
}
