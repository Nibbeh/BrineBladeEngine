using System;
using System.Linq;
using System.Collections.Generic;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

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

    private SaveGameData Snapshot() => new(
        Version: 1,
        Player: _state.Player,
        World: _state.World,
        CurrentNodeId: _state.CurrentNodeId,
        Gold: _state.Gold,
        Flags: _state.Flags.ToList(),
        Inventory: _state.Inventory.ToList(),
        Equipment: _state.Equipment.ToDictionary(kv => kv.Key, kv => kv.Value),
        CurrentHp: _state.CurrentHp,
        CurrentMana: _state.CurrentMana
    );

    public void SaveInteractive()
    {
        var existing = _saves.ListSaves();
        var suggested = existing.Count == 0
            ? "slot1"
            : $"slot{existing.Select(s => s.SlotId)
                            .Select(id => int.TryParse(new string(id.Where(char.IsDigit).ToArray()), out var x) ? x : 0)
                            .DefaultIfEmpty(0).Max() + 1}";

        var slot = SuggestAndAskSlot(suggested);
        if (string.IsNullOrWhiteSpace(slot))
        {
            SimpleConsoleUI.Notice("Save cancelled.");
            return;
        }

        _saves.Save(slot, Snapshot());
        SimpleConsoleUI.Notice($"Saved ✓  ({slot})");
    }

    public bool LoadInteractive()
    {
        var list = _saves.ListSaves();
        if (list.Count == 0)
        {
            SimpleConsoleUI.Notice("No save files found.");
            return false;
        }

        var lines = list
            .OrderByDescending(s => s.LastWriteTimeUtc)
            .Select((s, i) => (i + 1,
                $"{s.SlotId} — {s.PlayerName}, {s.CurrentNodeId}, {s.Gold}g, Day {s.Day} {s.Hour:00}:{s.Minute:00}"))
            .ToList();

        // Convert lines to a list of strings for ShowSaves
        SimpleConsoleUI.ShowSaves(lines.Select(l => l.Item2).ToList());

        var choiceStr = SimpleConsoleUI.Ask("Load which slot? (number, 0 to cancel)");
        if (!int.TryParse(choiceStr, out var choice) || choice <= 0 || choice > lines.Count)
        {
            SimpleConsoleUI.Notice("Load cancelled.");
            return false;
        }

        var slotId = list
            .OrderByDescending(s => s.LastWriteTimeUtc)
            .ElementAt(choice - 1).SlotId;

        var data = _saves.Load(slotId);
        if (data is null)
        {
            SimpleConsoleUI.Notice("Failed to load (file missing or corrupt).");
            return false;
        }

        _state.ApplyFrom(data);
        SimpleConsoleUI.Notice($"Loaded ✓  ({slotId})");
        return true;
    }

    private static string SuggestAndAskSlot(string suggested)
    {
        Console.Write($"Save slot id (default: {suggested}): ");
        var s = (Console.ReadLine() ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(s) ? suggested : s;
    }
}

