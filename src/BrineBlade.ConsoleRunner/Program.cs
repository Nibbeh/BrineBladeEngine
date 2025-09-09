using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using BrineBlade.AppCore.Bootstrap;
   using BrineBlade.AppCore.Flows;
using BrineBlade.AppCore.Orchestration;
using BrineBlade.Infrastructure.Content;
using BrineBlade.Infrastructure.DI;
using BrineBlade.Services.Abstractions;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Rules;

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

static StartMode ParseStartMode(string[] args)
{
    // Priority: CLI arg --new-game / --seeded > ENV BRINEBLADE_START
    if (args.Any(a => string.Equals(a, "--new-game", StringComparison.OrdinalIgnoreCase)))
        return StartMode.NewGame;
    if (args.Any(a => string.Equals(a, "--seeded", StringComparison.OrdinalIgnoreCase)))
        return StartMode.Seeded;

    var env = Environment.GetEnvironmentVariable("BRINEBLADE_START");
    return string.Equals(env, "NewGame", StringComparison.OrdinalIgnoreCase)
        ? StartMode.NewGame
        : StartMode.Seeded;
}

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var contentRoot = FindUp("Content");
var saveRoot = FindUp("Saves");
Directory.CreateDirectory(saveRoot);

// --- Preflight content validation (hard gate) ---
try
{
    var pre = ContentLinter.Preflight(contentRoot, EffectProcessor.KnownOps);
    Console.WriteLine($"[Content OK] Nodes={pre.NodeCount}, Dialogues={pre.DialogueCount}, Items={pre.ItemCount}, Enemies={pre.EnemyCount}");
}
catch (ContentValidationException ex)
{
    Console.WriteLine("[Content ERROR]");
    foreach (var line in ex.Errors.Take(10))
        Console.WriteLine(" - " + line);
    if (ex.Errors.Count > 10) Console.WriteLine($" ... and {ex.Errors.Count - 10} more.");
    return; // fail fast
}

// --- Choose start mode (seeded vs new game) ---
var startMode = ParseStartMode(args);

// Build initial GameState using the chosen mode
var state = startMode == StartMode.Seeded
    ? TestSeed.MakeInitialState()
    : CharacterCreationFlow.MakeInitialState(); // placeholder now, real flow later

// Deterministic RNG seed (configurable via ENV; defaults to 12345)
var seed = int.TryParse(Environment.GetEnvironmentVariable("BRINEBLADE_RNG_SEED"), out var s) ? s : 12345;

// 2) Build services and register the chosen GameState instance
var services = new ServiceCollection()
    .AddBrineBladeEngine(contentRoot, saveRoot)
    .AddDeterministicRandom(seed) // IRandom → SeededRandom
    .AddSingleton(state); // the exact instance used by session

using var sp = services.BuildServiceProvider();

// 3) Catalogs / UI / Effects / Session
var classCatalog = sp.GetRequiredService<ClassCatalog>();
var specCatalog = sp.GetRequiredService<SpecCatalog>();
// (your existing class/spec logic can remain)

var ui = new ConsoleGameUI();
var effects = new EffectProcessor(state, sp.GetRequiredService<IInventoryService>(), ui);

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
