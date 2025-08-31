using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.Services.Abstractions;

public sealed record InvOpResult(bool Success, string? Reason = null);

public interface IInventoryService
{
    // Inventory
    InvOpResult TryAdd(GameState state, string itemId, int qty = 1);
    InvOpResult TryRemove(GameState state, string itemId, int qty = 1);
    IReadOnlyList<(string ItemId, string Name, int Qty, bool Equipable)> BuildInventoryView(GameState state);

    // Equipment
    InvOpResult TryEquip(GameState state, string itemId, EquipmentSlot? targetSlot = null);
    InvOpResult TryUnequip(GameState state, EquipmentSlot slot);

    // Derived stats from equipped gear
    StatDelta ComputeEquipmentBonuses(GameState state);
}
