namespace BrineBlade.Domain.Entities;

public sealed record SpecDef(
    string Id,                 // e.g., "SPEC_WARRIOR_CHAMPION"
    string ClassId,            // "CLASS_WARRIOR"
    string Name,               // "Champion"
    IReadOnlyList<string>? StartFlags = null,
    IReadOnlyList<string>? Tags = null
);

