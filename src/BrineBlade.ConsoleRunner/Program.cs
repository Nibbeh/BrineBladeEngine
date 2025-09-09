using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

using BrineBlade.AppCore.Bootstrap;
using BrineBlade.AppCore.Config;       // <-- StartMode enum lives here
using BrineBlade.AppCore.Flows;
using BrineBlade.AppCore.Orchestration;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Rules;

using BrineBlade.Infrastructure.DI;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Services.Abstractions;

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
Directory.CreateDirectory(contentRoot);
Directory.CreateDirectory(saveRoot);

// DI
var services = new ServiceCollection()
    .AddBrineBladeEngine(contentRoot, saveRoot);

var sp = services.BuildServiceProvider();

// choose start mode from args: --new or --seed (default: seed)
StartMode mode = args.Any(a => a.Equals("--new", StringComparison.OrdinalIgnoreCase))
    ? StartMode.NewGame
    : StartMode.Seeded;

// bootstrap state
var state = mode switch
{
    StartMode.NewGame => CharacterCreationFlow.MakeInitialState(),
    _ => TestSeed.MakeInitialState()
};

// wire UI + rules
var ui = new ConsoleGameUI();
var effects = new EffectProcessor(state,
    sp.GetRequiredService<IInventoryService>(), ui);

// session
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
