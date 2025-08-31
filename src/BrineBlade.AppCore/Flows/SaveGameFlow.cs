using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;
using System.Linq;

namespace BrineBlade.AppCore.Flows;

public sealed class SaveGameFlow
{
    private readonly GameState _state;
    private readonly ISaveGameService _saves;

    public SaveGameFlow(GameState state, ISaveGameService saves)
    {
        _state = state;
        _saves = saves;
    }

    public void SaveInteractive()
    {
        var slot = SimpleConsoleUI.Ask("Save slot name (e.g., quick, slot1)");
        if (string.IsNullOrWhiteSpace(slot)) { SimpleConsoleUI.Notice("Save cancelled."); return; }
        _saves.Save(slot, Snapshot());
        SimpleConsoleUI.Notice($"Saved ? {slot}");
    }

    public bool LoadInteractive()
    {
        var list = _saves.ListSaves();
        if (list.Count == 0) { SimpleConsoleUI.Notice("No save files found."); return false; }

        var lines = list.Select((s, i) =>
            (i + 1, $"{s.SlotId}  —  {s.PlayerName}, {s.CurrentNodeId}, {s.Gold}g, Day {s.Day} {s.Hour:00}:{s.Minute:00}")).ToList();

        SimpleConsoleUI.ShowSaves(lines);

        var choice = SimpleConsoleUI.Ask("Load which number");
        if (!int.TryParse(choice, out var n) || n < 1 || n > list.Count)
        { SimpleConsoleUI.Notice("Load cancelled."); return false; }

        var slotId = list[n - 1].SlotId;
        var data = _saves.Load(slotId);
        if (data is null) { SimpleConsoleUI.Notice("Load failed (file missing or corrupt)."); return false; }

        _state.ApplyFrom(data);
        SimpleConsoleUI.Notice($"Loaded ? {slotId} (Node={_state.CurrentNodeId})");
        return true;
    }

    public SaveGameData Snapshot() => new(
     Version: 1,
     Player: _state.Player,
     World: _state.World,
     CurrentNodeId: _state.CurrentNodeId,
     Gold: _state.Gold,
     Flags: _state.Flags.ToList(),
     Inventory: _state.Inventory.ToList(),
     Equipment: new Dictionary<EquipmentSlot, string>(_state.Equipment),
     CurrentHp: _state.CurrentHp,
     CurrentMana: _state.CurrentMana
 );


}
