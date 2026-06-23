using System.IO;
using DLSS_Swapper.Data;

namespace DLSS_Swapper.Helpers;

public static class DLLPaths
{
    public static string DllNameForGameAssetType(GameAssetType gameAssetType)
    {
        // NOTE: DLL type
        return gameAssetType switch
        {
            GameAssetType.DLSS => "nvngx_dlss.dll",
            GameAssetType.DLSS_G => "nvngx_dlssg.dll",
            GameAssetType.DLSS_D => "nvngx_dlssd.dll",
            GameAssetType.FSR_31_DX12 => "amd_fidelityfx_dx12.dll",
            GameAssetType.FSR_31_VK => "amd_fidelityfx_vk.dll",
            GameAssetType.XeSS => "libxess.dll",
            GameAssetType.XeSS_FG => "libxess_fg.dll",
            GameAssetType.XeLL => "libxell.dll",
            GameAssetType.XeSS_DX11 => "libxess_dx11.dll",
            _ => string.Empty,
        };
    }

    public static string GetExpectedDllPath(DLLRecord dllRecord, bool isImported)
    {
        var recordType = dllRecord.GetRecordSimpleType();

        var dllsPath = Path.Combine(Storage.GetStorageFolder(), "dlls", (isImported ? $"imported" : string.Empty), recordType);
        if (string.IsNullOrWhiteSpace(dllsPath))
        {
            return string.Empty;
        }

        var individualDllPath = Path.Combine(dllsPath, $"{recordType}_v{dllRecord.Version}_{dllRecord.MD5Hash}");
        if (string.IsNullOrWhiteSpace(individualDllPath))
        {
            return string.Empty;
        }

        return individualDllPath;
    }

    public static string GetExpectedDllFileName(DLLRecord dllRecord, bool isImported)
    {
        var dllPath = GetExpectedDllPath(dllRecord, isImported);
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            return string.Empty;
        }

        var dllName = DllNameForGameAssetType(dllRecord.AssetType);
        if (string.IsNullOrWhiteSpace(dllName))
        {
            return string.Empty;
        }

        return Path.Combine(dllPath, dllName);
    }
}
