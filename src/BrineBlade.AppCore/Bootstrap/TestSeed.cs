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
            // Baselines so you never enter combat at 0/0
            CurrentHp = 20,
            CurrentMana = 10
        };

        // Optional: flags so your header can humanize class/spec
        state.Flags.Add("class.warrior");
        state.Flags.Add("spec.champion");

        return state;
    }
}
