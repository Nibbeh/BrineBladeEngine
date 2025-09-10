using System;

namespace BrineBlade.Domain.Game;

public sealed record SaveSlotInfo(
    string SlotId,
    string FilePath,
    DateTime LastWriteTimeUtc,
    string PlayerName,
    string CurrentNodeId,
    int Gold,
    int Day, int Hour, int Minute
);

