namespace AssetManager.Application.BackgroundTasks;

public sealed class InMemoryBackgroundTaskCenter : IBackgroundTaskCenter
{
    private readonly object _gate = new();
    private readonly int _historyLimit;
    private readonly Dictionary<Guid, TaskEntry> _entries = new();

    public InMemoryBackgroundTaskCenter(int historyLimit = 50)
    {
        _historyLimit = historyLimit > 0 ? historyLimit : 50;
    }

    public event Action<IReadOnlyList<BackgroundTaskSnapshot>>? SnapshotsChanged;

    public IBackgroundTaskSession StartTask(
        BackgroundTaskStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var taskId = Guid.NewGuid();
        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var startedAt = DateTimeOffset.UtcNow;
        var snapshot = new BackgroundTaskSnapshot(
            taskId,
            request.Kind,
            request.Title,
            request.InitialStatusText,
            BackgroundTaskState.Running,
            request.InitialProgress ?? BackgroundTaskProgress.None,
            request.IsCancelable,
            startedAt,
            null,
            null,
            request.PluginId);

        lock (_gate)
        {
            _entries[taskId] = new TaskEntry(snapshot, linkedCancellation);
        }

        PublishSnapshots();

        return new Session(
            taskId,
            linkedCancellation.Token,
            (statusText, progress) => UpdateTask(taskId, statusText, progress),
            statusText => CompleteTask(taskId, statusText),
            (statusText, errorMessage) => CompleteTaskPartially(taskId, statusText, errorMessage),
            (exception, statusText) => FailTask(taskId, exception, statusText),
            statusText => CancelTask(taskId, statusText));
    }

    public bool RequestCancel(Guid taskId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(taskId, out var entry))
            {
                return false;
            }

            if (entry.Snapshot.State != BackgroundTaskState.Running || !entry.Snapshot.IsCancelable)
            {
                return false;
            }

            entry.Cancellation.Cancel();
            return true;
        }
    }

    public IReadOnlyList<BackgroundTaskSnapshot> GetSnapshots()
    {
        lock (_gate)
        {
            return BuildOrderedSnapshots();
        }
    }

    private void UpdateTask(
        Guid taskId,
        string statusText,
        BackgroundTaskProgress? progress)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(taskId, out var entry)
                || entry.Snapshot.State != BackgroundTaskState.Running)
            {
                return;
            }

            entry.Snapshot = entry.Snapshot with
            {
                StatusText = statusText,
                Progress = progress ?? entry.Snapshot.Progress
            };
        }

        PublishSnapshots();
    }

    private void CompleteTask(Guid taskId, string statusText)
    {
        TransitionTask(
            taskId,
            BackgroundTaskState.Completed,
            statusText,
            null,
            null);
    }

    private void CompleteTaskPartially(
        Guid taskId,
        string statusText,
        string? errorMessage)
    {
        TransitionTask(
            taskId,
            BackgroundTaskState.PartialSuccess,
            statusText,
            errorMessage,
            null);
    }

    private void FailTask(
        Guid taskId,
        Exception exception,
        string? statusText)
    {
        TransitionTask(
            taskId,
            BackgroundTaskState.Failed,
            statusText ?? exception.Message,
            exception.Message,
            exception.GetType().Name);
    }

    private void CancelTask(
        Guid taskId,
        string? statusText)
    {
        TransitionTask(
            taskId,
            BackgroundTaskState.Canceled,
            statusText ?? "Canceled.",
            null,
            null);
    }

    private void TransitionTask(
        Guid taskId,
        BackgroundTaskState nextState,
        string statusText,
        string? errorMessage,
        string? errorType)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(taskId, out var entry)
                || entry.Snapshot.State != BackgroundTaskState.Running)
            {
                return;
            }

            entry.Snapshot = entry.Snapshot with
            {
                State = nextState,
                StatusText = statusText,
                FinishedAt = DateTimeOffset.UtcNow,
                ErrorMessage = errorMessage,
                ErrorType = errorType,
                IsCancelable = false
            };

            if (nextState == BackgroundTaskState.Canceled)
            {
                entry.Cancellation.Cancel();
            }

            entry.Cancellation.Dispose();
            TrimHistory();
        }

        PublishSnapshots();
    }

    private void TrimHistory()
    {
        var finishedTaskIds = _entries.Values
            .Where(entry => entry.Snapshot.State != BackgroundTaskState.Running)
            .OrderByDescending(entry => entry.Snapshot.FinishedAt ?? entry.Snapshot.StartedAt)
            .Skip(_historyLimit)
            .Select(entry => entry.Snapshot.Id)
            .ToArray();

        foreach (var finishedTaskId in finishedTaskIds)
        {
            _entries.Remove(finishedTaskId);
        }
    }

    private void PublishSnapshots()
    {
        var snapshots = GetSnapshots();
        SnapshotsChanged?.Invoke(snapshots);
    }

    private IReadOnlyList<BackgroundTaskSnapshot> BuildOrderedSnapshots()
    {
        return _entries.Values
            .Select(entry => entry.Snapshot)
            .OrderBy(snapshot => snapshot.State == BackgroundTaskState.Running ? 0 : 1)
            .ThenByDescending(snapshot => snapshot.State == BackgroundTaskState.Running
                ? snapshot.StartedAt
                : snapshot.FinishedAt ?? snapshot.StartedAt)
            .ToArray();
    }

    private sealed class TaskEntry(
        BackgroundTaskSnapshot snapshot,
        CancellationTokenSource cancellation)
    {
        public BackgroundTaskSnapshot Snapshot { get; set; } = snapshot;

        public CancellationTokenSource Cancellation { get; } = cancellation;
    }

    private sealed class Session(
        Guid taskId,
        CancellationToken cancellationToken,
        Action<string, BackgroundTaskProgress?> update,
        Action<string> complete,
        Action<string, string?> completePartially,
        Action<Exception, string?> fail,
        Action<string?> cancel) : IBackgroundTaskSession
    {
        private int _isCompleted;

        public Guid TaskId { get; } = taskId;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public void Update(
            string statusText,
            BackgroundTaskProgress? progress = null)
        {
            if (Volatile.Read(ref _isCompleted) != 0)
            {
                return;
            }

            update(statusText, progress);
        }

        public void Complete(string statusText)
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            complete(statusText);
        }

        public void CompletePartially(
            string statusText,
            string? errorMessage = null)
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            completePartially(statusText, errorMessage);
        }

        public void Fail(
            Exception exception,
            string? statusText = null)
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            fail(exception, statusText);
        }

        public void Cancel(string? statusText = null)
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            cancel(statusText);
        }

        public void Dispose()
        {
        }
    }
}
