namespace BrineBlade.Domain.Entities;

public sealed record EnemyDef(
    string Id,                  // ENEMY_BANDIT
    string Name,                // Bandit
    Stats BaseStats,            // STR/DEX/INT/VIT etc.
    IReadOnlyList<string>? Abilities = null,  // ability ids like AB_STRIKE
    int Level = 1,
    int? Hp = null,             // optional override
    int? Mana = null,           // optional override
    IReadOnlyList<string>? LootTable = null   // item ids for drops
);
