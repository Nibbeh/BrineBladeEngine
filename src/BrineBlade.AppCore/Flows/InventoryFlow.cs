using System;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows;

// AppCore should not depend on Infrastructure. Keep it UI-only + services.
public sealed class InventoryFlow(GameState state, IInventoryService inv)
{
    private readonly GameState _state = state;
    private readonly IInventoryService _inv = inv;

    public void Run()
    {
        while (true)
        {
            var rows = _inv.BuildInventoryView(_state);

            Console.Clear();
            Console.WriteLine("== Inventory ==");
            for (int i = 0; i < rows.Count; i++)
                Console.WriteLine($"{i + 1}. {rows[i].Name} x{rows[i].Qty} {(rows[i].Equipable ? "[E]" : "")}");

            Console.WriteLine("\n== Equipment ==");
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
            {
                var has = _state.Equipment.TryGetValue(slot, out var id);
                Console.WriteLine($"{slot,-8} : {(has ? id : "(empty)")}");
            }

            var bonus = _inv.ComputeEquipmentBonuses(_state);
            Console.WriteLine($"\nBonuses: STR {bonus.Strength:+#;-#;0}  DEX {bonus.Dexterity:+#;-#;0}  INT {bonus.Intelligence:+#;-#;0}  VIT {bonus.Vitality:+#;-#;0}");

            Console.WriteLine("\nCommands: E <#> equip, U <slot> unequip, D <#> drop 1, Q back");
            Console.Write("> ");
            var line = (Console.ReadLine() ?? "").Trim();
            if (string.Equals(line, "q", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2 && string.Equals(parts[0], "e", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1], out var n) && n >= 1 && n <= rows.Count)
            {
                var res = _inv.TryEquip(_state, rows[n - 1].ItemId);
                SimpleConsoleUI.Notice(res.Success ? "Equipped." : $"Cannot equip: {res.Reason}");
            }
            else if (parts.Length == 2 && string.Equals(parts[0], "u", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<EquipmentSlot>(parts[1], true, out var slot))
            {
                var res = _inv.TryUnequip(_state, slot);
                SimpleConsoleUI.Notice(res.Success ? "Unequipped." : $"Cannot unequip: {res.Reason}");
            }
            else if (parts.Length == 2 && string.Equals(parts[0], "d", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(parts[1], out var di) && di >= 1 && di <= rows.Count)
            {
                var res = _inv.TryRemove(_state, rows[di - 1].ItemId, 1);
                SimpleConsoleUI.Notice(res.Success ? "Dropped 1." : $"Cannot drop: {res.Reason}");
            }
        }
    }
}
