namespace AssetManager.Application.BackgroundTasks;

public sealed record BackgroundTaskStartRequest(
    BackgroundTaskKind Kind,
    string Title,
    string InitialStatusText,
    bool IsCancelable = false,
    string? PluginId = null,
    BackgroundTaskProgress? InitialProgress = null);
