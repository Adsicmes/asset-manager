namespace AssetManager.Application.BackgroundTasks;

public sealed class NullBackgroundTaskCenter : IBackgroundTaskCenter
{
    public static NullBackgroundTaskCenter Instance { get; } = new();

    private NullBackgroundTaskCenter()
    {
    }

    public event Action<IReadOnlyList<BackgroundTaskSnapshot>>? SnapshotsChanged
    {
        add { }
        remove { }
    }

    public IBackgroundTaskSession StartTask(
        BackgroundTaskStartRequest request,
        CancellationToken cancellationToken = default)
    {
        return new Session(cancellationToken);
    }

    public bool RequestCancel(Guid taskId)
    {
        return false;
    }

    public IReadOnlyList<BackgroundTaskSnapshot> GetSnapshots()
    {
        return [];
    }

    private sealed class Session(CancellationToken cancellationToken) : IBackgroundTaskSession
    {
        public Guid TaskId { get; } = Guid.Empty;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public void Update(
            string statusText,
            BackgroundTaskProgress? progress = null)
        {
        }

        public void Complete(string statusText)
        {
        }

        public void CompletePartially(
            string statusText,
            string? errorMessage = null)
        {
        }

        public void Fail(
            Exception exception,
            string? statusText = null)
        {
        }

        public void Cancel(string? statusText = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
