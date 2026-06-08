using AssetManager.Plugin.Abstractions;

namespace AssetManager.Plugin.Host;

public sealed class PluginRegistry
{
    private readonly List<IAssetManagerPlugin> _plugins = [];

    public IReadOnlyList<IAssetManagerPlugin> Plugins => _plugins;

    public void Register(IAssetManagerPlugin plugin)
    {
        if (_plugins.Any(existing =>
                string.Equals(existing.Manifest.Id, plugin.Manifest.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Plugin is already registered: {plugin.Manifest.Id}");
        }

        _plugins.Add(plugin);
    }

    public PluginContribution DescribeAll()
    {
        var contributions = _plugins.Select(plugin => plugin.Describe()).ToArray();
        return new PluginContribution(
            contributions.SelectMany(contribution => contribution.AssetTypes).ToArray(),
            contributions.SelectMany(contribution => contribution.Previews).ToArray(),
            contributions.SelectMany(contribution => contribution.UiElements).ToArray());
    }
}
