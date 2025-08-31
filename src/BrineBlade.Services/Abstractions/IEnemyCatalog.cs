using System.Collections.Generic;
using BrineBlade.Domain.Entities;

namespace BrineBlade.Services.Abstractions
{
    public interface IEnemyCatalog
    {
        IReadOnlyDictionary<string, EnemyDef> All { get; }
        bool TryGet(string id, out EnemyDef def);
        EnemyDef GetRequired(string id);
    }
}
