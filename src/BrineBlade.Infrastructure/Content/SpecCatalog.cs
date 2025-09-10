using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed class SpecCatalog
{
    public Dictionary<string, SpecDef> All { get; }

    public SpecCatalog(string contentRoot)
        => All = Load(Path.Combine(contentRoot, "specs"));

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static Dictionary<string, SpecDef> Load(string dir)
    {
        var map = new Dictionary<string, SpecDef>(StringComparer.Ordinal);
        if (!Directory.Exists(dir)) return map;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var def = JsonSerializer.Deserialize<SpecDef>(json, JsonOpts);
                if (def is { Id.Length: > 0 }) map[def.Id] = def;
            }
            catch { }
        }
        return map;
    }
}

