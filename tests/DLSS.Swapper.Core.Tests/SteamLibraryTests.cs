using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data;
using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Interfaces;
using Nito.AsyncEx;
using SQLite;
using Xunit;

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

sealed class FakeSteamPathProvider : ISteamPathProvider
{
    readonly string? _path;
    public FakeSteamPathProvider(string? path) => _path = path;
    public string? GetSteamInstallPath() => _path;
}

sealed class FakeGameManager : IGameManager
{
    readonly List<GameBase> _added = new();

    public List<TGame> GetGames<TGame>() where TGame : GameBase => new();
    public TGame? GetGame<TGame>(string platformId) where TGame : GameBase => null;
    public GameBase AddGame(GameBase game, bool scrollIntoView = false) { _added.Add(game); return game; }
    public GameLibrarySettings? GetGameLibrarySettings(GameLibrary gameLibrary)
        => new GameLibrarySettings { IsEnabled = true };
    public List<GameLibrary> GetGameLibraries(bool onlyEnabled) => new();
    public void RemoveGame(GameBase game) { }
    public void AddUnknownGameAssets(GameLibrary gameLibrary, string gameTitle, List<GameAsset> gameAssets) { }
}

sealed class InMemoryDatabase : IDatabase
{
    public AsyncLock Mutex { get; } = new();
    public SQLiteAsyncConnection Connection { get; }

    InMemoryDatabase(SQLiteAsyncConnection conn) => Connection = conn;

    public static async Task<InMemoryDatabase> CreateAsync()
    {
        var conn = new SQLiteAsyncConnection(":memory:");
        await conn.CreateTableAsync<SteamGame>();
        await conn.CreateTableAsync<GameAsset>();
        await conn.CreateTableAsync<GameHistory>();
        return new InMemoryDatabase(conn);
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class SteamLibraryTests
{
    [Fact]
    public async Task ListGamesAsync_ReturnsScannedGame_FromFixture()
    {
        var root = SteamFixture.Make();
        try
        {
            GameBase.DatabaseService = await InMemoryDatabase.CreateAsync();
            var lib = new SteamLibrary(new FakeSteamPathProvider(root), new FakeGameManager());
            var games = await lib.ListGamesAsync(forceNeedsProcessing: false);
            Assert.Contains(games, g => g.PlatformId == "400" && g.Title == "Portal");
        }
        finally
        {
            Directory.Delete(root, true);
            GameBase.DatabaseService = null;
        }
    }

    [Fact]
    public void IsInstalled_FalseWhenPathProviderReturnsNull()
    {
        var lib = new SteamLibrary(new FakeSteamPathProvider(null), new FakeGameManager());
        Assert.False(lib.IsInstalled());
    }
}
