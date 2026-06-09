namespace AssetManager.Application.Library;

public sealed record LibrarySyncProgress(
    LibrarySyncStage Stage,
    int ProcessedAssets = 0,
    int? TotalAssets = null,
    int UpdatedCount = 0,
    int MovedCount = 0,
    int MissingCount = 0,
    int NewAssetCount = 0);
