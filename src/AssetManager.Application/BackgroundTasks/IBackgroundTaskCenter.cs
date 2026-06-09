namespace AssetManager.Application.BackgroundTasks;

public interface IBackgroundTaskCenter : IBackgroundTaskFeed
{
    IBackgroundTaskSession StartTask(
        BackgroundTaskStartRequest request,
        CancellationToken cancellationToken = default);

    bool RequestCancel(Guid taskId);
}
