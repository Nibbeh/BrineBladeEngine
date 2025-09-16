
using Xunit;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

public class RuntimeRulesTests
{
    private GameState Seed()
    {
        var s = new GameState(new Character("p","Test","Human","Warrior"),
                              new WorldState{Day=1,Hour=9,Minute=0},
                              "N_START");
        s.Flags.Add("race.human");
        s.Flags.Add("class.warrior");
        s.Flags.Add("spec.champion");
        return s;
    }

    [Fact]
    public void Requires_Dotted_And_Colon_Syntax_Passes()
    {
        var s = Seed();
        s.Flags.Add("race.elf"); // simulate elf alt
        s.Flags.Add("class.rogue");
        s.Flags.Add("spec.templar");
        Assert.True(RequiresEvaluator.Passes(s, new() {"race.elf","class:rogue","spec:templar"}));
    }

    [Fact]
    public void Stat_Expression_Uses_Flag_Seasoning()
    {
        var s = Seed();
        s.Flags.Add("class.mage");
        Assert.True(RequiresEvaluator.Passes(s, new() {"stat:Intelligence>=12"}));
    }
}
