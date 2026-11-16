# ChromaLab

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Desktop showcase lab for [ChromaFx](https://github.com/chromafx/chromafx) — open images, try filters, save results.

Built with **Avalonia**. Local builds use **ProjectReferences** into a sibling `chromafx/` tree (tracks library edits without NuGet). Release builds on GitHub Actions use **NuGet** packages (pinned to a released ChromaFx line).

## Download

Self-contained zips for **win-x64**, **osx-arm64**, and **linux-x64** are on the [Releases](https://github.com/chromafx/chromalab/releases) page (current: **v0.1.0**). Unzip and run `ChromaLab` (`ChromaLab.exe` on Windows).

## Requirements

- .NET 10 SDK (or newer with roll-forward) — for building from source
- For local dogfood: sibling checkout `../chromafx` (same layout as [chromafx-workspace](https://github.com/chromafx))

Native WebP/AVIF/JPEG XL open/save works when `ChromaFx.Native` RID assets are present (sibling `native/build.sh` locally, or the NuGet package at release). Managed formats work without that.

## Run from source

```bash
cd chromalab
dotnet run --project src/ChromaLab
```

Force NuGet packages instead of sibling ProjectReferences:

```bash
dotnet run --project src/ChromaLab -p:UseLocalChromaFx=false
```

## Releases (maintainers)

Tag `v*.*.*` to publish zips via [GitHub Actions](.github/workflows/release.yml). `workflow_dispatch` builds the same zips as artifacts without creating a release.

Bump the library pin with `-p:ChromaFxPackageVersion=0.6.x` (default `0.6.4`) when you want release builds on a newer ChromaFx line.

## What’s wired

- Conventional **menu bar** (File / Edit / View / Layers / Filters) + **icon toolbar** + status bar
- Open / **Save** / **Save As…** / **Export…** / Reset; **drag-and-drop** open
- **Layers** — flat raster stack; filters edit the active layer; composite for display. Menu: Add / Duplicate / Delete / Select Prev·Next / Toggle Visibility
- **OpenRaster (`.ora`)** — lab-only open/save of the layer document (not in ChromaFx.IO). Nested groups flatten on import. Export stays flattened stills only.
- **Export options** — JPEG quality; TIFF None/Deflate; WebP/AVIF/JXL quality + lossless (native RIDs)
- **Async** open / save / export / filters (UI stays responsive; busy cursor + controls disabled)
- **Dirty** tracking — title/status `•` marker; Save / Don’t Save / Cancel on open & quit
- **Undo / Redo** (up to 24 document snapshots; Ctrl/⌘Z, Ctrl/⌘Y, Ctrl/⌘⇧Z)
- **Shortcuts** — Ctrl/⌘O open, Ctrl/⌘S save, Ctrl/⌘⇧S save as, Ctrl/⌘E export, Ctrl/⌘0 100%, Ctrl/⌘1 fit, Ctrl/⌘± zoom
- **Zoom / pan** — wheel zoom-to-cursor, drag to pan; image centered when it fits
- Status: message · filename/size/active layer · zoom %
- Preview via `WriteableBitmap` bridge
- **Filter catalog** (`Filters/FilterCatalog.cs`) — grouped Filters menu; toolbar favorites (Greyscale / Invert / Web-safe)
- **Filter inspector** (right sidebar) — parametric filters **live-preview** as you tweak (debounced); **Apply** commits to undo, **Cancel** drops the preview. Param-less filters run immediately.

GIF playback and a dedicated layers panel come later.

## License

Apache 2.0 — see [LICENSE](LICENSE).

Copyright 2026 Ho Tzin Mein.
