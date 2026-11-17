# ChromaLab

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Desktop **image editor** powered by [ChromaFx](https://github.com/chromafx/chromafx) — open documents, edit layers, transform & filter, save / export. Built to be usable day-to-day while dogfooding the library.

Built with **Avalonia**. Local builds use **ProjectReferences** into a sibling `chromafx/` tree (tracks library edits without NuGet). Release builds on GitHub Actions use **NuGet** packages (pinned to a released ChromaFx line).

## Download

Self-contained zips for **win-x64**, **osx-arm64**, and **linux-x64** are on the [Releases](https://github.com/chromafx/chromalab/releases) page (current: **v0.2.0**).

- **Windows** — unzip and run `ChromaLab.exe` (embedded app icon).
- **macOS** — unzip and open `ChromaLab.app` (Dock/Finder icon from `Assets/icon.icns`).
- **Linux** — unzip and run `./ChromaLab`. Optional: `./install-desktop-entry.sh` installs a launcher + icon under `~/.local/share`.

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

- **Editor shell** — top bar (brand + document actions), left **Tools / Filters** rail, canvas with checkerboard, right **inspector + layers** panel, status bar
- Open / **Save** / **Save As…** / **Export…** / Reset; **drag-and-drop** open; empty-state Open
- **Tools** — Hand/pan, **Crop** (active layer), Rotate / Flip via ChromaFx
- **Filters** — searchable catalog from `FilterCatalog`; parametric filters **live-preview** in the inspector; **Apply** commits (undo); **Cancel** drops preview; **Randomize seed** when present
- **Layers** — thumbs, visibility, active row, Add / Duplicate / Delete / Move up·down; filters edit the active layer; composite for display
- **OpenRaster (`.ora`)** — lab-only open/save of the layer document (not in ChromaFx.IO). Nested groups flatten on import. Export stays flattened stills only.
- **Export options** — JPEG quality; TIFF None/Deflate; WebP/AVIF/JXL quality + lossless (native RIDs)
- **Async** open / save / export / filters (busy cursor + controls disabled)
- **Dirty** tracking — title/status `•` marker; Save / Don’t Save / Cancel on open & quit
- **Undo / Redo** (up to 24 document snapshots; Ctrl/⌘Z, Ctrl/⌘Y, Ctrl/⌘⇧Z)
- **Hold-to-compare** — Space (or toolbar) shows the document-open original
- **Shortcuts** — Ctrl/⌘O open, Ctrl/⌘S save, Ctrl/⌘⇧S save as, Ctrl/⌘E export, Ctrl/⌘0 100%, Ctrl/⌘1 fit, Ctrl/⌘± zoom
- **Zoom / pan** — wheel zoom-to-cursor, drag to pan

## Roadmap

- **Macros** — save an ordered sequence of catalog Applies (id + params) and run them for **bulk processing** over many files
- GIF playback, canvas-size UI polish, richer transform tools

## License

Apache 2.0 — see [LICENSE](LICENSE).

Copyright 2026 Ho Tzin Mein.
