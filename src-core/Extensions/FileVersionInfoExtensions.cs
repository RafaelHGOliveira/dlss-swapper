using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DLSS_Swapper.Helpers;

namespace DLSS_Swapper.Extensions;

internal static class FileVersionInfoExtensions
{
    internal static string GetMD5Hash(this FileVersionInfo fileVersionInfo)
    {
        try
        {
            using (var fileStream = File.OpenRead(fileVersionInfo.FileName))
            {
                return fileStream.GetMD5Hash();
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"{fileVersionInfo.FileName}");
            Debugger.Break();
        }

        return string.Empty;
    }

    internal static string GetFormattedFileVersion(this FileVersionInfo fileVersionInfo)
    {
        if (fileVersionInfo.FileMajorPart == 0 && fileVersionInfo.FileMinorPart == 0
            && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var v = PeVersionReader.ReadFileVersion(fileVersionInfo.FileName);
            if (v != null)
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        return $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FileBuildPart}.{fileVersionInfo.FilePrivatePart}";
    }

    internal static ulong GetFileVersionNumber(this FileVersionInfo fileVersionInfo)
    {
        return ((ulong)fileVersionInfo.FileMajorPart << 48) +
                ((ulong)fileVersionInfo.FileMinorPart << 32) +
                ((ulong)fileVersionInfo.FileBuildPart << 16) +
                ((ulong)fileVersionInfo.FilePrivatePart);
    }
}
