# AGENTS.md

This file provides guidance to coding agents (Codex, Claude Code, ...) when working with code in this repository.

> **Single source:** this file is the canonical copy. `CLAUDE.md` only contains `@AGENTS.md`, which Claude Code resolves as an import, so edit this file and leave `CLAUDE.md` alone.

## What this is

DLSS Swapper is a WinUI 3 / Windows App SDK desktop app (C#, .NET 10, x64 only) that scans installed games from several launchers, finds upscaler DLLs (DLSS, FSR 3.1, XeSS, XeLL, ...), and swaps them for other versions downloaded from a manifest. A single project lives in `src/DLSS Swapper.csproj`; there are **no automated tests** in the repo.

This checkout is a **fork** (`origin` = RafaelHGOliveira, `upstream` = beeradmoore). The batch DLL swap feature (multi-select on the games page, `BatchDllPickerControl`, `BatchSwapSummaryControl`, `Data/BatchSwapResult.cs`, `GamesPage_Batch_*` strings) is fork-only and does not exist upstream. Keep upstream merges in mind when touching `GameGridPage*`.

PR #913 (batch swap) is opened from the fork's `main`, so follow-ups land by pushing to `origin/main`. Never let internal docs (`docs/superpowers/`) reach `main` — keep specs and plans on a separate branch. When answering review feedback, leave the PR description as it is and put the whole update (changes, plus screenshots with a caption above each) in a new comment; `gh` cannot upload images, so the user attaches them through the web UI.

## Build and run

Requires Windows, .NET SDK 10 (`global.json` pins 10.0.100, rolls forward), and the Windows App SDK workload. `nuget.config` adds the CommunityToolkit Labs feed (needed for `MarkdownTextBlock`).

```powershell
# Debug build (installer-style storage in %LOCALAPPDATA%\DLSS Swapper\DEBUG)
dotnet build "src\DLSS Swapper.csproj" -c Debug

# Portable debug build (storage in src\bin\...\StoredData\DEBUG next to the exe)
dotnet build "src\DLSS Swapper.csproj" -c Debug_Portable

# Run the built app
dotnet run --project "src\DLSS Swapper.csproj" -c Debug
```

Debug data lives in `%LOCALAPPDATA%\DLSS Swapper\DEBUG\`: `json\settings.json` (set `"Language"` to `en-US` for upstream-facing screenshots) and `dlss_swapper.db` (open read-only from python to inspect `game_asset`/`game_history`). The installed release uses the same path without `DEBUG`. An app launched from an elevated agent session inherits admin and shows the "running with administrative privileges" banner. To update the local install without NSIS, publish (`dotnet publish "src\DLSS Swapper.csproj" --runtime win-x64 --self-contained -c Release -p:PublishDir=<dir>\`) and have the user run `robocopy <dir> "C:\Program Files\DLSS Swapper" /E` elevated, leaving `<Version>` untouched.

Configurations: `Debug`, `Release`, `Debug_Portable`, `Release_Portable`. The `*_Portable` ones define the `PORTABLE` constant, which changes where `Storage.cs` puts the SQLite DB, settings JSON, downloaded DLLs and image cache.

Release packaging is scripted in `package/` (see `package/README.md`): `build_Portable.cmd` / `build_Installer.cmd` run `dotnet publish` (self-contained, ReadyToRun), `package_Portable.cmd` zips, `package_Installer.cmd` needs NSIS 3.x on PATH. `build_all.cmd` runs everything and first refreshes `src/Assets/static_manifest.json` via `extras/update_manifest.cmd`. The version lives in two places that must match: `<Version>` in the csproj and `app_version` in `package/config.cmd`. CI (`.github/workflows/build-for-distribute.yml`) runs on `v1.*`/`v2.*` tags and signs via SignPath.

Helper tools in `extras/`:
- `SortTranslations/sort.ps1` — re-sorts every `.resw` alphabetically by key. Run it after adding strings.
- `InNeedOfTranslationDetector/` — console app that finds hard-coded UI strings in XAML/C#.

## Architecture

### Singletons and startup
`App.xaml.cs` initialises logging (Serilog via `Logger.cs`), language, and a shared `App.CurrentApp.HttpClient` (proxy-aware; call `RegenerateHttpClient()` after proxy changes). Global state is held in singletons: `Settings.Instance` (JSON, every setter auto-saves), `GameManager.Instance`, `DLLManager.Instance`, `LanguageManager.Instance`, `Database` (sqlite-net). `MainWindow` hosts a NavigationView with the pages in `Pages/`; extra windows (`DiagnosticsWindow`, `TranslationToolboxWindow`, `NetworkTesterWindow`) go through `WindowManager`.

### Games and libraries
- `Interfaces/IGameLibrary.cs` defines the `GameLibrary` flags enum and the interface each launcher implements. Each library is a singleton under `Data/<Launcher>/` (`SteamLibrary`, `GOGLibrary`, `EpicGamesStoreLibrary`, `UbisoftConnectLibrary`, `XboxLibrary`, `BattleNetLibrary`, `EAAppLibrary`, `ManuallyAddedLibrary`) with a matching `<Launcher>Game` subclass of `Data/Game.cs`.
- `Game` is abstract, `ObservableObject`, and also a sqlite-net row (`[Column]`/`[Ignore]` attributes). Each subclass has its own table created in `Database.cs`. `Game.ProcessGame()` walks the install path, classifies DLLs into `GameAsset`s (`Data/GameAsset.cs`, also a table) and writes `GameHistory` events.
- Swapping: `Game.UpdateDllAsync(DLLRecord)` verifies MD5 + Authenticode (`WinTrust.cs`, bypassable with the `AllowUntrusted` setting), backs up the original as `<dll>.dlsss` the first time, and copies the new DLL in. `Game.ResetDllAsync` restores from that backup. Both return `(Success, Message, PromptToRelaunchAsAdmin)` tuples rather than throwing.
- `GameManager` owns the observable collections and the `AdvancedCollectionView`s that pages bind to; go through it to add/remove games so UI stays in sync.

### DLL records and the manifest
- `GameAssetType` (`Data/GameAssetType.cs`) enumerates every DLL kind. Each swappable type has a paired `*_BACKUP` member; `DLLManager.GetAssetBackupType` maps between them. Numeric values are persisted in SQLite, so never renumber.
- `DLLManager` holds one `ObservableCollection<DLLRecord>` per user-facing type, loaded from `Manifest` (`Data/Manifest.cs`) plus an "imported" manifest for user-supplied DLLs. `DLLRecord.LocalRecord` tracks download state on disk. `KnownDLLs`/`HashedKnownDLL` identify DLLs by hash.
- `src/Assets/static_manifest.json` is an embedded fallback copied from the separate `dlss-swapper-manifest-builder` repo (`extras/update_manifest.cmd`); it is also mirrored to `docs/manifest.json`. Do not hand-edit it.
- All JSON goes through `System.Text.Json` source generation: any new type that is (de)serialised must be registered in `SourceGenerationContext.cs`.

### Localization pattern (follow it for every UI string)
1. Add the key to `src/Translations/en-US/Resources.resw`. Keys are prefixed by page/area (`GamesPage_`, `SettingsPage_`, `General_`, ...); templates with placeholders end in `Template` and are read with `ResourceHelper.GetFormattedResourceTemplate`.
2. Expose it on the view model's companion class `<Model>TranslationProperties.cs`, which extends `LocalizedViewModelBase` and has one `[TranslationProperty]` property per string returning `ResourceHelper.GetString("Key")`. `LocalizedViewModelBase` re-raises `PropertyChanged` for those properties on language change, so XAML binds to `TranslationProperties.Foo` instead of literal text.
3. Never put literal text in XAML `Text=`/`Content=`; the detector in `extras/` flags it.

`.resw` files are UTF-8 **with BOM** and end without a trailing newline (what `sort.ps1` writes); some edit tools strip the BOM, so check line 1 of `git diff` before committing.

Other languages are only added to `LanguageManager.GetKnownLanguages()`/`GetLanguageName()` by hand. `LANG_HUNT` is a debug-only pseudo-language that renders every string as `...` to spot untranslated text. The Translation Toolbox window lets translators live-edit strings via `ResourceHelper.UpdateFromLiveTranslations`.

### MVVM conventions
- CommunityToolkit.Mvvm with C# `partial` properties (`[ObservableProperty]`, `[RelayCommand]`, `LangVersion=preview`). Each page/control is a trio: `Foo.xaml` + `Foo.xaml.cs` (thin) + `FooModel.cs` (+ `FooModelTranslationProperties.cs`).
- Dialogs are `EasyContentDialog`/`FakeContentDialog` from `UserControls/`; async commands use `AsyncAwaitBestPractices`.
- **UI thread:** `Game`/`GameAsset` properties are bound to the UI. Library scans and the batch swap worker run off-thread, so any write to an observable property must be marshalled with `App.CurrentApp.RunOnUIThreadAsync(...)` or the page's `DispatcherQueue.TryEnqueue` (see upstream fix #933, which also caps concurrent `ProcessGame` calls with a semaphore). Resolve localized strings on the UI thread before handing work to a background task (see the comments in `BatchSwapResult`).

### Native interop
`Microsoft.Windows.CsWin32` generates P/Invoke from `src/NativeMethods.txt`; add Win32 function names there rather than writing `DllImport` by hand. `NVAPIHelper`/`DLSSSettingsManager` drive DLSS preset overrides via NvAPI.

## Adding things

- **New DLL asset type:** add enum members (type + `_BACKUP`, new unused numbers) in `GameAssetType`; extend `DLLManager` (records collection, `GetAssetBackupType`, `GetAssetTypeName`, `DllNameForGameAssetType`, `IsInKnownGameAsset`), `Manifest`, and the `Current*` properties in `Game`; add UI in `DLLPickerControl`/`GameControl`; add resw strings.
- **New game library:** add a `GameLibrary` flag, a `Data/<Launcher>/` folder with `<Launcher>Library` + `<Launcher>Game`, register in `IGameLibrary.GetGameLibrary` and `Database` table creation, and register any JSON models in `SourceGenerationContext`.
- **PRs upstream** use `.github/pull_request_template.md` (bug fix / feature / docs / translation).
