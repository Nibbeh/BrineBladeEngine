using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using BrineBlade.AppCore.Bootstrap;
using BrineBlade.AppCore.Orchestration;
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
Directory.CreateDirectory(saveRoot);

// 1) Seed first
var state = TestSeed.MakeInitialState();

// 2) Build services and register the seeded state instance
var services = new ServiceCollection()
    .AddBrineBladeEngine(contentRoot, saveRoot)
    .AddSingleton(state); // <- this is key

using var sp = services.BuildServiceProvider();

// 3) Use catalogs to augment the SAME instance (state in DI == this state)
var classCatalog = sp.GetRequiredService<ClassCatalog>();
var specCatalog = sp.GetRequiredService<SpecCatalog>();
// ... your class/spec flag logic (unchanged)

// 4) Compose session with the same state (fine to pass it directly)
var session = new GameSession(
    state,
    sp.GetRequiredService<IContentStore>(),
    sp.GetRequiredService<ISaveGameService>(),
    sp.GetRequiredService<IInventoryService>(),
    sp.GetRequiredService<ICombatService>(),
    sp.GetRequiredService<IEnemyCatalog>());

session.RunConsoleLoop();
