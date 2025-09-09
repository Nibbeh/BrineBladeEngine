using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;
using System;

namespace BrineBlade.Infrastructure.Services;

public sealed class CombatService : ICombatService
{
    private readonly IRandom _rng;

    public CombatService(IRandom rng)
    {
        _rng = rng;
    }

    public CombatResult StartCombat(GameState state, EnemyDef enemy)
    {
        int playerHp = state.CurrentHp;
        int enemyHp = enemy.Hp ?? enemy.BaseStats.MaxHp;

        while (playerHp > 0 && enemyHp > 0)
        {
            // Player attacks
            int playerRoll = _rng.Next(1, 21);
            if (playerRoll + 5 >= 10)
                enemyHp -= _rng.Next(1, 6) + 2;

            if (enemyHp <= 0) break;

            // Enemy attacks
            int enemyRoll = _rng.Next(1, 21);
            if (enemyRoll + 3 >= 10)
                playerHp -= _rng.Next(1, 6);

            if (playerHp <= 0) break;
        }

        bool playerWon = playerHp > 0 && enemyHp <= 0;

        var loot = playerWon && enemy.LootTable is not null
            ? enemy.LootTable
            : new List<string>();

        return new CombatResult(playerWon, Math.Max(playerHp, 0), Math.Max(enemyHp, 0), loot);
    }
}
