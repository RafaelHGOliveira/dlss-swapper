using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Platform;

namespace DLSS.Swapper.UnoLinux.Pages;

public partial class UnoGameGridPageModel : ObservableObject
{
    readonly IGameLibraryFactory _factory;

    [ObservableProperty]
    bool _isLoading;

    [ObservableProperty]
    bool _hideNonSwappable;

    readonly List<GameBase> _allGames = new();
    public ObservableCollection<GameBase> FilteredGames { get; } = new();

    public AsyncRelayCommand LoadGamesCommand { get; }

    public UnoGameGridPageModel(IGameLibraryFactory factory)
    {
        _factory = factory;
        LoadGamesCommand = new AsyncRelayCommand(LoadGamesAsync);
    }

    partial void OnHideNonSwappableChanged(bool value) => ApplyFilter();

    void ApplyFilter()
    {
        FilteredGames.Clear();
        foreach (var game in _allGames)
        {
            if (!HideNonSwappable || game.HasSwappableItems)
                FilteredGames.Add(game);
        }
    }

    async Task LoadGamesAsync()
    {
        IsLoading = true;
        _allGames.Clear();
        FilteredGames.Clear();
        try
        {
            foreach (var library in _factory.CreateEnabledLibraries())
            {
                if (!library.IsInstalled()) continue;
                await library.LoadGamesFromCacheAsync();
                var games = await library.ListGamesAsync(forceNeedsProcessing: false);
                foreach (var game in games)
                    _allGames.Add(game);
            }
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
