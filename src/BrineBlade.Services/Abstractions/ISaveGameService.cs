using System.Collections.Generic;
using BrineBlade.Domain.Game;

namespace BrineBlade.Services.Abstractions;

public interface ISaveGameService
{
    IReadOnlyList<SaveSlotInfo> ListSaves();
    void Save(string slotId, SaveGameData data);
    SaveGameData? Load(string slotId);
}
