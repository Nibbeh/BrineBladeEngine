using System.Collections.Generic;

namespace BrineBlade.Domain.Game;

public sealed record CombatResult(
    bool PlayerWon,
    int PlayerHpRemaining,
    int EnemyHpRemaining,
    IReadOnlyList<string> Loot = null!
);

