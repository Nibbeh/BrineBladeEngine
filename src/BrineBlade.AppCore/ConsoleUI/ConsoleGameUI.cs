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
    /// Visual-only console skin. Same API, nicer presentation:
    /// - Colored title/header bars
    /// - Compact HUD (day/time, name, gold, HP)
    /// - Clean body box with graceful word-wrap
    /// - Polished options row and hotkey footer
    /// Logic and signatures remain untouched.
    /// </summary>
    public sealed class ConsoleGameUI : IGameUI
    {
        // Box characters (rounded/triple for a premium feel)
        private const string H = "─";
        private const string V = "│";
        private const string TL = "┌";
        private const string TR = "┐";
        private const string BL = "└";
        private const string BR = "┘";
        private const string SEP_L = "├";
        private const string SEP_R = "┤";

        // Theme
        private static readonly ConsoleColor TitleFg = ConsoleColor.Cyan;
        private static readonly ConsoleColor HudLabelFg = ConsoleColor.DarkGray;
        private static readonly ConsoleColor HudValueFg = ConsoleColor.White;
        private static readonly ConsoleColor BodyFg = ConsoleColor.Gray;
        private static readonly ConsoleColor BorderFg = ConsoleColor.DarkGray;
        private static readonly ConsoleColor OptionKeyFg = ConsoleColor.Yellow;
        private static readonly ConsoleColor OptionFg = ConsoleColor.White;
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

            // Sizing
            int width = Clamp(Console.WindowWidth - 2, 78, 120);
            int inner = width - 4; // inside of the box (two borders + one space each side)

            // Header
            DrawTopBorder(width);
            DrawTitle(title, width);
            DrawSep(width);
            DrawHud(state, width);
            DrawSep(width);

            // Body
            WriteBody(body ?? string.Empty, inner, width);

            // Options
            if (options is { Count: > 0 })
            {
                DrawSep(width);
                WriteOptions(options, inner, width);
            }

            // Footer / prompt
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

            Console.ForegroundColor = BodyFg;
            foreach (var raw in lines ?? Array.Empty<string>())
            {
                foreach (var line in Wrap(raw ?? string.Empty, inner))
                    BoxLine(line, width);
            }
            Console.ResetColor();

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

                // globals
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

                // soft error re-prompt
                Console.ForegroundColor = HintFg;
                Console.WriteLine("Enter a valid option number, 'i' for inventory, '?' for help, or 'q' to quit.");
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

        public void Notice(string message) => SimpleConsoleUI.Notice(message);
        public void Notice(IEnumerable<string> messages) => SimpleConsoleUI.Notice(messages);

        // ───────────────────────────────── helpers ─────────────────────────────────

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
            // We only use fields available on GameState to avoid changing logic.
            string day = $"Day {s.World.Day}";
            string time = $"{s.World.Hour:00}:{s.World.Minute:00}";
            string name = s.Player?.Name ?? "Hero";
            string gold = $"{s.Gold}";
            string hp = $"{s.CurrentHp}";

            // Compose: Day/Time | Name | HP | Gold
            var left = new StringBuilder();
            left.Append("⏳ "); ColorLabelValue(day, time, out string dayTime);
            left.Append(dayTime);
            left.Append("   ");

            left.Append("🧝 "); ColorLabelValue("Name", name, out string namePair);
            left.Append(namePair);
            left.Append("   ");

            ColorLabelValue("HP", hp, out string hpPair);
            left.Append(hpPair);
            left.Append("   ");

            ColorLabelValue("Gold", gold, out string goldPair);
            left.Append(goldPair);

            // render line inside the box
            Console.ForegroundColor = BorderFg;
            Console.Write($"{V} ");
            int inner = width - 4;

            // We already applied colors within pairs; just write and pad.
            int visibleLen = StripAnsiLength(left.ToString());
            Console.Write(left.ToString());
            Console.ResetColor();

            // pad remainder (safe padding; no ANSI length here)
            int pad = Math.Max(0, inner - visibleLen);
            Console.Write(new string(' ', pad));

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
        }

        private static void WriteBody(string body, int inner, int width)
        {
            Console.ForegroundColor = BodyFg;
            foreach (var line in Wrap(body, inner))
                BoxLine(line, width);
            Console.ResetColor();
        }

        private static void WriteOptions(IReadOnlyList<(string Key, string Label)> options, int inner, int width)
        {
            // Render as:  [1] Attack   [2] Guard   ...
            var sb = new StringBuilder("  ");
            for (int i = 0; i < options.Count; i++)
            {
                var (_, label) = options[i];

                sb.Append("[");
                Console.ForegroundColor = OptionKeyFg;
                // number is i+1 for display; the Key value is informational
                BoxInline(sb.ToString(), width); sb.Clear();

                Console.Write($"{i + 1}");
                Console.ResetColor();

                Console.ForegroundColor = OptionFg;
                Console.Write("] ");
                Console.Write(label);

                if (i < options.Count - 1) Console.Write("   ");
                Console.ResetColor();
            }

            // close the options line inside the box
            int textLen = Console.CursorLeft - 2; // rough on same line
            if (textLen < inner)
                Console.Write(new string(' ', inner - textLen));

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
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

            Console.Write(text.PadRight(width - 4));

            Console.ForegroundColor = BorderFg;
            Console.WriteLine($" {V}");
            Console.ResetColor();
        }

        private static void BoxInline(string prefixWritten, int width)
        {
            // Helper to flush any pending prefix within option line with box left border already accounted for.
            // No-op: alignment is handled by caller. Exists to keep code intention clear.
        }

        // Formatting utils
        private static IEnumerable<string> Wrap(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return string.Empty;
                yield break;
            }

            var words = text.Replace("\r", "").Split('\n');
            foreach (var para in words)
            {
                var tokens = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var line = new StringBuilder();

                foreach (var w in tokens)
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

        private static string PadCenter(string s, int width)
        {
            s ??= string.Empty;
            if (s.Length >= width) return s[..width];
            int pad = width - s.Length;
            return new string(' ', pad / 2) + s + new string(' ', pad - pad / 2);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static void ColorLabelValue(string label, string value, out string formatted)
        {
            // Produces colored label:value and returns raw (for width calc) while also writing live colored text.
            var raw = $"{label}: {value}";
            formatted = raw;

            Console.ForegroundColor = HudLabelFg;
            Console.Write(label + ": ");
            Console.ForegroundColor = HudValueFg;
            Console.Write(value);
            Console.ResetColor();
        }

        private static int StripAnsiLength(string s)
        {
            // Console.Write with colors doesn't inject ANSI on Windows Console,
            // but just in case (cross shells), approximate by plain length.
            // We only use this for padding; being conservative is fine.
            return s.Length;
        }
    }
}
