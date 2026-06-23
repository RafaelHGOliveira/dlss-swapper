using System;
using System.Collections.Generic;
using System.Linq;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Core.Data;

public static class GameLibraryScope
{
    public static IReadOnlyList<GameLibrary> SupportedLibraries(IGameLibraryFactory factory)
        => Enum.GetValues<GameLibrary>().Where(v => factory.Get(v) is not null).ToList();
}
