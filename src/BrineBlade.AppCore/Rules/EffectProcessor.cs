using System;
using System.Collections.Generic;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Rules;

public sealed class EffectProcessor
{
    public static readonly HashSet<string> KnownOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "endDialogue",
        "setFlag",
        "addGold",
        "advanceTime",
        "goto",
        "startDialogue",
        "combat",
        "addItem",
        "removeItemByName"
    };

    private readonly GameState _state;
    private readonly IInventoryService _inv;
    private readonly IGameUI _ui;

    public EffectProcessor(GameState state, IInventoryService inv, IGameUI ui)
    {
        _state = state;
        _inv = inv;
        _ui = ui;
    }

    public readonly record struct Outcome(bool EndDialogue);

    public Outcome ApplyAll(IEnumerable<EffectSpec>? effects,
                            Action<string>? startDialogue = null,
                            Action<string>? startCombat = null)
    {
        if (effects is null) return new Outcome(EndDialogue: false);

        bool endDialogue = false;
        foreach (var e in effects)
        {
            switch (e.Op)
            {
                case "endDialogue":
                    endDialogue = true;
                    break;

                case "setFlag" when !string.IsNullOrWhiteSpace(e.Id):
                    _state.Flags.Add(e.Id!);
                    _ui.Notice($"Flag set: {e.Id}");
                    break;

                case "addGold" when e.Amount is not null:
                    _state.Gold += e.Amount.Value;
                    _ui.Notice($"+{e.Amount} gold (total {_state.Gold})");
                    break;

                case "advanceTime" when e.Minutes is not null:
                    _state.AdvanceMinutes(e.Minutes.Value);
                    _ui.Notice($"+{e.Minutes} min");
                    break;

                case "goto" when !string.IsNullOrWhiteSpace(e.To):
                    _state.CurrentNodeId = e.To!;
                    _ui.Notice($"Travel → {_state.CurrentNodeId}");
                    break;

                case "startDialogue" when !string.IsNullOrWhiteSpace(e.Id):
                    if (startDialogue is not null) startDialogue(e.Id!);
                    else _ui.Notice("[WARN] Dialogue system unavailable.");
                    break;

                case "combat" when !string.IsNullOrWhiteSpace(e.Id):
                    if (startCombat is not null) startCombat(e.Id!);
                    else _ui.Notice("[WARN] Combat system unavailable.");
                    break;

                case "addItem" when !string.IsNullOrWhiteSpace(e.Id):
                    {
                        var qty = e.Qty.GetValueOrDefault(1);
                        var r = _inv.TryAdd(_state, e.Id!, qty);
                        _ui.Notice(r.Success ? $"Picked up {e.Id} x{qty}." : $"Cannot add item: {r.Reason}");
                        break;
                    }

                case "removeItemByName" when !string.IsNullOrWhiteSpace(e.Id):
                    {
                        var qty = e.Qty.GetValueOrDefault(1);
                        var r = _inv.TryRemove(_state, e.Id!, qty);
                        _ui.Notice(r.Success ? $"Removed {e.Id} x{qty}." : $"Cannot remove item: {r.Reason}");
                        break;
                    }
            }
        }

        return new Outcome(endDialogue);
    }
}
