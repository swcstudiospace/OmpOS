using Avalonia.Data.Converters;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Views.Settings;

/// <summary>The value converters of the Settings views. Each one only reads words from the copy classes; none holds a literal.</summary>
internal static class SettingsConverters
{
    /// <summary>Gets the converter from a plugin row to the sentence a screen reader speaks for it.</summary>
    public static readonly FuncValueConverter<PluginRow?, string?> PluginSummary =
        new(static row => row is null ? null : SettingsCopy.PluginSummary(row));

    /// <summary>Gets the converter from a plugin row to the word for its availability.</summary>
    public static readonly FuncValueConverter<PluginRow?, string?> PluginAvailability =
        new(static row => row is null ? null : row.IsFound ? SettingsCopy.PluginFoundLabel : SettingsCopy.PluginMissingLabel);

    /// <summary>Gets the converter from a plugin row to its version, or to the words that say it is not known.</summary>
    public static readonly FuncValueConverter<PluginRow?, string?> PluginVersion =
        new(static row => row is null ? null : string.IsNullOrWhiteSpace(row.Version) ? SettingsCopy.PluginVersionUnknown : row.Version.Trim());

    /// <summary>Gets the converter from a provider template to the accessible name of its Use template button.</summary>
    public static readonly FuncValueConverter<ProviderTemplate?, string?> UseTemplateName =
        new(static template => template is null ? null : ModelsCopy.UseTemplateName(template.DisplayName));

    /// <summary>Gets the converter from a provider template to the title of its group.</summary>
    public static readonly FuncValueConverter<ProviderTemplate?, string?> TemplateGroup =
        new(static template => template is null ? null : ModelsCopy.GroupTitle(template.Group));

    /// <summary>Gets the converter from a provider template to the label of its protocol.</summary>
    public static readonly FuncValueConverter<ProviderTemplate?, string?> TemplateProtocol =
        new(static template => template is null ? null : ModelsCopy.ProtocolLabel(template.Protocol));

    /// <summary>Gets the converter from a provider template to the line that names its endpoint.</summary>
    public static readonly FuncValueConverter<ProviderTemplate?, string?> TemplateEndpoint =
        new(static template => template is null ? null : ModelsCopy.TemplateEndpointText(template.BaseEndpoint));
}
