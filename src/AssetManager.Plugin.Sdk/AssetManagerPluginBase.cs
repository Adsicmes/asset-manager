using AssetManager.Plugin.Abstractions;

namespace AssetManager.Plugin.Sdk;

public abstract class AssetManagerPluginBase : IAssetManagerPlugin
{
    protected AssetManagerPluginBase(PluginManifest manifest)
    {
        Manifest = manifest;
    }

    public PluginManifest Manifest { get; }

    public virtual PluginContribution Describe()
    {
        return PluginContribution.Empty;
    }
}
