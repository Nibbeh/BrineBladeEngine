using BrineBlade.Domain.Entities;

namespace BrineBlade.Services.Abstractions;

public interface IContentStore
{
    Node? GetNodeById(string id);
    Dialogue? GetDialogueById(string id);
}
