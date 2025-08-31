using System;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;
using BrineBlade.AppCore.Flows;


namespace BrineBlade.AppCore.Orchestration;

public sealed class GameSession
{
    private readonly GameState _state;
    private readonly IContentStore _content;
    private readonly ISaveGameService _saves;
    private readonly IInventoryService _inventory;
    private readonly DialogueFlow _dialogueFlow;
    private readonly SaveGameFlow _saveFlow;
    private readonly NodeFlow _nodeFlow;

    public GameSession(GameState state,
                       IContentStore content,
                       ISaveGameService saves,
                       IInventoryService inventory,
                       ICombatService combat,
                       IEnemyCatalog enemies)
    {
        _state = state;
        _content = content;
        _saves = saves;
        _inventory = inventory;

        _dialogueFlow = new DialogueFlow(_state, _content);
        _saveFlow = new SaveGameFlow(_state, _saves);

        var combatFlow = new CombatFlow(_state, combat, enemies);

        _nodeFlow = new NodeFlow(_state, _content, _dialogueFlow, _saveFlow, OpenInventory, combatFlow);
    }

    public void RunConsoleLoop()
    {
        Console.Clear();
        Console.WriteLine($"[BOOT] Player: {_state.Player.Name} ({_state.Player.Race} {_state.Player.Archetype})  Gold={_state.Gold}");
        Console.WriteLine($"[TIME] Day {_state.World.Day}, {_state.World.Hour:00}:{_state.World.Minute:00}");
        while (_nodeFlow.RenderAndChoose()) { }
        Console.WriteLine("[EXIT] Thanks for playing the slice.");
    }

    private void OpenInventory()
        => new InventoryFlow(_state, _inventory).Run();
}
