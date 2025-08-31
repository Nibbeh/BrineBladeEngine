using BrineBlade.Domain.Entities;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;

namespace BrineBlade.Domain.Game;

public sealed class GameState
{
    public Character Player { get; set; }
    public WorldState World { get; set; }
    public string CurrentNodeId { get; set; }
    public int Gold { get; set; } = 0;
    public HashSet<string> Flags { get; } = new();

    // NEW (storage only; logic in services)
    public List<ItemStack> Inventory { get; } = new();
    public Dictionary<EquipmentSlot, string> Equipment { get; } = new(); // slot -> ItemId

    // === Combat-related state ===
    public int CurrentHp { get; set; }
    public int CurrentMana { get; set; }

    public GameState(Character player, WorldState world, string startNodeId)
    {
        Player = player;
        World = world;
        CurrentNodeId = startNodeId;

        // Default values – these will later be seeded from ClassDef/Stats
        CurrentHp = 20;
        CurrentMana = 10;
    }

    // Domain/Game/GameState.cs

    public void AdvanceMinutes(int minutes)
    {
        // No-op for zero/negative; we only support forward time in this slice.
        if (minutes <= 0) return;

        // Add minutes and carry into hours
        var extraHours = Math.DivRem(World.Minute + minutes, 60, out var newMinute);
        World.Minute = newMinute;

        // Add hours and carry into days
        var extraDays = Math.DivRem(World.Hour + extraHours, 24, out var newHour);
        World.Hour = newHour;

        // Move the calendar forward
        World.Day += extraDays;
    }

    public void ApplyFrom(SaveGameData data)
    {
        Player = data.Player;
        World = data.World;
        CurrentNodeId = data.CurrentNodeId;
        Gold = data.Gold;


        Flags.Clear();
        foreach (var f in data.Flags) Flags.Add(f);

        Inventory.Clear();
        if (data.Inventory is { Count: > 0 }) Inventory.AddRange(data.Inventory);

        Equipment.Clear();
        if (data.Equipment is { Count: > 0 })
            foreach (var kv in data.Equipment) Equipment[kv.Key] = kv.Value;

        // carry HP/MP across loads; clamp to >= 0 (no Character stats exist yet)
        if (data.CurrentHp.HasValue) CurrentHp = Math.Max(0, data.CurrentHp.Value);
        if (data.CurrentMana.HasValue) CurrentMana = Math.Max(0, data.CurrentMana.Value);


    }
}
