using System.Text.Json;
using BrineBlade.Domain.Entities;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Content;

public sealed class JsonContentStore : IContentStore
{
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dialogue> _dialogues = new(StringComparer.Ordinal);

    public JsonContentStore(string contentRoot)
    {
        LoadAll(contentRoot);
    }

    public Node? GetNodeById(string id) => _nodes.TryGetValue(id, out var n) ? n : null;
    public Dialogue? GetDialogueById(string id) => _dialogues.TryGetValue(id, out var d) ? d : null;

    private static JsonSerializerOptions JsonOpts => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private void LoadAll(string contentRoot)
    {
        var nodesDir = Directory.Exists(Path.Combine(contentRoot, "nodes"))
            ? Path.Combine(contentRoot, "nodes")
            : Path.Combine(contentRoot, "samples", "nodes");

        var dlgDir = Directory.Exists(Path.Combine(contentRoot, "dialogues"))
            ? Path.Combine(contentRoot, "dialogues")
            : Path.Combine(contentRoot, "samples", "dialogues");

        if (Directory.Exists(nodesDir))
        {
            foreach (var file in Directory.EnumerateFiles(nodesDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<Node>(json, JsonOpts);
                    if (model is { Id.Length: > 0 })
                        _nodes[model.Id] = model;
                }
                catch { /* authoring-time tolerance */ }
            }
        }

        if (Directory.Exists(dlgDir))
        {
            foreach (var file in Directory.EnumerateFiles(dlgDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<Dialogue>(json, JsonOpts);
                    if (model is { Id.Length: > 0 })
                        _dialogues[model.Id] = model;
                }
                catch { /* authoring-time tolerance */ }
            }
        }
    }
}
