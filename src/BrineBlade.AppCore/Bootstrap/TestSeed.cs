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
            CurrentHp = 20,   // never start at 0
            CurrentMana = 10
        };

        // Show class/spec nicely in the header
        state.Flags.Add("class.warrior");
        state.Flags.Add("spec.champion");

        // Give a little money for testing (Program may add Class StartGold on top)
        state.Gold = Math.Max(state.Gold, 10);

        return state;
    }
}
