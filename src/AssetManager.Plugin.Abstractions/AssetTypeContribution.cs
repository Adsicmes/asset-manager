namespace AssetManager.Plugin.Abstractions;

public sealed record AssetTypeContribution(
    string TypeId,
    IReadOnlyList<string> Extensions);
