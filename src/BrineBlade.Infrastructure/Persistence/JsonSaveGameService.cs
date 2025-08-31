using System.Text.Json;
using System.Linq;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Persistence;

public sealed class JsonSaveGameService(string saveRoot) : ISaveGameService
{
    private readonly string _saveRoot = saveRoot;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string SlotPath(string slotId) => Path.Combine(_saveRoot, $"{Sanitize(slotId)}.save.json");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "slot" : name;
    }

    public IReadOnlyList<SaveSlotInfo> ListSaves()
    {
        Directory.CreateDirectory(_saveRoot);
        var list = new List<SaveSlotInfo>();

        foreach (var file in Directory.EnumerateFiles(_saveRoot, "*.save.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<SaveGameData>(json, _opts);
                if (data is null) continue;

                var fi = new FileInfo(file);
                list.Add(new SaveSlotInfo(
                    SlotId: Path.GetFileNameWithoutExtension(file).Replace(".save", ""),
                    FilePath: file,
                    LastWriteTimeUtc: fi.LastWriteTimeUtc,
                    PlayerName: data.Player.Name,
                    CurrentNodeId: data.CurrentNodeId,
                    Gold: data.Gold,
                    Day: data.World.Day,
                    Hour: data.World.Hour,
                    Minute: data.World.Minute
                ));
            }
            catch { }
        }

        return list.OrderByDescending(s => s.LastWriteTimeUtc).ToList();
    }

    public void Save(string slotId, SaveGameData data)
    {
        Directory.CreateDirectory(_saveRoot);
        var path = SlotPath(slotId);
        var json = JsonSerializer.Serialize(data, _opts);
        File.WriteAllText(path, json);
    }

    public SaveGameData? Load(string slotId)
    {
        var path = SlotPath(slotId);
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SaveGameData>(json, _opts);
    }
}
