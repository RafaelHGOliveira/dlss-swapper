namespace DLSS_Swapper.Interfaces;

[Flags]
public enum GameLibrary : uint
{
    Steam = 1,
    GOG = 2,
    EpicGamesStore = 4,
    UbisoftConnect = 8,
    XboxApp = 16,
    ManuallyAdded = 32,
    BattleNet = 64,
    EAApp = 128,
};
