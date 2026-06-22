namespace DLSS_Swapper.Core.Interfaces;

public interface ISettings
{
    bool AllowUntrusted { get; }
    string[] IgnoredPaths { get; }
}
