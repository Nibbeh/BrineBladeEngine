// src/BrineBlade.AppCore/Rules/RequiresEvaluator.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BrineBlade.Domain.Entities;
using BrineBlade.Domain.Game;

namespace BrineBlade.AppCore.Rules
{
    public static class RequiresEvaluator
    {
        public static bool Passes(GameState state, List<string>? reqs)
        {
            if (reqs is null || reqs.Count == 0) return true;
            foreach (var r in reqs)
                if (!PassesSingle(state, r)) return false;
            return true;
        }

        public static bool PassesSingle(GameState state, string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement)) return true;
            requirement = requirement.Trim();

            if (requirement.StartsWith("flag:", StringComparison.OrdinalIgnoreCase))
            {
                var flag = requirement[5..];
                if (flag.StartsWith("inv.", StringComparison.OrdinalIgnoreCase))
                {
                    var id = flag[4..];
                    var hasInBag = state.Inventory.Any(s => s.ItemId.Equals(id, StringComparison.Ordinal) && s.Quantity > 0);
                    var equipped = state.Equipment.Values.Any(v => string.Equals(v, id, StringComparison.Ordinal));
                    return hasInBag || equipped;
                }
                return state.Flags.Contains(flag);
            }

            if (requirement.StartsWith("race:", StringComparison.OrdinalIgnoreCase))
                return state.Player.Race.Equals(requirement[5..].Trim(), StringComparison.OrdinalIgnoreCase);

            if (requirement.StartsWith("archetype:", StringComparison.OrdinalIgnoreCase) ||
                requirement.StartsWith("class:", StringComparison.OrdinalIgnoreCase))
            {
                var val = requirement.Split(':', 2)[1].Trim();
                return state.Player.Archetype.Equals(val, StringComparison.OrdinalIgnoreCase);
            }

            if (requirement.StartsWith("spec:", StringComparison.OrdinalIgnoreCase))
                return state.Flags.Contains($"spec.{requirement[5..].Trim()}");

            if (requirement.StartsWith("stat:", StringComparison.OrdinalIgnoreCase))
            {
                var expr = requirement[5..].Trim();
                var stats = state.PlayerStats();
                return EvaluateStatExpr(stats, expr);
            }

            return false;
        }

        private static Stats PlayerStats(this GameState s) => new Stats(10, 10, 10, 10, 10, 10, 10);

        private static bool EvaluateStatExpr(Stats stats, string expr)
        {
            string[] ops = new[] { ">=", "<=", "==", "!=", ">", "<" };
            string? op = ops.FirstOrDefault(o => expr.Contains(o, StringComparison.Ordinal));
            if (op is null) return false;
            var parts = expr.Split(op, 2);
            if (parts.Length != 2) return false;
            var key = parts[0].Trim().ToUpperInvariant();
            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rhs)) return false;

            int lhs = key switch
            {
                "STR" => stats.Strength,
                "DEX" => stats.Dexterity,
                "INT" => stats.Intelligence,
                "VIT" => stats.Vitality,
                "CHA" => stats.Charisma,
                "PER" => stats.Perception,
                "LCK" or "LUCK" => stats.Luck,
                _ => int.MinValue
            };
            if (lhs == int.MinValue) return false;

            return op switch
            {
                ">=" => lhs >= rhs,
                "<=" => lhs <= rhs,
                ">" => lhs > rhs,
                "<" => lhs < rhs,
                "==" => lhs == rhs,
                "!=" => lhs != rhs,
                _ => false
            };
        }
    }
}
