namespace BrineBlade.Domain.Entities;

public sealed record EffectSpec(
    string Op,
    string? Id = null,        // item id, dialogue id, enemy id, etc.
    string? To = null,        // for goto
    int? Minutes = null,
    int? Amount = null,
    int? Qty = null,
    string? Slot = null       // NEW: for equip/unequip (e.g., "Head", "Weapon", "Offhand")
);
