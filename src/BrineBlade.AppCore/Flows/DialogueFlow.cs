using System.Collections.Generic;
using System.Linq;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Flows;

public sealed class DialogueFlow
{
    private readonly GameState _state;
    private readonly IContentStore _content;
    private bool _end;

    public DialogueFlow(GameState state, IContentStore content)
    {
        _state = state;
        _content = content;
    }

    public void Run(string dialogueId)
    {
        var dlg = _content.GetDialogueById(dialogueId);
        if (dlg is null) { SimpleConsoleUI.Notice($"[DIALOGUE] Missing id={dialogueId}"); return; }

        _end = false;
        var lineId = dlg.StartLineId;

        while (!_end)
        {
            if (!dlg.Lines.TryGetValue(lineId, out var line))
            { SimpleConsoleUI.Notice($"[DIALOGUE] No line '{lineId}'."); return; }

            var choices = (line.Choices ?? new()).Where(c => PassesRequires(c.Requires)).ToList();

            // If there are NO choices: render the line, apply any line-level effects, pause, then exit dialogue.
            if (choices.Count == 0)
            {
                SimpleConsoleUI.RenderFrame(_state, $"Dialogue: {dlg.NpcId}", line.Text, new List<(string Key, string Label)>());
                if (line.Effects is not null) ApplyEffects(line.Effects);

                // Give the player a beat to read the line.
                System.Console.Write("\n(Press Enter to continue...)");
                System.Console.ReadLine();

                // End the dialogue (either via effect or because there are no choices).
                return;
            }

            // Normal dialogue line with choices
            var menu = new List<(string Key, string Label)>();
            for (int i = 0; i < choices.Count; i++) menu.Add(((i + 1).ToString(), choices[i].Label));

            SimpleConsoleUI.RenderFrame(_state, $"Dialogue: {dlg.NpcId}", line.Text, menu);

            var cmd = SimpleConsoleUI.ReadCommand(menu.Count);
            if (cmd.Type == ConsoleCommandType.Quit) { _end = true; return; }
            if (cmd.Type == ConsoleCommandType.Help) { SimpleConsoleUI.ShowHelp(); continue; }
            if (cmd.Type != ConsoleCommandType.Choose) { continue; }

            var chosen = choices[cmd.ChoiceIndex];

            if (chosen.Effects is not null) ApplyEffects(chosen.Effects);

            if (!string.IsNullOrWhiteSpace(chosen.Goto))
            {
                lineId = chosen.Goto!;
            }
            else if (_end)
            {
                return;
            }
            // else: loop and show the same line again (rare, but ok)
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

    private void ApplyEffects(List<EffectSpec> effects)
    {
        foreach (var e in effects)
        {
            switch (e.Op)
            {
                case "endDialogue": _end = true; break;
                case "setFlag" when !string.IsNullOrWhiteSpace(e.Id):
                    _state.Flags.Add(e.Id!);
                    SimpleConsoleUI.Notice($"Flag set: {e.Id}"); break;
                case "addGold" when e.Amount is not null:
                    _state.Gold += e.Amount!.Value;
                    SimpleConsoleUI.Notice($"+{e.Amount} gold (total {_state.Gold})"); break;
                case "advanceTime" when e.Minutes is not null:
                    _state.AdvanceMinutes(e.Minutes!.Value);
                    SimpleConsoleUI.Notice($"+{e.Minutes} min"); break;
                case "goto" when !string.IsNullOrWhiteSpace(e.To):
                    _state.CurrentNodeId = e.To!;
                    SimpleConsoleUI.Notice($"Travel → {_state.CurrentNodeId}"); break;
            }
        }
    }
}
