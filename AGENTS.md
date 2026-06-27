# AGENTS.md — DLSS Swapper Linux Fork

Instruções para agentes de IA trabalhando neste repositório.

## Contexto

Fork de `beeradmoore/dlss-swapper` adicionando suporte Linux. Leia `CLAUDE.md` e `docs/superpowers/specs/2026-06-22-linux-support-design.md` antes de qualquer tarefa não trivial.

## Estrutura de projetos

| Projeto | Path | Plataforma | Status |
|---|---|---|---|
| Windows UI | `src/` | net10.0-windows, WinUI 3 | existente |
| Core compartilhado | `src-core/` | net10.0 | em desenvolvimento |
| Linux UI | `src-linux/` | net10.0, Avalonia | em desenvolvimento |

Ambiente de dev é Linux (SDK .NET 10.0.x). `src/` (WinUI) **só compila em Windows/CI** — não tentar aqui. `src-core/` e os testes rodam localmente: `dotnet test`.

## Regras críticas

1. **Nunca quebrar o build de `src/`** — é o projeto Windows original; só mexer para extrair código para `src-core/`
2. **`src-core/` sem deps de plataforma** — proibido: `Microsoft.Win32`, `System.Runtime.InteropServices` para P/Invoke, qualquer referência a WinUI
3. **Abstração via `ILauncherPathProvider`** — nunca chamar Registry direto de dentro do Core
4. **Detecção Linux via filesystem** — sem Wine/Proton reg hacks; usar os paths documentados na spec
5. **Nunca commitar** `docs/superpowers/` — são specs locais de desenvolvimento. Não estão no `.gitignore`; ao fazer `git add`, garanta que esse path fique de fora.
6. **Manter `CLAUDE.md` e `AGENTS.md` sincronizados** — toda alteração de regra/contexto relevante em um deve ser refletida no outro.

## Padrões do codebase existente

- MVVM com CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- Cada launcher implementa `IGameLibrary` com método `ListGamesAsync()` e `IsInstalled()`
- `GameLibrary` é um enum `[Flags]` — novos launchers ganham nova flag
- Singleton pattern: `SteamLibrary.Instance`, etc.
- Logging via Serilog (`Log.Debug`, `Log.Warning`, `Log.Error`)

## Ao adicionar detecção de launcher Linux

1. Implementar o método correspondente em `LinuxLauncherPathProvider`
2. A `*Library.cs` no Core usa o provider injetado — sem `if (Linux)` na lógica de negócio
3. Testar com path inexistente (launcher não instalado) — deve retornar lista vazia sem exceção

## Referências

- Design spec: `docs/superpowers/specs/2026-06-22-linux-support-design.md`
- Referência de detecção Linux: `Recol/DLSS-Updater` — `dlss_updater/linux_paths.py`
- Upstream: `https://github.com/beeradmoore/dlss-swapper`
