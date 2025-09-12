using System.Collections.Generic;

namespace BrineBlade.Domain.Entities;

public sealed record Node(
    string Id,
    string Title,
    string Description,
    List<string>? Paragraphs = null,          // NEW: supports Content JSON "Paragraphs"
    List<NodeExit>? Exits = null,
    List<NodeOption>? Options = null
);

// NEW: optional "Text" lets Content label exits explicitly
public sealed record NodeExit(
    string To,
    List<string>? Requires = null,
    string? Text = null
);

public sealed record NodeOption(
    string Id,
    string Label,
    List<string>? Requires = null,
    List<EffectSpec>? Effects = null
);
