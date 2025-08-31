namespace BrineBlade.Domain.Entities;

public sealed record Stats(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Vitality,
    int Charisma,
    int Perception,
    int Luck)
{
    // Derived values – very simple for now
    public int MaxHp => Vitality * 10 + Strength * 2;
    public int MaxMana => Intelligence * 5 + Perception * 2;
    public int CritChance => Luck + Dexterity / 2;
}
