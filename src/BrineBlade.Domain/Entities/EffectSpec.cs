namespace BrineBlade.Domain.Entities;

public sealed record EffectSpec(
    string Op,
    string? Id = null,
    string? To = null,
    int? Minutes = null,
    int? Amount = null,
    int? Qty = null
);
