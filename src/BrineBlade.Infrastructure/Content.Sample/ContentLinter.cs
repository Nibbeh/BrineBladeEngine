using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BrineBlade.Infrastructure.Content;

public sealed record PreflightSummary(int NodeCount, int DialogueCount, int ItemCount, int EnemyCount, int ClassCount);

public static class ContentLinter
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // Keep in sync with your engine's allowed ops
    private static readonly HashSet<string> KnownOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "endDialogue","setFlag","addGold","advanceTime","goto","startDialogue","combat",
        "addItem","removeItemByName","equip","unequip"
    };

    public static PreflightSummary Preflight(string root)
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

        // 1) Schema validation
        ValidateFolder(contentRoot, "nodes", Path.Combine(schemaRoot, "Node.schema.json"), issues);
        ValidateFolder(contentRoot, "dialogues", Path.Combine(schemaRoot, "Dialogue.schema.json"), issues);
        ValidateFolder(contentRoot, "items", Path.Combine(schemaRoot, "Item.schema.json"), issues);
        ValidateFolder(contentRoot, "enemies", Path.Combine(schemaRoot, "Enemy.schema.json"), issues);
        ValidateFolder(contentRoot, "classes", Path.Combine(schemaRoot, "Class.schema.json"), issues);

        // 2) Parse JSON (no Domain/AppCore types)
        var (nodes, dialogues) = LoadModels(contentRoot, issues);

        // 3) Cross-refs + verbs + drift checks
        CheckReferences(nodes, dialogues, issues);
        CheckVerbs(nodes, dialogues, issues);
        CheckDrift(contentRoot, issues);

        if (issues.Count > 0)
            throw new ContentValidationException(issues); // ← uses your existing exception type (with IEnumerable<string>)
    }

    private static void ValidateFolder(string root, string folder, string schemaFile, List<string> issues)
    {
        var dir = Path.Combine(root, folder);
        if (!Directory.Exists(dir)) return;
        var schemaJson = File.ReadAllText(schemaFile);
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(file);
            var errs = JsonSchemaValidator.Validate(json, schemaJson, file);
            if (errs.Count > 0) issues.AddRange(errs);
        }
    }

    private static (Dictionary<string, JsonObject> nodes, Dictionary<string, JsonObject> dialogues)
        LoadModels(string root, List<string> issues)
    {
        var nodes = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var dialogues = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        void LoadDir(string folder, Action<string, JsonObject> add)
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var obj = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
                    if (obj is null) { issues.Add($"{file}: invalid JSON"); continue; }
                    var id = GetString(obj["Id"]);
                    if (string.IsNullOrWhiteSpace(id)) { issues.Add($"{file}: missing Id"); continue; }
                    add(id!, obj);
                }
                catch (Exception ex)
                {
                    issues.Add($"{file}: exception during parse: {ex.Message}");
                }
            }
        }

        LoadDir("nodes", (id, obj) => nodes[id] = obj);
        LoadDir("dialogues", (id, obj) => dialogues[id] = obj);
        return (nodes, dialogues);
    }

    private static void CheckReferences(Dictionary<string, JsonObject> nodes, Dictionary<string, JsonObject> dialogues, List<string> issues)
    {
        foreach (var (nodeId, node) in nodes)
        {
            if (node["Exits"] is JsonArray exits)
            {
                foreach (var ex in exits.OfType<JsonObject>())
                {
                    var to = GetString(ex["To"]);
                    if (string.IsNullOrWhiteSpace(to) || !nodes.ContainsKey(to!))
                        issues.Add($"Node {nodeId}: exit To='{to}' not found.");
                }
            }
            if (node["Options"] is JsonArray opts)
            {
                foreach (var opt in opts.OfType<JsonObject>())
                {
                    if (opt["Effects"] is not JsonArray effs) continue;
                    foreach (var e in effs.OfType<JsonObject>())
                    {
                        var op = GetString(e["Op"]);
                        if (string.Equals(op, "goto", StringComparison.OrdinalIgnoreCase))
                        {
                            var to = GetString(e["To"]);
                            if (string.IsNullOrWhiteSpace(to) || !nodes.ContainsKey(to!))
                                issues.Add($"Node {nodeId}: goto.To '{to}' not found.");
                        }
                        if (string.Equals(op, "startDialogue", StringComparison.OrdinalIgnoreCase))
                        {
                            var id = GetString(e["Id"]);
                            if (string.IsNullOrWhiteSpace(id) || !dialogues.ContainsKey(id!))
                                issues.Add($"Node {nodeId}: startDialogue.Id '{id}' not found.");
                        }
                    }
                }
            }
        }

        foreach (var (dlgId, dlg) in dialogues)
        {
            var lines = dlg["Lines"] as JsonObject;
            var start = GetString(dlg["StartLineId"]);
            if (lines is null || lines.Count == 0) { issues.Add($"Dialogue {dlgId}: no Lines."); continue; }
            if (string.IsNullOrWhiteSpace(start) || !lines.ContainsKey(start!))
                issues.Add($"Dialogue {dlgId}: StartLineId '{start}' not in Lines.");

            foreach (var (lineId, lineNode) in lines)
            {
                if (lineNode is not JsonObject line) continue;
                if (line["Choices"] is JsonArray choices)
                {
                    foreach (var c in choices.OfType<JsonObject>())
                    {
                        var gotoId = GetString(c["Goto"]);
                        if (!string.IsNullOrWhiteSpace(gotoId) && !lines.ContainsKey(gotoId!))
                            issues.Add($"Dialogue {dlgId}/{lineId}: choice goto '{gotoId}' not found.");
                    }
                }
            }
        }
    }

    private static void CheckVerbs(Dictionary<string, JsonObject> nodes, Dictionary<string, JsonObject> dialogues, List<string> issues)
    {
        foreach (var (nodeId, node) in nodes)
        {
            if (node["Options"] is not JsonArray opts) continue;
            foreach (var opt in opts.OfType<JsonObject>())
            {
                if (opt["Effects"] is not JsonArray effs) continue;
                foreach (var e in effs.OfType<JsonObject>())
                {
                    var op = GetString(e["Op"]);
                    if (string.IsNullOrWhiteSpace(op) || !KnownOps.Contains(op!))
                        issues.Add($"Node {nodeId}: unknown Op '{op}'.");
                }
            }
        }

        foreach (var (dlgId, dlg) in dialogues)
        {
            var lines = dlg["Lines"] as JsonObject;
            if (lines is null) continue;

            foreach (var (_, lineNode) in lines)
            {
                if (lineNode is not JsonObject line) continue;

                if (line["Effects"] is JsonArray lineEffs)
                {
                    foreach (var e in lineEffs.OfType<JsonObject>())
                    {
                        var op = GetString(e["Op"]);
                        if (string.IsNullOrWhiteSpace(op) || !KnownOps.Contains(op!))
                            issues.Add($"Dialogue {dlgId}: line effect unknown Op '{op}'.");
                    }
                }

                if (line["Choices"] is JsonArray choices)
                {
                    foreach (var c in choices.OfType<JsonObject>())
                    {
                        if (c["Effects"] is not JsonArray ceffs) continue;
                        foreach (var e in ceffs.OfType<JsonObject>())
                        {
                            var op = GetString(e["Op"]);
                            if (string.IsNullOrWhiteSpace(op) || !KnownOps.Contains(op!))
                                issues.Add($"Dialogue {dlgId}: choice effect unknown Op '{op}'.");
                        }
                    }
                }
            }
        }
    }

    private static void CheckDrift(string root, List<string> issues)
    {
        void Scan(string folder, Action<string, JsonObject> check)
        {
            var dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                var node = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
                if (node != null) check(file, node);
            }
        }

        Scan("nodes", (file, node) =>
        {
            if (node["Options"] is JsonArray opts)
            {
                foreach (var o in opts.OfType<JsonObject>())
                    if (o.ContainsKey("Text"))
                        issues.Add($"{file}: NodeOption has non-canonical 'Text' (use 'Label').");
            }
            if (node["Exits"] is JsonArray exs)
            {
                foreach (var ex in exs.OfType<JsonObject>())
                    if (ex.ContainsKey("Label"))
                        issues.Add($"{file}: NodeExit has non-canonical 'Label' (use 'Text').");
            }
        });

        Scan("dialogues", (file, dlg) =>
        {
            if (dlg["Lines"] is not JsonObject lines) return;
            foreach (var (_, lineNode) in lines)
            {
                if (lineNode is not JsonObject line) continue;
                if (line["Choices"] is not JsonArray choices) continue;
                foreach (var c in choices.OfType<JsonObject>())
                    if (c.ContainsKey("Text"))
                        issues.Add($"{file}: DialogueChoice has non-canonical 'Text' (use 'Label').");
            }
        });
    }

    private static string? GetString(JsonNode? n) => (n as JsonValue)?.GetValue<string>();
}
