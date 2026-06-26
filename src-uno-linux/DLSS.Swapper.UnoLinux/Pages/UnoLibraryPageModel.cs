using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;

namespace DLSS.Swapper.UnoLinux.Pages;

public partial class UnoLibraryPageModel : ObservableObject
{
    [ObservableProperty]
    public partial GameAssetType SelectedType { get; set; } = GameAssetType.DLSS;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public ObservableCollection<DLLRecord> CurrentRecords { get; } = new();

    public UnoLibraryPageModel()
    {
        RebuildCurrentRecords();
    }

    partial void OnSelectedTypeChanged(GameAssetType value) => RebuildCurrentRecords();

    public void RebuildCurrentRecords()
    {
        var app = (App)Application.Current;
        var settings = app.Settings;
        var source = app.DllManager.GetRecordsForAssetType(SelectedType);
        CurrentRecords.Clear();
        foreach (var r in source)
        {
            if (settings.OnlyShowDownloadedDlls && r.LocalRecord?.IsDownloaded != true) continue;
            if (!settings.AllowDebugDlls && r.IsDevFile) continue;
            CurrentRecords.Add(r);
        }
    }

    public void DownloadRecord(DLLRecord record) => _ = record.DownloadAsync();

    public void DeleteRecord(DLLRecord record)
    {
        record.LocalRecord?.Delete();
        RebuildCurrentRecords();
    }

    public void OpenFolder(DLLRecord record)
    {
        var dir = Path.GetDirectoryName(record.LocalRecord?.ExpectedPath);
        if (dir is not null)
        {
            using var proc = Process.Start("xdg-open", dir);
        }
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            var app = (App)Application.Current;
            await app.DllManager.LoadManifestAsync();
            RebuildCurrentRecords();
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
