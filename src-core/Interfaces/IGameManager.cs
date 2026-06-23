using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;
using System.Collections.Generic;

namespace DLSS_Swapper.Core.Interfaces;

public interface IGameManager
{
    void RemoveGame(GameBase game);
    void AddUnknownGameAssets(GameLibrary gameLibrary, string gameTitle, List<GameAsset> gameAssets);
    List<TGame> GetGames<TGame>() where TGame : GameBase;
    TGame? GetGame<TGame>(string platformId) where TGame : GameBase;
    GameBase AddGame(GameBase game, bool scrollIntoView = false);
    GameLibrarySettings? GetGameLibrarySettings(GameLibrary gameLibrary);
    List<GameLibrary> GetGameLibraries(bool onlyEnabled);
}
