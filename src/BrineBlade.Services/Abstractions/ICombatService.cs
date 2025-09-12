// REPLACE ENTIRE FILE
// src/BrineBlade.Services/Abstractions/ICombatService.cs
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.Services.Abstractions
{
    public enum CombatAction
    {
        Attack = 1,
        Guard = 2,
        SecondWind = 3,
        UseItem = 4,
        Flee = 5
    }

    public interface ICombatCallbacks
    {
        CombatAction ChooseAction(
            int round,
            int playerHp,
            int playerMaxHp,
            int enemyHp,
            bool canSecondWind,
            bool hasUsableItem
        );

        void Show(string message);

        bool TryUseHealingItem(out int healAmount, out string label);
    }

    // Snapshot for Character Sheet / Player Menu
    public sealed record PlayerSnapshot(
        int MaxHp,
        int CurrentHp,
        int Strength,
        int Dexterity,
        int Intelligence,
        int Vitality,
        int Charisma,
        int Perception,
        int Luck,
        int ArmorClass,
        int DamageReduction,
        string WeaponLabel,
        int WeaponDie,
        int CritMin,
        int Penetration,
        bool HasShield,
        double ShieldBlockChance
    );

    public interface ICombatService
    {
        CombatResult StartCombat(GameState state, EnemyDef enemy);

        CombatResult StartCombatInteractive(GameState state, EnemyDef enemy, ICombatCallbacks cb);

        // For Player Menu / Character Sheet
        PlayerSnapshot GetPlayerSnapshot(GameState state);
    }
}
