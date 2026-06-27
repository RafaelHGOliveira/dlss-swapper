using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper;
using DLSS_Swapper.Data;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Linux.Platform.Linux;
using DLSS.Swapper.UnoLinux.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Pages;

public partial class UnoLibraryPageModel : ObservableObject
{
    static readonly GameAssetType[] AllTypes =
    [
        GameAssetType.DLSS,
        GameAssetType.DLSS_G,
        GameAssetType.DLSS_D,
        GameAssetType.FSR_31_DX12,
        GameAssetType.FSR_31_VK,
        GameAssetType.XeSS,
        GameAssetType.XeLL,
        GameAssetType.XeSS_FG,
        GameAssetType.XeSS_DX11,
    ];

    readonly IFilePickerService _picker = new LinuxFilePickerService();

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

    public async Task DeleteRecord(DLLRecord record, XamlRoot xamlRoot)
    {
        var app = (App)Application.Current;
        var wasImported = record.LocalRecord?.IsImported == true;
        var didDelete = record.LocalRecord?.Delete() == true;
        if (!didDelete)
        {
            await ShowMessageAsync(xamlRoot, "Delete failed", "The DLL file could not be deleted. The file may be locked or you may not have permission to remove it.");
            return;
        }
        if (wasImported)
        {
            // RemoveImportedRecordAsync removes from both lists and saves the manifest.
            await app.DllManager.RemoveImportedRecordAsync(record);
        }
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

    // ── Import ──────────────────────────────────────────────────────────────────

    public async Task ImportAsync(XamlRoot xamlRoot)
    {
        var app = (App)Application.Current;

        if (app.DllManager.ImportedManifest is null)
        {
            await ShowMessageAsync(xamlRoot, "Import unavailable", "The imported manifest could not be loaded, so importing is disabled.");
            return;
        }

        if (app.Settings.HasShownWarning == false)
        {
            await ShowMessageAsync(xamlRoot, "Warning",
                "Only import DLLs from sources you trust. Malicious DLLs can compromise your system. DLSS Swapper cannot verify the safety of imported files.");
            app.Settings.HasShownWarning = true;
            app.Settings.SaveSettings();
        }

        var files = await _picker.PickFilesAsync(new[] { ".dll", ".zip" }, true);
        if (files.Count == 0)
        {
            return;
        }

        var results = await Task.Run(() => RunImport(app.DllManager, files));

        if (results.Any(r => r.Success))
        {
            await app.DllManager.SaveImportedManifestJsonAsync();
            RebuildCurrentRecords();
        }

        var summary = string.Join("\n", results.Select(r =>
            $"{(r.Success ? "OK" : "FAILED")}: {Path.GetFileName(r.FilePath)} — {r.Message}"));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "No files were imported.";
        }
        await ShowMessageAsync(xamlRoot, "Import finished", summary);
    }

    static List<DLLImportResult> RunImport(LinuxDLLManager dllManager, IReadOnlyList<string> files)
    {
        var results = new List<DLLImportResult>();

        var tempExtractPath = Path.Combine(Storage.GetTemp(), "import", Guid.NewGuid().ToString("D"));
        Storage.CreateDirectoryIfNotExists(tempExtractPath);

        foreach (var importFile in files)
        {
            if (string.IsNullOrEmpty(importFile) || File.Exists(importFile) == false)
            {
                results.Add(DLLImportResult.FromFail(importFile ?? string.Empty, "File not found."));
                continue;
            }

            try
            {
                if (importFile.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    // First check if the whole zip matches a known DLLRecord by its ZipMD5Hash.
                    var newZipHash = string.Empty;
                    using (var fileStream = File.OpenRead(importFile))
                    {
                        newZipHash = fileStream.GetMD5Hash();
                    }

                    var matched = false;
                    if (string.IsNullOrWhiteSpace(newZipHash) == false)
                    {
                        foreach (var type in AllTypes)
                        {
                            var record = dllManager.GetRecordsForAssetType(type)
                                .FirstOrDefault(x => string.Equals(x.ZipMD5Hash, newZipHash, StringComparison.InvariantCultureIgnoreCase));
                            if (record is not null && HandleLocalDLLRecordZip(importFile, record, results))
                            {
                                matched = true;
                                break;
                            }
                        }
                    }

                    if (matched)
                    {
                        continue;
                    }

                    // Not a known zip — extract each DLL and import individually.
                    using (var archive = ZipFile.OpenRead(importFile))
                    {
                        var zippedDlls = archive.Entries.Where(x => x.Name.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase)).ToArray();
                        if (zippedDlls.Length == 0)
                        {
                            throw new Exception("Zip did not contain any DLLs.");
                        }

                        foreach (var zippedDll in zippedDlls)
                        {
                            var tempFile = Path.Combine(tempExtractPath, Guid.NewGuid().ToString("D"), zippedDll.Name);
                            Storage.CreateDirectoryForFileIfNotExists(tempFile);
                            zippedDll.ExtractToFile(tempFile, true);

                            try
                            {
                                results.Add(dllManager.ImportDll(tempFile, zippedDll.FullName));
                            }
                            catch (Exception err)
                            {
                                Logger.Error(err);
                                results.Add(DLLImportResult.FromFail(zippedDll.FullName, err.Message));
                            }

                            File.Delete(tempFile);
                        }
                    }
                }
                else if (importFile.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        results.Add(dllManager.ImportDll(importFile));
                    }
                    catch (Exception err)
                    {
                        Logger.Error(err);
                        results.Add(DLLImportResult.FromFail(importFile, err.Message));
                    }
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
                results.Add(DLLImportResult.FromFail(importFile, err.Message));
            }
        }

