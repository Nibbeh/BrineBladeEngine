using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly ItemCatalog _items;
        private readonly ClassCatalog _classes; // if your DI doesn't provide this, you can stub Allowed* checks out.

        public InventoryService(ItemCatalog items, ClassCatalog classes)
        {
            _items = items;
            _classes = classes;
        }

        // ---------------------------
        // Inventory
        // ---------------------------
        public InvOpResult TryAdd(GameState state, string itemId, int qty = 1)
        {
            if (qty <= 0) return new(false, "qty<=0");
            if (!_items.TryGet(itemId, out var def)) return new(false, "unknown item");

            var remaining = qty;

            if (def.Stackable)
            {
                // top-up existing stacks by replacing records (init-only safe)
                for (int i = 0; i < state.Inventory.Count && remaining > 0; i++)
                {
                    var st = state.Inventory[i];
                    if (!string.Equals(st.ItemId, itemId, StringComparison.OrdinalIgnoreCase)) continue;

                    var canAdd = Math.Max(0, def.MaxStack - st.Quantity);
                    var add = Math.Min(canAdd, remaining);
                    if (add <= 0) continue;

                    state.Inventory[i] = new ItemStack(st.ItemId, st.Quantity + add);
                    remaining -= add;
                }
            }

            // add new stacks as needed
            while (remaining > 0)
            {
                var add = def.Stackable ? Math.Min(def.MaxStack, remaining) : 1;
                state.Inventory.Add(new ItemStack(itemId, add));
                remaining -= add;
                if (!def.Stackable) break;
            }

            return new(true);
        }

        public InvOpResult TryRemove(GameState state, string itemId, int qty = 1)
        {
            if (qty <= 0) return new(false, "qty<=0");

            var total = state.Inventory.Where(s => string.Equals(s.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                                       .Sum(s => s.Quantity);
            if (total < qty) return new(false, "not enough");

            var left = qty;

            // remove from last-in stacks first
            for (int i = state.Inventory.Count - 1; i >= 0 && left > 0; i--)
            {
                var st = state.Inventory[i];
                if (!string.Equals(st.ItemId, itemId, StringComparison.OrdinalIgnoreCase)) continue;

                var take = Math.Min(st.Quantity, left);
                var newQty = st.Quantity - take;
                left -= take;

                if (newQty > 0)
                    state.Inventory[i] = new ItemStack(st.ItemId, newQty);
                else
                    state.Inventory.RemoveAt(i);
            }

            return new(true);
        }

        public IReadOnlyList<(string ItemId, string Name, int Qty, bool Equipable)> BuildInventoryView(GameState state)
        {
            var list = new List<(string, string, int, bool)>();
            foreach (var st in state.Inventory)
            {
                if (!_items.TryGet(st.ItemId, out var d)) continue; // skip unknown ids
                var equipable = d.Slot is not null;
                list.Add((st.ItemId, d.Name, st.Quantity, equipable));
            }

            var equippedIds = state.Equipment.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Use Item1/Item2/... to avoid named-tuple property binding glitches
            return list
                .OrderByDescending(r => equippedIds.Contains(r.Item1)) // Item1 = ItemId
                .ThenBy(r => r.Item2, StringComparer.CurrentCultureIgnoreCase) // Item2 = Name
                .Select(r => (ItemId: r.Item1, Name: r.Item2, Qty: r.Item3, Equipable: r.Item4))
                .ToList();
        }

        // ---------------------------
        // Equipment
        // ---------------------------
        public InvOpResult TryEquip(GameState state, string itemId, EquipmentSlot? targetSlot = null)
        {
            if (!_items.TryGet(itemId, out var def)) return new(false, "unknown item");
            if (def.Slot is null) return new(false, "not equipable");

            var slot = targetSlot ?? def.Slot.Value;

            // Optional class/armor/weapon gates (safe if catalogs exist)
            var classId = FindPlayerClassId(state);
            if (classId is not null && _classes.All.TryGetValue(classId, out var clazz))
            {
                if (def.AllowedClasses is { Count: > 0 } && !def.AllowedClasses.Contains(clazz.Id))
                    return new(false, "class not allowed");

                if (def.ArmorType is not null && clazz.AllowedArmorTypes is { Count: > 0 } &&
                    !clazz.AllowedArmorTypes.Contains(def.ArmorType.Value))
                    return new(false, "armor type not permitted");

                if (def.WeaponType is not null && clazz.AllowedWeaponTypes is { Count: > 0 } &&
                    !clazz.AllowedWeaponTypes.Contains(def.WeaponType.Value))
                    return new(false, "weapon type not permitted");
            }

            // 2H weapon vs offhand exclusivity
            if (slot == EquipmentSlot.Offhand &&
                state.Equipment.TryGetValue(EquipmentSlot.Weapon, out var wId) &&
                _items.TryGet(wId, out var wDef) &&
                wDef.WeaponType == WeaponType.TwoHanded)
            {
                return new(false, "offhand blocked by 2H weapon");
            }

            if (slot == EquipmentSlot.Weapon &&
                def.WeaponType == WeaponType.TwoHanded &&
                state.Equipment.ContainsKey(EquipmentSlot.Offhand))
            {
                var offId = state.Equipment[EquipmentSlot.Offhand];
                state.Equipment.Remove(EquipmentSlot.Offhand);
                TryAdd(state, offId, 1);
            }

            // must own at least 1 copy
            var have = state.Inventory.Where(s => string.Equals(s.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                                      .Sum(s => s.Quantity);
            if (have <= 0) return new(false, "not in inventory");

            // consume 1
            TryRemove(state, itemId, 1);

            // return previously equipped (if any)
            if (state.Equipment.TryGetValue(slot, out var prevId))
            {
                TryAdd(state, prevId, 1);
            }

            state.Equipment[slot] = itemId;
            return new(true);
        }

        public InvOpResult TryUnequip(GameState state, EquipmentSlot slot)
        {
            if (!state.Equipment.TryGetValue(slot, out var itemId))
                return new(false, "slot empty");

            var add = TryAdd(state, itemId, 1);
            if (!add.Success) return add;

            state.Equipment.Remove(slot);
            return new(true);
        }

        // ---------------------------
        // Bonuses
        // ---------------------------
        public StatDelta ComputeEquipmentBonuses(GameState state)
        {
            var sum = new StatDelta();
            foreach (var kv in state.Equipment)
            {
                if (_items.TryGet(kv.Value, out var def) && def.Bonuses is not null)
                {
                    sum = sum with
                    {
                        Strength = sum.Strength + def.Bonuses.Strength,
                        Dexterity = sum.Dexterity + def.Bonuses.Dexterity,
                        Intelligence = sum.Intelligence + def.Bonuses.Intelligence,
                        Vitality = sum.Vitality + def.Bonuses.Vitality,
                        Charisma = sum.Charisma + def.Bonuses.Charisma,
                        Perception = sum.Perception + def.Bonuses.Perception,
                        Luck = sum.Luck + def.Bonuses.Luck
                    };
                }
            }
            return sum;
        }

        private static string? FindPlayerClassId(GameState s)
        {
            var flag = s.Flags.FirstOrDefault(f => f.StartsWith("class.", StringComparison.Ordinal));
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

