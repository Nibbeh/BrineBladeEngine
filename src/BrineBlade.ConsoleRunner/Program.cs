using BrineBlade.AppCore.Bootstrap;
using BrineBlade.AppCore.Config;       // StartMode enum
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Flows;
using BrineBlade.AppCore.Orchestration;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Infrastructure.Persistence;
using BrineBlade.Infrastructure.Services;
using BrineBlade.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Text;

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

// Resolve content and saves
var contentRoot = FindUp("Content");
var saveRoot = FindUp("Saves");
Directory.CreateDirectory(saveRoot);

// DI
var services = new ServiceCollection();

// Content + catalogs
services.AddSingleton<IContentStore>(_ => new JsonContentStore(contentRoot));
services.AddSingleton<ItemCatalog>(_ => new ItemCatalog(contentRoot));
services.AddSingleton<ClassCatalog>(_ => new ClassCatalog(contentRoot));
services.AddSingleton<EnemyCatalog>(_ => new EnemyCatalog(contentRoot));
services.AddSingleton<IEnemyCatalog>(sp => sp.GetRequiredService<EnemyCatalog>());

// Persistence
services.AddSingleton<ISaveGameService>(_ => new JsonSaveGameService(saveRoot));

// RNG for combat, etc.  <<< NEW LINE
services.AddSingleton<IRandom, DefaultRandom>();

// Rules/services
services.AddSingleton<ICombatService, CombatService>();
services.AddSingleton<IDialogueService, DialogueService>();
services.AddSingleton<IInventoryService, InventoryService>();
services.AddSingleton<IWorldSimService, WorldSimService>();
services.AddSingleton<IClockService, ClockService>();

var sp = services.BuildServiceProvider();

// Choose start mode (default: seeded)
StartMode mode = StartMode.Seeded;
var arg = (Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? "").Trim().ToLowerInvariant();
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
