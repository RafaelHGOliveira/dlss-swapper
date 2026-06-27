using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DLSS_Swapper;
using DLSS.Swapper.UnoLinux.Services;
using Microsoft.UI.Xaml;

namespace DLSS.Swapper.UnoLinux.Pages;

public partial class UnoSettingsPageModel : ObservableObject
{
    // Known cultures (key = culture, value = display name) mirrored from
    // LanguageManager.GetKnownLanguages() on the Windows side.
    static readonly KeyValuePair<string, string>[] KnownLanguages =
    [
        new("ar-SA", "العربية (السعودية)"),
        new("ar-SY", "العربية (سوريا)"),
        new("ca-ES", "Català"),
        new("cs-CZ", "Čeština"),
        new("de-DE", "Deutsch"),
        new("en-AU", "English (Australia)"),
        new("en-GB", "English (United Kingdom)"),
        new("en-US", "English (United States)"),
        new("es-ES", "Español"),
        new("fa-IR", "فارسی"),
        new("fi-FI", "Suomi"),
        new("fr-FR", "Français"),
        new("it-IT", "Italiano"),
        new("ja-JP", "日本語"),
        new("ko-KR", "한국어"),
        new("pl-PL", "Polski"),
        new("pt-BR", "Português (Brasil)"),
        new("ru-RU", "Русский"),
        new("th-TH", "ไทย"),
        new("tr-TR", "Türkçe"),
        new("uk-UA", "Українська"),
        new("vi-VN", "Tiếng Việt"),
        new("zh-CN", "中文 (简体)"),
        new("zh-TW", "中文 (繁體)"),
    ];

    readonly IFilePickerService _picker = new LinuxFilePickerService();

    // Guards constructor-time writes so initialising the controls does not
    // re-persist settings or re-apply the theme on every page load.
    bool _initializing;

    public ObservableCollection<string> IgnoredPaths { get; } = new();

    public IReadOnlyList<KeyValuePair<string, string>> Languages => KnownLanguages;

    public IReadOnlyList<LoggingLevel> LoggingLevels { get; } =
        (LoggingLevel[])Enum.GetValues(typeof(LoggingLevel));

    public string LogDirectory => Path.Combine(Storage.GetTemp(), "logs");

    public string AppVersion =>
        typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";

    [ObservableProperty]
    public partial bool LightThemeSelected { get; set; }

    [ObservableProperty]
    public partial bool DarkThemeSelected { get; set; }

    [ObservableProperty]
    public partial bool DefaultThemeSelected { get; set; }

    [ObservableProperty]
    public partial bool AllowDebugDlls { get; set; }

    [ObservableProperty]
    public partial bool OnlyShowDownloadedDlls { get; set; }

    [ObservableProperty]
    public partial LoggingLevel SelectedLoggingLevel { get; set; }

    [ObservableProperty]
    public partial KeyValuePair<string, string> SelectedLanguage { get; set; }

    public UnoSettingsPageModel()
    {
        _initializing = true;

        var app = (App)Application.Current;
        var settings = app.Settings;

        LightThemeSelected = settings.AppTheme == "Light";
        DarkThemeSelected = settings.AppTheme == "Dark";
        DefaultThemeSelected = settings.AppTheme != "Light" && settings.AppTheme != "Dark";

        AllowDebugDlls = settings.AllowDebugDlls;
        OnlyShowDownloadedDlls = settings.OnlyShowDownloadedDlls;
        SelectedLoggingLevel = settings.LoggingLevel;
        SelectedLanguage = Languages.FirstOrDefault(kvp => kvp.Key == settings.Language);

        foreach (var path in settings.IgnoredPaths)
        {
            IgnoredPaths.Add(path);
        }

        _initializing = false;
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    partial void OnLightThemeSelectedChanged(bool value)
    {
        if (value) ApplyTheme("Light");
    }

    partial void OnDarkThemeSelectedChanged(bool value)
    {
        if (value) ApplyTheme("Dark");
    }

    partial void OnDefaultThemeSelectedChanged(bool value)
    {
        if (value) ApplyTheme("Default");
    }

    void ApplyTheme(string theme)
    {
        if (_initializing) return;

        var app = (App)Application.Current;
        app.Settings.AppTheme = theme;
        app.Settings.SaveSettings();

        var elementTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (app.MainWindowRef.Content is FrameworkElement root)
        {
            root.RequestedTheme = elementTheme;
        }
    }

    // ── DLL behaviour ───────────────────────────────────────────────────────────

    partial void OnAllowDebugDllsChanged(bool value)
    {
        if (_initializing) return;
        var app = (App)Application.Current;
        app.Settings.AllowDebugDlls = value;
        app.Settings.SaveSettings();
    }

    partial void OnOnlyShowDownloadedDllsChanged(bool value)
    {
        if (_initializing) return;
        var app = (App)Application.Current;
        app.Settings.OnlyShowDownloadedDlls = value;
        app.Settings.SaveSettings();
    }

    // ── Logging ─────────────────────────────────────────────────────────────────

    partial void OnSelectedLoggingLevelChanged(LoggingLevel value)
    {
        if (_initializing) return;
        var app = (App)Application.Current;
        app.Settings.LoggingLevel = value;
        app.Settings.SaveSettings();
    }

    [RelayCommand]
    void OpenLogFolder()
    {
        using var proc = Process.Start("xdg-open", LogDirectory);
    }

    // ── Language ─────────────────────────────────────────────────────────────────

    partial void OnSelectedLanguageChanged(KeyValuePair<string, string> value)
    {
        if (_initializing) return;
        var app = (App)Application.Current;
        app.Settings.Language = value.Key ?? string.Empty;
        app.Settings.SaveSettings();
    }

    // ── Ignored paths ────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task AddIgnoredPathAsync()
    {
        var folder = await _picker.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (IgnoredPaths.Contains(folder)) return;

        IgnoredPaths.Add(folder);
        PersistIgnoredPaths();
    }

    public void RemoveIgnoredPath(string path)
    {
        if (IgnoredPaths.Remove(path))
        {
            PersistIgnoredPaths();
        }
    }

    void PersistIgnoredPaths()
    {
        var app = (App)Application.Current;
        app.Settings.IgnoredPaths = IgnoredPaths.ToArray();
        app.Settings.SaveSettings();
    }

    // ── About ────────────────────────────────────────────────────────────────────

    [RelayCommand]
    void OpenReleases()
    {
        using var proc = Process.Start("xdg-open", "https://github.com/beeradmoore/dlss-swapper/releases");
    }
}
