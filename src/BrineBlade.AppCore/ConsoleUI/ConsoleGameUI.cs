using System.Collections.Generic;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.ConsoleUI
{
    /// <summary>
    /// Default console UI implementation. Thin adapter around <see cref="SimpleConsoleUI"/>.
    /// </summary>
    public sealed class ConsoleGameUI : IGameUI
    {
        public void RenderFrame(GameState state, string title, string body, IReadOnlyList<(string Key, string Label)> options)
            => SimpleConsoleUI.RenderFrame(state, title, body, options);

        public void RenderModal(GameState state, string title, IReadOnlyList<string> lines, bool waitForEnter = true)
            => SimpleConsoleUI.RenderModal(state, title, lines, waitForEnter);

        public void ShowHelp() => SimpleConsoleUI.ShowHelp();

        public ConsoleCommand ReadCommand(int optionsCount) => SimpleConsoleUI.ReadCommand(optionsCount);

        public void Notice(string message) => SimpleConsoleUI.Notice(message);

        public void Notice(IEnumerable<string> messages) => SimpleConsoleUI.Notice(messages);
    }
}