        if (Directory.Exists(tempExtractPath))
        {
            try
            {
                Directory.Delete(tempExtractPath, true);
            }
            catch (Exception err)
            {
                Logger.Error(err);
            }
        }

        return results;
    }

    static bool HandleLocalDLLRecordZip(string importedPath, DLLRecord dllRecord, List<DLLImportResult> results)
    {
        if (dllRecord.LocalRecord is null)
        {
            results.Add(DLLImportResult.FromFail(importedPath, "DLL record has no local record."));
            return false;
        }

        if (File.Exists(dllRecord.LocalRecord.ExpectedPath))
        {
            dllRecord.LocalRecord.IsDownloaded = true;
            results.Add(DLLImportResult.FromSucces(dllRecord.LocalRecord.ExpectedPath, "Already downloaded.", true));
            return true;
        }

        try
        {
            using (var fileStream = File.OpenRead(importedPath))
            using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read, true))
            {
                LinuxDLLManager.HandleExtractFromZip(zipArchive, dllRecord);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            results.Add(DLLImportResult.FromFail(importedPath, "Failed to extract DLL from zip."));
            return false;
        }

        dllRecord.LocalRecord.IsDownloaded = true;
        results.Add(DLLImportResult.FromSucces(importedPath, "Imported as existing record.", true));
        return true;
    }

    // ── Export ──────────────────────────────────────────────────────────────────

    public async Task ExportAsync(XamlRoot xamlRoot)
    {
        var app = (App)Application.Current;

        var toExport = new List<(string SourceFileName, string EntryName)>();
        foreach (var type in AllTypes)
        {
            foreach (var dllRecord in app.DllManager.GetRecordsForAssetType(type))
            {
                if (dllRecord.LocalRecord is null || dllRecord.LocalRecord.IsDownloaded == false)
                {
                    continue;
                }

                var expectedPathDirectory = Path.GetDirectoryName(dllRecord.LocalRecord.ExpectedPath);
                if (string.IsNullOrWhiteSpace(expectedPathDirectory))
                {
                    continue;
                }

                var internalZipDir = AssetTypeDisplayName(dllRecord.AssetType);
                if (dllRecord.LocalRecord.IsImported)
                {
                    internalZipDir = Path.Combine("Imported", internalZipDir);
                }
                internalZipDir = Path.Combine(internalZipDir, new DirectoryInfo(expectedPathDirectory).Name);

                toExport.Add((dllRecord.LocalRecord.ExpectedPath,
                    Path.Combine(internalZipDir, Path.GetFileName(dllRecord.LocalRecord.ExpectedPath))));
            }
        }

        if (toExport.Count == 0)
        {
            await ShowMessageAsync(xamlRoot, "Export", "There are no downloaded DLLs to export.");
            return;
        }

        var savePath = await _picker.PickSaveFileAsync("dlss_swapper_export", ".zip");
        if (string.IsNullOrWhiteSpace(savePath))
        {
            return;
        }
        if (savePath.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase) == false)
        {
            savePath += ".zip";
        }

        var error = await Task.Run(() => ExportWorker(savePath, toExport));

        if (error is null)
        {
            await ShowMessageAsync(xamlRoot, "Export finished", $"Exported {toExport.Count} DLL(s).");
        }
        else
        {
            await ShowMessageAsync(xamlRoot, "Export failed", "Could not export the DLLs.");
        }
    }

    static Exception? ExportWorker(string zipPath, List<(string SourceFileName, string EntryName)> filesToAdd)
    {
        try
        {
            using (var fileStream = File.Create(zipPath))
            using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                foreach (var fileToAdd in filesToAdd)
                {
                    zipArchive.CreateEntryFromFile(fileToAdd.SourceFileName, fileToAdd.EntryName);
                }
            }
            return null;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
            catch (Exception err2)
            {
                Logger.Error(err2);
            }
            return err;
        }
    }

    static string AssetTypeDisplayName(GameAssetType assetType) => assetType switch
    {
        GameAssetType.DLSS => "DLSS",
        GameAssetType.DLSS_G => "DLSS G",
        GameAssetType.DLSS_D => "DLSS D",
        GameAssetType.FSR_31_DX12 => "FSR 3.1 DX12",
        GameAssetType.FSR_31_VK => "FSR 3.1 VK",
        GameAssetType.XeSS => "XeSS",
        GameAssetType.XeLL => "XeLL",
        GameAssetType.XeSS_FG => "XeSS FG",
        GameAssetType.XeSS_DX11 => "XeSS DX11",
        _ => "Other",
    };

    static async Task ShowMessageAsync(XamlRoot xamlRoot, string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 400,
            },
            CloseButtonText = "OK",
            XamlRoot = xamlRoot,
        };
        await dialog.ShowAsync();
    }
}
