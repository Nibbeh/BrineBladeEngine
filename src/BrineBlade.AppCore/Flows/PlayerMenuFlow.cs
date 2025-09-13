// src/BrineBlade.AppCore/Flows/PlayerMenuFlow.cs
using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows
{
    public sealed class PlayerMenuFlow
    {
        private readonly GameState _state;
        private readonly IGameUI _ui;
        private readonly ICombatService _combat;
        private readonly IInventoryService? _inventory;

        public PlayerMenuFlow(GameState state, IGameUI ui, ICombatService combat, IInventoryService? inventory = null)
        {
            _state = state;
            _ui = ui;
            _combat = combat;
            _inventory = inventory;
        }

        public void Open()
        {
            while (true)
            {
                var snap = _combat.GetPlayerSnapshot(_state);

                string header =
                    $"{_state.Player.Name} — Human Warrior (Champion)\n" +
                    $"HP {snap.CurrentHp}/{snap.MaxHp}  |  AC {snap.ArmorClass}  DR {snap.DamageReduction}  |  " +
                    $"Weapon {snap.WeaponLabel} d{snap.WeaponDie}  crit {snap.CritMin}+  pen {snap.Penetration}" +
                    (snap.HasShield ? $"  |  Shield block {Math.Round(snap.ShieldBlockChance * 100)}%" : "");

                const string body =
                    "Choose a section:\n" +
                    "• Character Sheet — view derived stats & talents\n" +
                    "• Inventory — use items, sort\n" +
                    "• Equipment — view/unequip/quick-equip\n";

                var options = new List<(string, string)>
                {
                    ("1", "Character Sheet"),
                    ("2", "Inventory"),
                    ("3", "Equipment"),
                    ("4", "Close")
                };

                _ui.RenderFrame(_state, "Player Menu", header + "\n\n" + body, options);

                var cmd = _ui.ReadCommand(options.Count);
                if (cmd.Type == ConsoleCommandType.Quit || (cmd.Type == ConsoleCommandType.Choose && cmd.ChoiceIndex == 3))
                    return;

                if (cmd.Type == ConsoleCommandType.Choose)
                {
                    switch (cmd.ChoiceIndex)
                    {
                        case 0: ShowCharacterSheet(); break;
                        case 1: ManageInventory(); break;
                        case 2: ManageEquipment(); break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // Character Sheet
        // ---------------------------------------------------------------------
        private void ShowCharacterSheet()
        {
            var s = _combat.GetPlayerSnapshot(_state);

            string stats =
                $"STR {s.Strength,2}   DEX {s.Dexterity,2}   INT {s.Intelligence,2}\n" +
                $"VIT {s.Vitality,2}   CHA {s.Charisma,2}   PER {s.Perception,2}   LCK {s.Luck,2}\n\n" +
                $"AC {s.ArmorClass}   DR {s.DamageReduction}\n" +
                $"Weapon: {s.WeaponLabel} d{s.WeaponDie}  |  crit {s.CritMin}+  pen {s.Penetration}\n" +
                (s.HasShield ? $"Shield: block {Math.Round(s.ShieldBlockChance * 100)}%\n" : "") +
                $"Flags: {string.Join(", ", _state.Flags)}";

            _ui.RenderModal(_state, "Character Sheet", stats.Split('\n'), waitForEnter: true);
        }

        // ---------------------------------------------------------------------
        // Inventory
        // ---------------------------------------------------------------------
        private void ManageInventory()
        {
            if (_state.Inventory.Count == 0)
            {
                _ui.RenderModal(_state, "Inventory", new[] { "You have no items." }, true);
                return;
            }

            while (true)
            {
                var lines = BuildInventoryTable(out var ids, out var qtys);
                lines.Add(string.Empty);
                lines.Add("Actions: [1] Use (consumables)   [2] Drop   [3] Sort (A→Z)   [4] Close");

                _ui.RenderFrame(
                    _state,
                    $"Inventory ({_state.Inventory.Sum(i => i.Quantity)} items)",
                    string.Join("\n", lines),
                    new List<(string, string)>
                    {
                        ("1", "Use"),
                        ("2", "Drop"),
                        ("3", "Sort A→Z"),
                        ("4", "Close")
                    }
                );

                var cmd = _ui.ReadCommand(4);
                if (cmd.Type == ConsoleCommandType.Quit || (cmd.Type == ConsoleCommandType.Choose && cmd.ChoiceIndex == 3))
                    return;

                if (cmd.Type == ConsoleCommandType.Choose)
                {
                    if (cmd.ChoiceIndex == 2)
                    {
                        // Sort underlying stacks by ItemId; grouped view will follow.
                        _state.Inventory.Sort((a, b) => string.Compare(a.ItemId, b.ItemId, StringComparison.OrdinalIgnoreCase));
                        continue;
                    }

                    int pick = PromptPickIndex(ids.Count);
                    if (pick < 0) continue;

                    string id = ids[pick];

                    if (cmd.ChoiceIndex == 0) UseConsumable(id);
                    else if (cmd.ChoiceIndex == 1) DropOne(id);
                }
            }
        }

        /// <summary>
        /// Builds a grouped, case-insensitive inventory table:
        /// - Groups duplicate stacks by ItemId
        /// - Sums quantities
        /// - Sorts by friendly label A→Z
        /// Returns printable lines and parallel lists of ids/qtys for selection.
        /// </summary>
        private List<string> BuildInventoryTable(out List<string> ids, out List<int> qtys)
        {
            var rows = _state.Inventory
                .GroupBy(s => s.ItemId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    ItemId = g.Key,
                    Qty = g.Sum(x => x.Quantity),
                    Label = FriendlyName(g.Key)
                })
                .OrderBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ids = rows.Select(r => r.ItemId).ToList();
            qtys = rows.Select(r => r.Qty).ToList();

            var lines = new List<string>
            {
                "  #  │ Item Id                 │ Qty │ Label",
                "─────┼─────────────────────────┼─────┼────────────────────────────"
            };

            for (int i = 0; i < rows.Count; i++)
                lines.Add($"{i + 1,3}  │ {Trunc(rows[i].ItemId, 25),-25} │ {rows[i].Qty,3} │ {rows[i].Label}");

            return lines;
        }

        private void UseConsumable(string id)
        {
            int heal = id.ToUpperInvariant() switch
            {
                "ITM_HEALTH_POTION_MINOR" => 8,
                "ITM_BREAD" => 4,
                "ITM_HERB_SALVE" => 5,
                _ => 0
            };

            if (heal <= 0)
            {
                _ui.RenderModal(_state, "Use Item", new[] { $"'{id}' is not usable (yet)." }, true);
                return;
            }

            if (!TryRemove(id, 1))
            {
                _ui.RenderModal(_state, "Use Item", new[] { $"You do not have '{id}'." }, true);
                return;
            }

            var snap = _combat.GetPlayerSnapshot(_state);
            int before = snap.CurrentHp;

            _state.CurrentHp = Math.Min(snap.MaxHp, snap.CurrentHp + heal);

            int gained = _state.CurrentHp - before;
            _ui.RenderModal(_state, "Use Item", new[] { $"You use {FriendlyName(id)} and recover {gained} HP." }, true);
        }

        private void DropOne(string id)
        {
            if (!TryRemove(id, 1))
            {
                _ui.RenderModal(_state, "Drop", new[] { $"You do not have '{id}'." }, true);
                return;
            }

            _ui.RenderModal(_state, "Drop", new[] { $"Dropped one '{FriendlyName(id)}'." }, true);
        }

        private bool TryRemove(string id, int qty)
        {
            if (_inventory is not null) return _inventory.TryRemove(_state, id, qty).Success;

            // Fallback edit respecting init-only ItemStack
            int remaining = qty;

            for (int i = _state.Inventory.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var s = _state.Inventory[i];
                if (!string.Equals(s.ItemId, id, StringComparison.OrdinalIgnoreCase)) continue;

                int take = Math.Min(s.Quantity, remaining);
                int newQty = s.Quantity - take;

                _state.Inventory.RemoveAt(i);
                if (newQty > 0) _state.Inventory.Insert(i, new ItemStack(s.ItemId, newQty));

                remaining -= take;
            }

            return remaining == 0;
        }

        private void TryAdd(string id, int qty)
        {
            if (_inventory is not null)
            {
                _inventory.TryAdd(_state, id, qty);
                return;
            }

            int idx = _state.Inventory.FindIndex(s => string.Equals(s.ItemId, id, StringComparison.OrdinalIgnoreCase));

            if (idx < 0)
            {
                _state.Inventory.Add(new ItemStack(id, qty));
            }
            else
            {
                var s = _state.Inventory[idx];
                _state.Inventory[idx] = new ItemStack(s.ItemId, s.Quantity + qty);
            }
        }

        // ---------------------------------------------------------------------
        // Equipment
        // ---------------------------------------------------------------------
        private void ManageEquipment()
        {
            while (true)
            {
                var lines = new List<string>
                {
                    $"Head:    {SlotLabel(EquipmentSlot.Head)}",
                    $"Chest:   {SlotLabel(EquipmentSlot.Chest)}",
                    $"Weapon:  {SlotLabel(EquipmentSlot.Weapon)}",
                    $"Offhand: {SlotLabel(EquipmentSlot.Offhand)}",
                    string.Empty,
                    "Actions: [1] Unequip Slot   [2] Quick-Equip from Inventory   [3] Close"
                };

                _ui.RenderFrame(
                    _state,
                    "Equipment",
                    string.Join("\n", lines),
                    new List<(string, string)>
                    {
                        ("1", "Unequip"),
                        ("2", "Quick-Equip"),
                        ("3", "Close")
                    }
                );

                var cmd = _ui.ReadCommand(3);
                if (cmd.Type == ConsoleCommandType.Quit || (cmd.Type == ConsoleCommandType.Choose && cmd.ChoiceIndex == 2))
                    return;

                if (cmd.Type == ConsoleCommandType.Choose)
                {
                    if (cmd.ChoiceIndex == 0)
                    {
                        var slot = PromptSlot();
                        if (slot is null) continue;

                        if (!_state.Equipment.TryGetValue(slot.Value, out var item) || string.IsNullOrWhiteSpace(item))
                        {
                            _ui.RenderModal(_state, "Unequip", new[] { "That slot is empty." }, true);
                            continue;
                        }

                        TryAdd(item, 1);
                        _state.Equipment[slot.Value] = string.Empty;

                        _ui.RenderModal(_state, "Unequip", new[] { $"Unequipped '{FriendlyName(item)}'." }, true);
                    }
                    else if (cmd.ChoiceIndex == 1)
                    {
                        var candidates = _state.Inventory
                            .Select(s =>
                            {
                                var slot = InferSlot(s.ItemId);
                                return slot is null
                                    ? default((EquipmentSlot Slot, string Id, string Label)?)
                                    : (slot.Value, s.ItemId, FriendlyName(s.ItemId));
                            })
                            .Where(t => t.HasValue)
                            .Select(t => t!.Value)
                            .ToList();

                        if (candidates.Count == 0)
                        {
                            _ui.RenderModal(_state, "Quick-Equip", new[] { "No equippable items found in your inventory." }, true);
                            continue;
                        }

                        var list = new List<string>
                        {
                            "  #  │ Slot     │ Item Id                 │ Label",
                            "─────┼──────────┼─────────────────────────┼────────────────────────────"
                        };

                        for (int i = 0; i < candidates.Count; i++)
                        {
                            list.Add($"{i + 1,3}  │ {candidates[i].Slot,-8} │ {Trunc(candidates[i].Id, 25),-25} │ {candidates[i].Label}");
                        }

                        _ui.RenderFrame(
                            _state,
                            "Quick-Equip",
                            string.Join("\n", list),
                            new List<(string, string)> { ("1", "Equip"), ("2", "Cancel") }
                        );

                        int pickIdx = PromptPickIndex(candidates.Count);
                        if (pickIdx < 0) continue;

                        var (slotCh, idCh, _) = candidates[pickIdx];
                        Equip(slotCh, idCh);
                    }
                }
            }
        }

        private EquipmentSlot? PromptSlot()
        {
            var options = new List<(string, string)>
            {
                ("1", "Head"),
                ("2", "Chest"),
                ("3", "Weapon"),
                ("4", "Offhand"),
                ("5", "Cancel")
            };

            _ui.RenderFrame(_state, "Choose Slot", "Pick a slot to unequip:", options);

            var cmd = _ui.ReadCommand(options.Count);
            if (cmd.Type == ConsoleCommandType.Choose)
            {
                return cmd.ChoiceIndex switch
                {
                    0 => EquipmentSlot.Head,
                    1 => EquipmentSlot.Chest,
                    2 => EquipmentSlot.Weapon,
                    3 => EquipmentSlot.Offhand,
                    _ => (EquipmentSlot?)null
                };
            }

            return null;
        }

        private void Equip(EquipmentSlot slot, string id)
        {
            if (!TryRemove(id, 1))
            {
                _ui.RenderModal(_state, "Equip", new[] { $"You do not have '{id}'." }, true);
                return;
            }

            if (_state.Equipment.TryGetValue(slot, out var oldId) && !string.IsNullOrWhiteSpace(oldId))
            {
                TryAdd(oldId, 1);
            }

            _state.Equipment[slot] = id;

            _ui.RenderModal(_state, "Equip", new[] { $"Equipped '{FriendlyName(id)}' in {slot}." }, true);
        }

        private static EquipmentSlot? InferSlot(string id)
        {
            id = id.ToUpperInvariant();

            if (id.Contains("SWORD") || id.Contains("DAGGER") || id.Contains("AXE") || id.Contains("BOW") || id.Contains("STAFF"))
                return EquipmentSlot.Weapon;

            if (id.Contains("SHIELD"))
                return EquipmentSlot.Offhand;

            if (id.Contains("ARMOR"))
                return EquipmentSlot.Chest;

            if (id.Contains("HELM") || id.Contains("CAP") || id.Contains("HOOD"))
                return EquipmentSlot.Head;

            return null;
        }

        private string SlotLabel(EquipmentSlot slot)
        {
            return _state.Equipment.TryGetValue(slot, out var id) && !string.IsNullOrWhiteSpace(id)
                ? $"{FriendlyName(id)} ({id})"
                : "(empty)";
        }

        private static string FriendlyName(string id) => id.ToUpperInvariant() switch
        {
            "ITM_SWORD_SHORT" => "Short Sword",
            "ITM_DAGGER_IRON" => "Iron Dagger",
            "ITM_SHIELD_WOOD" => "Wooden Shield",
            "ITM_ARMOR_LEATHER" => "Leather Armor",
            "ITM_CAP_CLOTH" => "Cloth Cap",
            "ITM_HEALTH_POTION_MINOR" => "Minor Health Potion",
            "ITM_BREAD" => "Loaf of Bread",
            "ITM_HERB_SALVE" => "Herbal Salve",
            _ => id
        };

        private static string Trunc(string s, int max)
            => s.Length <= max ? s : s[..Math.Max(0, max - 1)] + "…";

        private int PromptPickIndex(int max)
        {
            _ui.RenderModal(_state, "Select Row", new[] { "Enter the row number to act on, or 0 to cancel." }, waitForEnter: false);

            while (true)
            {
                Console.Write("> ");
                var raw = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (int.TryParse(raw.Trim(), out int n))
                {
                    if (n == 0) return -1;
                    if (n >= 1 && n <= max) return n - 1;
                }

                _ui.RenderModal(_state, "Select Row", new[] { "Invalid number." }, true);
                return -1;
            }
        }
    }
}
