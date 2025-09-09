using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.ConsoleUI;

public enum ConsoleCommandType { Choose, Quit, Help, Refresh, Inventory, Save, Load, None }
public readonly record struct ConsoleCommand(ConsoleCommandType Type, int ChoiceIndex = -1);

public static class SimpleConsoleUI
{
    private const int MaxLog = 6;
    private static readonly Queue<string> _log = new();

    // ----------------- small log shown at bottom of frames -----------------
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

    // ----------------- core frame / modal rendering -----------------
    public static void RenderFrame(GameState state, string title, string body, IReadOnlyList<(string Key, string Label)> options)
    {
        Console.Clear();

        // Header
        Console.WriteLine($"== {title} ==");
        Console.WriteLine($"Gold: {state.Gold}  |  Day {state.World.Day}  {state.World.Hour:00}:{state.World.Minute:00}");
        Console.WriteLine(new string('-', 64));

        // Body
        if (!string.IsNullOrWhiteSpace(body))
        {
            foreach (var line in Wrap(body, 80)) Console.WriteLine(line);
            Console.WriteLine();
        }

        // Options
        for (int i = 0; i < options.Count; i++)
        {
            var (key, label) = options[i];
            var hot = key.Length == 1 ? key.ToUpperInvariant() : $"{i + 1}";
            Console.WriteLine($"{i + 1,2}. {label}  [{hot}]");
        }

        // Footer / help
        Console.WriteLine();
        Console.WriteLine("[H]elp  [I]nventory  [S]ave  [L]oad  [R]efresh  [Q]uit");
        Console.WriteLine(new string('-', 64));

        // Log
        if (_log.Count > 0)
        {
            foreach (var msg in _log) Console.WriteLine($"> {msg}");
            Console.WriteLine(new string('-', 64));
        }

        Console.Write("Choose: ");
    }

    public static void RenderModal(GameState state, string title, IReadOnlyList<string> lines, bool waitForEnter = true)
    {
        Console.Clear();
        Console.WriteLine($"== {title} ==");
        Console.WriteLine($"Gold: {state.Gold}  |  Day {state.World.Day}  {state.World.Hour:00}:{state.World.Minute:00}");
        Console.WriteLine(new string('-', 64));

        foreach (var ln in lines.SelectMany(l => Wrap(l, 80)))
            Console.WriteLine(ln);

        Console.WriteLine(new string('-', 64));
        if (waitForEnter)
        {
            Console.Write("Press Enter...");
            Console.ReadLine();
        }
    }

    public static void ShowHelp()
    {
        Console.Clear();
        Console.WriteLine("Controls");
        Console.WriteLine(new string('-', 32));
        Console.WriteLine("  - Enter the number beside an option to choose it.");
        Console.WriteLine("  - H: Help   I: Inventory   S: Save   L: Load   R: Refresh   Q: Quit");
        Console.WriteLine();
        Pause();
    }

    // ----------------- save/load helpers -----------------
    public static void ShowSaves(IReadOnlyList<(int Index, string Line)> lines)
    {
        Console.Clear();
        Console.WriteLine("Save Slots");
        Console.WriteLine(new string('-', 64));
        foreach (var (idx, text) in lines)
            Console.WriteLine($"{idx,2}. {text}");
        Console.WriteLine(new string('-', 64));
    }

    public static int Ask(string prompt)
    {
        Console.Write($"{prompt} ");
        var s = (Console.ReadLine() ?? string.Empty).Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
    }

    // ----------------- input -----------------
    public static ConsoleCommand ReadCommand(int optionsCount)
    {
        var input = (Console.ReadLine() ?? string.Empty).Trim();

        if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Quit);
        if (string.Equals(input, "h", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Help);
        if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Refresh);
        if (string.Equals(input, "i", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Inventory);
        if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Save);
        if (string.Equals(input, "l", StringComparison.OrdinalIgnoreCase)) return new(ConsoleCommandType.Load);

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            var idx = n - 1;
            if (idx >= 0 && idx < optionsCount) return new(ConsoleCommandType.Choose, idx);
        }

        return new(ConsoleCommandType.None);
    }

    // ----------------- small helpers -----------------
    private static void Pause()
    {
        Console.Write("Press Enter...");
        Console.ReadLine();
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        foreach (var w in words)
        {
            var probe = (line.Length == 0) ? w : line + " " + w;
            if (probe.Length > width)
            {
                if (line.Length > 0) { yield return line; line = w; }
                else { yield return w; line = ""; }
            }
            else line = probe;
        }
        if (line.Length > 0) yield return line;
    }

    // (kept from your older helper set; used by Node banners etc.)
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
