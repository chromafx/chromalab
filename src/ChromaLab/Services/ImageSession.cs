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

    private readonly DocumentHistory _history = new();
    private LayerDocument? _original;
    private Image? _originalComposite;
    private LayerDocument? _document;
    private Image? _composite;
    private WriteableBitmap? _preview;
    private int _previewGeneration;
    private int _previewInFlight;
    private bool _comparing;
    private Animation? _animation;
    private bool _showingAnimationFrame;

    /// <summary>
    /// Raised after the working document or preview changes.
    /// </summary>
    public event EventHandler<SessionChangeKind>? Changed;

    public bool HasImage => _document is not null;
    public LayerDocument? Document => _document;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public bool IsPreviewing { get; private set; }
    public bool IsDirty { get; private set; }
    public string? Path { get; private set; }

    public string SizeLabel =>
        _document is null ? "No image" : $"{_document.Width}×{_document.Height}";

    public string LayerLabel => _document?.LayerStatusLabel ?? "";

    public string DisplayName =>
        Path is null ? "Untitled" : System.IO.Path.GetFileName(Path);

    public WriteableBitmap? Preview => _preview;
    public bool HasAnimation => _animation is { Count: > 1 };
    public int AnimationFrameCount => _animation?.Count ?? 0;
    public bool IsShowingAnimationFrame => _showingAnimationFrame;
    public bool IsComparing => _comparing;
    public ushort AnimationRepeatCount => _animation?.RepeatCount ?? 0;

    /// <summary>
    /// Loads off the UI thread, then swaps state on the caller context.
    /// </summary>
    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        var (doc, anim) = await Task.Run(() => DocumentStorage.Load(path), cancellationToken)
            .ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        Replace(doc, path, clearHistory: true, dirty: false, anim);
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

        var ora = OraCodec.IsOraPath(path);
        LayerDocument? docSnap = ora ? _document.Clone() : null;
        Image? compositeSnap = ora ? null : _composite.Clone();

        await Task.Run(
                () =>
                {
                    if (ora)
                        DocumentStorage.Write(docSnap!, null, path, encoder);
                    else
                        DocumentStorage.WriteFile(compositeSnap!, path, encoder);
                },
                cancellationToken
            )
            .ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        Path = path;
        IsDirty = false;
        RaiseChanged(SessionChangeKind.Full);
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
        await Task.Run(() => DocumentStorage.WriteFile(snapshot, path, encoder), cancellationToken)
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
        _history.PushUndo(_document.Clone());
        _document = _original.Clone();
        IsDirty = false;
        CommitDisplay(SessionChangeKind.Full);
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

        var composite = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    work.ActiveLayer.Pixels.Mutate(action);
                    return work.Composite();
                },
                cancellationToken
            )
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        _history.PushUndo(undoSnapshot);
        _document = work;
        _composite = composite;
        IsDirty = true;
        RefreshPreview();
        RaiseChanged(SessionChangeKind.Full);
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

        // Coalesce: only one preview mutation in flight; newer requests supersede via generation.
        var generation = Interlocked.Increment(ref _previewGeneration);
        while (Interlocked.CompareExchange(ref _previewInFlight, 1, 0) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            if (generation != _previewGeneration)
                return;
        }

        try
        {
            if (generation != _previewGeneration)
                return;

            var work = _document.Clone();
            var composite = await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        work.ActiveLayer.Pixels.Mutate(action);
                        cancellationToken.ThrowIfCancellationRequested();
                        return work.Composite();
                    },
                    cancellationToken
                )
                .ConfigureAwait(true);

            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _previewGeneration)
                return;

            _preview = AvaloniaImageBridge.UpdateOrCreate(_preview, composite);
            IsPreviewing = true;
            RaiseChanged(SessionChangeKind.PreviewOnly);
        }
        finally
        {
            Interlocked.Exchange(ref _previewInFlight, 0);
        }
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
        RaiseChanged(SessionChangeKind.PreviewOnly);
    }

    public bool Undo()
    {
        if (_document is null)
            return false;

        EndPreview();
        var previous = _history.TryUndo(_document);
        if (previous is null)
            return false;

        _document = previous;
        IsDirty = true;
        CommitDisplay(SessionChangeKind.Full);
        return true;
    }

    public bool Redo()
    {
        if (_document is null)
            return false;

        EndPreview();
        var next = _history.TryRedo(_document);
        if (next is null)
            return false;

        _document = next;
        IsDirty = true;
        CommitDisplay(SessionChangeKind.Full);
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
        _history.PushUndo(_document.Clone());
        edit(_document);
        IsDirty = true;
        CommitDisplay(SessionChangeKind.Full);
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
        MacroRunner.ValidateOrThrow(macro);

        EndPreview();
        var undoSnapshot = _document.Clone();
        var work = _document.Clone();

        var composite = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    MacroRunner.Apply(work.ActiveLayer.Pixels, macro, cancellationToken);
                    return work.Composite();
                },
                cancellationToken
            )
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        _history.PushUndo(undoSnapshot);
        _document = work;
        _composite = composite;
        IsDirty = true;
        RefreshPreview();
        RaiseChanged(SessionChangeKind.Full);
    }

    /// <summary>
    /// Changes the document canvas size (one undo step). Layer pixels/offsets unchanged.
    /// </summary>
    public void ResizeCanvas(int width, int height)
    {
        if (_document is null)
            throw new InvalidOperationException("No image loaded.");
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas size must be at least 1×1.");
        if (_document.Width == width && _document.Height == height)
            return;

        EndPreview();
        _history.PushUndo(_document.Clone());
        _document.SetCanvasSize(width, height);
        IsDirty = true;
        CommitDisplay(SessionChangeKind.Full);
    }

    /// <summary>
    /// Crops the active layer to a document-space rectangle (one undo step).
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
        RaiseChanged(SessionChangeKind.Selection);
    }

    /// <summary>
    /// Moves the active layer selection by one step (wraps).
    /// </summary>
    public void SelectAdjacentLayer(int delta)
    {
        if (_document is null || _document.Layers.Count == 0)
            return;

        EndPreview();
        if (delta < 0)
            _document.SelectPrevious();
        else
            _document.SelectNext();
        RaiseChanged(SessionChangeKind.Selection);
    }

    public void BeginCompare()
    {
        if (_original is null || _comparing)
            return;

        EndPreview();
        var composite = GetOriginalComposite();
        _preview = AvaloniaImageBridge.UpdateOrCreate(_preview, composite);
        _comparing = true;
        RaiseChanged(SessionChangeKind.PreviewOnly);
    }

    public void EndCompare()
    {
        if (!_comparing)
            return;

        _comparing = false;
        RefreshPreview();
        RaiseChanged(SessionChangeKind.PreviewOnly);
    }

    /// <summary>
    /// Formats PSNR/SSIM of the committed composite vs the document-open original (same size only).
    /// </summary>
    public string? SimilarityVsOriginalLabel()
    {
        if (_original is null || _composite is null)
            return null;

        var original = GetOriginalComposite();
        if (original.Width != _composite.Width || original.Height != _composite.Height)
            return null;

        var psnr = ImageSimilarity.Psnr(original, _composite);
        var ssim = ImageSimilarity.Ssim(original, _composite);
        var psnrText = double.IsPositiveInfinity(psnr) ? "∞" : $"{psnr:0.0} dB";
        return $"PSNR {psnrText} · SSIM {ssim:0.000}";
    }

    public void ShowAnimationFrame(int index)
    {
        if (_animation is null || _animation.Count == 0)
            return;

        index = Math.Clamp(index, 0, _animation.Count - 1);
        EndPreview();
        _comparing = false;
        _preview = AvaloniaImageBridge.UpdateOrCreate(_preview, _animation[index]);
        _showingAnimationFrame = true;
        RaiseChanged(SessionChangeKind.PreviewOnly);
    }

    public int GetAnimationFrameDelayMs(int index)
    {
        if (_animation is null || _animation.Count == 0)
            return 100;

        index = Math.Clamp(index, 0, _animation.Count - 1);
        var centiseconds = _animation.GetFrameDelay(index);
        return centiseconds <= 0 ? 100 : Math.Max(20, centiseconds * 10);
    }

    public void RestoreDocumentPreview()
    {
        if (!_showingAnimationFrame)
            return;
        _showingAnimationFrame = false;
        RefreshPreview();
        RaiseChanged(SessionChangeKind.PreviewOnly);
    }

    /// <summary>
    /// Builds a small preview bitmap of <paramref name="entry"/> on a downscaled active-layer clone.
    /// </summary>
    public WriteableBitmap? CreateFilterVariantThumb(
        FilterEntry entry,
        FilterValues values,
        int maxEdge = 64
    )
    {
        if (_document?.ActiveLayer is null)
            return null;

        var source = _document.ActiveLayer.Pixels;
        if (source.Width <= 0 || source.Height <= 0)
            return null;

        var thumb = AvaloniaImageBridge.CreateThumbnail(source, maxEdge);
        thumb.Mutate(ctx => entry.Apply(ctx, values));
        return AvaloniaImageBridge.ToBitmap(thumb);
    }

    public void Dispose()
    {
        EndPreview();
        _preview?.Dispose();
        _preview = null;
        _history.Clear();
        _composite = null;
        _originalComposite = null;
        _document = null;
        _original = null;
        _animation = null;
        _showingAnimationFrame = false;
        IsDirty = false;
    }

    private void Replace(
        LayerDocument document,
        string? path,
        bool clearHistory,
        bool dirty,
        Animation? animation = null
    )
    {
        EndPreview();
        _preview?.Dispose();
        _preview = null;
        if (clearHistory)
            _history.Clear();

        _original = document.Clone();
        _originalComposite = null;
        _document = document;
        Path = path;
        IsDirty = dirty;
        _animation = animation is { Count: > 1 } ? animation : null;
        _showingAnimationFrame = false;
        CommitDisplay(SessionChangeKind.Full);
    }

    private void CommitDisplay(SessionChangeKind kind)
    {
        RebuildComposite();
        RefreshPreview();
        RaiseChanged(kind);
    }

    private void RebuildComposite() => _composite = _document?.Composite();

    private Image GetOriginalComposite()
    {
        if (_original is null)
            throw new InvalidOperationException("No original document.");
        return _originalComposite ??= _original.Composite();
    }

    private void EndPreview()
    {
        Interlocked.Increment(ref _previewGeneration);
        IsPreviewing = false;
        _comparing = false;
        _showingAnimationFrame = false;
    }

    private void RefreshPreview()
    {
        if (_composite is null)
        {
            _preview?.Dispose();
            _preview = null;
        }
        else
        {
            _preview = AvaloniaImageBridge.UpdateOrCreate(_preview, _composite);
        }

        _showingAnimationFrame = false;
    }

    private void RaiseChanged(SessionChangeKind kind) => Changed?.Invoke(this, kind);
}
