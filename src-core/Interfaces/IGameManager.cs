using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;
using System.Collections.Generic;

namespace DLSS_Swapper.Core.Interfaces;

public interface IGameManager
{
    void RemoveGame(GameBase game);
    void AddUnknownGameAssets(GameLibrary gameLibrary, string gameTitle, List<GameAsset> gameAssets);
}
