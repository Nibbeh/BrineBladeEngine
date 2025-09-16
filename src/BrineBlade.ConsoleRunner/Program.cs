using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using BrineBlade.AppCore.Bootstrap;
using BrineBlade.AppCore.Config;       // StartMode enum
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.AppCore.Orchestration;
using BrineBlade.AppCore.Rules;
using BrineBlade.Domain.Game;
using BrineBlade.Infrastructure.Content; // ContentLinter
using BrineBlade.Infrastructure.DI;
using BrineBlade.Services.Abstractions;

// Helper to locate folders whether running from bin/ or repo root
static string FindUp(string leaf)
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 8; i++)
    {
        var probe = Path.Combine(dir, leaf);
        if (Directory.Exists(probe)) return Path.GetFullPath(probe);
        dir = Path.GetFullPath(Path.Combine(dir, ".."));
    }
    return Path.Combine(AppContext.BaseDirectory, leaf);
}

// --- Read-only scanner for smart punctuation / BOM (never edits files) ---
static void ScanContentForEncodingIssues(string root)
{
    int flagged = 0;
    foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
    {
        var bytes = File.ReadAllBytes(file);
        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string text = File.ReadAllText(file, Encoding.UTF8);
        var bad = Regex.Matches(text, "[\u2018\u2019\u201C\u201D\u2013\u2014]");
        if (hasBom || bad.Count > 0)
        {
            Console.WriteLine($"[SCAN] {file}");
            if (hasBom) Console.WriteLine("       - UTF-8 BOM detected");
            if (bad.Count > 0)
            {
                var uniques = bad.Select(m => m.Value[0]).Distinct().Select(ch => $"U+{(int)ch:X4}");
                Console.WriteLine("       - Smart chars: " + string.Join(" ", uniques));
            }
            flagged++;
        }
    }
    Console.WriteLine(flagged == 0 ? "[SCAN] No issues found." : $"[SCAN] {flagged} file(s) flagged.");
}

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// --- Content/Saves resolution with overrides ---
var cli = args; // use implicit top-level 'args' (no redeclare)

string? contentOverrideArg = cli.FirstOrDefault(a =>
    a.StartsWith("--content=", StringComparison.OrdinalIgnoreCase))
    ?.Split('=', 2)[1].Trim('"');

var contentEnv = Environment.GetEnvironmentVariable("BRINEBLADE_CONTENT");

var contentRoot = !string.IsNullOrWhiteSpace(contentOverrideArg) ? contentOverrideArg
               : !string.IsNullOrWhiteSpace(contentEnv) ? contentEnv!
               : FindUp("Content");

contentRoot = Path.GetFullPath(contentRoot);
var saveRoot = Path.GetFullPath(FindUp("Saves"));
Directory.CreateDirectory(saveRoot);

if (!Directory.Exists(contentRoot))
{
    Console.Error.WriteLine($"[FATAL] Content folder not found: {contentRoot}");
    Console.Error.WriteLine("Tip: pass --content=\"C:\\path\\to\\Content\" or set BRINEBLADE_CONTENT env var.");
    return;
}

if (cli.Any(a => a.Equals("--scan-encoding", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine($"[INFO] Scanning Content for encoding/punctuation issues: {contentRoot}");
    ScanContentForEncodingIssues(contentRoot);
    return;
}

Console.WriteLine($"[INFO] Using Content: {contentRoot}");
Console.WriteLine($"[INFO] Using Saves:   {saveRoot}");

// --- Content preflight (counts) ---
var summary = ContentLinter.Summarize(contentRoot);
Console.WriteLine($"[CHK] Nodes={summary.NodeCount} Dialogues={summary.DialogueCount} Items={summary.ItemCount} Enemies={summary.EnemyCount} Classes={summary.ClassCount}");

// --- Strict validation (fail fast) ---
var schemaRoot = Path.Combine(contentRoot, "schemas"); // expects Content/schemas/*
try
{
    ContentLinter.ValidateOrThrow(contentRoot, schemaRoot);
    Console.WriteLine("[CHK] Schema + content validation: OK");
}
catch (ContentValidationException ex)
{
    Console.Error.WriteLine("[FATAL] Content validation failed:");
    foreach (var err in ex.Errors) Console.Error.WriteLine(" - " + err);
    return;
}
catch (Exception ex)
{
    Console.Error.WriteLine("[FATAL] Content validation crashed:");
    Console.Error.WriteLine(ex.ToString());
    return;
}

// DI (use the engine extension for one source of truth)
var services = new ServiceCollection()
    .AddBrineBladeEngine(contentRoot, saveRoot); // also registers IRandom via DefaultRandom

var sp = services.BuildServiceProvider();

// Choose start mode (default: seeded). You can still pass 'new' or 'newgame'.
StartMode mode =
    cli.Any(a => a.Equals("new", StringComparison.OrdinalIgnoreCase) ||
                 a.Equals("newgame", StringComparison.OrdinalIgnoreCase))
    ? StartMode.NewGame
    : StartMode.Seeded;

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

// --- Quick must-have spot-checks (keeps your previous checks) ---
var store = sp.GetRequiredService<IContentStore>();
string[] mustHave = { "N_START", "N_MAIN_GATE_BRIDGE", "N_SHANTY_ALLEY" }; // adjust to your seed/tutorial IDs

foreach (var id in mustHave)
{
    bool ok = store.GetNodeById(id) is not null;
    Console.WriteLine($"[CHK] Node '{id}': {(ok ? "OK" : "MISSING")}");
}
if (store.GetNodeById("N_START") is null)
{
    Console.Error.WriteLine("[FATAL] Required node 'N_START' missing in current Content. Check path/JSON.");
    return;
}

// Session
var session = new GameSession(
    state,
    store,
    sp.GetRequiredService<ISaveGameService>(),
    sp.GetRequiredService<IInventoryService>(),
    sp.GetRequiredService<ICombatService>(),
    sp.GetRequiredService<IEnemyCatalog>(),
    ui,
    effects);

session.RunConsoleLoop();
