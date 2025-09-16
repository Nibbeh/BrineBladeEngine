// src/BrineBlade.Infrastructure/Persistence/SaveMigrator.cs
using BrineBlade.Domain.Game;

namespace BrineBlade.Infrastructure.Persistence
{
    public static class SaveMigrator
    {
        public const int Latest = 1;
        public static SaveGameData Upgrade(SaveGameData data) => data;
    }
}
