// src/BrineBlade.AppCore/Rules/EffectProcessor.cs
using System;
using System.Collections.Generic;
using BrineBlade.AppCore.ConsoleUI;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.AppCore.Rules
{
    /// <summary>
    /// Registry-based effect processor. Each Op is handled by a small, schema-validating handler.
    /// Keeps the public API identical to the previous EffectProcessor (constructor and ApplyAll signature).
    /// </summary>
    public sealed class EffectProcessor
    {
        public static readonly HashSet<string> KnownOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "endDialogue","setFlag","addGold","addItem","removeItemByName",
            "advanceTime","goto","startDialogue","combat"
        };

        private readonly GameState _state;
        private readonly IInventoryService _inv;
        private readonly IGameUI _ui;
        private readonly Dictionary<string, Action<EffectSpec, Action<string>, Action<string>>> _handlers;

        public EffectProcessor(GameState state, IInventoryService inv, IGameUI ui)
        {
            _state = state;
            _inv = inv;
            _ui = ui;

            _handlers = new(StringComparer.OrdinalIgnoreCase)
            {
                ["endDialogue"] = (e, sd, sc) => _endDialogue = true,
                ["setFlag"] = (e, sd, sc) => Require(e.Id, "setFlag", "Id", id => _state.Flags.Add(id)),
                ["addGold"] = (e, sd, sc) => Require(e.Amount, "addGold", "Amount", n => _state.Gold += n),
                ["addItem"] = (e, sd, sc) => Require2(e.Id, e.Qty ?? 1, "addItem",
                                                                () => _inv.TryAdd(_state, e.Id!, e.Qty ?? 1)),
                ["removeItemByName"] = (e, sd, sc) => Require2(e.Id, e.Qty ?? 1, "removeItemByName",
                                                                () => _inv.TryRemove(_state, e.Id!, e.Qty ?? 1)),
                ["advanceTime"] = (e, sd, sc) => Require(e.Minutes, "advanceTime", "Minutes", m => AdvanceMinutes(m)),
                ["goto"] = (e, sd, sc) => Require(e.To, "goto", "To", to => _state.CurrentNodeId = to),
                ["startDialogue"] = (e, sd, sc) => Require(e.Id, "startDialogue", "Id", id => sd(id)),
                ["combat"] = (e, sd, sc) => Require(e.Id, "combat", "Id", id => sc(id)),
            };
        }

        public readonly record struct Outcome(bool EndDialogue);
        private bool _endDialogue;

        public Outcome ApplyAll(IEnumerable<EffectSpec>? effects,
                                Action<string> startDialogue,
                                Action<string> startCombat)
        {
            _endDialogue = false;
            if (effects is null) return new Outcome(false);

            foreach (var e in effects)
            {
                if (!KnownOps.Contains(e.Op))
                {
                    _ui.Notice($"[Effect] Unknown op '{e.Op}'. Skipped.");
                    continue;
                }

                try { _handlers[e.Op](e, startDialogue, startCombat); }
                catch (Exception ex) { _ui.Notice($"[Effect:{e.Op}] {ex.Message}"); }
            }

            return new Outcome(_endDialogue);
        }

        private static void Require(string? val, string op, string field, Action<string> apply)
        {
            if (string.IsNullOrWhiteSpace(val))
                throw new InvalidOperationException($"{op} requires non-empty '{field}'.");
            apply(val!);
        }

        private static void Require(int? val, string op, string field, Action<int> apply)
        {
            if (!val.HasValue)
                throw new InvalidOperationException($"{op} requires integer '{field}'.");
            apply(val!.Value);
        }

        private static void Require2(string? id, int qty, string op, Action apply)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException($"{op} requires non-empty 'Id'.");
            if (qty <= 0)
                throw new InvalidOperationException($"{op} requires positive 'Qty'.");
            apply();
        }

        private void AdvanceMinutes(int minutes)
        {
            if (minutes <= 0) return;
            var world = _state.World;
            int total = world.Minute + minutes;
            world.Minute = total % 60;
            int addHours = total / 60;
            world.Hour = (world.Hour + addHours) % 24;
            world.Day = world.Day + (addHours / 24);
            _state.World = world;
        }
    }
}
