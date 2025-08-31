using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows;

public sealed class NodeFlow
{
    private readonly GameState _state;
    private readonly IContentStore _content;
    private readonly DialogueFlow _dialogue;
    private readonly SaveGameFlow _save;
    private readonly Action _showInventory;
    private readonly CombatFlow _combat; 

    public NodeFlow(GameState state,
                    IContentStore content,
                    DialogueFlow dialogue,
                    SaveGameFlow save,
                    Action showInventory,
                    CombatFlow combat)
    {
        _state = state;
        _content = content;
        _dialogue = dialogue;
        _save = save;
        _showInventory = showInventory ?? (() => SimpleConsoleUI.Notice("Inventory (unavailable)."));
        _combat = combat;
    }

    public bool RenderAndChoose()
    {
        var node = _content.GetNodeById(_state.CurrentNodeId);
        if (node is null) { SimpleConsoleUI.Notice($"Unknown node '{_state.CurrentNodeId}'."); return false; }

        var runtime = new List<(string key, string label, List<EffectSpec>? effects)>();
        int index = 1;

        if (node.Options is not null)
        {
            foreach (var opt in node.Options)
            {
                if (!PassesRequires(opt.Requires)) continue;
                runtime.Add((index.ToString(), opt.Label, opt.Effects));
                index++;
            }
        }

        if (node.Exits is not null)
        {
            foreach (var ex in node.Exits)
            {
                if (!PassesRequires(ex.Requires)) continue;
                var label = $"Travel to [{ex.To}]";
                var fx = new List<EffectSpec> { new("goto", To: ex.To) };
                runtime.Add((index.ToString(), label, fx));
                index++;
            }
        }

        SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
            runtime.Select(r => (r.key, r.label)).ToList());

        while (true)
        {
            var cmd = SimpleConsoleUI.ReadCommand(runtime.Count);
            switch (cmd.Type)
            {
                case ConsoleCommandType.Choose:
                    ApplyEffects(runtime[cmd.ChoiceIndex].effects);
                    return true;

                case ConsoleCommandType.Quit:
                    return false;

                case ConsoleCommandType.Help:
                    SimpleConsoleUI.ShowHelp();
                    SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
                        runtime.Select(r => (r.key, r.label)).ToList());
                    break;

                case ConsoleCommandType.Inventory:
                    _showInventory();
                    SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
                        runtime.Select(r => (r.key, r.label)).ToList());
                    break;

                case ConsoleCommandType.Save:
                    _save.SaveInteractive();
                    SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
                        runtime.Select(r => (r.key, r.label)).ToList());
                    break;

                case ConsoleCommandType.Load:
                    if (_save.LoadInteractive()) return true;
                    SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
                        runtime.Select(r => (r.key, r.label)).ToList());
                    break;

                case ConsoleCommandType.Refresh:
                case ConsoleCommandType.None:
                default:
                    SimpleConsoleUI.RenderFrame(_state, node.Title, node.Description,
                        runtime.Select(r => (r.key, r.label)).ToList());
                    break;
            }
        }
    }

    private bool PassesRequires(List<string>? reqs)
    {
        if (reqs is null || reqs.Count == 0) return true;
        foreach (var r in reqs)
        {
            if (r.StartsWith("flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flag = r[5..];
                if (!_state.Flags.Contains(flag)) return false;
            }
        }
        return true;
    }

    private void ApplyEffects(List<EffectSpec>? effects)
    {
        if (effects is null) return;

        foreach (var e in effects)
        {
            switch (e.Op)
            {
                case "goto" when !string.IsNullOrWhiteSpace(e.To):
                    _state.CurrentNodeId = e.To!;
                    SimpleConsoleUI.Notice($"Travel -> {_state.CurrentNodeId}");
                    break;

                case "startDialogue" when !string.IsNullOrWhiteSpace(e.Id):
                    _dialogue.Run(e.Id!);
                    break;

                case "startCombat" when !string.IsNullOrWhiteSpace(e.Id): 
                    _combat.Run(e.Id!);
                    break;

                case "setFlag" when !string.IsNullOrWhiteSpace(e.Id):
                    _state.Flags.Add(e.Id!);
                    SimpleConsoleUI.Notice($"Flag set: {e.Id}");
                    break;

                case "addGold" when e.Amount is not null:
                    _state.Gold += e.Amount!.Value;
                    SimpleConsoleUI.Notice($"+{e.Amount} gold (total {_state.Gold})");
                    break;

                case "advanceTime" when e.Minutes is not null:
                    _state.AdvanceMinutes(e.Minutes!.Value);
                    SimpleConsoleUI.Notice($"+{e.Minutes} min");
                    break;
            }
        }
    }
}
