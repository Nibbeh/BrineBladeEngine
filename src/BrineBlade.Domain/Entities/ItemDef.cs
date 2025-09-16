// Domain/Entities/ItemModels.cs
namespace BrineBlade.Domain.Entities;

public enum ItemType { Weapon, Armor, Shield, Consumable, Material, Quest, Misc }

public sealed record ItemDef(
    string Id,
    string Name,
    ItemType Type,
    double Weight,
    int Value,
    bool Stackable = false,
    int MaxStack = 1,
    EquipmentSlot? Slot = null,
    ArmorType? ArmorType = null,
    WeaponType? WeaponType = null,
    IReadOnlyList<string>? AllowedClasses = null,
    IReadOnlyList<string>? AllowedTags = null,
    StatDelta? Bonuses = null
);

// State stores IDs/qty only; resolution happens in services using catalogs.
public sealed record ItemStack(string ItemId, int Quantity);

