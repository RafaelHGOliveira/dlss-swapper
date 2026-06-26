using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DLSS.Swapper.UnoLinux.Services;

public sealed class LinuxFilePickerService : IFilePickerService
{
    private enum PickerTool { KDialog, Zenity, None }

    private static readonly PickerTool _tool = DetectTool();

    private static PickerTool DetectTool()
    {
        if (File.Exists("/usr/bin/kdialog")) return PickerTool.KDialog;
        if (File.Exists("/usr/bin/zenity")) return PickerTool.Zenity;
        if (FindOnPath("kdialog") != null) return PickerTool.KDialog;
        if (FindOnPath("zenity") != null) return PickerTool.Zenity;
        return PickerTool.None;
    }

    private static string? FindOnPath(string exe)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(':'))
        {
            var full = Path.Combine(dir, exe);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string StartDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<string>> PickFilesAsync(string[] extensions, bool allowMultiple)
    {
        if (_tool == PickerTool.None) return Array.Empty<string>();

        string? stdout = _tool == PickerTool.KDialog
            ? await RunKDialogOpenFile(extensions, allowMultiple)
            : await RunZenityOpenFile(extensions, allowMultiple);

        if (string.IsNullOrWhiteSpace(stdout)) return Array.Empty<string>();

        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }

    public async Task<string?> PickSaveFileAsync(string defaultName, string extension)
    {
        if (_tool == PickerTool.None) return null;

        string? stdout = _tool == PickerTool.KDialog
            ? await RunKDialogSaveFile(defaultName, extension)
            : await RunZenitySaveFile(defaultName, extension);

        return string.IsNullOrWhiteSpace(stdout) ? null : stdout.Trim();
    }

    public async Task<string?> PickFolderAsync()
    {
        if (_tool == PickerTool.None) return null;

        string? stdout = _tool == PickerTool.KDialog
            ? await RunKDialogFolder()
            : await RunZenityFolder();

        return string.IsNullOrWhiteSpace(stdout) ? null : stdout.Trim();
    }

    // ── kdialog ───────────────────────────────────────────────────────────────
    // Filter format: "Label (*.ext1 *.ext2)"
    // Multiple:      --multiple --separate-output  → one path per stdout line
    // Cancel:        non-zero exit code

    private async Task<string?> RunKDialogOpenFile(string[] extensions, bool allowMultiple)
    {
        var si = new ProcessStartInfo("kdialog") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--getopenfilename");
        si.ArgumentList.Add(StartDir);
        si.ArgumentList.Add(BuildKDialogFilter(extensions));
        if (allowMultiple)
        {
            si.ArgumentList.Add("--multiple");
            si.ArgumentList.Add("--separate-output");
        }
        return await RunProcess(si);
    }

    private async Task<string?> RunKDialogSaveFile(string defaultName, string extension)
    {
        var si = new ProcessStartInfo("kdialog") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--getsavefilename");
        si.ArgumentList.Add(Path.Combine(StartDir, defaultName));
        si.ArgumentList.Add(BuildKDialogFilter(new[] { extension }));
        return await RunProcess(si);
    }

    private async Task<string?> RunKDialogFolder()
    {
        var si = new ProcessStartInfo("kdialog") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--getexistingdirectory");
        si.ArgumentList.Add(StartDir);
        return await RunProcess(si);
    }

    private static string BuildKDialogFilter(string[] extensions)
    {
        if (extensions.Length == 0) return "*";
        var patterns = string.Join(" ", extensions.Select(e => $"*.{e.TrimStart('.')}"));
        return $"Files ({patterns})";
    }

    // ── zenity ────────────────────────────────────────────────────────────────
    // Filter format: --file-filter=Label | *.ext1 *.ext2
    // Multiple:      --multiple --separator=<newline> → one path per stdout line
    // Cancel:        non-zero exit code

    private async Task<string?> RunZenityOpenFile(string[] extensions, bool allowMultiple)
    {
        var si = new ProcessStartInfo("zenity") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--file-selection");
        if (allowMultiple)
        {
            si.ArgumentList.Add("--multiple");
            si.ArgumentList.Add("--separator=\n"); // pass actual LF so output is newline-delimited
        }
        if (extensions.Length > 0)
        {
            var patterns = string.Join(" ", extensions.Select(e => $"*.{e.TrimStart('.')}"));
            si.ArgumentList.Add($"--file-filter=Files | {patterns}");
        }
        return await RunProcess(si);
    }

    private async Task<string?> RunZenitySaveFile(string defaultName, string extension)
    {
        var si = new ProcessStartInfo("zenity") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--file-selection");
        si.ArgumentList.Add("--save");
        si.ArgumentList.Add($"--filename={defaultName}");
        if (!string.IsNullOrEmpty(extension))
        {
            var pattern = $"*.{extension.TrimStart('.')}";
            si.ArgumentList.Add($"--file-filter=Files | {pattern}");
        }
        return await RunProcess(si);
    }

    private async Task<string?> RunZenityFolder()
    {
        var si = new ProcessStartInfo("zenity") { RedirectStandardOutput = true, UseShellExecute = false };
        si.ArgumentList.Add("--file-selection");
        si.ArgumentList.Add("--directory");
        return await RunProcess(si);
    }

    // ── Shared runner ─────────────────────────────────────────────────────────

    private static async Task<string?> RunProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process == null) return null;

        // Read stdout concurrently with waiting to avoid pipe-buffer deadlock.
        var readTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await readTask;

        return process.ExitCode == 0 ? stdout : null;
    }
}
