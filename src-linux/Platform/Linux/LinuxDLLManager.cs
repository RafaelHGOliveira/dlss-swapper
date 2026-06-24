using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
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

    public ObservableCollection<DLLRecord> DLSS { get; } = new();
    public ObservableCollection<DLLRecord> DLSS_G { get; } = new();
    public ObservableCollection<DLLRecord> DLSS_D { get; } = new();
    public ObservableCollection<DLLRecord> FSR_31_DX12 { get; } = new();
    public ObservableCollection<DLLRecord> FSR_31_VK { get; } = new();
    public ObservableCollection<DLLRecord> XeSS { get; } = new();
    public ObservableCollection<DLLRecord> XeLL { get; } = new();
    public ObservableCollection<DLLRecord> XeSS_FG { get; } = new();
    public ObservableCollection<DLLRecord> XeSS_DX11 { get; } = new();

    public LinuxDLLManager(HttpClient httpClient)
    {
        _httpClient = httpClient;

        DLLRecord.RunOnUIThreadDelegate = action => { Dispatcher.UIThread.InvokeAsync(action); };
        DLLRecord.ExtractFromZipDelegate = HandleExtractFromZip;
        DLLRecord.GetDownloadErrorMessageDelegate = t => $"Could not download {t}.";
        DLLRecord.CreateTranslationPropertiesDelegate = () => null;

        FileDownloader.RunOnUIThreadDelegate = action => { Dispatcher.UIThread.InvokeAsync(action); };
        FileDownloader.GetHttpClientDelegate = () => _httpClient;
    }

    static void HandleExtractFromZip(ZipArchive zipArchive, DLLRecord record)
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

            if (manifest is null) return;

            // 3. Populate collections
            PopulateCollection(DLSS, manifest.DLSS);
            PopulateCollection(DLSS_G, manifest.DLSS_G);
            PopulateCollection(DLSS_D, manifest.DLSS_D);
            PopulateCollection(FSR_31_DX12, manifest.FSR_31_DX12);
            PopulateCollection(FSR_31_VK, manifest.FSR_31_VK);
            PopulateCollection(XeSS, manifest.XeSS);
            PopulateCollection(XeLL, manifest.XeLL);
            PopulateCollection(XeSS_FG, manifest.XeSS_FG);
            PopulateCollection(XeSS_DX11, manifest.XeSS_DX11);
        }
        catch (Exception err)
        {
            Logger.Error(err, "Failed to load manifest");
        }
    }

    void PopulateCollection(ObservableCollection<DLLRecord> collection, List<DLLRecord> records)
    {
        collection.Clear();
        foreach (var record in records)
        {
            LoadLocalRecord(record);
            collection.Add(record);
        }
    }

    void LoadLocalRecord(DLLRecord record)
    {
        record.LocalRecord = LocalRecord.FromExpectedPath(DLLPaths.GetExpectedDllFileName(record, isImported: false));
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
