using System.Text.Json;
using BrineBlade.Domain.Entities;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Content;

public sealed class JsonContentStore : IContentStore
{
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dialogue> _dialogues = new(StringComparer.OrdinalIgnoreCase);

    public JsonContentStore(string contentRoot)
    {
        LoadAll(contentRoot);
    }

    // ---- Interface: exact members expected by IContentStore ----
    public Node GetNodeById(string id)
    {
        if (_nodes.TryGetValue(id, out var node)) return node;
        throw new KeyNotFoundException($"Unknown node id '{id}'.");
    }

    public Dialogue GetDialogueById(string id)
    {
        if (_dialogues.TryGetValue(id, out var dlg)) return dlg;
        throw new KeyNotFoundException($"Unknown dialogue id '{id}'.");
    }

    // ---- Optional helpers some callers already use ----
    public bool TryGetNode(string id, out Node node) => _nodes.TryGetValue(id, out node!);
    public bool TryGetDialogue(string id, out Dialogue dialogue) => _dialogues.TryGetValue(id, out dialogue!);

    private void LoadAll(string root)
    {
        var nodeDir = Path.Combine(root, "nodes");
        if (Directory.Exists(nodeDir))
        {
            foreach (var file in Directory.EnumerateFiles(nodeDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<Node>(json, JsonOpts);
                    if (model is { Id.Length: > 0 })
                        _nodes[model.Id] = model; // case-insensitive
                }
                catch { /* tolerate authoring-time errors */ }
            }
        }

        var dlgDir = Path.Combine(root, "dialogues");
        if (Directory.Exists(dlgDir))
        {
            foreach (var file in Directory.EnumerateFiles(dlgDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<Dialogue>(json, JsonOpts);
                    if (model is { Id.Length: > 0 })
                        _dialogues[model.Id] = model; // case-insensitive
                }
                catch { /* tolerate authoring-time errors */ }
            }
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

