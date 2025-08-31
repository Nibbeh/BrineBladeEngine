using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services;

public sealed class InventoryService(ItemCatalog items, ClassCatalog classes) : IInventoryService
{
    private readonly ItemCatalog _items = items;
    private readonly ClassCatalog _classes = classes;

    public InvOpResult TryAdd(GameState state, string itemId, int qty = 1)
    {
        if (qty <= 0) return new(false, "qty<=0");
        if (!_items.TryGet(itemId, out var def)) return new(false, "unknown item");

        if (!def.Stackable)
        {
            for (int i = 0; i < qty; i++) state.Inventory.Add(new(itemId, 1));
            return new(true);
        }

        var remaining = qty;
        // fill existing stacks
        foreach (var st in state.Inventory.Where(s => s.ItemId == itemId).ToList())
        {
            if (def.MaxStack <= 1) break;
            var space = def.MaxStack - st.Quantity;
            if (space <= 0) continue;
            var add = Math.Min(space, remaining);
            var idx = state.Inventory.IndexOf(st);
            state.Inventory[idx] = st with { Quantity = st.Quantity + add };
            remaining -= add;
            if (remaining <= 0) return new(true);
        }
        // new stacks if needed
        while (remaining > 0)
        {
            var add = def.MaxStack > 1 ? Math.Min(def.MaxStack, remaining) : 1;
            state.Inventory.Add(new(itemId, add));
            remaining -= add;
        }
        return new(true);
    }

    public InvOpResult TryRemove(GameState state, string itemId, int qty = 1)
    {
        if (qty <= 0) return new(false, "qty<=0");
        var left = qty;
        foreach (var st in state.Inventory.Where(s => s.ItemId == itemId).ToList())
        {
            var take = Math.Min(st.Quantity, left);
            var idx = state.Inventory.IndexOf(st);
            var newQty = st.Quantity - take;
            if (newQty <= 0) state.Inventory.RemoveAt(idx);
            else state.Inventory[idx] = st with { Quantity = newQty };
            left -= take;
            if (left == 0) return new(true);
        }
        return new(false, "not enough");
    }

    public IReadOnlyList<(string ItemId, string Name, int Qty, bool Equipable)> BuildInventoryView(GameState state)
    {
        var list = new List<(string, string, int, bool)>();
        foreach (var st in state.Inventory)
        {
            if (!_items.TryGet(st.ItemId, out var d)) continue;
            var equipable = d.Slot is not null;
            list.Add((st.ItemId, d.Name, st.Quantity, equipable));
        }
        return list;
    }

    public InvOpResult TryEquip(GameState state, string itemId, EquipmentSlot? targetSlot = null)
    {
        if (!_items.TryGet(itemId, out var def)) return new(false, "unknown item");
        if (def.Slot is null) return new(false, "not equipable");

        var slot = targetSlot ?? def.Slot.Value;

        // class lookup
        if (!_classes.All.TryGetValue(FindPlayerClassId(state) ?? "", out var clazz))
            return new(false, "class unknown");

        // content-driven restrictions
        if (def.AllowedClasses is { Count: > 0 } && !def.AllowedClasses.Contains(clazz.Id))
            return new(false, "class not allowed for this item");

        if (def.ArmorType is not null && clazz.AllowedArmorTypes is { Count: > 0 } &&
            !clazz.AllowedArmorTypes.Contains(def.ArmorType.Value))
            return new(false, "armor type not permitted");

        if (def.WeaponType is not null && clazz.AllowedWeaponTypes is { Count: > 0 } &&
            !clazz.AllowedWeaponTypes.Contains(def.WeaponType.Value))
            return new(false, "weapon type not permitted");

        // NEW: if trying to equip into Offhand while a 2H weapon is equipped, block it
        if (slot == EquipmentSlot.Offhand &&
            state.Equipment.TryGetValue(EquipmentSlot.Weapon, out var curWeapId) &&
            _items.TryGet(curWeapId, out var curWeapDef) &&
            curWeapDef.WeaponType == WeaponType.TwoHanded)
        {
            return new(false, "offhand disabled by two-handed weapon");
        }

        // NEW: equipping a 2H returns any offhand item to inventory (instead of deleting it)
        if (def.WeaponType == WeaponType.TwoHanded &&
            state.Equipment.TryGetValue(EquipmentSlot.Offhand, out var offhandId))
        {
            // best-effort return; even if this fails, we still clear to keep state consistent
            TryAdd(state, offhandId, 1);
            state.Equipment.Remove(EquipmentSlot.Offhand);
        }

        // take 1 from inventory (stackable or not)
        var rm = TryRemove(state, itemId, 1);
        if (!rm.Success) return rm;

        // if target slot occupied ? move old back to inventory
        if (state.Equipment.TryGetValue(slot, out var oldId))
            TryAdd(state, oldId, 1);

        state.Equipment[slot] = itemId;
        return new(true);
    }


    public InvOpResult TryUnequip(GameState state, EquipmentSlot slot)
    {
        if (!state.Equipment.TryGetValue(slot, out var itemId))
            return new(false, "slot empty");
        state.Equipment.Remove(slot);
        return TryAdd(state, itemId, 1);
    }

    public StatDelta ComputeEquipmentBonuses(GameState state)
    {
        var total = new StatDelta();
        foreach (var id in state.Equipment.Values)
            if (_items.TryGet(id, out var d) && d.Bonuses is not null)
                total += d.Bonuses;
        return total;
    }

    private static string? FindPlayerClassId(GameState s)
    {
        // prefer explicit class flag (e.g., "class.warrior" -> "CLASS_WARRIOR")
        var flag = s.Flags.FirstOrDefault(f => f.StartsWith("class.", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(flag))
            return "CLASS_" + flag["class.".Length..].ToUpperInvariant();
        // fallback: map by archetype name (best-effort)
        return s.Player.Archetype?.ToUpperInvariant() switch
        {
            "WARRIOR" => "CLASS_WARRIOR",
            "MAGE" => "CLASS_MAGE",
            "ROGUE" => "CLASS_ROGUE",
            _ => null
        };
    }
}
