namespace GrowthTracker.Infrastructure;

/// <summary>Per-OS locations for data, backups, logs and settings (TDD §7.3).</summary>
public sealed record AppPaths(string DataDirectory, string DatabasePath, string BackupDirectory,
                              string LogDirectory, string SettingsPath)
{
    public static AppPaths ForCurrentOs() =>
        Resolve(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Pure resolution so every OS layout can be tested on any machine.</summary>
    public static AppPaths Resolve(bool isWindows, Func<string, string?> env, string home)
    {
        if (isWindows)
        {
            string local = Path.Combine(env("LOCALAPPDATA") ?? Path.Combine(home, "AppData", "Local"), "GrowthTracker");
            string roaming = Path.Combine(env("APPDATA") ?? Path.Combine(home, "AppData", "Roaming"), "GrowthTracker");
            return new AppPaths(local, Path.Combine(local, "growth.db"), Path.Combine(local, "backups"),
                                Path.Combine(local, "logs"), Path.Combine(roaming, "settings.json"));
        }

        string data = Path.Combine(Xdg(env, "XDG_DATA_HOME", home, ".local", "share"), "growth-tracker");
        string state = Path.Combine(Xdg(env, "XDG_STATE_HOME", home, ".local", "state"), "growth-tracker");
        string config = Path.Combine(Xdg(env, "XDG_CONFIG_HOME", home, ".config"), "growth-tracker");
        return new AppPaths(data, Path.Combine(data, "growth.db"), Path.Combine(data, "backups"),
                            Path.Combine(state, "logs"), Path.Combine(config, "settings.json"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
    }

    private static string Xdg(Func<string, string?> env, string variable, string home, params string[] fallback)
    {
        string? value = env(variable);
        return string.IsNullOrWhiteSpace(value) ? Path.Combine([home, .. fallback]) : value;
    }
}
