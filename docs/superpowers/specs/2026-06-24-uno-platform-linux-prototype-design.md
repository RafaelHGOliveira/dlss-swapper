# Uno Platform Linux Prototype — Design Spec

**Data:** 2026-06-24  
**Status:** Aprovado  
**Objetivo:** Validar visualmente se Uno Platform + Skia produz uma UI Linux com visual WinUI 3 (Fluent Design) aproveitando o XAML existente de `src/`.

---

## Contexto

O fork Linux (`src-linux/`) usa Avalonia como framework UI. A interface foi implementada do zero em `.axaml` e não reaproveitou o XAML do projeto Windows (`src/`). A hipótese a validar é: **Uno Platform consegue rodar o XAML de `src/` no Linux com fidelidade visual suficiente para justificar uma migração completa?**

O `src-linux/` (Avalonia) permanece intacto durante o protótipo — é o fallback caso Uno não satisfaça.

---

## Arquitetura

### Projeto novo: `src-uno-linux/`

Projeto independente, não substitui nada. Referencia `src-core/` para lógica compartilhada e reutiliza as implementações de plataforma Linux existentes.

```
src-uno-linux/
  DLSS.Swapper.UnoLinux.csproj      # net10.0, Uno.WinUI, Skia backend
  Program.cs                         # entry point Skia/Linux
  App.xaml / App.xaml.cs
  MainWindow.xaml / .cs              # portado de src/MainWindow.xaml
  Pages/
    GameGridPage.xaml / .cs          # portado de src/Pages/GameGridPage.xaml
    GameGridPageModel.cs             # portado de src/Pages/GameGridPageModel.cs
  Platform/Linux/                    # copiado de src-linux/Platform/Linux/
    LinuxSteamPathProvider.cs
    LinuxStoragePathProvider.cs
    LinuxGameFactory.cs
    LinuxGameManager.cs
    LinuxDatabase.cs
    LinuxSettings.cs
    LinuxDLLManager.cs
```

### Dependências do csproj

```xml
<TargetFramework>net10.0</TargetFramework>
<!-- versão a confirmar na implementação — verificar compatibilidade com .NET 10 -->
<PackageReference Include="Uno.WinUI" Version="5.x" />
<!-- backend Linux: Uno.WinUI.Skia.Gtk (requer GTK3) ou Uno.WinUI.Skia.X11 (Wayland via XWayland) -->
<!-- escolha feita na implementação após testar qual está disponível no ambiente -->
<PackageReference Include="Uno.WinUI.Skia.Gtk" Version="5.x" />
<ProjectReference Include="../src-core/DLSS.Swapper.Core.csproj" />
```

### Namespaces XAML

O XAML de `src/` usa `http://schemas.microsoft.com/winfx/2006/xaml/presentation` e `using:Microsoft.UI.Xaml.Controls` — exatamente o que Uno implementa. Não há mudança de namespace. O que requer ajuste:

| Origem Windows | Substituição no protótipo |
|---|---|
| `CommunityToolkit.WinUI.Converters` | Implementar converters simples inline ou usar `CommunityToolkit.Mvvm` equivalente |
| `using:DLSS_Swapper.*` (src/) | Mover o necessário para src-core/ ou duplicar no protótipo |
| `FakeContentDialog` (UserControl Windows) | Omitir no protótipo — usar `ContentDialog` padrão |

---

## Escopo do protótipo

### Inclui (necessário para validação visual)

- `MainWindow` com `NavigationView` (menu lateral compacto, título, ícone do app)
- `GameGridPage` com `GridView` de jogos (covers da CDN Steam, título, versão DLSS)
- Detecção Steam real via `src-core/` + `LinuxSteamPathProvider`
- Download de covers com fallback para `header.jpg`

### Exclui (não bloqueia validação)

- `SwapDialog` (download/swap/restore de DLLs)
- `LibraryPage`, `SettingsPage`, `AcknowledgementsPage`
- Busca e filtros de jogos
- `GameHistoryControl`, `DLLPickerControl`, `ImportDLLSummaryControl`
- Funcionalidades de `LinuxDLLManager` além do carregamento inicial

---

## Critério de sucesso

O protótipo é considerado bem-sucedido se:

1. A janela abre no Wayland (via XWayland) sem erros fatais
2. `NavigationView` renderiza com visual Fluent Design (sidebar compacta, animações de hover)
3. `GridView` de jogos exibe covers e títulos com layout equivalente ao Windows
4. Performance de scroll aceitável (sem travamentos visíveis)

Se os 4 critérios forem atendidos → decisão de migrar `src-linux/` para Uno Platform.  
Se falharem → manter Avalonia, avaliar `FluentAvalonia` para paridade visual.

---

## O que não muda

- `src/` (WinUI 3 Windows) — sem alterações
- `src-linux/` (Avalonia) — sem alterações, continua buildando
- `src-core/` — sem alterações (pode receber código movido de src/ se necessário)
- Testes — sem alterações

---

## Riscos conhecidos

| Risco | Mitigação |
|---|---|
| Uno Skia Linux pode exigir GTK instalado | Testar localmente; fallback: X11 FrameBuffer |
| `CommunityToolkit.WinUI.Converters` não existe no Uno | Implementar converters necessários no protótipo |
| ViewModels de `src/` referenciam tipos WinUI (ex: `FontIcon`) | Uno fornece `Microsoft.UI.Xaml.Controls.FontIcon` — deve resolver sem mudança |
| .NET 10 + Uno Platform — combinação pode não ser estável | Verificar versão mínima suportada; considerar net8.0 se necessário |
