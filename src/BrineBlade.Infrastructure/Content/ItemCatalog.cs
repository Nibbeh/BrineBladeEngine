using System.Text.Json;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Infrastructure.Content;

public sealed class ItemCatalog
{
    public IReadOnlyDictionary<string, ItemDef> All => _all;
    private readonly Dictionary<string, ItemDef> _all = new(StringComparer.Ordinal);

    public ItemCatalog(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "items"); // standardize: lowercase
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var def = JsonSerializer.Deserialize<ItemDef>(json, JsonOpts);
                if (def is { Id.Length: > 0 }) _all[def.Id] = def;
            }
            catch
            {
                // Authoring-safe: ignore malformed files while developing content
            }
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
