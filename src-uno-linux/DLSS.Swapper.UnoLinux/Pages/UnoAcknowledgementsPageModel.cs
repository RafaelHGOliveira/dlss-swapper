using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DLSS.Swapper.UnoLinux.Pages;

public partial class UnoAcknowledgementsPageModel : ObservableObject
{
    // The segment that marks the embedded acknowledgement resources. The full
    // manifest prefix is derived dynamically because this assembly's RootNamespace
    // (DLSS.Swapper.UnoLinux) differs from the Windows project (DLSS_Swapper).
    const string AcknowledgementsSegment = ".Acknowledgements.";

    static readonly Regex FileRegex = new(@"^(?<name>.*)\.(?<file>license\.txt|notes\.md)$");

    public ObservableCollection<AcknowledgementItem> Acknowledgements { get; } = new();

    [ObservableProperty]
    public partial AcknowledgementItem? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string DetailText { get; set; } = string.Empty;

    public UnoAcknowledgementsPageModel()
    {
        var assembly = GetType().Assembly;
        var items = new Dictionary<string, AcknowledgementItem>(StringComparer.Ordinal);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var segmentIndex = resourceName.IndexOf(AcknowledgementsSegment, StringComparison.OrdinalIgnoreCase);
            if (segmentIndex < 0)
            {
                continue;
            }

            var match = FileRegex.Match(resourceName);
            if (!match.Success)
            {
                continue;
            }

            // Library name = everything after the ".Acknowledgements." segment, with
            // the trailing ".license.txt" / ".notes.md" stripped by the regex's "name" group.
            var nameStart = segmentIndex + AcknowledgementsSegment.Length;
            var fullName = match.Groups["name"].Value;
            if (fullName.Length <= nameStart)
            {
                continue;
            }

            var displayName = fullName.Substring(nameStart);

            // MSBuild's manifest-name generation applies the Everett-identifier transform,
            // mangling '-' to '_' (e.g. "FidelityFX-SDK" -> "FidelityFX_SDK"). Remap the two
            // affected folders back, mirroring the Windows loader. A blanket '_'->'-' replace
            // would corrupt unaffected names, so the remap is explicit and per-case.
            displayName = displayName switch
            {
                "FidelityFX_SDK" => "FidelityFX-SDK",
                "SQLite_net" => "SQLite-net",
                _ => displayName,
            };

            if (!items.TryGetValue(displayName, out var item))
            {
                item = new AcknowledgementItem(displayName);
                items[displayName] = item;
            }

            var file = match.Groups["file"].Value;
            if (file == "notes.md")
            {
                item.NotesResourceName = resourceName;
            }
            else if (file == "license.txt")
            {
                item.LicenseResourceName = resourceName;
            }
        }

        foreach (var item in items.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            Acknowledgements.Add(item);
        }

        SelectedItem = Acknowledgements.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(AcknowledgementItem? value)
    {
        DetailText = value is null ? string.Empty : BuildDetailText(value);
    }

    static string BuildDetailText(AcknowledgementItem item)
    {
        var assembly = item.GetType().Assembly;
        var builder = new StringBuilder();

        var notes = ReadResource(assembly, item.NotesResourceName);
        if (!string.IsNullOrEmpty(notes))
        {
            builder.Append(notes);
        }

        var license = ReadResource(assembly, item.LicenseResourceName);
        if (!string.IsNullOrEmpty(license))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
            builder.Append(license);
        }

        return builder.ToString();
    }

    static string? ReadResource(Assembly assembly, string? resourceName)
    {
        if (resourceName is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

public sealed class AcknowledgementItem
{
    public AcknowledgementItem(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string? NotesResourceName { get; set; }

    public string? LicenseResourceName { get; set; }

    public override string ToString() => Name;
}
