namespace AssetManager.Plugin.Abstractions;

public sealed record PluginManifest(
    string Id,
    string Name,
    Version Version,
    string? Description = null);
