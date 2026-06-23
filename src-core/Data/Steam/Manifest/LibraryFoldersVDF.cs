using System.Collections.Generic;

namespace DLSS_Swapper.Data.Steam.Manifest;

public class LibraryFoldersVDF
{
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Apps { get; set; } = new Dictionary<string, string>();
}
