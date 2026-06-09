namespace AssetManager.Application.Library;

public sealed record AssetImportProgress(
    int CopiedFiles,
    int DiscoveredFiles,
    long CopiedBytes,
    long DiscoveredBytes,
    string? CurrentItemName = null);
