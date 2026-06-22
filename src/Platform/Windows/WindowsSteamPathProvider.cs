using DLSS_Swapper.Core.Platform;
using Microsoft.Win32;
using System;
using System.IO;

namespace DLSS_Swapper.Platform.Windows;

public sealed class WindowsSteamPathProvider : ISteamPathProvider
{
    public string? GetSteamInstallPath()
    {
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var installPath = key?.GetValue("InstallPath") as string;
            return (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath)) ? installPath : null;
        }
        catch (Exception err)
        {
            Logger.Error(err);
            return null;
        }
    }
}
