using System;
using System.Linq;
using System.Collections.Generic;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows;

// AppCore should not depend on Infrastructure. Keep it UI-only + services.
public sealed class InventoryFlow
{
    private readonly GameState _state;
    private readonly IInventoryService _inv;

    public InventoryFlow(GameState state, IInventoryService inv)
    {
        _state = state;
        _inv = inv;
    }

    public void Run()
    {
        while (true)
        {
            var rows = _inv.BuildInventoryView(_state)
                .Select(r =>
                {
                    var equipped = _state.Equipment.Values.Any(v => string.Equals(v, r.ItemId, StringComparison.Ordinal));
                    return (r.ItemId, r.Name, r.Qty, r.Equipable, Equipped: equipped);
                })
                .ToList();

            var lines = new List<string>
            {
                "Inventory:",
                "  #  Name                              Qty   Notes",
                "  -- --------------------------------- ----- -------------------------"
            };

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var notes = new List<string>();
                if (r.Equipable) notes.Add("[E]");
                if (r.Equipped) notes.Add("(equipped)");
                lines.Add($"  {i + 1,2}  {r.Name,-33} {r.Qty,5}  {string.Join(' ', notes)}");
            }

            lines.Add("");
            lines.Add("Equipment:");
            foreach (var kv in _state.Equipment.OrderBy(k => k.Key))
            {
                var id = string.IsNullOrWhiteSpace(kv.Value) ? "-" : kv.Value;
                lines.Add($"  {kv.Key,-10} : {id}");
            }

            lines.Add("");
            lines.Add("Commands:  e N = equip item N   |  u SLOT = unequip slot   |  d N = drop 1   |  q = close");
            SimpleConsoleUI.RenderModal(_state, "Inventory", lines, waitForEnter: false);

            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var op = parts[0].ToLowerInvariant();

            if (op == "q") break;

            if (op == "e" && parts.Length == 2 && int.TryParse(parts[1], out var idx) && idx >= 1 && idx <= rows.Count)
            {
                var sel = rows[idx - 1];
                var res = _inv.TryEquip(_state, sel.ItemId);
                SimpleConsoleUI.Notice(res.Success ? $"Equipped {sel.Name}." : $"Cannot equip: {res.Reason}");
            }
            else if (op == "u" && parts.Length == 2 && Enum.TryParse<EquipmentSlot>(parts[1], true, out var slot))
            {
                var res = _inv.TryUnequip(_state, slot);
                SimpleConsoleUI.Notice(res.Success ? "Unequipped." : $"Cannot unequip: {res.Reason}");
            }
            else if (op == "d" && parts.Length == 2 && int.TryParse(parts[1], out var di) && di >= 1 && di <= rows.Count)
            {
                var res = _inv.TryRemove(_state, rows[di - 1].ItemId, 1);
                SimpleConsoleUI.Notice(res.Success ? "Dropped 1." : $"Cannot drop: {res.Reason}");
            }
            else
            {
                SimpleConsoleUI.Notice("Unknown command. Use: e N | u SLOT | d N | q");
            }
        }
    }
}
