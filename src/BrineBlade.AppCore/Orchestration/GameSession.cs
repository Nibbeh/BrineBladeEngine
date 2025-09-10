using System;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Flows;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Orchestration
{
    public sealed class GameSession
    {
        private readonly GameState _state;
        private readonly IContentStore _content;
        private readonly ISaveGameService _saves;
        private readonly IInventoryService _inventory;
        private readonly ICombatService _combatSvc;
        private readonly IEnemyCatalog _enemies;
        private readonly IGameUI _ui;
        private readonly DialogueFlow _dialogueFlow;
        private readonly SaveGameFlow _saveFlow;
        private readonly CombatFlow _combatFlow;
        private readonly NodeFlow _nodeFlow;

        public GameSession(
            GameState state,
            IContentStore content,
            ISaveGameService saves,
            IInventoryService inventory,
            ICombatService combat,
            IEnemyCatalog enemies,
            IGameUI ui,
            EffectProcessor effects)
        {
            _state = state;
            _content = content;
            _saves = saves;
            _inventory = inventory;
            _combatSvc = combat;
            _enemies = enemies;
            _ui = ui;

            _saveFlow = new SaveGameFlow(_state, _saves);
            _dialogueFlow = new DialogueFlow(_state, _content, _ui, effects);
            _combatFlow = new CombatFlow(_state, _combatSvc, _enemies, _inventory, _ui);
            _nodeFlow = new NodeFlow(_state, _content, _dialogueFlow, _saveFlow, OpenInventory, _combatFlow, _ui, effects);
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

        private void OpenInventory() => new InventoryFlow(_state, _inventory).Run();
    }
}

