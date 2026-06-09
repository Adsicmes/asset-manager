namespace AssetManager.Application.Library;

public sealed record AssetImportOptions(
    long? MaxCopyBytesPerSecond,
    IProgress<AssetImportProgress>? Progress = null)
{
    public const long LowImpactMaxCopyBytesPerSecond = 32L * 1024L * 1024L;

    public static AssetImportOptions Default { get; } = new(
        (long?)null,
        null);

    public static AssetImportOptions LowImpact { get; } = new(
        LowImpactMaxCopyBytesPerSecond,
        null);

    public bool IsThrottleEnabled => MaxCopyBytesPerSecond is > 0;
}
