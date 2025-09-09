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
        private bool _end;

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
            if (dlg is null) { _ui.Notice($"[DIALOGUE] Unknown dialogue '{dialogueId}'."); return; }

            _end = false;
            var lineId = dlg.StartLineId;

            while (!_end)
            {
                if (!dlg.Lines.TryGetValue(lineId, out var line))
                { _ui.Notice($"[DIALOGUE] No line '{lineId}'."); return; }

                var choices = (line.Choices ?? new List<DialogueChoice>()).Where(c => PassesRequires(c.Requires)).ToList();

                // No choices: show line, apply effects, exit.
                if (choices.Count == 0)
                {
                    _ui.RenderModal(_state, $"Dialogue: {dlg.NpcId}", new[] { line.Text }, waitForEnter: true);
                    var outcome = _effects.ApplyAll(line.Effects);
                    if (outcome.EndDialogue) return;
                    return;
                }

                // Normal line with choices
                var menu = choices.Select((choice, i) => ((i + 1).ToString(), choice.Label)).ToList();
                _ui.RenderFrame(_state, $"Dialogue: {dlg.NpcId}", line.Text, menu);

                var cmd = _ui.ReadCommand(menu.Count);
                if (cmd.Type == ConsoleCommandType.Quit) { _end = true; return; }
                if (cmd.Type == ConsoleCommandType.Help) { _ui.ShowHelp(); continue; }
                if (cmd.Type != ConsoleCommandType.Choose) { continue; }

                var chosen = choices[cmd.ChoiceIndex];

                var outcome2 = _effects.ApplyAll(chosen.Effects);
                if (outcome2.EndDialogue) return;

                if (!string.IsNullOrWhiteSpace(chosen.Goto))
                {
                    lineId = chosen.Goto!;
                }
                else if (_end)
                {
                    return;
                }
            }
        }

        private bool PassesRequires(List<string>? reqs)
        {
            if (reqs is null || reqs.Count == 0) return true;
            foreach (var r in reqs)
            {
                if (r.StartsWith("flag:", System.StringComparison.OrdinalIgnoreCase))
                {
                    var flag = r[5..];
                    if (!_state.Flags.Contains(flag)) return false;
                }
            }
            return true;
        }
    }
}
