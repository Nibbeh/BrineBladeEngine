// src/BrineBlade.Infrastructure/Content.Sample/ContentLinter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BrineBlade.Infrastructure.Content
{
    public sealed record PreflightSummary(int NodeCount, int DialogueCount, int ItemCount, int EnemyCount, int ClassCount);

    public static class ContentLinter
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static readonly HashSet<string> AllowedOps = new(StringComparer.OrdinalIgnoreCase)
        {
            "endDialogue","setFlag","addGold","addItem","removeItemByName",
            "advanceTime","goto","startDialogue","combat"
        };

        public static PreflightSummary Summarize(string root)
        {
            int Count(string folder) => Directory.Exists(Path.Combine(root, folder))
                ? Directory.EnumerateFiles(Path.Combine(root, folder), "*.json", SearchOption.AllDirectories).Count()
                : 0;

            return new PreflightSummary(
                Count("nodes"), Count("dialogues"), Count("items"), Count("enemies"), Count("classes"));
        }

        public static void ValidateOrThrow(string contentRoot, string schemaRoot)
        {
            var issues = new List<string>();

            // Basic JSON well-formedness (avoid external schema to match your project)
            ValidateFolderJson(contentRoot, "nodes", issues);
            ValidateFolderJson(contentRoot, "dialogues", issues);
            ValidateFolderJson(contentRoot, "items", issues);
            ValidateFolderJson(contentRoot, "enemies", issues);
            ValidateFolderJson(contentRoot, "classes", issues);

            // Cross-reference validation
            var (nodes, dialogues, items, enemies) = LoadMaps(contentRoot);
            ValidateCrossRefs(contentRoot, nodes, dialogues, items, enemies, issues);

            if (issues.Count > 0)
                // FIX: pass IEnumerable<string> instead of a single joined string
                throw new ContentValidationException(issues);
        }

        private static void ValidateFolderJson(string root, string folder, List<string> issues)
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try { JsonNode.Parse(File.ReadAllText(file)); }
                catch (Exception ex) { issues.Add($"[{folder}] {file}: {ex.Message}"); }
            }
        }

        private static (HashSet<string> nodes, HashSet<string> dialogues, HashSet<string> items, HashSet<string> enemies)
            LoadMaps(string root)
        {
            var nodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dialogues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var enemies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void LoadIds(string folder, Action<JsonNode> capture)
            {
                var dir = Path.Combine(root, folder);
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var node = JsonNode.Parse(File.ReadAllText(file));
                        if (node is null) continue;
                        capture(node);
                    }
                    catch { }
                }
            }

            LoadIds("nodes", n => { var id = n?["Id"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(id)) nodes.Add(id!); });
            LoadIds("dialogues", n => { var id = n?["Id"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(id)) dialogues.Add(id!); });
            LoadIds("items", n => { var id = n?["Id"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(id)) items.Add(id!); });
            LoadIds("enemies", n => { var id = n?["Id"]?.GetValue<string>(); if (!string.IsNullOrWhiteSpace(id)) enemies.Add(id!); });

            return (nodes, dialogues, items, enemies);
        }

        private static void ValidateCrossRefs(
            string root,
            HashSet<string> nodes,
            HashSet<string> dialogues,
            HashSet<string> items,
            HashSet<string> enemies,
            List<string> issues)
        {
            void Each(string folder, Action<string, JsonNode> check)
            {
                var dir = Path.Combine(root, folder);
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var node = JsonNode.Parse(File.ReadAllText(file));
                        if (node is null) continue;
                        check(file, node);
                    }
                    catch (Exception ex) { issues.Add($"[{folder}] {file}: {ex.Message}"); }
                }
            }

            // Nodes: exits/option effects
            Each("nodes", (file, n) =>
            {
                if (n["Exits"] is JsonArray exits)
                {
                    foreach (var ex in exits.OfType<JsonObject>())
                    {
                        var to = ex["To"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(to) && !nodes.Contains(to!))
                            issues.Add($"[nodes] {file}: exit -> '{to}' not found.");
                    }
                }
                if (n["Options"] is JsonArray opts)
                {
                    foreach (var opt in opts.OfType<JsonObject>())
                    {
                        if (opt["Effects"] is JsonArray eff)
                            foreach (var e in eff.OfType<JsonObject>())
                                ValidateEffect(file, e, nodes, dialogues, items, enemies, issues);
                    }
                }
            });

            // Dialogues: line effects and choice gotos
            Each("dialogues", (file, d) =>
            {
                var lines = d["Lines"] as JsonObject;
                var lineIds = lines?.Select(kv => kv.Key).ToHashSet() ?? new HashSet<string>();
                if (lines is not null)
                {
                    foreach (var kv in lines)
                    {
                        var line = kv.Value as JsonObject;
                        if (line is null) continue;

                        if (line["Effects"] is JsonArray eff)
                            foreach (var e in eff.OfType<JsonObject>())
                                ValidateEffect(file, e, nodes, dialogues, items, enemies, issues);

                        if (line["Choices"] is JsonArray choices)
                        {
                            foreach (var ch in choices.OfType<JsonObject>())
                            {
                                var gotoLine = ch["Goto"]?.GetValue<string>();
                                if (!string.IsNullOrWhiteSpace(gotoLine) && !lineIds.Contains(gotoLine!))
                                    issues.Add($"[dialogues] {file}: choice goto -> '{gotoLine}' not a valid line id.");
                                if (ch["Effects"] is JsonArray ceff)
                                    foreach (var e in ceff.OfType<JsonObject>())
                                        ValidateEffect(file, e, nodes, dialogues, items, enemies, issues);
                            }
                        }
                    }
                }
            });
        }

        private static void ValidateEffect(
            string file,
            JsonObject e,
            HashSet<string> nodes,
            HashSet<string> dialogues,
            HashSet<string> items,
            HashSet<string> enemies,
            List<string> issues)
        {
            var op = e["Op"]?.GetValue<string>() ?? "";
            if (!AllowedOps.Contains(op))
                issues.Add($"[effects] {file}: unknown Op '{op}'.");

            switch (op)
            {
                case "goto":
                    var to = e["To"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(to) || !nodes.Contains(to!))
                        issues.Add($"[effects] {file}: goto -> '{to}' not found.");
                    break;
                case "startDialogue":
                    var did = e["Id"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(did) || !dialogues.Contains(did!))
                        issues.Add($"[effects] {file}: startDialogue Id '{did}' not found.");
                    break;
                case "combat":
                    var eid = e["Id"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(eid) || !enemies.Contains(eid!))
                        issues.Add($"[effects] {file}: combat Id '{eid}' not found.");
                    break;
                case "addItem":
                case "removeItemByName":
                    var iid = e["Id"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(iid) || !items.Contains(iid!))
                        issues.Add($"[effects] {file}: item Id '{iid}' not found.");
                    break;
                case "addGold":
                    if (e["Amount"]?.GetValue<int?>() is null)
                        issues.Add($"[effects] {file}: addGold requires Amount.");
                    break;
                case "advanceTime":
                    if (e["Minutes"]?.GetValue<int?>() is null)
                        issues.Add($"[effects] {file}: advanceTime requires Minutes.");
                    break;
                default:
                    break;
            }
        }
    }
}
