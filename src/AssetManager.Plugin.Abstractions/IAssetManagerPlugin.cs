namespace AssetManager.Plugin.Abstractions;

public interface IAssetManagerPlugin
{
    PluginManifest Manifest { get; }

    PluginContribution Describe();
}
