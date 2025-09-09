// AppCore/Flows/CombatFlow.cs
using System.Collections.Generic;
using System.Linq;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows
{
    public sealed class CombatFlow
    {
        private readonly GameState _state;
        private readonly ICombatService _combat;
        private readonly IEnemyCatalog _enemies;
        private readonly IGameUI _ui;

        public CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies, IGameUI ui)
        {
            _state = state;
            _combat = combat;
            _enemies = enemies;
            _ui = ui;
        }

        public void Run(string enemyId)
        {
            if (!_enemies.TryGet(enemyId, out var enemy))
            {
                _ui.Notice($"[COMBAT] Unknown enemy '{enemyId}'.");
                return;
            }
            ExecuteCombat(enemy);
        }

        public void Run(EnemyDef enemy) => ExecuteCombat(enemy);

        private void ExecuteCombat(EnemyDef enemy)
        {
            var result = _combat.StartCombat(_state, enemy);

            // Mutate state (single-writer pattern)
            _state.CurrentHp = result.PlayerHpRemaining;

            var lines = new List<string>
            {
                $"Encounter: {_state.Player.Name} vs {enemy.Name}",
                result.PlayerWon ? "You won!" : "You were defeated."
            };

            lines.Add($"Aftermath — You: {_state.CurrentHp} HP, Foe: {result.EnemyHpRemaining} HP");

            if (result.PlayerWon && result.Loot is { Count: > 0 })
            {
                foreach (var drop in result.Loot) lines.Add($"Loot: {drop}");
            }

            // Modal screen so node redraw doesn't wipe it
            _ui.RenderModal(_state, "Combat", lines, waitForEnter: true);

            // Also keep a short trail in the Recent log after returning
            _ui.Notice(lines.TakeLast(System.Math.Min(2, lines.Count)));
        }
    }
}
