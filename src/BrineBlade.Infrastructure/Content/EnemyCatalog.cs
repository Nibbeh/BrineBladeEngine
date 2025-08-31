// src/BrineAndBlade.Infrastructure/Content/EnemyCatalog.cs
using BrineBlade.Domain.Entities;
using BrineBlade.Services.Abstractions;

using System.Text.Json;

namespace BrineBlade.Infrastructure.Content
{
    public sealed class EnemyCatalog : IEnemyCatalog
    {
        public IReadOnlyDictionary<string, EnemyDef> All => _all;
        private readonly Dictionary<string, EnemyDef> _all = new(StringComparer.Ordinal);

        public EnemyCatalog(string contentRoot)
        {
            var dir = Path.Combine(contentRoot, "enemies");
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var def = JsonSerializer.Deserialize<EnemyDef>(json, JsonOpts);
                    if (def is { Id.Length: > 0 })
                        _all[def.Id] = def;
                }
                catch
                {
                    // safe failure on malformed files
                }
            }
        }

        public bool TryGet(string id, out EnemyDef def) => _all.TryGetValue(id, out def!);

        public EnemyDef GetRequired(string id) =>
            _all.TryGetValue(id, out var def)
                ? def
                : throw new KeyNotFoundException($"Unknown enemy id '{id}'.");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
