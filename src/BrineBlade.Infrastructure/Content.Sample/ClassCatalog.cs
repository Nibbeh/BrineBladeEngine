using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed class ClassCatalog
{
    public Dictionary<string, ClassDef> All { get; }

    public ClassCatalog(string contentRoot)
        => All = Load(Path.Combine(contentRoot, "classes"));

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static Dictionary<string, ClassDef> Load(string dir)
    {
        var map = new Dictionary<string, ClassDef>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir)) return map; ;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var def = JsonSerializer.Deserialize<ClassDef>(json, JsonOpts);
                if (def is { Id.Length: > 0 }) map[def.Id] = def;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine($"[CLASS LOAD ERROR] {file}: {ex.Message}");
#endif
            }

        }
        return map;
    }
}

