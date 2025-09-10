// AppCore/Flows/CombatFlow.cs
using System;
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
        private readonly IInventoryService? _inventory; // optional for backward-compat

        // Backward-compatible ctor (no inventory auto-pickup)
        public CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies, IGameUI ui)
        {
            _state = state;
            _combat = combat;
            _enemies = enemies;
            _ui = ui;
            _inventory = null;
        }

        // New ctor enabling auto-pickup of loot
        public CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies, IInventoryService inventory, IGameUI ui)
        {
            _state = state;
            _combat = combat;
            _enemies = enemies;
            _ui = ui;
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
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
                result.PlayerWon ? "You won!" : "You were defeated.",
                $"Aftermath — You: {_state.CurrentHp} HP, Foe: {result.EnemyHpRemaining} HP"
            };

            // Use only loot reported by the combat result; type as IReadOnlyList for Count
            IReadOnlyList<string> loot = (result.PlayerWon && result.Loot is { Count: > 0 })
                ? result.Loot
                : Array.Empty<string>();

            if (loot.Count > 0)
            {
                foreach (var itemId in loot)
                {
                    if (_inventory is null)
                    {
                        // Old path: just list the loot
                        lines.Add($"Loot: {itemId}");
                        continue;
                    }

                    try
                    {
                        // We don’t assume a bool return; just invoke it and report success optimistically.
                        _inventory.TryAdd(_state, itemId, 1);
                        lines.Add($"Loot: {itemId} (added to inventory)");
                    }
                    catch
                    {
                        lines.Add($"Loot: {itemId} (couldn’t add)");
                    }
                }
            }

            // Modal screen so node redraw doesn't wipe it
            _ui.RenderModal(_state, "Combat", lines, waitForEnter: true);

            // Keep a short trail in the Recent log after returning
            _ui.Notice(lines.TakeLast(Math.Min(2, lines.Count)));
        }
    }
}

