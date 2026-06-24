using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Linux.Platform.Linux;
using DLSS_Swapper.Linux.ViewModels;

namespace DLSS_Swapper.Linux.Views;

public partial class SwapDialog : Window
{
    public string? GameTitle { get; private set; }
    public List<SwapDialogViewModel>? Tabs { get; private set; }
    public bool HasNoTabs => Tabs is null || Tabs.Count == 0;

    public SwapDialog()
    {
        InitializeComponent();
    }

    public SwapDialog(GameBase game, LinuxDLLManager dllManager) : this()
    {
        GameTitle = game.Title;
        Tabs = game.GameAssets
            .Select(a => a.AssetType)
            .Where(t => !t.ToString().EndsWith("_BACKUP", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Select(t => new SwapDialogViewModel(game, t, dllManager))
            .ToList();
        DataContext = this;
    }
}
