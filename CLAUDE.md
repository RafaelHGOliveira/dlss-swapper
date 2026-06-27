# DLSS Swapper — Linux Fork

Fork de `beeradmoore/dlss-swapper` com suporte Linux em desenvolvimento.

## Estrutura do projeto

```
src/           # UI Windows — WinUI 3, net10.0-windows, win-x64 (projeto original)
src-core/      # Lógica compartilhada — net10.0, sem deps de plataforma (em desenvolvimento)
src-linux/     # Stub de compatibilidade de testes (Avalonia preservado em branch linux-ui-avalonia)
src-uno-linux/ # UI Linux — Uno Platform 6 + Skia, net10.0-desktop (ativo)
docs/superpowers/specs/  # Design specs
```

## Contexto do fork

- Upstream: `beeradmoore/dlss-swapper`
- Objetivo: adicionar suporte Linux (AppImage) mantendo o Windows intacto
- Design spec: `docs/superpowers/specs/2026-06-22-linux-support-design.md`
- Referência de detecção de jogos Linux: `Recol/DLSS-Updater` (Python, MIT)

## Build (src-uno-linux)

```bash
# Build
dotnet build src-uno-linux/DLSS.Swapper.UnoLinux/DLSS.Swapper.UnoLinux.csproj

# Rodar (usar -c Release para evitar spam de erro do DevServer do Uno no Debug)
WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/run/user/1000 \
  dotnet run --project src-uno-linux/DLSS.Swapper.UnoLinux/DLSS.Swapper.UnoLinux.csproj --no-build -c Release

# Verificar pureza (zero resultados esperados):
grep -rE "using Avalonia" src-uno-linux/
```

## Executar e depurar (src-uno-linux)

```bash
# Lançar em background (nunca pkill — exit 144 no sandbox; usar kill <pid>)
WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/run/user/1000 \
  setsid dotnet run --project src-uno-linux/DLSS.Swapper.UnoLinux/DLSS.Swapper.UnoLinux.csproj --no-build -c Release \
  > /tmp/dlss-uno-app.log 2>&1 &
# Logs Serilog em: /tmp/DLSS Swapper/logs/; stdout em: /tmp/dlss-uno-app.log
# Trazer janela à frente: wmctrl -a "DLSS Swapper"  (ou xdotool search --name "DLSS Swapper" windowactivate)
```

> Skill `dlss-run` aponta para `src-linux/` (stub Avalonia) — adaptar o comando para `src-uno-linux/` manualmente.

## Build

Ambiente de dev é Linux (SDK .NET 10.0.x). `src/` (WinUI, net10.0-windows) **só compila em Windows/CI** — não tentar aqui. `src-core/` (net10.0) e os testes rodam localmente: `dotnet test`.

```bash
# Windows (requer Windows + Windows App SDK)
dotnet build src/

# Core isolado (Linux — verificar que compila sem deps Windows):
dotnet build src-core/DLSS.Swapper.Core.csproj
dotnet test tests/DLSS.Swapper.Core.Tests/

# Verificar pureza de plataforma (deve retornar zero resultados):
grep -rE "using Microsoft\.(UI|Win32)|using Windows\.|NvAPIWrapper" src-core/
grep -rE "using Microsoft\.(UI|Win32)|using Windows\.|NvAPIWrapper" src-linux/

# Linux (src-linux/ existe; compilar e rodar):
dotnet build src-linux/DLSS.Swapper.Linux.csproj
WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/run/user/1000 dotnet run --project src-linux/DLSS.Swapper.Linux.csproj --no-build
# Logs Serilog em: /tmp/DLSS Swapper/logs/
dotnet publish src-linux/DLSS.Swapper.Linux.csproj -r linux-x64 --self-contained
```

## Regras de desenvolvimento

- **`LinuxDLLManager.PopulateCollection` deve receber e setar `GameAssetType`** — o JSON do manifest não tem campo `asset_type`; `DLLRecord.AssetType` default é `Unknown`. Sem setar antes de `LoadLocalRecord`, `DllNameForGameAssetType` retorna `""` e `.Single()` explode no zip durante download/swap. Espelha o `SetGameAssetType` do Windows (`src/Data/DLLManager.cs:261-269`).
- **`FileVersionInfo.GetVersionInfo()` retorna zeros no Linux para DLLs nativas Windows** (NVIDIA, AMD, Intel) — `FileVersionInfoExtensions.GetFormattedFileVersion()` já tem fallback via `PeVersionReader.ReadFileVersion()` (`src-core/Helpers/PeVersionReader.cs`), ativado quando `FileMajorPart == 0 && FileMinorPart == 0` em não-Windows. Não mexer na cadeia de chamadas sem verificar que o fallback ainda cobre o caso.
- **`GameControl`** (`Controls/GameControl.xaml+cs`) — dialog ao clicar em jogo; mostra capa + path de instalação + seção por tipo de DLL (versão atual + botão "Change" que abre `DLLPickerControl`).
- **`src/` não muda de comportamento** — qualquer alteração ali deve ser apenas para extrair código para `src-core/`, nunca para quebrar o build Windows
- **`src-core/` é zero plataforma** — sem `using Microsoft.Win32`, sem P/Invokes não-guardados, sem `RuntimeInformation` espalhado; toda abstração de plataforma fica em `src-core/Platform/`
- **Mover arquivos src/→src-core/: manter namespace idêntico** — assim os callers em src/ continuam resolvendo via Core sem alterar usings. Nunca renomear namespace ao mover.
- **Gotchas de acoplamento** (descobertos na extração):
  - `Logger.cs` depende de `Storage.GetTemp()` e `Settings.Instance.LoggingLevel` — mover só após Storage/Settings estarem em Core
  - `FSR31Helper.cs` tem `[DllImport("kernel32.dll")]` — requer `[SupportedOSPlatform("windows")]` + guard `RuntimeInformation.IsOSPlatform(Windows)` para ir ao Core
  - `DLLRecord.TranslationProperties` é `DLLRecordModelTranslationProperties` (WinUI) — no Core vira `object?` com delegate estático `CreateTranslationPropertiesDelegate`
  - `Database.cs` não vai para Core — depende de tipos src/ (SteamGame, etc.) para criar tabelas; fica em src/ implementando `IDatabase`
