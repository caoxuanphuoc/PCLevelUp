namespace GrowthTracker.Domain.Settings;

/// <summary>Key/value app setting (theme, review time, hide XP, score weights version).</summary>
public sealed class AppSetting
{
    public const string ThemeKey = "Theme";
    public const string ScoreWeightsVersionKey = "ScoreWeightsVersion";
    public const string HideXpKey = "HideXp";

    private AppSetting() { }

    public AppSetting(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; private set; } = "";
    public string Value { get; set; } = "";
}
