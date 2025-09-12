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
        private readonly IInventoryService? _inventory;

        // Old ctor (no auto-pickup)
        public CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies, IGameUI ui)
        {
            _state = state; _combat = combat; _enemies = enemies; _ui = ui; _inventory = null;
        }

        // New ctor with auto-pickup
        public CombatFlow(GameState state, ICombatService combat, IEnemyCatalog enemies, IInventoryService inventory, IGameUI ui)
        {
            _state = state; _combat = combat; _enemies = enemies; _ui = ui;
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
            var callbacks = new ConsoleCombatCallbacks(_state, _ui, _inventory);
            var result = _combat.StartCombatInteractive(_state, enemy, callbacks);

            // write-back HP
            _state.CurrentHp = result.PlayerHpRemaining;

            var lines = new List<string>
            {
                $"Encounter: {_state.Player.Name} vs {enemy.Name}",
                result.PlayerWon ? "You won!" : "You were defeated.",
                $"Aftermath — You: {_state.CurrentHp} HP, Foe: {result.EnemyHpRemaining} HP"
            };

            // Loot handling (auto-add if service present)
            IReadOnlyList<string> loot = (result.PlayerWon && result.Loot is { Count: > 0 }) ? result.Loot : Array.Empty<string>();
            if (loot.Count > 0)
            {
                foreach (var itemId in loot)
                {
                    if (_inventory is null) { lines.Add($"Loot: {itemId}"); continue; }
                    try { _inventory.TryAdd(_state, itemId, 1); lines.Add($"Loot: {itemId} (added to inventory)"); }
                    catch { lines.Add($"Loot: {itemId} (couldn’t add)"); }
                }
            }

            _ui.RenderModal(_state, "Combat", lines, waitForEnter: true);
            _ui.Notice(lines.TakeLast(Math.Min(2, lines.Count)));
        }
    }

    internal sealed class ConsoleCombatCallbacks : ICombatCallbacks
    {
        private readonly GameState _state;
        private readonly IGameUI _ui;
        private readonly IInventoryService? _inv;
        private readonly Queue<string> _log = new();

        public ConsoleCombatCallbacks(GameState state, IGameUI ui, IInventoryService? inv)
        { _state = state; _ui = ui; _inv = inv; }

        public CombatAction ChooseAction(int round, int playerHp, int playerMaxHp, int enemyHp, bool canSecondWind, bool hasUsableItem)
        {
            var body = string.Join(Environment.NewLine, _log);
            var options = new List<(string Key, string Label)>
            {
                ("1","Attack"),
                ("2","Guard (+2 AC)"),
                ("3", canSecondWind ? "Second Wind (heal)" : "Second Wind (unavailable)"),
                ("4","Use Item"),
                ("5","Flee"),
            };

            _ui.RenderFrame(_state,
                $"Combat — {_state.Player.Name} vs (foe) (Round {round})",
                $"You: {playerHp}/{playerMaxHp} HP   |   Foe: {enemyHp} HP" + Environment.NewLine +
                (body.Length == 0 ? "(no events yet)" : body),
                options);

            while (true)
            {
                var cmd = _ui.ReadCommand(options.Count);
                if (cmd.Type == ConsoleCommandType.Choose && cmd.ChoiceIndex >= 0 && cmd.ChoiceIndex < options.Count)
                    return (CombatAction)(cmd.ChoiceIndex + 1);
                if (cmd.Type == ConsoleCommandType.Help) { _ui.ShowHelp(); continue; }
                if (cmd.Type == ConsoleCommandType.Quit) return CombatAction.Flee;
            }
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _log.Enqueue("• " + message);
            while (_log.Count > 8) _log.Dequeue();
        }

        public bool TryUseHealingItem(out int healAmount, out string label)
        {
            healAmount = 0; label = "";
            if (_inv is null) return false;

            // Priority: Minor Potion → Herbal Salve
            if (Consume("ITM_HEALTH_POTION_MINOR")) { healAmount = 8; label = "Minor Health Potion"; return true; }
            if (Consume("ITM_HERB_SALVE")) { healAmount = 5; label = "Herbal Salve"; return true; }
            return false;

            bool Consume(string id)
            {
                var have = _state.Inventory.Where(s => s.ItemId.Equals(id, StringComparison.OrdinalIgnoreCase)).Sum(s => s.Quantity);
                if (have <= 0) return false;
                var r = _inv.TryRemove(_state, id, 1);
                if (!r.Success) return false;
                return true;
            }
        }
    }
}
