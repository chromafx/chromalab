# ChromaLab

Desktop showcase lab for [ChromaFx](https://github.com/chromafx/chromafx) — open images, try filters, save results.

Built with **Avalonia** and **ProjectReferences** into the sibling `chromafx/` library (tracks local edits without NuGet).

## Requirements

- .NET 10 SDK (or newer with roll-forward)
- Sibling checkout: `../chromafx` (same workspace layout as [chromafx-workspace](.))

Native WebP/AVIF/JPEG XL open/save works when `ChromaFx.Native` runtimes are built (`chromafx/native/build.sh`). Managed formats work without that.

## Run

```bash
cd chromalab
dotnet run --project src/ChromaLab
```

## What’s wired (scaffold)

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
