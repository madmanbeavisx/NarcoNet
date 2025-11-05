using BepInEx.Configuration;

namespace NarcoNet;

/// <summary>
///     Attributes for customizing BepInEx Configuration Manager behavior.
/// </summary>
internal sealed class ConfigurationManagerAttributes
{
    /// <summary>
    ///     Delegate for custom hotkey drawer.
    /// </summary>
    public delegate void CustomHotkeyDrawerFunc(
        ConfigEntryBase setting,
        ref bool isCurrentlyAcceptingInput);

    /// <summary>
    ///     Show numeric range as a percentage slider (0-100).
    /// </summary>
    public bool? ShowRangeAsPercent { get; set; }

    /// <summary>
    ///     Custom drawer function for rendering the config entry.
    /// </summary>
    public Action<ConfigEntryBase>? CustomDrawer { get; set; }

    /// <summary>
    ///     Custom hotkey drawer function for rendering hotkey config entries.
    /// </summary>
    public CustomHotkeyDrawerFunc? CustomHotkeyDrawer { get; set; }

    /// <summary>
    ///     Whether the setting is visible in the configuration manager.
    /// </summary>
    public bool? Browsable { get; set; }

    /// <summary>
    ///     Category to group this setting under.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///     Default value to display for this setting.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    ///     Hide the "Default" button for resetting the value.
    /// </summary>
    public bool? HideDefaultButton { get; set; }

    /// <summary>
    ///     Hide the setting name in the UI.
    /// </summary>
    public bool? HideSettingName { get; set; }

    /// <summary>
    ///     Description text for this setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Display name for this setting (overrides the config key name).
    /// </summary>
    public string? DispName { get; set; }

    /// <summary>
    ///     Sort order for this setting within its category.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    ///     Whether the setting is read-only and cannot be modified.
    /// </summary>
    public bool? ReadOnly { get; set; }

    /// <summary>
    ///     Whether this is an advanced setting (may be hidden by default).
    /// </summary>
    public bool? IsAdvanced { get; set; }

    /// <summary>
    ///     Function to convert the config value object to a string for display.
    /// </summary>
    public Func<object, string>? ObjToStr { get; set; }

    /// <summary>
    ///     Function to parse a string input back to the config value object.
    /// </summary>
    public Func<string, object>? StrToObj { get; set; }
}
