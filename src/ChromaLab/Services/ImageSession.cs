/*
 * Copyright 2026 Ho Tzin Mein
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Avalonia.Media.Imaging;
using ChromaFx.Core;
using ChromaFx.IO;
using ChromaFx.IO.Encoders;
using ChromaFx.Processing;
using ChromaFx.Processing.Numerics;
using ChromaLab.Filters;
using ChromaLab.Layers;
using ChromaLab.Macros;
using ChromaLab.Services.Ora;

namespace ChromaLab.Services;

/// <summary>
/// Holds the working <see cref="LayerDocument"/>, composite cache, undo/redo, and preview bitmap.
/// Filters mutate the active layer; display is the flattened composite.
/// </summary>
public sealed class ImageSession : IDisposable
{
    /// <summary>
    /// Maximum undo (and redo) depth. Older snapshots are dropped.
    /// </summary>
    public const int MaxHistory = 24;

    private LayerDocument? _original;
    private LayerDocument? _document;
    private Image? _composite;
    private WriteableBitmap? _preview;
    private int _previewGeneration;
    private bool _comparing;

    // Index 0 = oldest; end = most recent snapshot.
    private readonly List<LayerDocument> _undo = [];
    private readonly List<LayerDocument> _redo = [];

    /// <summary>
    /// Raised after the working document or preview changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets whether a document is loaded.
    /// </summary>
    public bool HasImage => _document is not null;

    /// <summary>
    /// Gets the working layer document when loaded.
    /// </summary>
    public LayerDocument? Document => _document;

    /// <summary>
    /// Gets whether Undo is available.
    /// </summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>
    /// Gets whether Redo is available.
    /// </summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Gets whether the displayed preview is a live filter preview (not yet Apply'd).
    /// </summary>
    public bool IsPreviewing { get; private set; }

    /// <summary>
    /// Gets whether the working document differs from the last open/save/reset baseline.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Gets the current file path when known.
    /// </summary>
    public string? Path { get; private set; }

    /// <summary>
    /// Gets image size text for the status bar.
    /// </summary>
    public string SizeLabel =>
        _document is null ? "No image" : $"{_document.Width}×{_document.Height}";

    /// <summary>
    /// Gets active layer status text for the status bar.
    /// </summary>
    public string LayerLabel => _document?.LayerStatusLabel ?? "";

    /// <summary>
    /// Gets a short display name for the open file (or "Untitled").
    /// </summary>
    public string DisplayName =>
        Path is null ? "Untitled" : System.IO.Path.GetFileName(Path);

    /// <summary>
    /// Gets the Avalonia preview (null when empty).
    /// </summary>
    public WriteableBitmap? Preview => _preview;

    /// <summary>
    /// Loads an image or .ora from disk into the session (clears history). Prefer <see cref="OpenAsync"/>.
    /// </summary>
    public void Open(string path)
    {
        var doc = LoadDocument(path);
        Replace(doc, path, clearHistory: true, dirty: false);
    }

    /// <summary>
    /// Loads off the UI thread, then swaps state on the caller context.
    /// </summary>
    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var doc = await Task.Run(() => LoadDocument(path), cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        Replace(doc, path, clearHistory: true, dirty: false);
    }

    /// <summary>
    /// Saves the document (.ora) or composite (other formats).
    /// Updates <see cref="Path"/> and clears dirty.
    /// </summary>
    public void Save(string path, IImageEncoder? encoder = null)
    {
        if (_document is null || _composite is null)
            throw new InvalidOperationException("No image loaded.");
        WritePath(_document, _composite, path, encoder);
        Path = path;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves off the UI thread, then updates path and clears dirty on the caller context.
    /// </summary>
    public async Task SaveAsync(
        string path,
        IImageEncoder? encoder = null,
        CancellationToken cancellationToken = default
    )
    {
        if (_document is null || _composite is null)
            throw new InvalidOperationException("No image loaded.");

        var docSnap = _document.Clone();
        var compositeSnap = _composite.Clone();
        await Task.Run(() => WritePath(docSnap, compositeSnap, path, encoder), cancellationToken)
            .ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        Path = path;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Writes a flattened copy with the given encoder. Does not change <see cref="Path"/> or dirty.
    /// </summary>
    public async Task ExportAsync(
        string path,
        IImageEncoder encoder,
        CancellationToken cancellationToken = default
    )
    {
        if (_composite is null)
            throw new InvalidOperationException("No image loaded.");

        ArgumentNullException.ThrowIfNull(encoder);
        var snapshot = _composite.Clone();
        await Task.Run(() => WriteFile(snapshot, path, encoder), cancellationToken)
            .ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Restores the document as opened. Pushes the current state onto undo (so Reset itself is undoable).
    /// </summary>
    public void Reset()
    {
        if (_original is null || _document is null)
            return;

        EndPreview();
        Push(_undo, _document.Clone());
        Clear(_redo);
        _document = _original.Clone();
        IsDirty = false;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Mutates the active layer (snapshots for undo first) and refreshes the composite preview.
    /// Prefer <see cref="MutateAsync"/> from the UI.
    /// </summary>
    public void Mutate(Action<IImageProcessingContext> action)
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        EndPreview();
        Push(_undo, _document.Clone());
        Clear(_redo);
        _document.ActiveLayer.Pixels.Mutate(action);
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clones the document, mutates the active layer on a worker, then commits undo + preview.
    /// </summary>
    public async Task MutateAsync(
        Action<IImageProcessingContext> action,
        CancellationToken cancellationToken = default
    )
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        EndPreview();
        var undoSnapshot = _document.Clone();
        var work = _document.Clone();

        try
        {
            await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        work.ActiveLayer.Pixels.Mutate(action);
                    },
                    cancellationToken
                )
                .ConfigureAwait(true);
        }
        catch
        {
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Push(_undo, undoSnapshot);
        Clear(_redo);
        _document = work;
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on a clone of the active layer and updates only the
    /// display bitmap (full composite with that preview layer). Does not touch undo/redo or dirty.
    /// </summary>
    public async Task PreviewAsync(
        Action<IImageProcessingContext> action,
        CancellationToken cancellationToken = default
    )
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        var generation = Interlocked.Increment(ref _previewGeneration);
        var work = _document.Clone();

        await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    work.ActiveLayer.Pixels.Mutate(action);
                },
                cancellationToken
            )
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        if (generation != _previewGeneration)
            return;

        var composite = work.Composite();
        _preview?.Dispose();
        _preview = AvaloniaImageBridge.ToBitmap(composite);
        IsPreviewing = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Drops a live preview and shows the committed composite again.
    /// </summary>
    public void ClearPreview()
    {
        var wasPreviewing = IsPreviewing;
        EndPreview();
        if (!wasPreviewing)
            return;

        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restores the previous document snapshot.
    /// </summary>
    public bool Undo()
    {
        if (_document is null || _undo.Count == 0)
            return false;

        EndPreview();
        Push(_redo, _document);
        _document = Pop(_undo);
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Re-applies a document snapshot undone earlier.
    /// </summary>
    public bool Redo()
    {
        if (_document is null || _redo.Count == 0)
            return false;

        EndPreview();
        Push(_undo, _document);
        _document = Pop(_redo);
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Pushes undo, runs <paramref name="edit"/> on the document, recomposites.
    /// </summary>
    public void EditDocument(Action<LayerDocument> edit)
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        ArgumentNullException.ThrowIfNull(edit);
        EndPreview();
        Push(_undo, _document.Clone());
        Clear(_redo);
        edit(_document);
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs a filter macro on the active layer as a single undo step.
    /// </summary>
    public async Task ApplyMacroAsync(
        MacroDefinition macro,
        CancellationToken cancellationToken = default
    )
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        ArgumentNullException.ThrowIfNull(macro);
        var missing = MacroRunner.MissingIds(macro);
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Unknown filter id(s): " + string.Join(", ", missing)
            );

        EndPreview();
        var undoSnapshot = _document.Clone();
        var work = _document.Clone();

        await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    MacroRunner.Apply(work.ActiveLayer.Pixels, macro, cancellationToken);
                },
                cancellationToken
            )
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        Push(_undo, undoSnapshot);
        Clear(_redo);
        _document = work;
        IsDirty = true;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Crops the active layer to a document-space rectangle (one undo step).
    /// Returns the cropped pixel size, or null if the rect misses the layer.
    /// </summary>
    public (int Width, int Height)? CropActiveLayer(int docX, int docY, int width, int height)
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");
        if (width < 1 || height < 1)
            return null;

        var layer = _document.ActiveLayer;
        var localX = docX - layer.X;
        var localY = docY - layer.Y;
        var layerW = layer.Pixels.Width;
        var layerH = layer.Pixels.Height;

        var x0 = Math.Clamp(localX, 0, layerW);
        var y0 = Math.Clamp(localY, 0, layerH);
        var x1 = Math.Clamp(localX + width, 0, layerW);
        var y1 = Math.Clamp(localY + height, 0, layerH);
        var w = x1 - x0;
        var h = y1 - y0;
        if (w < 1 || h < 1)
            return null;

        var bounds = new Rectangle(x0, y0, w, h);
        EditDocument(d =>
        {
            d.ActiveLayer.Pixels.Mutate(ctx => ctx.Crop(bounds));
            d.ActiveLayer.X += x0;
            d.ActiveLayer.Y += y0;
        });
        return (w, h);
    }

    /// <summary>
    /// Changes the active layer without pushing undo or marking dirty.
    /// </summary>
    public void SelectLayer(int index)
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");

        if (_document.ActiveIndex == index)
            return;

        EndPreview();
        _document.ActiveIndex = index;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Temporarily shows the document-open original composite (hold-to-compare).
    /// </summary>
    public void BeginCompare()
    {
        if (_original is null || _comparing)
            return;

        EndPreview();
        var composite = _original.Composite();
        _preview?.Dispose();
        _preview = AvaloniaImageBridge.ToBitmap(composite);
        _comparing = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Restores the committed display after <see cref="BeginCompare"/>.
    /// </summary>
    public void EndCompare()
    {
        if (!_comparing)
            return;

        _comparing = false;
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets whether hold-to-compare is showing the open-original composite.
    /// </summary>
    public bool IsComparing => _comparing;

    /// <summary>
    /// Formats PSNR/SSIM of the committed composite vs the document-open original (same size only).
    /// </summary>
    public string? SimilarityVsOriginalLabel()
    {
        if (_original is null || _composite is null)
            return null;

        var original = _original.Composite();
        if (original.Width != _composite.Width || original.Height != _composite.Height)
            return null;

        var psnr = ImageSimilarity.Psnr(original, _composite);
        var ssim = ImageSimilarity.Ssim(original, _composite);
        var psnrText = double.IsPositiveInfinity(psnr) ? "∞" : $"{psnr:0.0} dB";
        return $"PSNR {psnrText} · SSIM {ssim:0.000}";
    }

    /// <summary>
    /// Builds a small preview bitmap of <paramref name="entry"/> on a downscaled active-layer clone (E3).
    /// </summary>
    public WriteableBitmap? CreateFilterVariantThumb(FilterEntry entry, FilterValues values, int maxEdge = 64)
    {
        if (_document?.ActiveLayer is null)
            return null;

        var source = _document.ActiveLayer.Pixels;
        if (source.Width <= 0 || source.Height <= 0)
            return null;

        var scale = Math.Min(1.0, maxEdge / (double)Math.Max(source.Width, source.Height));
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        var thumb = source.Clone();
        thumb.Mutate(ctx =>
        {
            if (w != source.Width || h != source.Height)
                ctx.Resize(w, h);
            entry.Apply(ctx, values);
        });
        return AvaloniaImageBridge.ToBitmap(thumb);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        EndPreview();
        _preview?.Dispose();
        _preview = null;
        Clear(_undo);
        Clear(_redo);
        _composite = null;
        _document = null;
        _original = null;
        IsDirty = false;
    }

    private static LayerDocument LoadDocument(string path) =>
        OraCodec.IsOraPath(path) ? OraCodec.Read(path) : LayerDocument.FromImage(Image.Load(path));

    private static void WritePath(
        LayerDocument document,
        Image composite,
        string path,
        IImageEncoder? encoder
    )
    {
        if (OraCodec.IsOraPath(path))
            OraCodec.Write(path, document);
        else
            WriteFile(composite, path, encoder);
    }

    private static void WriteFile(Image image, string path, IImageEncoder? encoder)
    {
        if (encoder is null)
            image.Save(path);
        else
            image.Save(path, encoder);
    }

    private void Replace(LayerDocument document, string? path, bool clearHistory, bool dirty)
    {
        EndPreview();
        _preview?.Dispose();
        if (clearHistory)
        {
            Clear(_undo);
            Clear(_redo);
        }

        _original = document.Clone();
        _document = document;
        Path = path;
        IsDirty = dirty;
        RebuildComposite();
        RefreshPreview();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildComposite() => _composite = _document?.Composite();

    private void EndPreview()
    {
        Interlocked.Increment(ref _previewGeneration);
        IsPreviewing = false;
        _comparing = false;
    }

    private void RefreshPreview()
    {
        _preview?.Dispose();
        _preview = _composite is null ? null : AvaloniaImageBridge.ToBitmap(_composite);
    }

    private static void Push(List<LayerDocument> history, LayerDocument snapshot)
    {
        history.Add(snapshot);
        while (history.Count > MaxHistory)
            history.RemoveAt(0);
    }

    private static LayerDocument Pop(List<LayerDocument> history)
    {
        var last = history[^1];
        history.RemoveAt(history.Count - 1);
        return last;
    }

    private static void Clear(List<LayerDocument> history) => history.Clear();
}
