using DLSS_Swapper.Core.Platform;
using System;
using System.IO;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxStoragePathProvider : IStoragePathProvider
{
    private readonly string _homeDir;
    private readonly Func<string, string?> _getEnvVar;

    public LinuxStoragePathProvider()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable)
    {
    }

    public LinuxStoragePathProvider(string homeDir, Func<string, string?> getEnvVar)
    {
        _homeDir = homeDir;
        _getEnvVar = getEnvVar;
    }

    public string GetStorageRoot()
    {
        var xdgConfigHome = _getEnvVar("XDG_CONFIG_HOME");
        var configBase = !string.IsNullOrEmpty(xdgConfigHome)
            ? xdgConfigHome
            : Path.Combine(_homeDir, ".config");

        return Path.Combine(configBase, "dlss-swapper") + Path.DirectorySeparatorChar;
    }
}
