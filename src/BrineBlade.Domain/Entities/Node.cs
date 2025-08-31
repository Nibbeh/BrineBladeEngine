using System.Collections.Generic;

namespace BrineBlade.Domain.Entities;

public sealed record Node(
    string Id,
    string Title,
    string Description,
    List<NodeExit>? Exits = null,
    List<NodeOption>? Options = null
);

public sealed record NodeExit(string To, List<string>? Requires = null);

public sealed record NodeOption(
    string Id,
    string Label,
    List<string>? Requires = null,
    List<EffectSpec>? Effects = null
);
