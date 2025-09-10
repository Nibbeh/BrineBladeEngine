namespace BrineBlade.Domain.Entities;

public enum QuestState { NotStarted, Active, Completed, Failed }

public sealed record Quest(string Id, string Title, string Description);

