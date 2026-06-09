using AssetManager.Domain.Library;

public sealed record LibrarySyncResult(
    int UpdatedCount,
    int MovedCount,
    int MissingCount,
    int NewAssetCount,
    IReadOnlyList<AssetRecord>? AffectedAssets = null,
    IReadOnlyList<Guid>? RemovedAssetIds = null);
