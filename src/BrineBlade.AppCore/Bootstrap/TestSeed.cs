using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.Bootstrap;

public static class TestSeed
{
    public static GameState MakeInitialState()
    {
        var player = new Character("player", "Mykhel", "Human", "Warrior");
        var world = new WorldState { Day = 1, Hour = 9, Minute = 0 };

        var state = new GameState(player, world, "N_START")
        {
            CurrentHp = 20,
            CurrentMana = 10
        };

        // flags you already rely on
        state.Flags.Add("class.warrior");
        state.Flags.Add("spec.champion");
        state.Gold = Math.Max(state.Gold, 10);

        // --- starter kit: must match Content/items/*.json exactly ---
        state.Inventory.Add(new ItemStack("ITM_HEALTH_POTION_MINOR", 3));
        state.Inventory.Add(new ItemStack("ITM_BREAD", 2));

        state.Equipment[EquipmentSlot.Weapon] = "ITM_SWORD_SHORT";
        state.Equipment[EquipmentSlot.Offhand] = "ITM_SHIELD_WOOD";
        state.Equipment[EquipmentSlot.Chest] = "ITM_ARMOR_LEATHER";
        state.Equipment[EquipmentSlot.Head] = "ITM_CAP_CLOTH";

        return state;
    }
}
