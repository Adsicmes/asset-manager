using AssetManager.Domain.Library;

namespace AssetManager.Application.Library;

public sealed class LibraryApplicationService(
    IAssetLibraryRepository repository,
    IAssetContentStore contentStore,
    IAssetTypeResolver assetTypeResolver,
    IAssetActivityLog activityLog)
{
    private const int TextPreviewCharacterLimit = 32_000;

    public async Task<LibrarySession> OpenOrCreateAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var location = LibraryLocation.Create(rootPath);

        await contentStore.PrepareLibraryAsync(location, cancellationToken);
        await repository.InitializeAsync(location, cancellationToken);
        await activityLog.AppendAsync(location, "Library opened.", cancellationToken);

        var folders = await contentStore.ListFoldersAsync(location, cancellationToken);
        var assets = await SearchAsync(location, LibraryRelativePath.Root, string.Empty, [], cancellationToken);
        return new LibrarySession(location, LibraryRelativePath.Root, folders, assets);
    }

    public async Task<AssetImportResult> ImportPathsAsync(
        LibraryLocation location,
        LibraryRelativePath currentFolder,
        IEnumerable<string> sourcePaths,
        AssetImportOptions? importOptions = null,
        CancellationToken cancellationToken = default)
    {
        var sources = sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (sources.Length == 0)
        {
            return new AssetImportResult([], []);
        }

        var copyResult = await contentStore.CopyIntoLibraryAsync(
            location,
            currentFolder,
            sources,
            importOptions,
            cancellationToken);
        if (copyResult.CopiedFiles.Count == 0)
        {
            return new AssetImportResult([], copyResult.CreatedFolders);
        }

        try
        {
            var importedAssets = await repository.AddAssetsAsync(location, copyResult.CopiedFiles, cancellationToken);
            await activityLog.AppendAsync(location, $"Imported {importedAssets.Count} asset(s).", cancellationToken);
            return new AssetImportResult(importedAssets, copyResult.CreatedFolders);
        }
        catch
        {
            await contentStore.RollbackCopiedAssetsAsync(location, copyResult.CopiedFiles, cancellationToken);
            await activityLog.AppendAsync(location, "Import failed; copied files were rolled back.", cancellationToken);
            throw;
        }
    }

    public async Task<AssetRecord> CreateTextSnippetAsync(
        LibraryLocation location,
        LibraryRelativePath currentFolder,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var preparedFile = await contentStore.WriteTextSnippetAsync(
            location,
            currentFolder,
            NormalizeTextSnippetFileName(fileName),
            content,
            cancellationToken);

        try
        {
            var created = await repository.AddAssetsAsync(location, [preparedFile], cancellationToken);
            await activityLog.AppendAsync(location, $"Created text snippet {created[0].LibraryRelativePath}.", cancellationToken);
            return created[0];
        }
        catch
        {
            await contentStore.RollbackCopiedAssetsAsync(location, [preparedFile], cancellationToken);
            await activityLog.AppendAsync(location, "Text snippet creation failed; copied file was rolled back.", cancellationToken);
            throw;
        }
    }

    public Task<IReadOnlyList<AssetRecord>> SearchAsync(
        LibraryLocation location,
        LibraryRelativePath currentFolder,
        string query,
        IEnumerable<string> requiredTags,
        CancellationToken cancellationToken = default)
    {
        var request = new AssetSearchRequest(
            currentFolder,
            query.Trim(),
            NormalizeTags(requiredTags));

        return repository.SearchAsync(location, request, cancellationToken);
    }

    public async Task<AssetPreview> GetPreviewAsync(
        LibraryLocation location,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await repository.GetByIdAsync(location, assetId, cancellationToken)
                    ?? throw new InvalidOperationException("Asset was not found.");

        var fullPath = asset.FullPath(location);
        var fileExists = await contentStore.FileExistsAsync(fullPath, cancellationToken);
        if (asset.Status == AssetStatus.Missing || !fileExists)
        {
            return new AssetPreview(asset.Id, asset.TypeId, AssetStatus.Missing, fullPath, null);
        }

        var textContent = asset.TypeId == AssetTypeId.Text
            ? await contentStore.ReadTextPreviewAsync(fullPath, TextPreviewCharacterLimit, cancellationToken)
            : null;

        return new AssetPreview(asset.Id, asset.TypeId, AssetStatus.Available, fullPath, textContent);
    }

    public async Task UpdateMetadataAsync(
        LibraryLocation location,
        Guid assetId,
        string notes,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        await repository.UpdateMetadataAsync(location, assetId, notes.Trim(), NormalizeTags(tags), cancellationToken);
        await activityLog.AppendAsync(location, $"Updated metadata for asset {assetId}.", cancellationToken);
    }

    public Task<IReadOnlyList<LibraryFolder>> ListFoldersAsync(
        LibraryLocation location,
        CancellationToken cancellationToken = default)
    {
        return contentStore.ListFoldersAsync(location, cancellationToken);
    }

    public async Task CreateFolderAsync(
        LibraryLocation location,
        LibraryRelativePath folder,
        CancellationToken cancellationToken = default)
    {
        await contentStore.CreateFolderAsync(location, folder, cancellationToken);
        await activityLog.AppendAsync(location, $"Created folder {folder}.", cancellationToken);
    }

    public async Task<LibrarySyncResult> SynchronizeAsync(
        LibraryLocation location,
        IProgress<LibrarySyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new LibrarySyncProgress(LibrarySyncStage.ScanningFiles));

        var existingAssets = await repository.GetAllAsync(location, cancellationToken);
        var scannedFiles = await contentStore.ScanContentFilesAsync(location, cancellationToken);
        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.ReconcilingAssets,
            0,
            existingAssets.Count));

        var scannedByPath = scannedFiles.ToDictionary(
            file => file.LibraryRelativePath.Value,
            StringComparer.OrdinalIgnoreCase);

        var unusedScannedFiles = new List<StoredContentFile>(scannedFiles);
        var updatedCount = 0;
        var movedCount = 0;
        var missingCount = 0;
        var processedAssets = 0;

        foreach (var asset in existingAssets)
        {
            if (scannedByPath.TryGetValue(asset.LibraryRelativePath.Value, out var samePathFile))
            {
                await repository.UpdateAssetFileStateAsync(
                    location,
                    asset.Id,
                    samePathFile,
                    AssetStatus.Available,
                    cancellationToken);

                unusedScannedFiles.Remove(samePathFile);
                updatedCount++;
                processedAssets++;
                ReportSyncProgress();
                continue;
            }

            var movedFile = unusedScannedFiles.FirstOrDefault(file =>
                !string.IsNullOrWhiteSpace(file.ContentHash)
                && string.Equals(file.ContentHash, asset.ContentHash, StringComparison.OrdinalIgnoreCase));

            if (movedFile is not null)
            {
                await repository.UpdateAssetFileStateAsync(
                    location,
                    asset.Id,
                    movedFile,
                    AssetStatus.Available,
                    cancellationToken);

                unusedScannedFiles.Remove(movedFile);
                movedCount++;
                processedAssets++;
                ReportSyncProgress();
                continue;
            }

            await repository.DeleteAssetAsync(location, asset.Id, cancellationToken);
            missingCount++;
            processedAssets++;
            ReportSyncProgress();
        }

        var newAssets = unusedScannedFiles.Select(file => file.ToPreparedAssetFile(null, DateTimeOffset.UtcNow)).ToArray();
        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.RegisteringNewAssets,
            processedAssets,
            existingAssets.Count,
            updatedCount,
            movedCount,
            missingCount,
            newAssets.Length));

        if (newAssets.Length > 0)
        {
            await repository.AddAssetsAsync(location, newAssets, cancellationToken);
        }

        var result = new LibrarySyncResult(updatedCount, movedCount, missingCount, newAssets.Length);
        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.Completed,
            processedAssets,
            existingAssets.Count,
            updatedCount,
            movedCount,
            missingCount,
            newAssets.Length));
        await activityLog.AppendAsync(
            location,
            $"Synchronized library. Updated={result.UpdatedCount}; moved={result.MovedCount}; missing={result.MissingCount}; new={result.NewAssetCount}.",
            cancellationToken);

        return result;

        void ReportSyncProgress()
        {
            if (processedAssets < existingAssets.Count
                && processedAssets % 16 != 0)
            {
                return;
            }

            progress?.Report(new LibrarySyncProgress(
                LibrarySyncStage.ReconcilingAssets,
                processedAssets,
                existingAssets.Count,
                updatedCount,
                movedCount,
                missingCount));
        }
    }

    public async Task<LibrarySyncResult> SynchronizePathsAsync(
        LibraryLocation location,
        IEnumerable<string> changedPaths,
        IProgress<LibrarySyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var changedRelativePaths = NormalizeChangedLibraryPaths(location, changedPaths);
        if (changedRelativePaths.Count == 0)
        {
            return new LibrarySyncResult(0, 0, 0, 0, [], []);
        }

        progress?.Report(new LibrarySyncProgress(LibrarySyncStage.ScanningFiles));

        var scannedFiles = await contentStore.ScanContentFilesAsync(
            location,
            changedRelativePaths,
            cancellationToken);
        var existingAssets = await repository.GetByRelativePathPrefixesAsync(
            location,
            changedRelativePaths,
            cancellationToken);
        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.ReconcilingAssets,
            0,
            existingAssets.Count + scannedFiles.Count));

        var existingByPath = existingAssets
            .GroupBy(asset => asset.LibraryRelativePath.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var unmatchedExisting = existingAssets.ToDictionary(asset => asset.Id);
        var newFiles = new List<PreparedAssetFile>();
        var affectedAssets = new List<AssetRecord>();
        var removedAssetIds = new List<Guid>();
        var updatedCount = 0;
        var movedCount = 0;
        var missingCount = 0;
        var processedAssets = 0;

        foreach (var file in scannedFiles)
        {
            if (existingByPath.TryGetValue(file.LibraryRelativePath.Value, out var samePathAsset))
            {
                await repository.UpdateAssetFileStateAsync(
                    location,
                    samePathAsset.Id,
                    file,
                    AssetStatus.Available,
                    cancellationToken);

                unmatchedExisting.Remove(samePathAsset.Id);
                affectedAssets.Add(UpdateAssetRecordFromFile(samePathAsset, file));
                updatedCount++;
                processedAssets++;
                ReportSyncProgress();
                continue;
            }

            var movedAsset = unmatchedExisting.Values.FirstOrDefault(asset =>
                !string.IsNullOrWhiteSpace(file.ContentHash)
                && string.Equals(file.ContentHash, asset.ContentHash, StringComparison.OrdinalIgnoreCase));

            if (movedAsset is not null)
            {
                await repository.UpdateAssetFileStateAsync(
                    location,
                    movedAsset.Id,
                    file,
                    AssetStatus.Available,
                    cancellationToken);

                unmatchedExisting.Remove(movedAsset.Id);
                affectedAssets.Add(UpdateAssetRecordFromFile(movedAsset, file));
                movedCount++;
                processedAssets++;
                ReportSyncProgress();
                continue;
            }

            newFiles.Add(file.ToPreparedAssetFile(null, DateTimeOffset.UtcNow));
            processedAssets++;
            ReportSyncProgress();
        }

        foreach (var asset in unmatchedExisting.Values)
        {
            await repository.DeleteAssetAsync(location, asset.Id, cancellationToken);
            removedAssetIds.Add(asset.Id);
            missingCount++;
            processedAssets++;
            ReportSyncProgress();
        }

        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.RegisteringNewAssets,
            processedAssets,
            existingAssets.Count + scannedFiles.Count,
            updatedCount,
            movedCount,
            missingCount,
            newFiles.Count));

        IReadOnlyList<AssetRecord> createdAssets = [];
        if (newFiles.Count > 0)
        {
            createdAssets = await repository.AddAssetsAsync(location, newFiles, cancellationToken);
            affectedAssets.AddRange(createdAssets);
        }

        var result = new LibrarySyncResult(
            updatedCount,
            movedCount,
            missingCount,
            createdAssets.Count,
            affectedAssets,
            removedAssetIds);
        progress?.Report(new LibrarySyncProgress(
            LibrarySyncStage.Completed,
            processedAssets,
            existingAssets.Count + scannedFiles.Count,
            updatedCount,
            movedCount,
            missingCount,
            createdAssets.Count));
        await activityLog.AppendAsync(
            location,
            $"Synchronized changed paths. Updated={result.UpdatedCount}; moved={result.MovedCount}; missing={result.MissingCount}; new={result.NewAssetCount}.",
            cancellationToken);

        return result;

        void ReportSyncProgress()
        {
            var totalAssets = existingAssets.Count + scannedFiles.Count;
            if (processedAssets < totalAssets
                && processedAssets % 16 != 0)
            {
                return;
            }

            progress?.Report(new LibrarySyncProgress(
                LibrarySyncStage.ReconcilingAssets,
                processedAssets,
                totalAssets,
                updatedCount,
                movedCount,
                missingCount));
        }
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .SelectMany(tag => tag.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTextSnippetFileName(string fileName)
    {
        var normalized = string.IsNullOrWhiteSpace(fileName)
            ? $"snippet-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.txt"
            : fileName.Trim();

        return Path.GetExtension(normalized).Length == 0 ? normalized + ".txt" : normalized;
    }

    public AssetTypeId ResolveAssetType(string? extension)
    {
        return assetTypeResolver.Resolve(extension);
    }

    private static IReadOnlyList<LibraryRelativePath> NormalizeChangedLibraryPaths(
        LibraryLocation location,
        IEnumerable<string> changedPaths)
    {
        var fullRootPath = Path.GetFullPath(location.RootPath);
        var normalized = new Dictionary<string, LibraryRelativePath>(StringComparer.OrdinalIgnoreCase);

        foreach (var changedPath in changedPaths)
        {
            if (string.IsNullOrWhiteSpace(changedPath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(changedPath);
            if (!IsSameOrDescendantPath(fullPath, fullRootPath)
                || location.IsManagementPath(fullPath))
            {
                continue;
            }

            var relativePath = LibraryRelativePath.Create(Path.GetRelativePath(fullRootPath, fullPath));
            normalized[relativePath.Value] = relativePath;
        }

        return normalized.Values
            .OrderBy(path => path.Value.Length)
            .ToArray();
    }

    private static bool IsSameOrDescendantPath(string candidatePath, string ancestorPath)
    {
        var candidate = NormalizeFullPath(candidatePath);
        var ancestor = NormalizeFullPath(ancestorPath);

        return string.Equals(candidate, ancestor, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(ancestor + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFullPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static AssetRecord UpdateAssetRecordFromFile(AssetRecord asset, StoredContentFile file)
    {
        return asset with
        {
            DisplayName = file.DisplayName,
            LibraryRelativePath = file.LibraryRelativePath,
            TypeId = file.TypeId,
            Extension = file.Extension,
            SizeBytes = file.SizeBytes,
            CreatedAt = file.CreatedAt,
            ModifiedAt = file.ModifiedAt,
            ContentHash = file.ContentHash,
            Status = AssetStatus.Available
        };
    }
}
