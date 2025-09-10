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
    public sealed class DialogueFlow
    {
        private readonly GameState _state;
        private readonly IContentStore _content;
        private readonly IGameUI _ui;
        private readonly EffectProcessor _effects;

        public DialogueFlow(GameState state, IContentStore content, IGameUI ui, EffectProcessor effects)
        {
            _state = state;
            _content = content;
            _ui = ui;
            _effects = effects;
        }

        public void Run(string dialogueId)
        {
            var dlg = _content.GetDialogueById(dialogueId);
            if (dlg is null)
            {
                _ui.Notice($"Unknown dialogue '{dialogueId}'.");
                return;
            }

            var lineId = dlg.StartLineId;
            while (true)
            {
                if (!dlg.Lines.TryGetValue(lineId, out var line))
                {
                    _ui.Notice($"Dialogue line '{lineId}' missing.");
                    return;
                }

                // Build choices that pass Requires
                var choices = (line.Choices ?? new List<DialogueChoice>())
                    .Where(c => PassesRequires(c.Requires))
                    .Select((c, i) => (Key: (i + 1).ToString(), Choice: c))
                    .ToList();

                // Render
                var body = line.Text;
                var options = choices.Select(c => (c.Key, c.Choice.Label)).ToList();
                if (options.Count == 0) options.Add(("0", "[Continue]"));
                _ui.RenderFrame(_state, $"Dialogue: {dlg.NpcId}", body, options);

                var cmd = _ui.ReadCommand(options.Count);
                if (cmd.Type == ConsoleCommandType.Quit || cmd.Type == ConsoleCommandType.None) return;
                if (cmd.Type == ConsoleCommandType.Help) { _ui.ShowHelp(); continue; }
                if (cmd.Type != ConsoleCommandType.Choose)
                {
                    _ui.Notice("Choose an option (1..N).");
                    continue;
                }

                if (choices.Count == 0) // single continue
                {
                    // Apply inline effects if any
                    _effects.ApplyAll(line.Effects,
                        startDialogue: id => Run(id),
                        startCombat: id => { /* started later by NodeFlow */ });

                    return; // end of leaf
                }

                var chosen = choices[cmd.ChoiceIndex].Choice;

                // Apply effects then branch
                bool ended = false;
                if (chosen.Effects is { Count: > 0 })
                {
                    foreach (var e in chosen.Effects)
                        if (e.Op.Equals("endDialogue", StringComparison.OrdinalIgnoreCase))
                            ended = true;

                    _effects.ApplyAll(chosen.Effects,
                        startDialogue: id => Run(id),
                        startCombat: id => { /* started later by NodeFlow */ });
                }

                if (ended) return;

                if (!string.IsNullOrWhiteSpace(chosen.Goto))
                {
                    lineId = chosen.Goto!;
                    continue;
                }

                // No goto → finish line
                return;
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
                    // Special inventory virtual-flag: "flag:inv.ITM_ID"
                    if (flag.StartsWith("inv.", StringComparison.OrdinalIgnoreCase))
                    {
                        var id = flag[4..];
                        var hasInBag = _state.Inventory.Any(s => s.ItemId == id && s.Quantity > 0);
                        var equipped = _state.Equipment.Values.Any(v => string.Equals(v, id, StringComparison.Ordinal));
                        if (!hasInBag && !equipped) return false;
                    }
                    else
                    {
                        if (!_state.Flags.Contains(flag)) return false;
                    }
                }
            }
            return true;
        }
    }
}

