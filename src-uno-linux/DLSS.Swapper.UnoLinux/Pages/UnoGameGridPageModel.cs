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

    public ObservableCollection<GameBase> Games { get; } = new();
    public bool HasNoGames => Games.Count == 0;
    public AsyncRelayCommand LoadGamesCommand { get; }

    public UnoGameGridPageModel(IGameLibraryFactory factory)
    {
        _factory = factory;
        Games.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoGames));
        LoadGamesCommand = new AsyncRelayCommand(LoadGamesAsync);
    }

    async Task LoadGamesAsync()
    {
        IsLoading = true;
        Games.Clear();
        try
        {
            foreach (var library in _factory.CreateEnabledLibraries())
            {
                if (!library.IsInstalled()) continue;
                await library.LoadGamesFromCacheAsync();
                var games = await library.ListGamesAsync(forceNeedsProcessing: false);
                foreach (var game in games)
                    Games.Add(game);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
