namespace AssetManager.Application.BackgroundTasks;

public enum BackgroundTaskState
{
    Running,
    Completed,
    PartialSuccess,
    Failed,
    Canceled
}
