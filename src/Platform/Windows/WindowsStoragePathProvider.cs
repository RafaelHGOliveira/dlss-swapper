using DLSS_Swapper.Core.Platform;
using System;
using System.IO;

namespace DLSS_Swapper.Platform.Windows;

public sealed class WindowsStoragePathProvider : IStoragePathProvider
{
    public string GetStorageRoot()
    {
#if PORTABLE && DEBUG
        return Path.Combine(AppContext.BaseDirectory, "StoredData", "DEBUG");
#elif PORTABLE && !DEBUG
        return Path.Combine(AppContext.BaseDirectory, "StoredData");
#elif !PORTABLE && DEBUG
        return Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper", "DEBUG");
#else
        return Path.Combine(Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"), "DLSS Swapper");
#endif
    }
}
