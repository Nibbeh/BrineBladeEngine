using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed class ItemCatalog
{
    public IReadOnlyDictionary<string, ItemDef> All => _all;
    private readonly Dictionary<string, ItemDef> _all = new(StringComparer.OrdinalIgnoreCase);

    public ItemCatalog(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "items");
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var model = JsonSerializer.Deserialize<ItemDef>(json, JsonOpts);
                if (model is { Id.Length: > 0 })
                    _all[model.Id] = model; // OrdinalIgnoreCase key
            }
            catch { /* authoring-time tolerance */ }
        }
    }

    public bool TryGet(string id, out ItemDef def) => _all.TryGetValue(id, out def!);

    public ItemDef GetRequired(string id) =>
        _all.TryGetValue(id, out var def)
            ? def
            : throw new KeyNotFoundException($"Unknown item id '{id}'.");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

