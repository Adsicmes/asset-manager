namespace AssetManager.Plugin.Abstractions;

public sealed record PluginContribution(
    IReadOnlyList<AssetTypeContribution> AssetTypes,
    IReadOnlyList<PreviewContribution> Previews,
    IReadOnlyList<UiContribution> UiElements)
{
    public static readonly PluginContribution Empty = new([], [], []);
}
