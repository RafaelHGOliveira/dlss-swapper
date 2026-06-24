using DLSS_Swapper.Core.Interfaces;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxSettings : ISettings
{
    public bool AllowUntrusted => true;
    public string[] IgnoredPaths => [];
    public void SaveSettings() { }
}
