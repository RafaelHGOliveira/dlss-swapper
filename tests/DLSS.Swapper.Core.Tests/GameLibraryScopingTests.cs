using System.Collections.Generic;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;
using Xunit;

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

sealed class StubGameLibrary : IGameLibrary
{
    public StubGameLibrary(GameLibrary gameLibrary) => GameLibrary = gameLibrary;
    public GameLibrary GameLibrary { get; }
    public GameLibrarySettings? GameLibrarySettings => null;
    public string Name => GameLibrary.ToString();
    public Type GameType => typeof(GameBase);
    public System.Threading.Tasks.Task<IReadOnlyList<GameBase>> ListGamesAsync(bool forceNeedsProcessing) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<GameBase>>(new List<GameBase>());
    public System.Threading.Tasks.Task LoadGamesFromCacheAsync() => System.Threading.Tasks.Task.CompletedTask;
    public bool IsInstalled() => false;
}

sealed class FakeFactory : IGameLibraryFactory
{
    readonly Dictionary<GameLibrary, IGameLibrary> _supported;

    public FakeFactory(params GameLibrary[] supported)
    {
        _supported = new Dictionary<GameLibrary, IGameLibrary>();
        foreach (var lib in supported)
            _supported[lib] = new StubGameLibrary(lib);
    }

    public IGameLibrary? Get(GameLibrary gameLibrary)
        => _supported.TryGetValue(gameLibrary, out var lib) ? lib : null;

    public IReadOnlyList<IGameLibrary> CreateEnabledLibraries()
        => new List<IGameLibrary>(_supported.Values);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class GameLibraryScopingTests
{
    [Fact]
    public void SupportedLibraries_ReturnsOnlyFactoryProvided()
    {
        var factory = new FakeFactory(GameLibrary.Steam);
        var supported = GameLibraryScope.SupportedLibraries(factory);
        Assert.Equal(new[] { GameLibrary.Steam }, supported);
    }

    [Fact]
    public void SupportedLibraries_ReturnsEmpty_WhenFactorySupportsNone()
    {
        var factory = new FakeFactory();
        var supported = GameLibraryScope.SupportedLibraries(factory);
        Assert.Empty(supported);
    }

    [Fact]
    public void SupportedLibraries_ReturnsAll_WhenFactorySupportsAllEnum()
    {
        var all = System.Enum.GetValues<GameLibrary>();
        var factory = new FakeFactory(all);
        var supported = GameLibraryScope.SupportedLibraries(factory);
        Assert.Equal(all.Length, supported.Count);
    }
}
