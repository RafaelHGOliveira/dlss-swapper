using System.Linq;
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

        var swappable = game.GameAssets
            .Select(a => a.AssetType)
            .Where(t => t != GameAssetType.Unknown &&
                        !t.ToString().EndsWith("_BACKUP", System.StringComparison.Ordinal))
            .Distinct()
            .ToList();

        if (swappable.Count == 0)
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

        var dialog = new ContentDialog
        {
            Title = game.Title,
            PrimaryButtonText = "Swap",
            SecondaryButtonText = "Restore",
            CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            XamlRoot = XamlRoot,
        };

        DLLPickerControl? control = null;

        if (swappable.Count == 1)
        {
            control = new DLLPickerControl(game, swappable[0], dllManager);
            dialog.Content = control;
            control.ViewModel.SetHostDialog(dialog);
            dialog.PrimaryButtonCommand = control.ViewModel.SwapDllCommand;
            dialog.SecondaryButtonCommand = control.ViewModel.ResetDllCommand;
        }
        else
        {
            var combo = new ComboBox
            {
                ItemsSource = swappable,
                PlaceholderText = "Select DLL type…",
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                MinWidth = 400,
            };
            dialog.Content = combo;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is GameAssetType selectedType)
                {
                    control = new DLLPickerControl(game, selectedType, dllManager);
                    dialog.Content = control;
                    control.ViewModel.SetHostDialog(dialog);
                    dialog.PrimaryButtonCommand = control.ViewModel.SwapDllCommand;
                    dialog.SecondaryButtonCommand = control.ViewModel.ResetDllCommand;
                }
            };
        }

        dialog.Closing += (_, args) =>
        {
            if ((args.Result == ContentDialogResult.Primary ||
                 args.Result == ContentDialogResult.Secondary) &&
                control?.ViewModel.CanClose != true)
            {
                args.Cancel = true;
            }
        };

        await dialog.ShowAsync();
    }
}
