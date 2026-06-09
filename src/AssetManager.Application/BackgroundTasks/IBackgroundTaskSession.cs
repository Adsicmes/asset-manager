namespace AssetManager.Application.BackgroundTasks;

public interface IBackgroundTaskSession : IDisposable
{
    Guid TaskId { get; }

    CancellationToken CancellationToken { get; }

    void Update(
        string statusText,
        BackgroundTaskProgress? progress = null);

    void Complete(string statusText);

    void CompletePartially(
        string statusText,
        string? errorMessage = null);

    void Fail(
        Exception exception,
        string? statusText = null);

    void Cancel(string? statusText = null);
}
