# src-linux/ — Implementation Design Spec

**Date:** 2026-06-22
**Status:** Approved
**Complementa:** `2026-06-22-linux-support-design.md` (arquitetura geral)

Esta spec cobre os detalhes de implementação do passo 6 da ordem de implementação da spec principal: criar `src-linux/` com camada de plataforma Linux + UI Avalonia mínima funcional (lista de jogos, swap, download).

Escopo aprovado: **b** — plataforma + UI mínima funcional com download.

---

## Pré-trabalho em `src-core/` (passo 5 da spec principal, ainda não feito)

### Mover `SteamLibrary` para `src-core/Data/Steam/`

`SteamLibrary` está em `src/Data/Steam/` e cria `new SteamGame(appId)` — tipo concreto de `src/`. Para ser core-safe, recebe um delegate de criação de jogo:

```csharp
// src-core/Data/Steam/SteamLibrary.cs
internal sealed class SteamLibrary : IGameLibrary
{
    readonly ISteamPathProvider _steamPathProvider;
    readonly Func<string, GameBase> _gameFactory;

    public SteamLibrary(ISteamPathProvider steamPathProvider, Func<string, GameBase> gameFactory)
    {
        _steamPathProvider = steamPathProvider;
        _gameFactory = gameFactory;
    }
    // onde hoje tem: new SteamGame(appManifestACF.AppId)
    // vira:          _gameFactory(appManifestACF.AppId)
}
```

Fix obrigatório junto com o move — o regex de manifest usa separador Windows:

```csharp
// antes (quebra no Linux):
new Regex(@"^(.*)\\appmanifest_(?<app_id>\d*)\.acf$")

// depois (agnóstico de plataforma):
// substituir por: Path.GetFileName(filePath).StartsWith("appmanifest_") && filePath.EndsWith(".acf")
// ou regex com separador agnóstico: @"[\\/]appmanifest_(?<app_id>\d*)\.acf$"
```

Auditar todo uso de `\` literal e de concatenação de paths em `SteamLibrary` ao mover.

### Atualizar `WindowsGameFactory` (`src/Platform/Windows/`)

```csharp
[GameLibrary.Steam] = new SteamLibrary(steamPathProvider, id => new SteamGame(id)),
```

`SteamGame` permanece em `src/` — só a instanciação muda.

### `InternalsVisibleTo` para `src-linux/`

Adicionar em `src-core/Properties/AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("DLSS.Swapper.Linux")]
```

---

## Camada de plataforma Linux (`src-linux/Platform/Linux/`)

### `LinuxSteamPathProvider`

Testa caminhos em ordem; retorna o primeiro existente.

```csharp
public sealed class LinuxSteamPathProvider : ISteamPathProvider
{
    static readonly string[] _candidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "root"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".var", "app",
            "com.valvesoftware.Steam", ".local", "share", "Steam"),
    ];

    public string? GetSteamInstallPath()
        => _candidates.FirstOrDefault(Directory.Exists);
}
```

### `LinuxStoragePathProvider`

```csharp
public sealed class LinuxStoragePathProvider : IStoragePathProvider
{
    public string GetStorageRoot()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configBase = string.IsNullOrEmpty(xdg)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdg;
        return Path.Combine(configBase, "dlss-swapper");
    }
}
```

### `LinuxGame` e `LinuxSteamGame`

```csharp
// LinuxGame.cs — override mínimo dos hooks de plataforma
public abstract class LinuxGame : GameBase
{
    // RunOnUIThread/Async: implementação síncrona do Core já serve na v1
    protected override bool IsAdminUser() => false;      // sem conceito de admin para swap no Linux
    protected override bool VerifySignature(string dllPath) => true;  // no-op na v1
}

// LinuxSteamGame.cs
public sealed class LinuxSteamGame : LinuxGame
{
    public override GameLibrary GameLibrary => GameLibrary.Steam;
    public override bool IsReadyToPlay => Directory.Exists(InstallPath);

    public LinuxSteamGame(string appId)
    {
        PlatformId = appId;
        SetID();
    }

    protected override Task UpdateCacheImageAsync()
        => DownloadCoverAsync(
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{PlatformId}/library_600x900.jpg");
}
```

### `LinuxGameFactory`

V1 suporta apenas Steam + ManuallyAdded (spec principal, seção "Escopo da v1").

```csharp
public sealed class LinuxGameFactory : IGameLibraryFactory
{
    readonly Dictionary<GameLibrary, IGameLibrary> _libraries;

    public LinuxGameFactory()
    {
        var steamProvider = new LinuxSteamPathProvider();
        _libraries = new()
        {
            [GameLibrary.Steam]         = new SteamLibrary(steamProvider, id => new LinuxSteamGame(id)),
            [GameLibrary.ManuallyAdded] = new ManuallyAddedLibrary(),
        };
    }

    public IReadOnlyList<IGameLibrary> CreateEnabledLibraries() => _libraries.Values.ToList();

    public IGameLibrary? Get(GameLibrary gameLibrary)
        => _libraries.TryGetValue(gameLibrary, out var lib) ? lib : null;
}
```

---

## UI Avalonia (`src-linux/`)

### ViewModels

#### `MainViewModel`

Coordena libraries disponíveis e carregamento de jogos. Usa `IGameLibraryFactory` diretamente — sem DI container.

```csharp
public partial class MainViewModel : ObservableObject
{
    readonly IGameLibraryFactory _factory;

