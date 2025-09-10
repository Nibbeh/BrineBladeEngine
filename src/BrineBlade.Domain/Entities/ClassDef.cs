namespace BrineBlade.Domain.Entities;

public sealed record ClassDef(
    string Id,
    string Name,
    string Resource,
    int StartGold = 0,
    IReadOnlyList<string>? StartFlags = null,
    IReadOnlyList<string>? Tags = null,
    // NEW (optional, content-driven):
    IReadOnlyList<ArmorType>? AllowedArmorTypes = null,
    IReadOnlyList<WeaponType>? AllowedWeaponTypes = null
);

