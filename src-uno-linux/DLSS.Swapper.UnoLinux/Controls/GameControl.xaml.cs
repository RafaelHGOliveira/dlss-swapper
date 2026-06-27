using System;
using System.IO;
using System.Linq;
using DLSS.Swapper.UnoLinux.Converters;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Linux.Platform.Linux;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DLSS.Swapper.UnoLinux.Controls;

public sealed partial class GameControl : ContentDialog
{
    readonly GameBase _game;
    readonly LinuxDLLManager _dllManager;

    static readonly StringToImageSourceConverter _imageConverter = new();

    public GameControl(GameBase game, LinuxDLLManager dllManager)
    {
        InitializeComponent();
        _game = game;
        _dllManager = dllManager;

        Title = game.Title;
        InstallPathBox.Text = game.InstallPath;

        LoadCoverImage(game.CoverImage);
        BuildDllSections();
    }

    void LoadCoverImage(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        if (_imageConverter.Convert(path, typeof(ImageSource), null, string.Empty) is ImageSource source)
            CoverImageControl.Source = source;
    }

    void BuildDllSections()
    {
        var swappable = _game.GameAssets
            .Select(a => a.AssetType)
            .Where(t => t != GameAssetType.Unknown &&
                        !t.ToString().EndsWith("_BACKUP", StringComparison.Ordinal))
            .Distinct();

        foreach (var assetType in swappable)
            DllSectionsPanel.Children.Add(BuildSection(assetType));
    }

    UIElement BuildSection(GameAssetType assetType)
    {
        var current = _game.GameAssets.FirstOrDefault(a => a.AssetType == assetType);

        var versionBox = new TextBox
        {
            IsReadOnly = true,
            Text = current?.DisplayName ?? string.Empty,
            VerticalAlignment = VerticalAlignment.Stretch,
            PlaceholderText = "Not found",
        };

        var changeButton = new Button
        {
            Content = "Change",
            Tag = assetType,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        changeButton.Click += OnChangeClicked;

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(versionBox, 0);
        Grid.SetColumn(changeButton, 1);
        row.Children.Add(versionBox);
        row.Children.Add(changeButton);

        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(new TextBlock
        {
            Text = GetAssetTypeName(assetType),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        });
        section.Children.Add(row);
        return section;
    }

    async void OnChangeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var assetType = (GameAssetType)btn.Tag;

        var control = new DLLPickerControl(_game, assetType, _dllManager);
        var dialog = new ContentDialog
        {
            Title = _game.Title,
            PrimaryButtonText = "Swap",
            SecondaryButtonText = "Restore",
            CloseButtonText = "Close",
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            XamlRoot = XamlRoot,
            Content = control,
        };
        control.ViewModel.SetHostDialog(dialog);
        dialog.PrimaryButtonCommand = control.ViewModel.SwapDllCommand;
        dialog.SecondaryButtonCommand = control.ViewModel.ResetDllCommand;

        await dialog.ShowAsync();

        // After picker closes, refresh the version shown in the row
        if (btn.Parent is Grid row)
        {
            var updated = _game.GameAssets.FirstOrDefault(a => a.AssetType == assetType);
            foreach (var child in row.Children)
            {
                if (child is TextBox tb)
                {
                    tb.Text = updated?.DisplayName ?? string.Empty;
                    break;
                }
            }
        }
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = _game.InstallPath;
        if (Directory.Exists(dir))
        {
            using var proc = System.Diagnostics.Process.Start("xdg-open", dir);
        }
    }

    static string GetAssetTypeName(GameAssetType type) => type switch
    {
        GameAssetType.DLSS => "DLSS",
        GameAssetType.DLSS_G => "DLSS Frame Generation",
        GameAssetType.DLSS_D => "DLSS Ray Reconstruction",
        GameAssetType.FSR_31_DX12 => "FSR 3.1 (DX12)",
        GameAssetType.FSR_31_VK => "FSR 3.1 (Vulkan)",
        GameAssetType.XeSS => "XeSS",
        GameAssetType.XeSS_FG => "XeSS Frame Generation",
        GameAssetType.XeLL => "XeLL",
        GameAssetType.XeSS_DX11 => "XeSS (DX11)",
        _ => type.ToString(),
    };
}
