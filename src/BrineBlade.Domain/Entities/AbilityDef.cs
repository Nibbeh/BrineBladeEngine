namespace BrineBlade.Domain.Entities;

public sealed record AbilityDef(
    string Id,                 // AB_STRIKE
    string Name,               // Strike
    string ClassId,            // CLASS_WARRIOR (for grouping; specs can also grant)
    string? SpecId = null,     // optional: SPEC_WARRIOR_CHAMPION
    string? Resource = null,   // "Stamina" | "Mana" | "Focus"
    int Cost = 0,              // resource cost (future)
    IReadOnlyList<EffectSpec>? Effects = null, // when used in narrative/combat later
    IReadOnlyList<string>? Tags = null
);

