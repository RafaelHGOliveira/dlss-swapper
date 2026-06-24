using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Linux.Platform.Linux;

namespace DLSS_Swapper.Linux.ViewModels;

public partial class SwapDialogViewModel : ObservableObject
{
    readonly GameBase _game;
    readonly GameAssetType _assetType;
    readonly LinuxDLLManager _dllManager;

    public string AssetTypeName { get; }
    public GameAsset? CurrentAsset { get; }
    public ObservableCollection<DLLRecord> AvailableVersions { get; }

    [ObservableProperty] DLLRecord? _selectedRecord;
    [ObservableProperty] bool _isDownloading;
    [ObservableProperty] string _operationMessage = string.Empty;

    public bool HasBackup => _game.GameAssets.Any(a =>
        a.AssetType == _dllManager.GetAssetBackupType(_assetType));

    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand SwapCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }

    public SwapDialogViewModel(GameBase game, GameAssetType assetType, LinuxDLLManager dllManager)
    {
        _game = game;
        _assetType = assetType;
        _dllManager = dllManager;

        AssetTypeName = assetType.ToString().Replace("_", " ");
        CurrentAsset = game.GameAssets.FirstOrDefault(a => a.AssetType == assetType);
        AvailableVersions = dllManager.GetRecordsForAssetType(assetType);

        DownloadCommand = new AsyncRelayCommand(DownloadAsync, CanDownload);
        SwapCommand = new AsyncRelayCommand(SwapAsync, CanSwap);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => HasBackup);
    }

    bool CanDownload() => SelectedRecord is not null && SelectedRecord.LocalRecord?.IsDownloaded != true;
    bool CanSwap() => SelectedRecord is not null && SelectedRecord.LocalRecord?.IsDownloaded == true;

    partial void OnSelectedRecordChanged(DLLRecord? value)
    {
        DownloadCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    async Task DownloadAsync()
    {
        if (SelectedRecord is null) return;
        IsDownloading = true;
        OperationMessage = "Downloading...";
        try
        {
            var result = await SelectedRecord.DownloadAsync();
            OperationMessage = result.Success ? "Downloaded." : result.Message;
            SwapCommand.NotifyCanExecuteChanged();
            DownloadCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            OperationMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    async Task SwapAsync()
    {
        if (SelectedRecord is null) return;
        OperationMessage = "Swapping...";
        try
        {
            var result = await _game.UpdateDllAsync(SelectedRecord);
            OperationMessage = result.Success ? "Swap complete." : result.Message;
            OnPropertyChanged(nameof(HasBackup));
            RestoreCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            OperationMessage = $"Swap failed: {ex.Message}";
        }
    }

    async Task RestoreAsync()
    {
        OperationMessage = "Restoring...";
        try
        {
            var result = await _game.ResetDllAsync(_assetType);
            OperationMessage = result.Success ? "Restore complete." : result.Message;
            OnPropertyChanged(nameof(HasBackup));
            RestoreCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            OperationMessage = $"Restore failed: {ex.Message}";
        }
    }
}
