namespace AssetManager.Application.BackgroundTasks;

public sealed record BackgroundTaskSnapshot(
    Guid Id,
    BackgroundTaskKind Kind,
    string Title,
    string StatusText,
    BackgroundTaskState State,
    BackgroundTaskProgress Progress,
    bool IsCancelable,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorMessage = null,
    string? ErrorType = null,
    string? PluginId = null);
