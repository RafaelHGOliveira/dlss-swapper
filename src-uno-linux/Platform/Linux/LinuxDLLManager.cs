using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using DLSS_Swapper.Core;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Data;
using DLSS_Swapper.Extensions;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxDLLManager : IDLLManager
{
    readonly HttpClient _httpClient;
    readonly DispatcherQueue? _dispatcher;

    public ObservableCollection<DLLRecord> DLSS { get; } = new();
    public ObservableCollection<DLLRecord> DLSS_G { get; } = new();
    public ObservableCollection<DLLRecord> DLSS_D { get; } = new();
    public ObservableCollection<DLLRecord> FSR_31_DX12 { get; } = new();
    public ObservableCollection<DLLRecord> FSR_31_VK { get; } = new();
    public ObservableCollection<DLLRecord> XeSS { get; } = new();
    public ObservableCollection<DLLRecord> XeLL { get; } = new();
    public ObservableCollection<DLLRecord> XeSS_FG { get; } = new();
    public ObservableCollection<DLLRecord> XeSS_DX11 { get; } = new();

    public Manifest? ImportedManifest { get; private set; }

    public LinuxDLLManager(HttpClient httpClient)
    {
        _httpClient = httpClient;

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _dispatcher = dispatcher;
        DLLRecord.RunOnUIThreadDelegate = action => { dispatcher?.TryEnqueue(() => action()); };
        DLLRecord.ExtractFromZipDelegate = HandleExtractFromZip;
        DLLRecord.GetDownloadErrorMessageDelegate = t => $"Could not download {t}.";
        DLLRecord.CreateTranslationPropertiesDelegate = () => new DLSS.Swapper.UnoLinux.Shims.DLLRecordStrings();

        FileDownloader.RunOnUIThreadDelegate = action => { dispatcher?.TryEnqueue(() => action()); };
        FileDownloader.GetHttpClientDelegate = () => _httpClient;
    }

    public static void HandleExtractFromZip(ZipArchive zipArchive, DLLRecord record)
    {
        if (record.LocalRecord is null)
            throw new Exception("LocalRecord was null when attempting to extract dll from zip.");
        var dllName = DLLPaths.DllNameForGameAssetType(record.AssetType);
        var entry = zipArchive.Entries.Single(e => e.Name.Equals(dllName, StringComparison.OrdinalIgnoreCase));
        Storage.CreateDirectoryForFileIfNotExists(record.LocalRecord.ExpectedPath);
        entry.ExtractToFile(record.LocalRecord.ExpectedPath, overwrite: true);
    }

    public async Task LoadManifestAsync()
    {
        try
        {
            var manifestPath = Storage.GetManifestPath();
            Manifest? manifest = null;

            // 1. Load from cache if it exists
            if (File.Exists(manifestPath))
            {
                using var stream = File.OpenRead(manifestPath);
                manifest = await JsonSerializer.DeserializeAsync(stream, CoreSourceGenerationContext.Default.Manifest);
            }

            // 2. Try to download remote; update cache if changed
            try
            {
                var oldHash = string.Empty;
                if (File.Exists(manifestPath))
                {
                    using var fs = File.OpenRead(manifestPath);
                    oldHash = fs.GetMD5Hash();
                }

                using var memStream = new MemoryStream();
                var downloader = new FileDownloader("https://beeradmoore.github.io/dlss-swapper/manifest.json", 0);
                await downloader.DownloadFileToStreamAsync(memStream);

                memStream.Position = 0;
                var newHash = memStream.GetMD5Hash();

                if (oldHash != newHash)
                {
                    // Save new manifest to disk
                    Storage.CreateDirectoryForFileIfNotExists(manifestPath);
                    memStream.Position = 0;
                    using (var f = File.Create(manifestPath))
                        await memStream.CopyToAsync(f);

                    // Reload from updated cache
                    memStream.Position = 0;
                    manifest = await JsonSerializer.DeserializeAsync(memStream, CoreSourceGenerationContext.Default.Manifest);
                }
            }
            catch (Exception downloadErr)
            {
                Logger.Error(downloadErr, "Failed to update manifest from remote");
                // Continue with cached manifest if available
            }

            // 3. Populate collections from regular manifest (if available)
            if (manifest is not null)
            {
                PopulateCollection(DLSS, manifest.DLSS, GameAssetType.DLSS);
                PopulateCollection(DLSS_G, manifest.DLSS_G, GameAssetType.DLSS_G);
                PopulateCollection(DLSS_D, manifest.DLSS_D, GameAssetType.DLSS_D);
                PopulateCollection(FSR_31_DX12, manifest.FSR_31_DX12, GameAssetType.FSR_31_DX12);
                PopulateCollection(FSR_31_VK, manifest.FSR_31_VK, GameAssetType.FSR_31_VK);
                PopulateCollection(XeSS, manifest.XeSS, GameAssetType.XeSS);
                PopulateCollection(XeLL, manifest.XeLL, GameAssetType.XeLL);
                PopulateCollection(XeSS_FG, manifest.XeSS_FG, GameAssetType.XeSS_FG);
                PopulateCollection(XeSS_DX11, manifest.XeSS_DX11, GameAssetType.XeSS_DX11);
            }

            // 4. Always merge imported manifest (even if regular manifest was absent)
            await LoadImportedManifestAsync();
        }
        catch (Exception err)
        {
            Logger.Error(err, "Failed to load manifest");
        }
    }

    void PopulateCollection(ObservableCollection<DLLRecord> collection, List<DLLRecord> records, GameAssetType assetType)
    {
        records.Sort(); // descending by version, matching Windows MergeManifestsIntoMasterList
        collection.Clear();
        foreach (var record in records)
        {
            record.AssetType = assetType;
            LoadLocalRecord(record);
            collection.Add(record);
        }
    }

    void LoadLocalRecord(DLLRecord record)
    {
        record.LocalRecord = LocalRecord.FromExpectedPath(DLLPaths.GetExpectedDllFileName(record, isImported: false));
    }

    public async Task LoadImportedManifestAsync()
    {
        try
        {
            var manifestPath = Storage.GetImportedManifestPath();
            Manifest? importedManifest = null;

            if (File.Exists(manifestPath))
            {
                try
                {
                    using var stream = File.OpenRead(manifestPath);
                    importedManifest = await JsonSerializer.DeserializeAsync(stream, CoreSourceGenerationContext.Default.Manifest);
                }
                catch (Exception err)
                {
                    Logger.Error(err, "Failed to deserialize imported manifest; starting fresh");
                }
            }

            ImportedManifest = importedManifest ?? new Manifest();

            MergeImportedIntoCollection(DLSS, ImportedManifest.DLSS, GameAssetType.DLSS);
            MergeImportedIntoCollection(DLSS_G, ImportedManifest.DLSS_G, GameAssetType.DLSS_G);
            MergeImportedIntoCollection(DLSS_D, ImportedManifest.DLSS_D, GameAssetType.DLSS_D);
            MergeImportedIntoCollection(FSR_31_DX12, ImportedManifest.FSR_31_DX12, GameAssetType.FSR_31_DX12);
            MergeImportedIntoCollection(FSR_31_VK, ImportedManifest.FSR_31_VK, GameAssetType.FSR_31_VK);
            MergeImportedIntoCollection(XeSS, ImportedManifest.XeSS, GameAssetType.XeSS);
            MergeImportedIntoCollection(XeLL, ImportedManifest.XeLL, GameAssetType.XeLL);
            MergeImportedIntoCollection(XeSS_FG, ImportedManifest.XeSS_FG, GameAssetType.XeSS_FG);
            MergeImportedIntoCollection(XeSS_DX11, ImportedManifest.XeSS_DX11, GameAssetType.XeSS_DX11);
        }
        catch (Exception err)
        {
            Logger.Error(err, "Failed to load imported manifest");
        }
    }

    void MergeImportedIntoCollection(ObservableCollection<DLLRecord> collection, List<DLLRecord> importedRecords, GameAssetType assetType)
    {
        foreach (var record in importedRecords)
        {
            record.AssetType = assetType;
            record.LocalRecord = LocalRecord.FromExpectedPath(
                DLLPaths.GetExpectedDllFileName(record, isImported: true),
                isImported: true);

            // If the same DLL (by MD5) is also in the regular manifest, replace it with the
            // imported copy so the imported marking survives and both lists hold the same
            // object reference (needed for delete-by-reference). Key the match on MD5Hash,
            // NOT the BinarySearch sort position — DLLRecord.CompareTo sorts by version, so
            // distinct DLLs can share a sort index.
            var existing = collection.FirstOrDefault(r =>
                string.Equals(r.MD5Hash, record.MD5Hash, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                collection[collection.IndexOf(existing)] = record;
                continue;
            }

            var tempList = new List<DLLRecord>(collection);
            var insertIndex = tempList.BinarySearch(record);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;

            collection.Insert(insertIndex, record);
        }
    }

    public async Task<bool> SaveImportedManifestJsonAsync()
    {
        if (ImportedManifest is null) return false;
        var path = Storage.GetImportedManifestPath();
        Storage.CreateDirectoryForFileIfNotExists(path);
        using var fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, ImportedManifest, CoreSourceGenerationContext.Default.Manifest);
        return true;
    }

    public async Task RemoveImportedRecordAsync(DLLRecord record)
    {
        if (ImportedManifest is null) return;

        var (importedList, collection) = GetListsForAssetType(record.AssetType);
        importedList?.Remove(record);
        collection?.Remove(record);

        await SaveImportedManifestJsonAsync();
    }

    (List<DLLRecord>? importedList, ObservableCollection<DLLRecord>? collection) GetListsForAssetType(GameAssetType assetType)
    {
        if (ImportedManifest is null) return (null, null);
        return assetType switch
        {
            GameAssetType.DLSS => (ImportedManifest.DLSS, DLSS),
            GameAssetType.DLSS_G => (ImportedManifest.DLSS_G, DLSS_G),
            GameAssetType.DLSS_D => (ImportedManifest.DLSS_D, DLSS_D),
            GameAssetType.FSR_31_DX12 => (ImportedManifest.FSR_31_DX12, FSR_31_DX12),
            GameAssetType.FSR_31_VK => (ImportedManifest.FSR_31_VK, FSR_31_VK),
            GameAssetType.XeSS => (ImportedManifest.XeSS, XeSS),
            GameAssetType.XeLL => (ImportedManifest.XeLL, XeLL),
            GameAssetType.XeSS_FG => (ImportedManifest.XeSS_FG, XeSS_FG),
            GameAssetType.XeSS_DX11 => (ImportedManifest.XeSS_DX11, XeSS_DX11),
            _ => (null, null),
        };
    }

    static GameAssetType? AssetTypeForDllName(string fileName) => fileName switch
    {
        "nvngx_dlss.dll" => GameAssetType.DLSS,
        "nvngx_dlssg.dll" => GameAssetType.DLSS_G,
        "nvngx_dlssd.dll" => GameAssetType.DLSS_D,
        "amd_fidelityfx_dx12.dll" => GameAssetType.FSR_31_DX12,
        "amd_fidelityfx_vk.dll" => GameAssetType.FSR_31_VK,
        "libxess.dll" => GameAssetType.XeSS,
        "libxell.dll" => GameAssetType.XeLL,
        "libxess_dx11.dll" => GameAssetType.XeSS_DX11,
        "libxess_fg.dll" => GameAssetType.XeSS_FG,
        _ => null,
    };

    void RunOnUIThread(Action action)
    {
        if (_dispatcher is null)
            action();
        else
            _dispatcher.TryEnqueue(() => action());
    }

    /// <summary>
    /// Ported from Windows DLLManager.ImportDll (src/Data/DLLManager.cs:1123).
    /// WinTrust signature validation is dropped on Linux (no equivalent): IsSignatureValid
    /// is always false and untrusted DLLs are never rejected (AllowUntrusted is effectively
    /// always true on Linux).
    /// </summary>
    public DLLImportResult ImportDll(string filePath, string? zippedDllFullName = null, string? overrideFileName = null)
    {
        if (ImportedManifest is null)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, "Importing is disabled because the imported manifest could not be loaded.");
        }

        var fileName = overrideFileName ?? Path.GetFileName(filePath);

        var gameAssetType = AssetTypeForDllName(fileName);
        if (gameAssetType is null)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, "Unknown DLL type.");
        }

        var (importedRecordList, recordList) = GetListsForAssetType(gameAssetType.Value);
        if (importedRecordList is null || recordList is null)
        {
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, "Unknown DLL type.");
        }

        // No WinTrust on Linux — signature is never validated, never rejected (A3).
        var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
        var isTrusted = false;

        var dllHash = versionInfo.GetMD5Hash();

        var importingAsDownloadedDll = false;

        // We only need to check recordList and not importedRecordList as imported DLLs are in both lists.
        var existingDll = recordList.FirstOrDefault(x => string.Equals(x.MD5Hash, dllHash, StringComparison.InvariantCultureIgnoreCase));
        if (existingDll is not null)
        {
            // If the DLL is already imported/downloaded we can skip it.
            if (existingDll.LocalRecord?.IsDownloaded == true)
            {
                return DLLImportResult.FromSucces(zippedDllFullName ?? filePath, $"{fileName} is already imported.", false);
            }
            importingAsDownloadedDll = true;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var dllRecord = existingDll ?? new DLLRecord()
            {
                Version = versionInfo.GetFormattedFileVersion(),
                VersionNumber = versionInfo.GetFileVersionNumber(),
                MD5Hash = dllHash,
                FileSize = fileInfo.Length,
                ZipFileSize = 0,
                ZipMD5Hash = string.Empty,
                IsSignatureValid = isTrusted,
                AssetType = gameAssetType.Value,
            };

            var expectedPath = DLLPaths.GetExpectedDllFileName(dllRecord, !importingAsDownloadedDll);
            if (string.IsNullOrWhiteSpace(expectedPath))
            {
                return DLLImportResult.FromFail(zippedDllFullName ?? filePath, "Could not import DLL.");
            }
            Storage.CreateDirectoryForFileIfNotExists(expectedPath);

            // Copy the new record to where it should live.
            File.Copy(filePath, expectedPath, true);
            var newLocalRecord = LocalRecord.FromExpectedPath(expectedPath, !importingAsDownloadedDll);

            RunOnUIThread(() =>
            {
                dllRecord.LocalRecord = null;
                dllRecord.LocalRecord = newLocalRecord;
            });

            if (importingAsDownloadedDll == true)
            {
                // NOOP - DLL is already in the list, we just updated the LocalRecord for it.
            }
            else
            {
                // Insert into the main DLL list (bound to the UI).
                var tempList = new List<DLLRecord>(recordList);
                var insertIndex = tempList.BinarySearch(dllRecord);
                if (insertIndex < 0)
                {
                    insertIndex = ~insertIndex;
                }
                RunOnUIThread(() =>
                {
                    recordList.Insert(insertIndex, dllRecord);
                });

                // Insert into the list used for the imported manifest.
                var importedInsertIndex = importedRecordList.BinarySearch(dllRecord);
                if (importedInsertIndex < 0)
                {
                    importedInsertIndex = ~importedInsertIndex;
                }
                importedRecordList.Insert(importedInsertIndex, dllRecord);
            }

            return DLLImportResult.FromSucces(zippedDllFullName ?? filePath, fileName, importingAsDownloadedDll);
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return DLLImportResult.FromFail(zippedDllFullName ?? filePath, err.Message);
        }
    }

    public ObservableCollection<DLLRecord> GetRecordsForAssetType(GameAssetType assetType) => assetType switch
    {
        GameAssetType.DLSS => DLSS,
        GameAssetType.DLSS_G => DLSS_G,
        GameAssetType.DLSS_D => DLSS_D,
        GameAssetType.FSR_31_DX12 => FSR_31_DX12,
        GameAssetType.FSR_31_VK => FSR_31_VK,
        GameAssetType.XeSS => XeSS,
        GameAssetType.XeLL => XeLL,
        GameAssetType.XeSS_FG => XeSS_FG,
        GameAssetType.XeSS_DX11 => XeSS_DX11,
        _ => new ObservableCollection<DLLRecord>(),
    };

    public GameAssetType GetAssetBackupType(GameAssetType assetType) => assetType switch
    {
        GameAssetType.DLSS => GameAssetType.DLSS_BACKUP,
        GameAssetType.DLSS_G => GameAssetType.DLSS_G_BACKUP,
        GameAssetType.DLSS_D => GameAssetType.DLSS_D_BACKUP,
        GameAssetType.FSR_31_DX12 => GameAssetType.FSR_31_DX12_BACKUP,
        GameAssetType.FSR_31_VK => GameAssetType.FSR_31_VK_BACKUP,
        GameAssetType.XeSS => GameAssetType.XeSS_BACKUP,
        GameAssetType.XeLL => GameAssetType.XeLL_BACKUP,
        GameAssetType.XeSS_FG => GameAssetType.XeSS_FG_BACKUP,
        GameAssetType.XeSS_DX11 => GameAssetType.XeSS_DX11_BACKUP,
        _ => GameAssetType.Unknown,
    };

    public bool IsInKnownGameAsset(GameAsset gameAsset, GameLibrary gameLibrary, string titleBase64) => true;
}
