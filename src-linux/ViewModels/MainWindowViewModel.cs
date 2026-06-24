using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Linux.Platform.Linux;
using DLSS_Swapper.Linux.Views;

namespace DLSS_Swapper.Linux.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    readonly IGameLibraryFactory _factory;
    readonly LinuxDLLManager _dllManager;

    [ObservableProperty]
    bool _isLoading;

    [ObservableProperty]
    string _statusMessage = string.Empty;

    public ObservableCollection<GameBase> Games { get; } = new();
    public bool HasNoGames => Games.Count == 0;
    public AsyncRelayCommand LoadGamesCommand { get; }
    public RelayCommand<GameBase> OpenSwapDialogCommand { get; }

    public MainWindowViewModel(IGameLibraryFactory factory, LinuxDLLManager dllManager)
    {
        _factory = factory;
        _dllManager = dllManager;
        Games.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoGames));
        LoadGamesCommand = new AsyncRelayCommand(LoadGamesAsync);
        OpenSwapDialogCommand = new RelayCommand<GameBase>(OpenSwapDialog);
    }

    async Task LoadGamesAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning games...";
        Games.Clear();
        try
        {
            foreach (var library in _factory.CreateEnabledLibraries())
            {
                if (!library.IsInstalled()) continue;
                var games = await library.ListGamesAsync(forceNeedsProcessing: false);
                foreach (var game in games)
                    Games.Add(game);
            }
            StatusMessage = $"{Games.Count} game(s) found.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    void OpenSwapDialog(GameBase? game)
    {
        if (game is null) return;
        var dialog = new SwapDialog(game, _dllManager);
        dialog.Show();
    }
}