- **`ISteamPathProvider`** — abstração de path do Steam; Windows usa Registry, Linux usa filesystem. Composição via factory por plataforma (`IGameLibraryFactory`), sem DI container nem singletons
- **Detecção Linux v1** — apenas Steam (nativo + Proton); Epic/GOG/EA/Battle.net/Xbox (Heroic/Wine/Lutris) ficam para v2; ver spec para detalhes
- **Settings Linux** — `$XDG_CONFIG_HOME/dlss-swapper/` (fallback `~/.config/dlss-swapper/`); Windows continua em `%LOCALAPPDATA%/DLSS Swapper/`
- **Adicionar projeto à solução:** usar `dotnet sln "DLSS Swapper.sln" add path/to.csproj` — nunca editar o .sln manualmente (GUIDs e config mappings são frágeis)
- **`Storage` e `Logger` são `internal` no Core** — src-linux precisa de `InternalsVisibleTo("DLSS.Swapper.Linux")` em `src-core/Properties/AssemblyInfo.cs` para chamar `Storage.Initialize()`

## Dependências chave (src-uno-linux)

- Uno Platform 6 / Uno.Sdk 6.5.36, `net10.0-desktop`
- `Microsoft.UI.Xaml.*` — NÃO `using Avalonia`
- CommunityToolkit.Mvvm 8.x
- `InternalsVisibleTo("DLSS.Swapper.UnoLinux")` já em `src-core/Properties/AssemblyInfo.cs`
- **Gotchas Uno Platform:**
  - `DependencyObject` subclasses precisam de `partial` — Uno injeta código via source generators
  - `[ObservableProperty]` sintaxe CommunityToolkit 8.x: `public partial Type PropName { get; set; }`
  - `x:Bind` em `object?`: usar cast explícito `((ns:ConcreteType)Property).Member` — funciona em DataTemplate
  - `{Binding}` clássico necessário para cadeias duplo-nullable (`A?.B?.C`) — x:Bind falha em compile-time
  - `SafeFireAndForget` não disponível — usar `_ = SomeTask()` para fire-and-forget
  - Build Debug injeta `Uno.WinUI.DevServer` automaticamente; erro `RemoteControlClient` é não-fatal mas spam — usar `-c Release` para eliminar
  - `Window.Content` não disponível via `is FrameworkElement` cast antes do primeiro dispatch cycle pós-`Activate()` — para aplicar tema ou acessar o visual tree no `OnLaunched`, usar `DispatcherQueue.TryEnqueue` + elementos XAML nomeados diretamente (ex: `MainNavigationView.RequestedTheme`), nunca `(Window.Content as FrameworkElement)`.

## Stub src-linux/ (compatibilidade de testes)

`tests/DLSS.Swapper.Core.Tests/DLSS.Swapper.Core.Tests.csproj` referencia `src-linux/DLSS.Swapper.Linux.csproj`.
Esse projeto é um stub criado para satisfazer a referência (o projeto Avalonia original foi removido em `478c290`).
O stub compila `src-uno-linux/Platform/Linux/*.cs` e fornece `LinuxSteamPathProvider` / `LinuxStoragePathProvider` para os testes.
**Não deletar** — quebraria os 22 testes de plataforma Linux.

## Dependências chave (src/)

- WinUI 3 / Windows App SDK
- CommunityToolkit.Mvvm (MVVM)
- ValveKeyValue (parsing VDF Steam)
- SQLite-net (database local)
- Serilog (logging)
- .NET 10, SDK 10.0.100

## Dependências chave (src-linux/)

- Avalonia 11.3.0 (confirmado compatível com net10.0)
- Referência ao `src-core/DLSS.Swapper.Core.csproj`
- **Gotchas Avalonia:**
  - Não incluir `<ApplicationManifest>` no csproj Linux — é propriedade Windows-only
  - `Window` subclasses precisam de construtor público sem parâmetros (warning AVLN3001); overloads com parâmetros devem chamar `: this()`
  - `TaskCanceledException` do DBus ao fechar app é não-fatal — ignorar
  - `OnFrameworkInitializationCompleted` deve ser síncrono (`void`, não `async void`) — `await` antes de `base.OnFrameworkInitializationCompleted()` causa deadlock no dispatcher: janela nunca aparece, processo fica vivo sem erros. Padrão correto: setar `desktop.MainWindow`, chamar `base`, depois disparar async init com `_ = InitializeAsync(...)`.

## Git

- Remote `origin` → `RafaelHGOliveira/dlss-swapper`
- Remote `upstream` → `beeradmoore/dlss-swapper`
- Para sincronizar com upstream: `git fetch upstream && git merge upstream/main`
- **Nunca commitar** `docs/superpowers/` — são specs locais de desenvolvimento. Não estão no `.gitignore`; ao fazer `git add`, garanta que esse path fique de fora.
- **Manter `CLAUDE.md` e `AGENTS.md` sincronizados** — toda alteração de regra/contexto relevante em um deve ser refletida no outro.
