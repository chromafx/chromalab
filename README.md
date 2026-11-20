# ChromaLab

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Desktop **image editor** powered by [ChromaFx](https://github.com/chromafx/chromafx) — open documents, edit layers, transform & filter, save / export. Built to be usable day-to-day while dogfooding the library.

Built with **Avalonia**. Local builds use **ProjectReferences** into a sibling `chromafx/` tree (tracks library edits without NuGet). Release builds on GitHub Actions use **NuGet** packages (pinned to a released ChromaFx line).

## Download

Self-contained zips for **win-x64**, **osx-arm64**, and **linux-x64** are on the [Releases](https://github.com/chromafx/chromalab/releases) page (current: **v0.3.0**).

- **Windows** — unzip and run `ChromaLab.exe` (embedded app icon).
- **macOS** — unzip and open `ChromaLab.app` (Dock/Finder icon from `Assets/icon.icns`).
- **Linux** — unzip and run `./ChromaLab`. Optional: `./install-desktop-entry.sh` installs a launcher + icon under `~/.local/share`.

## Requirements

- .NET 10 SDK (or newer with roll-forward) — for building from source
- For local dogfood: clone [chromafx](https://github.com/chromafx/chromafx) as a sibling `../chromafx` directory

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

Bump the library pin with `-p:ChromaFxPackageVersion=0.6.x` (default `0.6.5`) when you want release builds on a newer ChromaFx line.

## What’s wired

- **Editor** — top bar, Tools / Filters rail, checkerboard canvas, inspector + layers, status bar; Lucide toolbar icons (see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md))
- **Document** — Open / Save / Save As… / Export… / Reset; drag-and-drop; dirty marker + Save / Don’t Save / Cancel; undo/redo (24); async busy state
- **Tools & canvas** — Hand/pan, Crop (active layer), Rotate/Flip; Resize / Rotate / Skew / Perspective / Curves (inspector); Canvas Size… (W×H only); zoom/pan (wheel, shortcuts); hold-to-compare (Space)
- **Filters** — searchable catalog; live preview → Apply (undo) / Cancel; Randomize; variant strip; Alt-click for `cx`/`cy`; compare shows PSNR/SSIM vs open-original
- **Layers & ORA** — thumbs, visibility, reorder, Add / Duplicate / Delete; filters target active layer; OpenRaster open/save (lab-only; nested groups flatten); Export is flattened stills
- **GIF** — multi-frame opens as frame 0; Tools → Play / Pause preview only (no animated export)
- **Export** — JPEG quality; TIFF None/Deflate; WebP/AVIF/JXL quality + lossless when native RIDs are present
- **Macros** — record catalog Applies to `*.chromafx-macro.json`; Open & Run; Bulk Process… a folder (skips `.ora` / `.gif`)
- **Shortcuts** — Ctrl/⌘O open, Ctrl/⌘S save, Ctrl/⌘⇧S save as, Ctrl/⌘E export, Ctrl/⌘Z/Y undo/redo, Ctrl/⌘0 100%, Ctrl/⌘1 fit, Ctrl/⌘± zoom

## Tests

```bash
dotnet test tests/ChromaLab.Tests -c Release
```

CI runs the same against NuGet ChromaFx (`UseLocalChromaFx=false`) on PRs and `master`.

## Roadmap

- Expand headless coverage (bulk macros, more session edge cases); Avalonia UI automation still deferred

## License

Apache 2.0 — see [LICENSE](LICENSE). In-app toolbar glyphs are a Lucide SVG subset (ISC; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)).

Copyright 2026 Ho Tzin Mein.
