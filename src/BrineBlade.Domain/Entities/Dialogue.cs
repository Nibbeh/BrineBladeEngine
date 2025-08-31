using System.Collections.Generic;

namespace BrineBlade.Domain.Entities;

public sealed record Dialogue(
    string Id,
    string NpcId,
    string StartLineId,
    Dictionary<string, DialogueLine> Lines
);

public sealed record DialogueLine(
    string Text,
    List<DialogueChoice>? Choices = null,
    List<EffectSpec>? Effects = null
);

public sealed record DialogueChoice(
    string Label,
    string? Goto = null,
    List<string>? Requires = null,
    List<EffectSpec>? Effects = null
);
