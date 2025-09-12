// REPLACE ENTIRE FILE
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
            Console.OutputEncoding = Encoding.UTF8;

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
            WriteHints();
            Console.Write("> ");
        }

        public void RenderModal(
            GameState state,
            string title,
            IReadOnlyList<string> lines,
            bool waitForEnter = true
        )
        {
            Console.OutputEncoding = Encoding.UTF8;

            int width = Clamp(Console.WindowWidth - 2, 70, 120);
            int inner = width - 4;

            DrawTopBorder(width);
            DrawTitle(title, width);
            DrawSep(width);

            // Preserve table-like lines; wrap prose
            foreach (var raw in lines ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(raw))
                {
                    BoxLine(string.Empty, width);
                    continue;
                }

                if (IsPreformatted(raw))
                {
                    BoxLine(Truncate(raw, inner), width);
                }
                else
                {
                    foreach (var w in Wrap(raw, inner))
                        BoxLine(w, width);
                }
            }

            DrawBottomBorder(width);

            if (waitForEnter)
            {
                Console.ForegroundColor = HintFg;
                Console.WriteLine("Press Enter...");
                Console.ResetColor();
                Console.ReadLine();
            }
        }

        public ConsoleCommand ReadCommand(int optionCount)
        {
            while (true)
            {
                string input = Console.ReadLine()?.Trim() ?? string.Empty;

                // global hotkeys
                if (string.Equals(input, "?", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Help, -1);

                if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Quit, -1);

                if (string.Equals(input, "i", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "inv", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "inventory", StringComparison.OrdinalIgnoreCase))
                    return new ConsoleCommand(ConsoleCommandType.Inventory, -1);

                // numbered choices
                if (int.TryParse(input, out int n) && n >= 1 && n <= optionCount)
                    return new ConsoleCommand(ConsoleCommandType.Choose, n - 1);

                // gentle re-prompt
                Console.ForegroundColor = HintFg;
                Console.WriteLine("Enter a valid number, 'i' for inventory, '?' for help, or 'q' to quit.");
                Console.ResetColor();
                Console.Write("> ");
            }
        }

        public void ShowHelp()
        {
            Console.ForegroundColor = HintFg;
            Console.WriteLine("Hotkeys: [1..N] choose  •  [I] inventory  •  [?] help  •  [Q] quit/back");
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

        private static void DrawSep(int width)
        {
            Console.ForegroundColor = BorderFg;
            Console.WriteLine($"{SEP_L}{new string('─', width - 2)}{SEP_R}");
            Console.ResetColor();
        }

        private static void DrawTitle(string title, int width)
        {
            string txt = PadCenter(title ?? string.Empty, width - 4);

            Console.ForegroundColor = BorderFg;
            Console.Write($"{V} ");
            Console.ForegroundColor = TitleFg;
            Console.Write(txt);
            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
        }

        private static void DrawHud(GameState s, int width)
        {
            // All from GameState; no new logic.
            string day = $"Day: {s.World.Day}";
            string time = $"Time: {s.World.Hour:00}:{s.World.Minute:00}";
            string name = $"Name: {s.Player?.Name ?? "Hero"}";
            string hp = $"HP: {s.CurrentHp}";
            string gold = $"Gold: {s.Gold}";

            string left = $"{day}   {time}   {name}";
            string right = $"{hp}   {gold}";

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
                // Label:value coloring (simple heuristic)
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
                        Console.Write(p.Substring(idx + 2));
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
            // Preserve tables/ASCII art; wrap plain prose
            foreach (var rawLine in (body ?? string.Empty).Replace("\r", "").Split('\n'))
            {
                if (string.IsNullOrEmpty(rawLine))
                {
                    BoxLine(string.Empty, width);
                    continue;
                }

                if (IsPreformatted(rawLine))
                {
                    BoxLine(Truncate(rawLine, inner), width);
                }
                else
                {
                    foreach (var w in Wrap(rawLine, inner))
                        BoxLine(w, width);
                }
            }
        }

        private static void WriteOptions(IReadOnlyList<(string Key, string Label)> options, int inner, int width)
        {
            // Build display strings like "[1] Attack"
            var display = options.Select((o, i) => $"[{i + 1}] {o.Label}").ToList();
            int maxLen = display.Max(s => s.Length);

            // Choose layout: 2 columns when it fits nicely and there are enough choices
            bool twoCols = options.Count >= 4 && (maxLen * 2 + 4) <= inner;
            int cols = twoCols ? 2 : 1;
            int colWidth = twoCols ? (inner / 2) : inner;
            int rows = (int)Math.Ceiling(display.Count / (double)cols);

            for (int r = 0; r < rows; r++)
            {
                string left = (r < display.Count) ? display[r] : string.Empty;
                string right = twoCols && (r + rows) < display.Count ? display[r + rows] : string.Empty;

                // Keep nicely padded columns
                string line = twoCols
                    ? left.PadRight(colWidth) + right.PadRight(inner - colWidth)
                    : left.PadRight(inner);

                BoxLine(line, width);
            }

            // Add a small breathing space after options when box ends
        }

        private static void WriteHints()
        {
            Console.ForegroundColor = HintFg;
            Console.WriteLine();
            Console.WriteLine("Tips: [1..N]=choose   [I]nventory   [?] help   [Q] quit/back");
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

            // Paragraph-aware wrapping
            var paragraphs = text.Split(new[] { "\\n\\n" }, StringSplitOptions.None); // keep lightweight; callers already split lines
            foreach (var para in new[] { text }) // single-line fallback
            {
                var words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var line = new StringBuilder();

                foreach (var w in words)
                {
                    if (line.Length == 0)
                    {
                        line.Append(w);
                        continue;
                    }

                    if (line.Length + 1 + w.Length > maxWidth)
                    {
                        yield return line.ToString();
                        line.Clear();
                        line.Append(w);
                    }
                    else
                    {
                        line.Append(' ').Append(w);
                    }
                }

                if (line.Length > 0) yield return line.ToString();
            }
        }

        private static bool IsPreformatted(string s)
        {
            // Heuristic: if line uses box-drawing or table glyphs, don't wrap it.
            return s.IndexOfAny(new[] { '│', '─', '┼', '┬', '┴', '┌', '┐', '└', '┘' }) >= 0
                   || s.Contains("  #") // our inventory table header
                   || s.Contains("Actions:");
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, Math.Max(0, max - 1));

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
