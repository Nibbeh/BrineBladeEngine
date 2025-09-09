using System.Collections.Generic;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.ConsoleUI
{
    /// <summary>
    /// Minimal UI seam so AppCore flows don't depend on the console.
    /// The default implementation is <see cref="ConsoleGameUI"/> which forwards to <see cref="SimpleConsoleUI"/>.
    /// </summary>
    public interface IGameUI
    {
        void RenderFrame(GameState state, string title, string body, IReadOnlyList<(string Key, string Label)> options);
        void RenderModal(GameState state, string title, IReadOnlyList<string> lines, bool waitForEnter = true);
        void ShowHelp();
        ConsoleCommand ReadCommand(int optionsCount);
        void Notice(string message);
        void Notice(IEnumerable<string> messages);
    }
}
