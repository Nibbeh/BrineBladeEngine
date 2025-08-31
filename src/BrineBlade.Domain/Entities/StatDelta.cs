namespace BrineBlade.Domain.Entities;

// Additive modifiers from gear (extend later if needed)
public sealed record StatDelta(
    int Strength = 0, int Dexterity = 0, int Intelligence = 0,
    int Vitality = 0, int Charisma = 0, int Perception = 0, int Luck = 0)
{
    public static StatDelta operator +(StatDelta a, StatDelta b) =>
        new(a.Strength + b.Strength, a.Dexterity + b.Dexterity, a.Intelligence + b.Intelligence,
            a.Vitality + b.Vitality, a.Charisma + b.Charisma, a.Perception + b.Perception, a.Luck + b.Luck);
}
