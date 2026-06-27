using DLSS.Swapper.UnoLinux.Controls;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Pages;

public sealed partial class GameGridPage : Page
{
    public UnoGameGridPageModel ViewModel { get; }

    public GameGridPage()
    {
        var app = (App)Microsoft.UI.Xaml.Application.Current;
        ViewModel = new UnoGameGridPageModel(app.GameFactory);
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadGamesCommand.ExecuteAsync(null);
    }

    async void OnGameItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not GameBase game) return;

        var app = (App)Microsoft.UI.Xaml.Application.Current;
        var dllManager = app.DllManager;

        var hasSwappable = game.GameAssets
            .Any(a => a.AssetType != GameAssetType.Unknown &&
                      !a.AssetType.ToString().EndsWith("_BACKUP", System.StringComparison.Ordinal));

        if (!hasSwappable)
        {
            var infoDialog = new ContentDialog
            {
                Title = game.Title,
                Content = "No swappable DLLs detected for this game.",
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            await infoDialog.ShowAsync();
            return;
        }

        var control = new GameControl(game, dllManager) { XamlRoot = XamlRoot };
        await control.ShowAsync();
    }
}