    [ObservableProperty] IGameLibrary? selectedLibrary;
    [ObservableProperty] ObservableCollection<GameBase> games = [];
    [ObservableProperty] bool isLoading;

    public IReadOnlyList<IGameLibrary> Libraries { get; }

    public MainViewModel(IGameLibraryFactory factory)
    {
        _factory = factory;
        Libraries = factory.CreateEnabledLibraries()
                           .Where(l => l.IsInstalled())
                           .ToList();
        SelectedLibrary = Libraries.FirstOrDefault();
    }

    partial void OnSelectedLibraryChanged(IGameLibrary? value) => _ = LoadGamesAsync();

    async Task LoadGamesAsync()
    {
        if (SelectedLibrary is null) return;
        IsLoading = true;
        Games.Clear();
        var list = await SelectedLibrary.ListGamesAsync();
        foreach (var g in list) Games.Add(g);
        IsLoading = false;
    }
}
```

#### `SwapViewModel`

Lógica de download + swap + restore. Recebe `GameBase` e `IDLLManager`.

```csharp
public partial class SwapViewModel : ObservableObject
{
    readonly GameBase _game;
    readonly IDLLManager _dllManager;

    [ObservableProperty] ObservableCollection<DLLRecord> availableVersions = [];
    [ObservableProperty] DLLRecord? selectedVersion;
    [ObservableProperty] bool isDownloading;
    [ObservableProperty] string statusMessage = string.Empty;

    public IAsyncRelayCommand SwapCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IAsyncRelayCommand DownloadCommand { get; }

    public SwapViewModel(GameBase game, IDLLManager dllManager)
    {
        _game = game;
        _dllManager = dllManager;
        SwapCommand   = new AsyncRelayCommand(SwapAsync);
        ResetCommand  = new AsyncRelayCommand(ResetAsync);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync);
        _ = LoadVersionsAsync();
    }

    async Task LoadVersionsAsync() { /* busca DLLRecords do DLLManager por tipo */ }
    async Task DownloadAsync()     { /* baixa versão selecionada via FileDownloader */ }
    async Task SwapAsync()         { /* chama _game.UpdateDllAsync(selectedVersion) */ }
    async Task ResetAsync()        { /* chama _game.ResetDllAsync(assetType) */ }
}
```

### Views (estrutura)

```
MainWindow.axaml
  ├── Sidebar: ListBox → Libraries (Steam, Adicionados manualmente)
  └── ContentArea
        └── GameListView.axaml
              └── ItemsControl de GameBase
                    └── [click] abre SwapDialog.axaml (Window modal)
                          ├── Seções por tipo de DLL (DLSS / DLSS-G / DLSS-D / XeSS / FSR3.1)
                          ├── ComboBox: versões disponíveis
                          ├── Botão "Download" (se LocalRecord == null)
                          └── Botões "Swap" / "Restore"
```

Sem requisito de estilo específico na v1 — layout funcional.

---

## Projeto `DLSS.Swapper.Linux.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>DLSS.Swapper.Linux</AssemblyName>
    <RootNamespace>DLSS_Swapper.Linux</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia"             Version="12.0.4" />
    <PackageReference Include="Avalonia.Desktop"     Version="12.0.4" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.4" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\src-core\DLSS.Swapper.Core.csproj" />
  </ItemGroup>
</Project>
```

### Startup (`Program.cs`)

```csharp
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var storageProvider = new LinuxStoragePathProvider();
        Storage.Initialize(storageProvider);           // Storage.Initialize, não Init
        Logger.Init(Storage.StoragePath, LoggingLevel.Info);

        var factory = new LinuxGameFactory();

        // Serviços estáticos (mesmo padrão de src/)
        GameBase.DatabaseService   = new Database();   // Database() sem parâmetros
        GameBase.DllManagerService = DLLManager.Instance;
        GameBase.SettingsService   = Settings.Instance;
        GameBase.GameManagerService = null;            // sem GameManager na v1

        BuildAvaloniaApp(factory).StartWithClassicDesktopLifetime(args);
    }

    static AppBuilder BuildAvaloniaApp(IGameLibraryFactory factory)
        => AppBuilder.Configure(() => new App(factory))
                     .UsePlatformDetect()
                     .WithInterFont()
                     .LogToTrace();
}
```

---

## Ordem de implementação

1. Mover `SteamLibrary` para `src-core/` + fix regex + atualizar `WindowsGameFactory`
2. Adicionar `InternalsVisibleTo("DLSS.Swapper.Linux")` em `AssemblyInfo.cs`
3. Criar `src-linux/DLSS.Swapper.Linux.csproj` + `Program.cs` + `App.axaml`
4. Implementar providers Linux + `LinuxGame` + `LinuxSteamGame` + `LinuxGameFactory`
5. Implementar `MainViewModel` + `MainWindow.axaml`
6. Implementar `SwapViewModel` + `SwapDialog.axaml`
7. Verificar `dotnet build src-linux/` sem erros
8. Smoke test: detectar Steam local, listar jogos, abrir SwapDialog

---

## Fora de escopo nesta iteração

- Script AppImage e job de CI (passo 7 da spec principal)
- Settings UI (tela de configurações)
- UI para adicionar jogos manualmente (`ManuallyAddedLibrary` existe na factory e aparece na sidebar, mas o fluxo de adicionar/remover jogos fica para depois)
- Estilo visual além do Fluent padrão
- NvAPI / DLSS preset no Linux
