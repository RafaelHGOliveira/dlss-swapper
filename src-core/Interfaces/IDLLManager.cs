using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Core.Interfaces;

public interface IDLLManager
{
    GameAssetType GetAssetBackupType(GameAssetType assetType);
    bool IsInKnownGameAsset(GameAsset gameAsset, GameLibrary gameLibrary, string titleBase64);
}
