// AppCore/Flows/CombatFlow.cs
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows;

public sealed class CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies)
{
    private readonly GameState _state = state;
    private readonly ICombatService _combat = combat;
    private readonly IEnemyCatalog _enemies = enemies;

    public void Run(string enemyId) => StartEncounter(enemyId);
    public void Run(EnemyDef enemy) => ExecuteCombat(enemy);
    public void Run() => SimpleConsoleUI.Notice("[COMBAT] No enemy id provided by node.");

    public void StartEncounter(string enemyId)
    {
        var enemy = _enemies.GetRequired(enemyId);
        ExecuteCombat(enemy);
    }

    private void ExecuteCombat(EnemyDef enemy)
    {
        // Optional safety: never start a fight at zero in dev
        if (_state.CurrentHp <= 0) _state.CurrentHp = 20;
        if (_state.CurrentMana < 0) _state.CurrentMana = 0;

        var lines = new List<string>
        {
            $"Encounter: {enemy.Name} (Lv {enemy.Level})",
            $"You: {_state.CurrentHp} HP    Foe: {(enemy.Hp ?? enemy.BaseStats.MaxHp)} HP"
        };

        var result = _combat.StartCombat(_state, enemy);

        // Single writer: apply the outcome here
        _state.CurrentHp = result.PlayerHpRemaining;

        lines.Add(result.PlayerWon ? "Victory!" : "Defeat…");
        lines.Add($"Aftermath — You: {_state.CurrentHp} HP, Foe: {result.EnemyHpRemaining} HP");

        if (result.PlayerWon && result.Loot is { Count: > 0 })
        {
            foreach (var drop in result.Loot) lines.Add($"Loot: {drop}");
        }

        // Modal screen so node redraw doesn't wipe it
        SimpleConsoleUI.RenderModal(_state, "Combat", lines, waitForEnter: true);

        // Also keep a short trail in the Recent log after returning
        SimpleConsoleUI.Notice(lines.TakeLast(Math.Min(2, lines.Count)));
    }
}
