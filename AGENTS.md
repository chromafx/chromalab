# Agent guide — ChromaLab

Avalonia desktop **image editor** powered by the sibling ChromaFx library (also a showcase / dogfood surface). Own git repo; not a NuGet package. Licensed under Apache 2.0 (`LICENSE`).

## Layout

| Path | Role |
|------|------|
| `src/ChromaLab/` | Avalonia app (WinExe, net10.0) |
| `Assets/` | App icons: `icon.png` (UI), `icon.ico` (Windows exe + window), `icon.icns` (macOS `.app`, not an AvaloniaResource) |
| `packaging/` | Release wrappers: `pack-macos.sh` (`.app` + icns), `pack-linux.sh` (`.desktop` + PNG), `generate-icns.sh` |
| `Layers/` | `RasterLayer`, `LayerDocument`, `LayerCompositor` — flat stack; filters target active layer |
| `Services/ImageSession.cs` | Document session façade; `SessionChangeKind` for targeted UI refresh; preview coalesce; hold-to-compare; GIF play-only preview |
| `Services/DocumentHistory.cs` | Undo/redo stacks (used by `ImageSession`) |
| `Services/DocumentStorage.cs` | Open/save/ORA/GIF load helpers |
| `Services/CropController.cs` | Crop drag/rect state for the canvas tool |
| `MainWindow.Animation.cs` | GIF play/pause timer; frame status |
| `Services/PreviewViewport.cs` | Zoom / pan / fit for the preview scroll host |
| `Services/ImageFileTypes.cs` | Shared open/drop extensions + file picker type |
| `Services/MacroFileTypes.cs` | Macro `*.chromafx-macro.json` picker type |
| `Services/UiHelpers.cs` | Wire / SetEnabled / selected-class helpers |
| `Services/Ora/OraCodec.cs` | OpenRaster `.ora` read/write (lab-only; not ChromaFx.IO) |
| `Services/AvaloniaImageBridge.cs` | `Image` → `WriteableBitmap` (row-copy + `UpdateOrCreate` + thumbnails) |
| `Controls/ExportOptionsPanel.cs` | Shared encoder-options UI for Export / Bulk |
| `Macros/` | `MacroDefinition` / `MacroJson` / `MacroRecorder` / `MacroRunner` / `MacroBulk` — `*.chromafx-macro.json` |
| `Dialogs/ConfirmDialog` | Save / Don’t Save / Cancel for dirty open/quit |
| `Dialogs/ExportDialog` | Save As / Export — format + encoder options + destination |
| `Dialogs/CanvasSizeDialog` | Edit → Canvas Size… — document W×H |
| `Dialogs/BulkProcessDialog` | Folder bulk: macro + format + progress / cancel |
| `Services/ExportFormatCatalog.cs` | Writable formats + option panels; OpenRaster is Save As (document) only |
| `Filters/FilterCatalog*.cs` | Grouped filter registry (partials by group); menus / browser / inspector built from it |
| `Filters/FilterParam.cs` | Inspector parameter descriptors + `FilterValues` (+ dict round-trip for macros) |
| `Filters/FilterExplorer.cs` | Randomize params, normalized center pick, variant value bags |
| `Controls/FilterInspector` | Right-hand params panel (select → tweak → Apply); Randomize; variant strip; Alt-click center hint |
| `Controls/FilterCatalogView` | Searchable left-rail filter browser |
| `Controls/LayersPanel` | Layers list with thumbs, visibility, reorder |
| `MainWindow` (+ `.Document` / `.Filters` / `.Canvas` / `.Layers` / `.Macros` partials) | Editor shell; document IO; filters; viewport/crop; layers; macro record/run |
| `tests/ChromaLab.Tests/` | Headless xunit.v3 (filters, macros, layers, ORA, session) |
| `.github/workflows/ci.yml` | PR/master: `dotnet build` + `dotnet test` with `UseLocalChromaFx=false` |
| `.github/workflows/release.yml` | Tag `v*.*.*` → self-contained zips (`win-x64`, `osx-arm64`, `linux-x64`) |

## ChromaFx references

- **Local (default):** when `../../../chromafx/src/ChromaFx/ChromaFx.csproj` exists, `UseLocalChromaFx=true` → ProjectReferences (meta + PixelArt + Retro + Native).
- **CI / release:** `-p:UseLocalChromaFx=false` → PackageReferences at `ChromaFxPackageVersion` (default `0.6.6`). Bump that property when pinning a newer library tag.
- Prefer ProjectReferences for day-to-day dogfooding; do not switch the default local path to NuGet.

## Commands

