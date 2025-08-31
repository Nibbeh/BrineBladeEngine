using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.Services.Abstractions;

/// <summary>
/// Core turn-based combat service.
/// Encapsulates dice rolls, damage, and win/loss resolution.
/// </summary>
public interface ICombatService
{
    CombatResult StartCombat(GameState state, EnemyDef enemy);
}
