using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.ConsoleUI;

public enum ConsoleCommandType { Choose, Quit, Help, Refresh, Inventory, Save, Load, None }
public readonly record struct ConsoleCommand(ConsoleCommandType Type, int ChoiceIndex = -1);

/// <summary>
/// Minimal but robust console UI for the text RPG. Keeps a tiny message log,
/// renders frames/modals, and parses a handful of commands.
/// </summary>
public static class SimpleConsoleUI
{
    private const int MaxLog = 6;
    private static readonly Queue<string> _log = new();

    public static void RenderFrame(GameState state, string title, string body, IReadOnlyList<(string Key, string Label)> options)
    {
        Console.Clear();
        Console.OutputEncoding = Encoding.UTF8;

        // Header bar — Character has no BaseStats in this repo, so only show current HP.
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine($"Day {state.World.Day}  {state.World.Hour:00}:{state.World.Minute:00}  |  {state.Player.Name}  HP {state.CurrentHp}  Gold {state.Gold}");
        Console.WriteLine(new string('-', 72));

        // Body
        WriteWrapped(body);
        Console.WriteLine();
        Console.WriteLine(new string('-', 72));

        // Options
        if (options is { Count: > 0 })
        {
            foreach (var (key, label) in options)
                Console.WriteLine($" {key}. {label}");
        }
        else
        {
            Console.WriteLine(" (no choices — press [R] to refresh, [H] for help)");
        }

        Console.WriteLine();
        // Footer commands
        Console.WriteLine("[1..N]=choose   [I]nventory   [S]ave   [L]oad   [H]elp   [R]efresh   [Q]uit");
        Console.WriteLine();

        // Message log
        if (_log.Count > 0)
        {
            Console.WriteLine("Recent:");
            foreach (var line in _log.Reverse())
                Console.WriteLine($"  • {line}");
        }
    }

    public static void RenderModal(GameState state, string title, IReadOnlyList<string> lines, bool waitForEnter = true)
    {
        Console.Clear();
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine($"Day {state.World.Day}  {state.World.Hour:00}:{state.World.Minute:00}");
        Console.WriteLine();

        foreach (var line in lines)
            WriteWrapped(line);

        if (waitForEnter)
        {
            Console.WriteLine();
            Console.WriteLine("(press Enter to continue)");
            Console.ReadLine();
        }
    }

    public static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Help:");
        Console.WriteLine("  Type a number (1..N) to choose an option.");
        Console.WriteLine("  I = Inventory, S = Save, L = Load, H = Help, R = Refresh, Q = Quit.");
        Console.WriteLine();
    }

    /// <summary>
    /// Lists save slots in a simple numbered table (used by SaveGameFlow).
    /// </summary>
    public static void ShowSaves(IReadOnlyList<string> lines)
    {
        Console.WriteLine();
        Console.WriteLine("Saved Games:");
        Console.WriteLine(new string('-', 40));
        int i = 1;
        foreach (var l in lines)
        {
            Console.WriteLine($" {i,2}. {l}");
            i++;
        }
        Console.WriteLine(new string('-', 40));
    }

    /// <summary>
    /// Ask for a free-form input line with a prompt and return the entered string.
    /// </summary>
    public static string Ask(string prompt)
    {
        Console.Write(prompt.EndsWith(" ") ? prompt : prompt + " ");
        return (Console.ReadLine() ?? string.Empty).Trim();
    }

    public static ConsoleCommand ReadCommand(int optionsCount)
    {
        Console.Write("> ");
        var input = (Console.ReadLine() ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(input))
            return new ConsoleCommand(ConsoleCommandType.Refresh);

        // Allow "1", "2", etc. and also "choose 1"
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            if (n >= 1 && n <= optionsCount)
                return new ConsoleCommand(ConsoleCommandType.Choose, n - 1);
            return new ConsoleCommand(ConsoleCommandType.None);
        }

        var token = input.ToLowerInvariant();
        return token switch
        {
            "q" or "quit" or "exit" => new ConsoleCommand(ConsoleCommandType.Quit),
            "h" or "help" => new ConsoleCommand(ConsoleCommandType.Help),
            "r" or "refresh" => new ConsoleCommand(ConsoleCommandType.Refresh),
            "i" or "inv" or "inventory" => new ConsoleCommand(ConsoleCommandType.Inventory),
            "s" or "save" => new ConsoleCommand(ConsoleCommandType.Save),
            "l" or "load" => new ConsoleCommand(ConsoleCommandType.Load),
            _ => new ConsoleCommand(ConsoleCommandType.None),
        };
    }

    public static void Notice(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _log.Enqueue(message);
        while (_log.Count > MaxLog) _log.Dequeue();
    }

    public static void Notice(IEnumerable<string> messages)
    {
        foreach (var m in messages) Notice(m);
    }

    private static void WriteWrapped(string text, int width = 72)
    {
        if (string.IsNullOrEmpty(text)) { Console.WriteLine(); return; }
        var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        foreach (var w in words)
        {
            if (line.Length + w.Length + 1 > width)
            {
                Console.WriteLine(line);
                line = w;
            }
            else
            {
                line = line.Length == 0 ? w : line + " " + w;
            }
        }
        if (line.Length > 0) Console.WriteLine(line);
    }

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

