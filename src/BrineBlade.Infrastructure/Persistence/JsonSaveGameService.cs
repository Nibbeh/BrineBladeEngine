// src/BrineBlade.Infrastructure/Persistence/JsonSaveGameService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BrineBlade.Domain.Game;
using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Persistence
{
    public sealed class JsonSaveGameService : ISaveGameService
    {
        private readonly string _saveRoot;
        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public JsonSaveGameService(string saveRoot)
        {
            _saveRoot = saveRoot;
        }

        private string SlotPath(string slotId) => Path.Combine(_saveRoot, $"{slotId}.json");

        public IReadOnlyList<SaveSlotInfo> ListSaves()
        {
            Directory.CreateDirectory(_saveRoot);
            var list = new List<SaveSlotInfo>();

            foreach (var file in Directory.EnumerateFiles(_saveRoot, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<SaveGameData>(json, _opts);
                    if (data is null) continue;
                    var fi = new FileInfo(file);

                    // Constructor assumed as: (slotId, filePath, lastWriteUtc, playerName, currentNodeId, day, hour, minute, gold)
                    var slotId = Path.GetFileNameWithoutExtension(file);
                    var playerName = data.Player?.Name ?? "Unknown";
                    var currentNode = data.CurrentNodeId ?? "";

                    list.Add(new SaveSlotInfo(
                        slotId,
                        fi.FullName,
                        fi.LastWriteTimeUtc,
                        playerName,
                        currentNode,
                        data.World.Day,
                        data.World.Hour,
                        data.World.Minute,
                        data.Gold
                    ));
                }
                catch
                {
                    // skip unreadable file
                }
            }

            // Avoid relying on unknown property names; stable fallback ordering by file time via reflection if present
            var prop = typeof(SaveSlotInfo).GetProperty("LastWriteUtc") ?? typeof(SaveSlotInfo).GetProperty("LastWriteTimeUtc");
            if (prop != null)
            {
                return list.OrderByDescending(s => (DateTime)prop.GetValue(s)!).ToList();
            }
            return list;
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
            var data = JsonSerializer.Deserialize<SaveGameData>(json, _opts);
            if (data is null) return null;
            return SaveMigrator.Upgrade(data);
        }
    }
}
