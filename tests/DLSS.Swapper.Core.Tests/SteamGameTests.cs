using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Interfaces;
using Xunit;

public class SteamGameTests
{
    [Fact]
    public void Constructor_SetsPlatformIdAndLibrary()
    {
        var game = new SteamGame("570");
        Assert.Equal("570", game.PlatformId);
        Assert.Equal(GameLibrary.Steam, game.GameLibrary);
    }

    [Theory]
    [InlineData(SteamStateFlag.StateFullyInstalled, true)]
    [InlineData((SteamStateFlag)0, false)]
    public void IsReadyToPlay_ReflectsStateFlags(SteamStateFlag flags, bool expected)
    {
        var game = new SteamGame("1") { StateFlags = flags };
        Assert.Equal(expected, game.IsReadyToPlay);
    }
}
