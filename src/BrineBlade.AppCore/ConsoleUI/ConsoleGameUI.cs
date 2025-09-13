
// src/BrineBlade.AppCore/ConsoleUI/ConsoleGameUI.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.ConsoleUI
{
    /// <summary>
    /// Visual-only console skin. Same API, nicer presentation & spacing.
    /// - Crisp box with consistent padding
    /// - Stable HUD (Day/Time | Name | HP | Gold)
    /// - Body renderer that preserves preformatted tables (inventory/equipment)
    /// - Options grid with auto 1- or 2-column layout, evenly spaced
    /// </summary>
    public sealed class ConsoleGameUI : IGameUI
    {
        // Box glyphs
        private const string V = "│";
        private const string TL = "┌";
        private const string TR = "┐";
        private const string BL = "└";
        private const string BR = "┘";
        private const string SEP_L = "├";
        private const string SEP_R = "┤";

        // Theme (foreground only; background left default for readability)
        private static readonly ConsoleColor TitleFg = ConsoleColor.Cyan;
        private static readonly ConsoleColor BorderFg = ConsoleColor.DarkGray;
        private static readonly ConsoleColor BodyFg = ConsoleColor.Gray;
        private static readonly ConsoleColor HudLabelFg = ConsoleColor.DarkGray;
        private static readonly ConsoleColor HudValueFg = ConsoleColor.White;
        private static readonly ConsoleColor HintFg = ConsoleColor.DarkGray;

        public void RenderFrame(
            GameState state,
            string title,
            string body,
            IReadOnlyList<(string Key, string Label)> options
        )
        {
            Console.Clear();

            int width = Clamp(Console.WindowWidth - 2, 78, 120);
            int inner = width - 4; // content width inside borders

            // Header
            DrawTopBorder(width);
            DrawTitle(title, width);
            DrawSep(width);
            DrawHud(state, width);
            DrawSep(width);

            // Body (smart wrap with table preservation)
            WriteBody(body ?? string.Empty, inner, width);

            // Options (auto 1- or 2-column)
            if (options is { Count: > 0 })
            {
                DrawSep(width);
                WriteOptions(options, inner, width);
            }

            // Footer
            DrawBottomBorder(width);
            WriteHints(); // <- updated text to say [I] Player Menu
            Console.Write("> ");
        }

        public void RenderModal(
            GameState state,
            string title,
            IReadOnlyList<string> lines,
            bool waitForEnter = true)
        {
            Console.Clear();

            int width = Clamp(Console.WindowWidth - 2, 78, 120);

            DrawTopBorder(width);
            DrawTitle(title, width);
            DrawSep(width);

            var inner = width - 4;
            foreach (var line in lines ?? Array.Empty<string>())
                BoxLine(line, width);

            DrawBottomBorder(width);
            if (waitForEnter)
            {
                Console.ForegroundColor = HintFg;
                Console.Write("Press Enter...");
                Console.ResetColor();
                Console.ReadLine();
            }
        }

        public ConsoleCommand ReadCommand(int optionCount)
        {
            while (true)
            {
                var input = Console.ReadLine()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(input))
                    return new ConsoleCommand(ConsoleCommandType.Refresh);

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Quit);

                if (string.Equals(input, "?", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "h", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "help", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Help);

                if (string.Equals(input, "i", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "inv", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "/inv", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Inventory); // now opens Player Menu

                if (string.Equals(input, "s", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "save", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Save);

                if (string.Equals(input, "l", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "load", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Load);

                // numbered choices
                if (int.TryParse(input, out int n) && n >= 1 && n <= optionCount)
                    return new ConsoleCommand(ConsoleCommandType.Choose, n - 1);

                // gentle re-prompt
                Console.ForegroundColor = HintFg;
                Console.WriteLine("Enter a valid number, 'i' for player menu, '?' for help, or 'q' to quit.");
                Console.ResetColor();
                Console.Write("> ");
            }
        }

        public void ShowHelp()
        {
            Console.ForegroundColor = HintFg;
            Console.WriteLine("Hotkeys: [1..N] choose  •  [I] player menu  •  [?] help  •  [Q] quit/back");
            Console.ResetColor();
            Console.Write("> ");
        }

        // Bubble to your existing rolling log
        public void Notice(string message) => SimpleConsoleUI.Notice(message);
        public void Notice(IEnumerable<string> messages) => SimpleConsoleUI.Notice(messages);

        // ───────────────────────────── visual helpers ─────────────────────────────

        private static void DrawTopBorder(int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.WriteLine($"{TL}{new string('─', width - 2)}{TR}");
            Console.ResetColor();
        }

        private static void DrawBottomBorder(int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.WriteLine($"{BL}{new string('─', width - 2)}{BR}");
            Console.ResetColor();
        }

        private static void DrawTitle(string title, int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.Write($"{V} ");
            Console.ResetColor();

            Console.ForegroundColor = TitleFg;
            Console.Write(PadCenter(title ?? string.Empty, width - 4));
            Console.ResetColor();

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
        }

        private static void DrawSep(int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.WriteLine($"{SEP_L}{new string('─', width - 2)}{SEP_R}");
            Console.ResetColor();
        }

        private static void DrawHud(GameState state, int width)
        {
            string left = $"Day: {state.World.Day}   Time: {state.World.Hour:00}:{state.World.Minute:00}";
            string right = $"Name: {state.Player.Name}   HP: {state.CurrentHp}   Gold: {state.Gold}";

            int inner = width - 4;
            int gap = Math.Max(2, inner - left.Length - right.Length);

            Console.ForegroundColor = BorderFg;
            Console.Write($"{V} ");
            Console.ResetColor();

            WriteHudPart(left);
            Console.Write(new string(' ', gap));
            WriteHudPart(right);

            int used = left.Length + gap + right.Length;
            if (used < inner) Console.Write(new string(' ', inner - used));

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();

            static void WriteHudPart(string s)
            {
                var parts = s.Split("   ", StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    int idx = p.IndexOf(':');
                    if (idx > 0)
                    {
                        Console.ForegroundColor = HudLabelFg;
                        Console.Write(p.Substring(0, idx + 2)); // "Label: "
                        Console.ForegroundColor = HudValueFg;
                        Console.Write(p[(idx + 2)..]);
                    }
                    else
                    {
                        Console.ForegroundColor = HudValueFg;
                        Console.Write(p);
                    }
                    Console.ResetColor();
                    if (i < parts.Length - 1) Console.Write("   ");
                }
            }
        }

        private static void WriteBody(string body, int inner, int width)
        {
            if (string.IsNullOrEmpty(body))
            {
                BoxLine(string.Empty, width);
                return;
            }

            // Preserve preformatted tables: if a line contains '│' or '┼' or '─', print verbatim
            var lines = body.Replace("\r\n", "\n").Split('\n');
            bool isTableBlock = lines.Any(l => l.Contains('│') || l.Contains('┼') || l.Contains('─'));

            if (isTableBlock)
            {
                foreach (var l in lines) BoxLine(l, width);
                return;
            }

            foreach (var para in body.Split(new[] { "\n\n" }, StringSplitOptions.None))
            {
                foreach (var wrapped in Wrap(para, inner))
                    BoxLine(wrapped, width);
                BoxLine(string.Empty, width);
            }
        }

        private static void WriteOptions(IReadOnlyList<(string Key, string Label)> options, int inner, int width)
        {
            // 2-column layout if enough width; else 1-column
            bool twoCol = inner >= 70 && options.Count >= 4;
            if (!twoCol)
            {
                foreach (var (k, l) in options)
                    BoxLine($" {k}. {l}", width);
            }
            else
            {
                int half = (inner - 2) / 2;
                for (int i = 0; i < options.Count; i += 2)
                {
                    var left = $" {options[i].Key}. {options[i].Label}";
                    var right = (i + 1 < options.Count) ? $" {options[i + 1].Key}. {options[i + 1].Label}" : string.Empty;
                    BoxLine(left.PadRight(half) + "  " + right, width);
                }
            }
        }

        private static void WriteHints()
        {
            Console.ForegroundColor = HintFg;
            Console.WriteLine();
            Console.WriteLine("Tips: [1..N]=choose   [I] Player Menu   [?] help   [Q] quit/back");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void BoxLine(string text, int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.Write($"{V} ");
            Console.ResetColor();

            Console.ForegroundColor = BodyFg;
            Console.Write(text.PadRight(width - 4));
            Console.ResetColor();

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
        }

        // ───────────────────────────── util helpers ─────────────────────────────

        private static IEnumerable<string> Wrap(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return string.Empty;
                yield break;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            foreach (var w in words)
            {
                if (line.Length + (line.Length > 0 ? 1 : 0) + w.Length > maxWidth)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(w);
            }
            if (line.Length > 0) yield return line.ToString();
        }

        private static string PadCenter(string s, int width)
        {
            s ??= string.Empty;
            if (s.Length >= width) return s[..width];
            int pad = width - s.Length;
            return new string(' ', pad / 2) + s + new string(' ', pad - pad / 2);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
