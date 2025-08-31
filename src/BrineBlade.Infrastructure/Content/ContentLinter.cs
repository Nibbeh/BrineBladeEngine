using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed record ContentLintReport(List<string> Warnings);

public static class ContentLinter
{
    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ContentLintReport Analyze(string contentRoot)
    {
        var warns = new List<string>();

        // Load nodes/dialogues exactly like JsonContentStore
        var nodes = Load<Node>(Path.Combine(contentRoot, "nodes"));
        var dialogs = Load<Dialogue>(Path.Combine(contentRoot, "dialogues"));

        // 1) Node exits must resolve
        foreach (var n in nodes.Values)
            if (n.Exits is not null)
                foreach (var ex in n.Exits)
                    if (!nodes.ContainsKey(ex.To))
                        warns.Add($"Node '{n.Id}' exit → '{ex.To}' is missing.");

        // 2) Dialogue StartLineId must exist
        foreach (var d in dialogs.Values)
            if (!d.Lines.ContainsKey(d.StartLineId))
                warns.Add($"Dialogue '{d.Id}' StartLineId '{d.StartLineId}' not in Lines.");

        // 3) startDialogue effects in nodes must point to real dialogues
        foreach (var n in nodes.Values)
        {
            var effects = new List<EffectSpec>();
            if (n.Options is not null) foreach (var o in n.Options) if (o.Effects is not null) effects.AddRange(o.Effects);
            if (n.Exits is not null) foreach (var e in n.Exits) { /* exits build goto at runtime */ }
            foreach (var e in effects.Where(e => string.Equals(e.Op, "startDialogue", StringComparison.OrdinalIgnoreCase)
                                              && !string.IsNullOrWhiteSpace(e.Id)))
                if (!dialogs.ContainsKey(e.Id!))
                    warns.Add($"Node '{n.Id}' references missing dialogue '{e.Id}'.");
        }

        return new ContentLintReport(warns);

        static Dictionary<string, T> Load<T>(string dir)
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            if (!Directory.Exists(dir)) return map;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                try
                {
                    var json = File.ReadAllText(file);
                    var m = JsonSerializer.Deserialize<T>(json, Opts)!;
                    var id = typeof(T) == typeof(Node) ? ((Node)(object)m).Id
                           : typeof(T) == typeof(Dialogue) ? ((Dialogue)(object)m).Id
                           : null;
                    if (!string.IsNullOrWhiteSpace(id)) map[id!] = m;
                }
                catch { /* authoring-safe */ }
            return map;
        }
    }
}
