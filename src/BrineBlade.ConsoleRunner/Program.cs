using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using BrineBlade.AppCore.Bootstrap;
using BrineBlade.AppCore.Config;       // StartMode enum
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Orchestration;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.DI;
using BrineBlade.Services.Abstractions;

// Helper to locate folders whether running from bin/ or repo root
static string FindUp(string leaf)
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 8; i++)
    {
        var probe = Path.Combine(dir, leaf);
        if (Directory.Exists(probe)) return probe;
        dir = Path.GetFullPath(Path.Combine(dir, ".."));
    }
    return Path.Combine(AppContext.BaseDirectory, leaf);
}

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var contentRoot = FindUp("Content");
var saveRoot = FindUp("Saves");
Directory.CreateDirectory(saveRoot);

// DI (use the engine extension for one source of truth)
var services = new ServiceCollection()
    .AddBrineBladeEngine(contentRoot, saveRoot); // also registers IRandom via DefaultRandom

var sp = services.BuildServiceProvider();

// Choose start mode (default: seeded)
StartMode mode = StartMode.Seeded;
var arg = (Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? "")
    .Trim().ToLowerInvariant();
if (arg is "new" or "newgame") mode = StartMode.NewGame;

// Prepare state
var state = mode switch
{
    StartMode.Seeded => TestSeed.MakeInitialState(),
    StartMode.NewGame => new GameState(
        new BrineBlade.Domain.Entities.Character("player", "Adventurer", "Human", "Warrior"),
        new BrineBlade.Domain.Entities.WorldState { Day = 1, Hour = 9, Minute = 0 },
        "N_START"),
    _ => TestSeed.MakeInitialState()
};

// UI + effects
IGameUI ui = new ConsoleGameUI();
var effects = new EffectProcessor(state, sp.GetRequiredService<IInventoryService>(), ui);

// Session
var session = new GameSession(
    state,
    sp.GetRequiredService<IContentStore>(),
    sp.GetRequiredService<ISaveGameService>(),
    sp.GetRequiredService<IInventoryService>(),
    sp.GetRequiredService<ICombatService>(),
    sp.GetRequiredService<IEnemyCatalog>(),
    ui,
    effects);

session.RunConsoleLoop();
