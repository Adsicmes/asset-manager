namespace AssetManager.Application.BackgroundTasks;

public sealed record BackgroundTaskProgress(
    long CompletedUnits,
    long? TotalUnits,
    string? UnitLabel = null,
    bool IsIndeterminate = false)
{
    public static BackgroundTaskProgress None { get; } = new(
        0,
        null,
        null,
        true);
}
