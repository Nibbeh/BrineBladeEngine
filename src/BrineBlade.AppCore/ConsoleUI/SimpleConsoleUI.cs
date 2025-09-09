using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BrineBlade.AppCore.ConsoleUI;

public enum ConsoleCommandType { Choose, Quit, Help, Refresh, Inventory, Save, Load, None }
public readonly record struct ConsoleCommand(ConsoleCommandType Type, int ChoiceIndex = -1);

public static class SimpleConsoleUI
{
    private const int MaxLog = 6;
    private static readonly Queue<string> _log = new();

    // --- Lightweight log API (works across screens) ---
    public static void Notice(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log.Enqueue(message);
        while (_log.Count > MaxLog) _log.Dequeue();
    }
    public static void Notice(IEnumerable<string> lines)
    {
        foreach (var l in lines) Notice(l);
    }

    // --- Standard node frame ---
    public static void RenderFrame(
        BrineBlade.Domain.Game.GameState state,
        string locationTitle,
        string locationDesc,
        IReadOnlyList<(string Key, string Label)> options)
    {
        Console.Clear();
        RenderHeader(state, locationTitle);
        Console.WriteLine(locationDesc);
        Console.WriteLine();
        RenderOptions(options);
        RenderFooter();
    }

    public static void RenderHeader(BrineBlade.Domain.Game.GameState state, string title)
    {
        // Prefer the seeded archetype; if absent, derive from the first "class.*" flag.
        var className = !string.IsNullOrWhiteSpace(state.Player.Archetype)
            ? state.Player.Archetype
            : Humanize(FirstFlagSuffix(state.Flags, "class.") ?? "�");

        // Pick first "spec.*" flag and humanize it for display.
        var specSuffix = "";
        var spec = FirstFlagSuffix(state.Flags, "spec.");
        if (!string.IsNullOrWhiteSpace(spec))
            specSuffix = $" ({Humanize(spec!)})";

        Console.WriteLine($"=== Brine & Blade � {title} ===");
        Console.WriteLine(
            $"Player: {state.Player.Name}  Class: {className}{specSuffix}  " +
            $"Gold: {state.Gold}  HP: {state.CurrentHp}  MP: {state.CurrentMana}  " +
            $"Time: Day {state.World.Day} {state.World.Hour:00}:{state.World.Minute:00}");
        Console.WriteLine(new string('-', 80));
    }

    public static void RenderOptions(IReadOnlyList<(string Key, string Label)> options)
    {
        for (int i = 0; i < options.Count; i++)
            Console.WriteLine($"  {options[i].Key}. {options[i].Label}");
        Console.WriteLine();
        Console.WriteLine("Commands: [H]elp  [I]nventory  [S]ave  [L]oad  [R]efresh  [Q]uit");
    }

    public static void RenderFooter()
    {
        Console.WriteLine(new string('-', 80));
        if (_log.Count > 0)
        {
            Console.WriteLine("Recent:");
            foreach (var line in _log)
                Console.WriteLine($"  � {line}");
        }
        Console.WriteLine();
        Console.Write("Choose (number) or command: ");
    }

    public static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Help:");
        Console.WriteLine("  - Type the number beside an option to perform it.");
        Console.WriteLine("  - H: Help   I: Inventory (stub)   S: Save   L: Load   R: Refresh   Q: Quit");
        Console.WriteLine();
        Pause();
    }

    public static ConsoleCommand ReadCommand(int optionsCount)
    {
        var input = (Console.ReadLine() ?? string.Empty).Trim();

        if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Quit);
        if (string.Equals(input, "h", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Help);
        if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Refresh);
        if (string.Equals(input, "i", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Inventory);
        if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Save);
        if (string.Equals(input, "l", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Load);

        if (int.TryParse(input, out var n) && n >= 1 && n <= optionsCount)
            return new(ConsoleCommandType.Choose, n - 1);

        Notice("Invalid input.");
        return new(ConsoleCommandType.None);
    }

    public static string Ask(string prompt)
    {
        Console.Write($"{prompt}: ");
        return (Console.ReadLine() ?? string.Empty).Trim();
    }

    public static void ShowSaves(IReadOnlyList<(int idx, string label)> lines)
    {
        Console.WriteLine();
        Console.WriteLine("Available saves:");
        foreach (var (idx, label) in lines) Console.WriteLine($"  {idx}. {label}");
        Console.WriteLine("  0. Cancel\n");
    }

    // --- Modal helpers for flows like Combat/Dialogue that should own the screen ---
    public static void RenderModal(
        BrineBlade.Domain.Game.GameState state,
        string title,
        IEnumerable<string> bodyLines,
        bool waitForEnter = true)
    {
        Console.Clear();
        RenderHeader(state, title);
        foreach (var line in bodyLines) Console.WriteLine(line);
        Console.WriteLine();
        Console.WriteLine(new string('-', 80));
        if (waitForEnter)
        {
            Console.Write("Press Enter to continue...");
            Console.ReadLine();
        }
    }

    public static void Pause(string prompt = "Press Enter to continue...")
    {
        Console.Write(prompt);
        Console.ReadLine();
    }

    // --- Helpers ---
    private static string? FirstFlagSuffix(IEnumerable<string> flags, string prefix)
    {
        foreach (var f in flags)
            if (f.StartsWith(prefix, StringComparison.Ordinal))
                return f[prefix.Length..];
        return null;
    }

    private static string Humanize(string slug)
    {
        var s = slug.Replace('_', ' ').Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}
