using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS.Swapper.UnoLinux.Shims;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Linux.Platform.Linux;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Controls;

public partial class DLLPickerControlModel : ObservableObject
{
    readonly GameBase _game;
    readonly GameAssetType _assetType;
    readonly LinuxDLLManager _dllManager;
    WeakReference<ContentDialog>? _hostDialog;
    bool _canClose;

    public List<DLLRecord> DLLRecords { get; }

    [ObservableProperty]
    public partial DLLRecord? SelectedDLLRecord { get; set; }

    [ObservableProperty]
    public partial bool CanSwap { get; set; }

    [ObservableProperty]
    public partial bool AnyDLLsVisible { get; set; }

    [ObservableProperty]
    public partial GameAsset? CurrentGameAsset { get; set; }

    [ObservableProperty]
    public partial GameAsset? BackupGameAsset { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool CanClose => _canClose;

    public DLLPickerStrings TranslationProperties { get; } = new();

    public DLLPickerControlModel(GameBase game, GameAssetType assetType, LinuxDLLManager dllManager)
    {
        _game = game;
        _assetType = assetType;
        _dllManager = dllManager;

        DLLRecords = [.. dllManager.GetRecordsForAssetType(assetType)];
        AnyDLLsVisible = DLLRecords.Count > 0;

        RefreshAssets();
    }

    public void SetHostDialog(ContentDialog dialog)
    {
        _hostDialog = new WeakReference<ContentDialog>(dialog);
        UpdateDialogButtons();
    }

    partial void OnSelectedDLLRecordChanged(DLLRecord? value)
    {
        CanSwap = value is not null;
        UpdateDialogButtons();
    }

    partial void OnBackupGameAssetChanged(GameAsset? value) => UpdateDialogButtons();

    void UpdateDialogButtons()
    {
        if (_hostDialog?.TryGetTarget(out var dialog) == true)
        {
            dialog.IsPrimaryButtonEnabled = CanSwap;
            dialog.IsSecondaryButtonEnabled = BackupGameAsset is not null;
        }
    }

    void RefreshAssets()
    {
        CurrentGameAsset = _game.GameAssets.FirstOrDefault(a => a.AssetType == _assetType);
        var backupType = _dllManager.GetAssetBackupType(_assetType);
        BackupGameAsset = _game.GameAssets.FirstOrDefault(a => a.AssetType == backupType);
        UpdateDialogButtons();
    }

    [RelayCommand]
    async Task SwapDllAsync()
    {
        if (SelectedDLLRecord?.LocalRecord is null)
        {
            ShowError("DLL record is unavailable.");
            return;
        }

        if (SelectedDLLRecord.LocalRecord.FileDownloader is not null)
            return; // já baixando — aguardar

        if (!SelectedDLLRecord.LocalRecord.IsDownloaded)
        {
            _ = SelectedDLLRecord.DownloadAsync();
            return;
        }

        var result = await _game.UpdateDllAsync(SelectedDLLRecord);
        if (result.Success)
        {
            _canClose = true;
            RefreshAssets();
            if (_hostDialog?.TryGetTarget(out var d) == true)
                d.Hide();
        }
        else
        {
            ShowError(result.Message);
        }
    }

    [RelayCommand]
    async Task ResetDllAsync()
    {
        var result = await _game.ResetDllAsync(_assetType);
        if (result.Success)
        {
            _canClose = true;
            RefreshAssets();
            if (_hostDialog?.TryGetTarget(out var d) == true)
                d.Hide();
        }
        else
        {
            ShowError(result.Message);
        }
    }

    [RelayCommand]
    void OpenDllPath()
    {
        if (CurrentGameAsset is null) return;
        var dir = Path.GetDirectoryName(CurrentGameAsset.Path);
        if (dir is not null)
        {
            using var proc = System.Diagnostics.Process.Start("xdg-open", dir);
        }
    }

    public void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}
