using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GARbro.GUI.Linux;

internal sealed class LinuxSettings
{
    private const int MaxRecentPaths = 12;

    public List<string> RecentPaths { get; set; } = new();
    public string OutputDirectory { get; set; }

    public static LinuxSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<LinuxSettings>(File.ReadAllText(path)) ?? new LinuxSettings();
            }
        }
        catch
        {
        }
        return new LinuxSettings();
    }

    public void Save()
    {
        try
        {
            var path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
        }
    }

    public void RememberPath(string path)
    {
        var normalized = NormalizeExistingPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        RecentPaths.RemoveAll(p => string.Equals(p, normalized, StringComparison.Ordinal));
        RecentPaths.Insert(0, normalized);
        if (RecentPaths.Count > MaxRecentPaths)
        {
            RecentPaths.RemoveRange(MaxRecentPaths, RecentPaths.Count - MaxRecentPaths);
        }
    }

    public IEnumerable<string> ExistingRecentPaths()
    {
        return RecentPaths
            .Select(NormalizeExistingPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal);
    }

    private static string NormalizeExistingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (Directory.Exists(path))
            {
                return new DirectoryInfo(path).FullName;
            }
            if (File.Exists(path))
            {
                return new FileInfo(path).FullName;
            }
        }
        catch
        {
        }
        return null;
    }

    private static string SettingsPath
    {
        get
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                configHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");
            }
            return Path.Combine(configHome, "garbro-linux", "settings.json");
        }
    }
}
