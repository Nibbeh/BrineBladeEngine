// REPLACE ENTIRE FILE
// src/BrineBlade.AppCore/Orchestration/GameSession.cs
using System;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Flows;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Orchestration
{
    /// <summary>
    /// Orchestrates a single game session: wires flows and runs the main node loop.
    /// </summary>
    public sealed class GameSession
    {
        private readonly GameState _state;
        private readonly IContentStore _content;
        private readonly ISaveGameService _saves;
        private readonly IInventoryService _inventory;
        private readonly ICombatService _combatSvc;
        private readonly IEnemyCatalog _enemies;
        private readonly IGameUI _ui;
        private readonly EffectProcessor _effects;

        private readonly DialogueFlow _dialogueFlow;
        private readonly SaveGameFlow _saveFlow;
        private readonly CombatFlow _combatFlow;
        private readonly NodeFlow _nodeFlow;

        public GameSession(
            GameState state,
            IContentStore content,
            ISaveGameService saves,
            IInventoryService inventory,
            ICombatService combatSvc,
            IEnemyCatalog enemies,
            IGameUI ui,
            EffectProcessor effects)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _combatSvc = combatSvc ?? throw new ArgumentNullException(nameof(combatSvc));
            _enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));

            // ✅ pass _effects to DialogueFlow (new ctor signature)
            _dialogueFlow = new DialogueFlow(_state, _content, _ui, _effects);
            _saveFlow = new SaveGameFlow(_state, _saves);
            _combatFlow = new CombatFlow(_state, _combatSvc, _enemies, _inventory, _ui);

            // Bind NodeFlow's hotkey [I] → Player Menu (not the legacy InventoryFlow)
            _nodeFlow = new NodeFlow(
                _state,
                _content,
                _dialogueFlow,
                _saveFlow,
                OpenPlayerMenu,
                _combatFlow,
                _ui,
                _effects);
        }

        public void RunConsoleLoop()
        {
            Console.Clear();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine($"[BOOT] Player: {_state.Player.Name}  Gold={_state.Gold}");
            Console.WriteLine($"[TIME] Day {_state.World.Day}, {_state.World.Hour:00}:{_state.World.Minute:00}");

            while (_nodeFlow.RenderAndChoose()) { }

            Console.WriteLine("[EXIT] Thanks for playing the slice.");
        }

        /// <summary>
        /// Opens the Player Menu (Character / Inventory / Equipment).
        /// Bound to the [I] hotkey through NodeFlow.
        /// </summary>
        private void OpenPlayerMenu()
        {
            var menu = new PlayerMenuFlow(_state, _ui, _combatSvc, _inventory);
            menu.Open();
        }
    }
}
