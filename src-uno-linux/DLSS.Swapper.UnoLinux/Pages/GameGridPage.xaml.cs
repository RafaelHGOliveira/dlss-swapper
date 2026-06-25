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
}
