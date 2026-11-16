# Agent guide — ChromaLab

Avalonia desktop showcase for the sibling ChromaFx library. Own git repo; not a NuGet package. Licensed under Apache 2.0 (`LICENSE`).

## Layout

| Path | Role |
|------|------|
| `src/ChromaLab/` | Avalonia app (WinExe, net10.0) |
| `Assets/` | App icon (`icon.png`, `icon.ico` — ChromaFx logo) |
| `Layers/` | `RasterLayer`, `LayerDocument`, `LayerCompositor` — flat stack; filters target active layer |
| `Services/ImageSession.cs` | Document session; composite cache; undo/redo of `LayerDocument` (max 24); `*Async` + `IsDirty` |
| `Services/Ora/OraCodec.cs` | OpenRaster `.ora` read/write (lab-only; not ChromaFx.IO) |
| `Services/AvaloniaImageBridge.cs` | `Image` → `WriteableBitmap` |
| `Dialogs/ConfirmDialog` | Save / Don’t Save / Cancel for dirty open/quit |
| `Dialogs/ExportDialog` | Save As / Export — format + encoder options + destination |
| `Services/ExportFormatCatalog.cs` | Writable formats + option panels; OpenRaster is Save As (document) only |
| `Filters/FilterCatalog.cs` | Grouped filter registry; menus/toolbar/inspector built from it |
| `Filters/FilterParam.cs` | Inspector parameter descriptors + `FilterValues` |
| `Controls/FilterInspector` | Right-hand params panel (select → tweak → Apply) |
| `MainWindow` | Menu/toolbar; Layers menu; inspector; zoom/pan; drag-drop; busy gate |
| `.github/workflows/release.yml` | Tag `v*.*.*` → self-contained zips (`win-x64`, `osx-arm64`, `linux-x64`) |

## ChromaFx references

- **Local (default):** when `../../../chromafx/src/ChromaFx/ChromaFx.csproj` exists, `UseLocalChromaFx=true` → ProjectReferences (meta + PixelArt + Retro + Native).
- **CI / release:** `-p:UseLocalChromaFx=false` → PackageReferences at `ChromaFxPackageVersion` (default `0.6.4`). Bump that property when pinning a newer library tag.
- Prefer ProjectReferences for day-to-day dogfooding; do not switch the default local path to NuGet.

## Commands

```bash
dotnet build
dotnet run --project src/ChromaLab
dotnet publish src/ChromaLab -c Release -r osx-arm64 --self-contained true -p:UseLocalChromaFx=false
```

`global.json` pins SDK 10 with `rollForward: latestMajor`. On machines with only .NET 11+: `DOTNET_ROLL_FORWARD=LatestMajor` if needed.

## Conventions

1. Minimal diffs; match existing Avalonia patterns in this repo.
2. Commit only when asked; never push unless asked.
3. Keep the lab useful for dogfooding library APIs — prefer ProjectReferences over NuGet locally.
4. Native codecs may throw if RID binaries are missing; surface errors in the status bar, don’t crash the process.
5. Retro dither uses Core `OrderedDitherMethod` (not the removed `RetroDitherMethod`). Processing adaptive quantize uses `DitherMethod`.
6. **Filter UX** — param-less catalog entries apply immediately; parametric ones open `FilterInspector` with **live preview** (`ImageSession.PreviewAsync`, debounced). Apply commits to undo; Cancel clears preview. Prefer one parametric entry with sliders over ± preset menu duplicates. Mutate/preview run on the **active layer**, then the session recomposites.
7. **Save / Save As / Export** — Save overwrites `Path` (or Save As if untitled). Save As rebinds the document. Export writes a **flattened** copy and leaves path/dirty alone. `.ora` Save/Save As uses `OraCodec` (layer stack); Export never writes ORA.
8. **Layers** — flat list; ORA nested stacks flatten on import; no adjustment layers; no layers panel yet. ORA is lab-only (`OraCodec`), never ChromaFx.IO.
9. **Explorers (later)** — Randomize/seed and generative UIs stay in the lab; see roadmap `lab-explorers`. Extend `FilterInspector` + `PreviewAsync`.
10. **Icons** — keep `Assets/icon.png` / `icon.ico` in sync with the ChromaFx brand mark; `ApplicationIcon` + `MainWindow.Icon` both point at the ico.
