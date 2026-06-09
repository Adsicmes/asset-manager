namespace AssetManager.Application.BackgroundTasks;

public interface IBackgroundTaskFeed
{
    event Action<IReadOnlyList<BackgroundTaskSnapshot>>? SnapshotsChanged;

    IReadOnlyList<BackgroundTaskSnapshot> GetSnapshots();
}
