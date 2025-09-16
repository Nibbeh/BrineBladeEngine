using System;
using System.Collections.Generic;
using System.Linq;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows
{
    public sealed class NodeFlow
    {
        private readonly GameState _state;
        private readonly IContentStore _content;
        private readonly DialogueFlow _dialogue;
        private readonly SaveGameFlow _save;
        private readonly Action _showInventory;
        private readonly CombatFlow _combat;
        private readonly IGameUI _ui;
        private readonly EffectProcessor _effects;

        public NodeFlow(GameState state,
                        IContentStore content,
                        DialogueFlow dialogue,
                        SaveGameFlow save,
                        Action showInventory,
                        CombatFlow combat,
                        IGameUI ui,
                        EffectProcessor effects)
        {
            _state = state;
            _content = content;
            _dialogue = dialogue;
            _save = save;
            _showInventory = showInventory ?? (() => ui.Notice("Inventory (unavailable)."));
            _combat = combat;
            _ui = ui;
            _effects = effects;
        }

        public bool RenderAndChoose()
        {
            var node = _content.GetNodeById(_state.CurrentNodeId);
            if (node is null) { _ui.Notice($"Unknown node '{_state.CurrentNodeId}'."); return false; }

            // Build the display body = Description + Paragraphs (if any)
            var body = node.Description +
                       ((node.Paragraphs is { Count: > 0 })
                           ? "\n\n" + string.Join("\n\n", node.Paragraphs)
                           : string.Empty);

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

                    // Prefer author-specified label; else derive from target node; else fallback
                    string label;
                    if (!string.IsNullOrWhiteSpace(ex.Text))
                    {
                        label = ex.Text!;
                    }
                    else
                    {
                        var target = _content.GetNodeById(ex.To);
                        label = (target is not null) ? $"To {target.Title}" : $"Travel to [{ex.To}]";
                    }

                    var fx = new List<EffectSpec> { new("goto", To: ex.To) };
                    runtime.Add((index.ToString(), label, fx));
                    index++;
                }
            }

            _ui.RenderFrame(_state, node.Title, body,
                runtime.Select(r => (r.key, r.label)).ToList());

            while (true)
            {
                var cmd = _ui.ReadCommand(runtime.Count);
                switch (cmd.Type)
                {
                    case ConsoleCommandType.Choose:
                        ApplyEffects(runtime[cmd.ChoiceIndex].effects);
                        return true;

                    case ConsoleCommandType.Quit:
                        return false;

                    case ConsoleCommandType.Help:
                        _ui.ShowHelp();
                        _ui.RenderFrame(_state, node.Title, body,
                            runtime.Select(r => (r.key, r.label)).ToList());
                        break;

                    case ConsoleCommandType.Inventory:
                        _showInventory();
                        _ui.RenderFrame(_state, node.Title, body,
                            runtime.Select(r => (r.key, r.label)).ToList());
                        break;

                    case ConsoleCommandType.Save:
                        _save.SaveInteractive();
                        _ui.Notice("Game saved.");
                        _ui.RenderFrame(_state, node.Title, body,
                            runtime.Select(r => (r.key, r.label)).ToList());
                        break;

                    case ConsoleCommandType.Load:
                        if (_save.LoadInteractive()) return true;
                        _ui.RenderFrame(_state, node.Title, body,
                            runtime.Select(r => (r.key, r.label)).ToList());
                        break;

                    case ConsoleCommandType.Refresh:
                    case ConsoleCommandType.None:
                    default:
                        _ui.RenderFrame(_state, node.Title, body,
                            runtime.Select(r => (r.key, r.label)).ToList());
                        break;
                }
            }
        }

        bool PassesRequires(List<string>? reqs)
        {
            return BrineBlade.AppCore.Rules.RequiresEvaluator.Passes(_state, reqs);
        }


        private void ApplyEffects(List<EffectSpec>? effects)
        {
            _effects.ApplyAll(effects,
                startDialogue: id => _dialogue.Run(id),
                startCombat: id => _combat.Run(id));
        }
    }
}
