using System.Collections.Generic;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Domain.Game;

// add nullable fields at the end, so older saves still deserialize
public sealed record SaveGameData(
    int Version,
    Character Player,
    WorldState World,
    string CurrentNodeId,
    int Gold,
    List<string> Flags,
    List<ItemStack>? Inventory = null,
    Dictionary<EquipmentSlot, string>? Equipment = null,
    int? CurrentHp = null,
    int? CurrentMana = null
);


