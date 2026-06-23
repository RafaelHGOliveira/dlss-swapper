using DLSS_Swapper.Core.Platform;
using System;
using System.IO;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxSteamPathProvider : ISteamPathProvider
{
    private readonly string _homeDir;
    private readonly Func<string, bool> _directoryExists;

    public LinuxSteamPathProvider()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Directory.Exists)
    {
    }

    public LinuxSteamPathProvider(string homeDir, Func<string, bool> directoryExists)
    {
        _homeDir = homeDir;
        _directoryExists = directoryExists;
    }

    public string? GetSteamInstallPath()
    {
        var candidates = new[]
        {
            Path.Combine(_homeDir, ".steam", "steam"),
            Path.Combine(_homeDir, ".steam", "root"),
            Path.Combine(_homeDir, ".local", "share", "Steam"),
            Path.Combine(_homeDir, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
        };

        foreach (var path in candidates)
        {
            if (_directoryExists(path))
                return path;
        }

        return null;
    }
}
