using DLSS_Swapper.Interfaces;
using System.Collections.Generic;

namespace DLSS_Swapper.Core.Platform;

public interface IGameLibraryFactory
{
    IReadOnlyList<IGameLibrary> CreateEnabledLibraries();
    IGameLibrary? Get(GameLibrary gameLibrary);
}
