using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.Services.Abstractions
{
    public enum CombatAction { Attack = 1, Guard = 2, SecondWind = 3, UseItem = 4, Flee = 5 }

    public interface ICombatCallbacks
    {
        // Ask the UI which action to take
        CombatAction ChooseAction(int round, int playerHp, int playerMaxHp, int enemyHp, bool canSecondWind, bool hasUsableItem);
        // Stream a line of combat text to the UI
        void Show(string message);
        // Try to consume a healing item and return how much it heals + a label to show
        bool TryUseHealingItem(out int healAmount, out string label);
    }

    public interface ICombatService
    {
        // Keeps your current auto-resolve (used by scripts if you want)
        CombatResult StartCombat(GameState state, EnemyDef enemy);

        // New: interactive, per-turn combat with UI callbacks
        CombatResult StartCombatInteractive(GameState state, EnemyDef enemy, ICombatCallbacks cb);
    }
}
