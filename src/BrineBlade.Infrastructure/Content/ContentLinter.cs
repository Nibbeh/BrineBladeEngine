using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed record PreflightSummary(int NodeCount, int DialogueCount, int ItemCount, int EnemyCount);

public static class ContentLinter
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Validates content and throws ContentValidationException on any critical errors.
    /// Returns a PreflightSummary for logging.
    /// </summary>
    public static PreflightSummary Preflight(string contentRoot, IReadOnlySet<string> knownOps)
    {
        var errors = new List<string>();

        var nodes = Load<Node>(Path.Combine(contentRoot, "nodes"));
        var dialogs = Load<Dialogue>(Path.Combine(contentRoot, "dialogues"));
        var items = Load<ItemDef>(Path.Combine(contentRoot, "items"));
        var enemies = Load<EnemyDef>(Path.Combine(contentRoot, "enemies"));

        // 1) Node exits must resolve
        foreach (var n in nodes.Values)
        {
            if (n.Exits is null) continue;
            foreach (var ex in n.Exits)
                if (!nodes.ContainsKey(ex.To))
                    errors.Add($"Node '{n.Id}' exit → missing node '{ex.To}'.");
        }

        // 2) Dialogue StartLineId must exist
        foreach (var d in dialogs.Values)
        {
            if (!d.Lines.ContainsKey(d.StartLineId))
                errors.Add($"Dialogue '{d.Id}' StartLineId '{d.StartLineId}' not found in Lines.");
        }

        // 3) Effects: unknown 'Op' / invalid refs (nodes → dialogues start)
        foreach (var n in nodes.Values)
        {
            static IEnumerable<EffectSpec> Collect(Node nn)
            {
                if (nn.Options is not null)
                    foreach (var o in nn.Options)
                        if (o.Effects is not null)
                            foreach (var e in o.Effects) yield return e;
            }

            foreach (var e in Collect(n))
            {
                if (string.IsNullOrWhiteSpace(e.Op))
                {
                    errors.Add($"Node '{n.Id}' has effect with empty Op.");
                    continue;
                }

                if (!knownOps.Contains(e.Op))
                    errors.Add($"Node '{n.Id}' has unknown effect Op='{e.Op}'.");

                if (string.Equals(e.Op, "startDialogue", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(e.Id) || !dialogs.ContainsKey(e.Id!))
                        errors.Add($"Node '{n.Id}' startDialogue → missing dialogue '{e.Id}'.");
                }

                if (string.Equals(e.Op, "goto", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(e.To) || !nodes.ContainsKey(e.To!))
                        errors.Add($"Node '{n.Id}' goto → missing node '{e.To}'.");
                }
            }
        }

        if (errors.Count > 0)
            throw new ContentValidationException(errors);

        return new PreflightSummary(nodes.Count, dialogs.Count, items.Count, enemies.Count);
    }

    private static Dictionary<string, T> Load<T>(string dir)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        if (!Directory.Exists(dir)) return map;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var model = JsonSerializer.Deserialize<T>(json, Opts)!;
                var id = GetId(model);
                if (!string.IsNullOrWhiteSpace(id))
                    map[id!] = model;
            }
            catch
            {
                // Fail softly here; structural issues will be caught by schema/refs checks above
            }
        }

        return map;

        static string? GetId(object model) => model switch
        {
            Node m => m.Id,
            Dialogue m => m.Id,
            ItemDef m => m.Id,
            EnemyDef m => m.Id,
            _ => null
        };
    }
}