```bash
dotnet build
dotnet run --project src/ChromaLab
dotnet test tests/ChromaLab.Tests -c Release
# CI / release path (NuGet ChromaFx, catches package global-using clashes):
dotnet test tests/ChromaLab.Tests -c Release -p:UseLocalChromaFx=false
dotnet publish src/ChromaLab -c Release -r osx-arm64 --self-contained true -p:UseLocalChromaFx=false
```

Headless tests live in `tests/ChromaLab.Tests` (xunit.v3). Prefer logic/session coverage; no Avalonia UI automation yet. `InternalsVisibleTo` grants the test assembly access to `DocumentHistory` / `DocumentStorage` / `LucideIconData`.

`global.json` pins SDK 10 with `rollForward: latestMajor`. On machines with only .NET 11+: `DOTNET_ROLL_FORWARD=LatestMajor` if needed.

## Conventions

1. Minimal diffs; match existing Avalonia patterns in this repo.
2. Commit only when asked; never push unless asked.
3. Keep the lab useful for dogfooding library APIs — prefer ProjectReferences over NuGet locally.
4. Native codecs may throw if RID binaries are missing; surface errors in the status bar, don’t crash the process.
5. Retro dither uses Core `OrderedDitherMethod` (not the removed `RetroDitherMethod`). Processing adaptive quantize uses `DitherMethod`.
6. **Filter UX** — param-less catalog entries apply immediately; parametric ones open `FilterInspector` with **live preview** (`ImageSession.PreviewAsync`, debounced). Apply commits to undo; Cancel clears preview. Prefer one parametric entry with sliders over ± preset menu duplicates. Mutate/preview run on the **active layer**, then the session recomposites. Keep Applies discrete (catalog id + `FilterValues`) for macros. **Explorers:** Randomize jitters all params (`FilterExplorer`); Alt-click canvas sets `cx`/`cy` when present; variant strip uses downscaled thumbs; hold-to-compare status shows Core `ImageSimilarity` PSNR/SSIM vs open-original. Tools-pane parametric shortcuts stay on the Tools rail (do not force Filters tab).
7. **Save / Save As / Export** — Save overwrites `Path` (or Save As if untitled). Save As rebinds the document. Export writes a **flattened** copy and leaves path/dirty alone. `.ora` Save/Save As uses `OraCodec` (layer stack); Export never writes ORA.
8. **Layers** — flat list + `LayersPanel`; ORA nested stacks flatten on import; no adjustment layers. Selection via `SelectLayer` does not dirty. Structural edits use `EditDocument` (undo). **Canvas Size** (`ImageSession.ResizeCanvas`) changes `LayerDocument` W×H only — not layer resampling. ORA is lab-only (`OraCodec`), never ChromaFx.IO.
9. **Tools** — Crop applies to the **active layer** (ChromaFx `Crop` + layer offset). Rotate/Flip use catalog entries. Hand pans; Space hold-to-compares against the open-original composite. Multi-frame GIF: document is still frame 0; **Play** shows `Animation` frames in the preview only (stops on edit/compare/busy).
10. **Macros** — `MacroRecorder` hooks successful `TryMutate` (catalog Applies only; not crop/layers). Save `*.chromafx-macro.json` via `MacroJson`. Open & Run → `ImageSession.ApplyMacroAsync` (one undo). Bulk → `BulkProcessDialog` / `MacroBulk` (skips `.ora`/`.gif`; does not dirty the open doc). Randomize / variants stay in the inspector (not recorded until Apply).
11. **Icons** — keep `icon.png` / `icon.ico` / `icon.icns` in sync with the ChromaFx brand mark. `ApplicationIcon` + `MainWindow.Icon` use the ico; macOS Dock uses `packaging/pack-macos.sh` → `AppIcon.icns`. Regenerate icns on macOS with `packaging/generate-icns.sh`. In-app glyphs are a **Lucide** SVG subset (`Assets/Icons/`, `LabIcon` + `LucideIconData`); tint with `Icon*Brush` resources in `App.axaml`. License: `THIRD_PARTY_NOTICES.md`. Refresh path data with `scripts/gen-lucide-icon-data.py`.
12. **Theme** — fixed dark editor chrome (`App.axaml` resources); do not follow system light variant for the shell.
13. **UI refresh** — `ImageSession.Changed` carries `SessionChangeKind`: `PreviewOnly` updates the canvas bitmap only; `Selection` updates layer chrome without rebuilding thumbs; `Full` refreshes command state + layers + preview. Prefer `CancelTransientModes` over ad-hoc preview/animation/crop teardown.
