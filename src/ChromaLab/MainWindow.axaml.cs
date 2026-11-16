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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChromaLab.Controls;
using ChromaLab.Dialogs;
using ChromaLab.Filters;
using ChromaLab.Services;

namespace ChromaLab;

public partial class MainWindow : Window
{
    private const double MinZoom = 0.05;
    private const double MaxZoom = 32.0;
    private const double ZoomStep = 1.25;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tga",
        ".tif",
        ".tiff",
        ".ico",
        ".webp",
        ".avif",
        ".jxl",
        ".ora",
    };

    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns =
        [
            "*.png",
            "*.jpg",
            "*.jpeg",
            "*.gif",
            "*.bmp",
            "*.tga",
            "*.tif",
            "*.tiff",
            "*.ico",
            "*.webp",
            "*.avif",
            "*.jxl",
            "*.ora",
        ],
    };

    private const int PreviewDebounceMs = 100;

    private readonly ImageSession _session = new();
    private readonly ScaleTransform _zoomTransform = new(1, 1);
    private readonly List<Control> _filterControls = [];
    private CancellationTokenSource? _previewCts;
    private double _zoom = 1.0;
    private bool _fitPending;
    private bool _panning;
    private bool _busy;
    private bool _forceClose;
    private Point _panStart;
    private Vector _panOffsetStart;

    public MainWindow()
    {
        InitializeComponent();
        ZoomHost.LayoutTransform = _zoomTransform;
        ApplyMenuGestures();
        BuildFilterUi();
        Inspector.ApplyRequested += (_, args) => TryMutate(args.Entry, args.Values);
        Inspector.ValuesChanged += (_, args) => SchedulePreview(args.Entry, args.Values);
        Inspector.CancelRequested += (_, _) => CancelInspectorPreview();
        _session.Changed += (_, _) => OnSessionChanged();

        Wire(OnOpenClick, OpenMenuItem, OpenButton);
        Wire(OnSaveClick, SaveMenuItem, SaveButton);
        Wire(OnSaveAsClick, SaveAsMenuItem);
        Wire(OnExportClick, ExportMenuItem, ExportButton);
        Wire((_, _) => TryUndo(), UndoMenuItem, UndoButton);
        Wire((_, _) => TryRedo(), RedoMenuItem, RedoButton);
        Wire(OnResetClick, ResetMenuItem, ResetButton);
        Wire((_, _) => ZoomBy(ZoomStep), ZoomInMenuItem, ZoomInButton);
        Wire((_, _) => ZoomBy(1.0 / ZoomStep), ZoomOutMenuItem, ZoomOutButton);
        Wire((_, _) => SetZoom(1.0), ActualSizeMenuItem, ActualSizeButton);
        Wire((_, _) => FitToWindow(), FitMenuItem, FitButton);
        Wire((_, _) => EditLayers(d => d.AddEmptyLayer()), LayerAddMenuItem);
        Wire((_, _) => EditLayers(d => d.DuplicateActive()), LayerDuplicateMenuItem);
        Wire((_, _) => EditLayers(d => d.TryDeleteActive()), LayerDeleteMenuItem);
        Wire((_, _) => EditLayers(d => d.SelectPrevious()), LayerPrevMenuItem);
        Wire((_, _) => EditLayers(d => d.SelectNext()), LayerNextMenuItem);
        Wire((_, _) => EditLayers(d => d.ToggleActiveVisible()), LayerToggleVisibleMenuItem);
        ExitMenuItem.Click += (_, _) => Close();

        PreviewScroll.PointerWheelChanged += OnPreviewWheel;
        PreviewFrame.PointerPressed += OnPreviewPointerPressed;
        PreviewFrame.PointerMoved += OnPreviewPointerMoved;
        PreviewFrame.PointerReleased += OnPreviewPointerReleased;
        PreviewFrame.PointerCaptureLost += (_, _) => _panning = false;
        PreviewScroll.SizeChanged += (_, _) =>
        {
            UpdateContentHostSize();
            if (_fitPending && _session.HasImage)
                FitToWindow();
        };

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // Tunnel so shortcuts work even when focus is in the scroll viewer / menus.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        RefreshUi();
        UpdateZoomLabel();
        UpdateMeta();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose || e.Cancel)
            return;

        if (_busy)
        {
            e.Cancel = true;
            StatusText.Text = "Wait for the current operation to finish.";
            return;
        }

        if (!_session.IsDirty)
            return;

        e.Cancel = true;
        _ = HandleDirtyCloseAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelPreviewWork();
        _session.Dispose();
        base.OnClosed(e);
    }

    private static void Wire(EventHandler<RoutedEventArgs> handler, params Control[] controls)
    {
        foreach (var control in controls)
        {
            switch (control)
            {
                case Button button:
                    button.Click += handler;
                    break;
                case MenuItem menuItem:
                    menuItem.Click += handler;
                    break;
            }
        }
    }

    private void BuildFilterUi()
    {
        FiltersMenu.Items.Clear();
        FavoriteTools.Children.Clear();
        _filterControls.Clear();

        foreach (var group in FilterCatalog.GroupsByOrder())
        {
            var submenu = new MenuItem { Header = group.Key };
            foreach (var entry in group)
            {
                var item = new MenuItem { Header = entry.Header, IsEnabled = false };
                var captured = entry;
                item.Click += (_, _) => OnFilterChosen(captured);
                submenu.Items.Add(item);
                _filterControls.Add(item);
            }

            FiltersMenu.Items.Add(submenu);
        }

        var favorites = FilterCatalog.ToolbarFavorites.ToList();
        FavoriteToolsSep.IsVisible = favorites.Count > 0;
        var fill = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? Brushes.White;

        foreach (var entry in favorites)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                Fill = fill,
                Data = Geometry.Parse(entry.ToolbarIcon!),
            };
            var tip = entry.ToolbarTip ?? entry.DisplayName;
            if (entry.HasParams)
                tip += " — adjust in inspector";
            var button = new Button
            {
                Classes = { "tool" },
                Content = path,
                IsEnabled = false,
                [ToolTip.TipProperty] = tip,
            };
            var captured = entry;
            button.Click += (_, _) => OnFilterChosen(captured);
            FavoriteTools.Children.Add(button);
            _filterControls.Add(button);
        }
    }

    /// <summary>
    /// Parametric filters open the inspector (live preview); param-less filters apply immediately.
    /// </summary>
    private void OnFilterChosen(FilterEntry entry)
    {
        if (_busy || !_session.HasImage)
            return;

        if (entry.HasParams)
        {
            // Select raises ValuesChanged → debounced preview.
            Inspector.Select(entry);
            StatusText.Text = $"{entry.DisplayName} — preview; Apply to commit";
            return;
        }

        CancelInspectorPreview();
        TryMutate(entry, entry.CreateDefaultValues());
    }

    private void SchedulePreview(FilterEntry entry, FilterValues values)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelPreviewWork();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        var valuesCopy = values.Clone();
        _ = RunPreviewAsync(entry, valuesCopy, ct);
    }

    private async Task RunPreviewAsync(
        FilterEntry entry,
        FilterValues values,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Task.Delay(PreviewDebounceMs, cancellationToken).ConfigureAwait(true);
            await _session
                .PreviewAsync(ctx => entry.Apply(ctx, values), cancellationToken)
                .ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested)
                StatusText.Text = $"Preview · {entry.DisplayName}";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer preview or Apply/Cancel.
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                StatusText.Text = $"Preview failed: {ex.Message}";
        }
    }

    private void CancelInspectorPreview()
    {
        CancelPreviewWork();
        _session.ClearPreview();
        if (!_busy && _session.HasImage && Inspector.SelectedEntry is null)
            StatusText.Text = "Ready";
    }

    private void CancelPreviewWork()
    {
        if (_previewCts is null)
            return;
        _previewCts.Cancel();
        _previewCts.Dispose();
        _previewCts = null;
    }

    private void ApplyMenuGestures()
    {
        // Display only — handling is in OnKeyDown (Ctrl on Win/Linux, Cmd on macOS).
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        OpenMenuItem.InputGesture = new KeyGesture(Key.O, primary);
        SaveMenuItem.InputGesture = new KeyGesture(Key.S, primary);
        SaveAsMenuItem.InputGesture = new KeyGesture(Key.S, primary | KeyModifiers.Shift);
        ExportMenuItem.InputGesture = new KeyGesture(Key.E, primary);
        UndoMenuItem.InputGesture = new KeyGesture(Key.Z, primary);
        RedoMenuItem.InputGesture = new KeyGesture(Key.Y, primary);
        ZoomInMenuItem.InputGesture = new KeyGesture(Key.OemPlus, primary);
        ZoomOutMenuItem.InputGesture = new KeyGesture(Key.OemMinus, primary);
        ActualSizeMenuItem.InputGesture = new KeyGesture(Key.D0, primary);
        FitMenuItem.InputGesture = new KeyGesture(Key.D1, primary);
    }

    private static bool HasPrimaryModifier(KeyEventArgs e) =>
        e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_busy || !HasPrimaryModifier(e) || e.Handled)
            return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.O when !shift:
                OnOpenClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S when shift:
                if (_session.HasImage)
                    OnSaveAsClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S:
                if (_session.HasImage)
                    OnSaveClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.E when !shift:
                if (_session.HasImage)
                    OnExportClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Z when shift:
                TryRedo();
                e.Handled = true;
                break;
            case Key.Z:
                TryUndo();
                e.Handled = true;
                break;
            case Key.Y when !shift:
                TryRedo();
                e.Handled = true;
                break;
            case Key.D0 or Key.NumPad0 when !shift:
                SetZoom(1.0);
                e.Handled = true;
                break;
            case Key.D1 or Key.NumPad1 when !shift:
                FitToWindow();
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add:
                ZoomBy(ZoomStep);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomBy(1.0 / ZoomStep);
                e.Handled = true;
                break;
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (!await ConfirmDiscardIfNeededAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open image",
                AllowMultiple = false,
                FileTypeFilter = [ImageFiles, FilePickerFileTypes.All],
            }
        );
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            StatusText.Text = "Could not resolve a local path for that file.";
            return;
        }

        await OpenPathAsync(path);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;

        await SaveDocumentAsync();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;

        await SaveAsAsync();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;

        await ExportAsync();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;

        // Reset is undoable — no confirm dialog.
        try
        {
            CancelPreviewWork();
            _session.Reset();
            StatusText.Text = $"Reset · {_session.SizeLabel}";
            UpdateMeta();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Reset failed: {ex.Message}";
        }
    }

    private void TryUndo()
    {
        if (_busy)
            return;
        CancelPreviewWork();
        if (_session.Undo())
        {
            StatusText.Text = $"Undo · {_session.SizeLabel}";
            UpdateMeta();
        }
    }

    private void TryRedo()
    {
        if (_busy)
            return;
        CancelPreviewWork();
        if (_session.Redo())
        {
            StatusText.Text = $"Redo · {_session.SizeLabel}";
            UpdateMeta();
        }
    }

    private async void TryMutate(FilterEntry entry, FilterValues values)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelPreviewWork();
        var label = entry.DisplayName;
        SetBusy(true, $"{label}…");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _session.MutateAsync(ctx => entry.Apply(ctx, values));
            sw.Stop();
            StatusText.Text = $"{label} · {_session.SizeLabel} · {sw.ElapsedMilliseconds} ms";
            UpdateMeta();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{label} failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = CanAcceptDrop(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_busy || !CanAcceptDrop(e))
            return;

        var path = GetDroppedImagePath(e);
        if (path is null)
            return;

        if (!await ConfirmDiscardIfNeededAsync())
            return;

        await OpenPathAsync(path);
    }

    private static bool CanAcceptDrop(DragEventArgs e) => GetDroppedImagePath(e) is not null;

    private static string? GetDroppedImagePath(DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return null;

        var file = e.DataTransfer.TryGetFile();
        var path = file?.TryGetLocalPath();
        if (path is null)
            return null;

        var ext = System.IO.Path.GetExtension(path);
        return ImageExtensions.Contains(ext) ? path : null;
    }

    private async Task OpenPathAsync(string path)
    {
        CancelInspectorPreview();
        Inspector.Clear();
        SetBusy(true, "Opening…");
        try
        {
            await _session.OpenAsync(path);
            _fitPending = true;
            StatusText.Text = $"Opened {_session.DisplayName}";
            UpdateMeta();
            Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Open failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Save to the current path, or Save As when untitled.
    /// </summary>
    private async Task<bool> SaveDocumentAsync()
    {
        if (_session.Path is null)
            return await SaveAsAsync();

        var encoder = ExportFormatCatalog.CreateEncoderForPath(_session.Path);
        SetBusy(true, "Saving…");
        try
        {
            await _session.SaveAsync(_session.Path, encoder);
            StatusText.Text = $"Saved → {_session.DisplayName}";
            UpdateMeta();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Save As with format + encoder options. Rebinds the document path.
    /// </summary>
    private async Task<bool> SaveAsAsync()
    {
        var result = await ExportDialog.ShowAsync(
            this,
            ExportDialogMode.SaveAs,
            _session.Path,
            _session.DisplayName
        );
        if (result is null)
            return false;

        SetBusy(true, "Saving…");
        try
        {
            await _session.SaveAsync(result.Path, result.Encoder);
            StatusText.Text = $"Saved → {_session.DisplayName} ({result.Format.DisplayName})";
            UpdateMeta();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Export a copy with format + encoder options (document path unchanged).
    /// </summary>
    private async Task ExportAsync()
    {
        var result = await ExportDialog.ShowAsync(
            this,
            ExportDialogMode.Export,
            SuggestExportPath(),
            _session.DisplayName
        );
        if (result is null)
            return;

        SetBusy(true, "Exporting…");
        try
        {
            await _session.ExportAsync(result.Path, result.Encoder);
            StatusText.Text =
                $"Exported → {System.IO.Path.GetFileName(result.Path)} ({result.Format.DisplayName})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string? SuggestExportPath()
    {
        if (_session.Path is null)
            return null;
        var dir = System.IO.Path.GetDirectoryName(_session.Path) ?? "";
        var name = System.IO.Path.GetFileNameWithoutExtension(_session.Path);
        return System.IO.Path.Combine(dir, name + "-export.png");
    }

    /// <summary>
    /// If dirty, prompts Save / Don't Save / Cancel. Returns false when the caller should abort.
    /// </summary>
    private async Task<bool> ConfirmDiscardIfNeededAsync()
    {
        if (!_session.IsDirty)
            return true;

        var result = await ConfirmDialog.ShowAsync(
            this,
            $"Save changes to “{_session.DisplayName}” before continuing?"
        );

        return result switch
        {
            ConfirmResult.Discard => true,
            ConfirmResult.Cancel => false,
            ConfirmResult.Save => await SaveDocumentAsync(),
            _ => false,
        };
    }

    private async Task HandleDirtyCloseAsync()
    {
        var result = await ConfirmDialog.ShowAsync(
            this,
            $"Save changes to “{_session.DisplayName}” before quitting?"
        );

        switch (result)
        {
            case ConfirmResult.Cancel:
                return;
            case ConfirmResult.Discard:
                _forceClose = true;
                Close();
                return;
            case ConfirmResult.Save:
                if (await SaveDocumentAsync())
                {
                    _forceClose = true;
                    Close();
                }
                return;
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        if (status is not null)
            StatusText.Text = status;
        Cursor = busy ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
        RefreshUi();
    }

    private void OnSessionChanged()
    {
        RefreshUi();
        UpdateMeta();
        // Keep current zoom across filter/undo; only open requests fit.
    }

    private void OnPreviewWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_session.HasImage || _busy)
            return;

        var factor = e.Delta.Y > 0 ? ZoomStep : 1.0 / ZoomStep;
        var anchor = e.GetPosition(PreviewScroll);
        ZoomBy(factor, anchor);
        e.Handled = true;
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_session.HasImage || _busy)
            return;

        var point = e.GetCurrentPoint(PreviewFrame);
        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _panning = true;
            _panStart = e.GetPosition(PreviewScroll);
            _panOffsetStart = PreviewScroll.Offset;
            e.Pointer.Capture(PreviewFrame);
            e.Handled = true;
        }
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning)
            return;

        var pos = e.GetPosition(PreviewScroll);
        var delta = pos - _panStart;
        PreviewScroll.Offset = new Vector(
            Math.Max(0, _panOffsetStart.X - delta.X),
            Math.Max(0, _panOffsetStart.Y - delta.Y)
        );
        e.Handled = true;
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_panning)
            return;
        _panning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ZoomBy(double factor, Point? viewportAnchor = null)
    {
        SetZoom(_zoom * factor, viewportAnchor);
    }

    private void SetZoom(double zoom, Point? viewportAnchor = null)
    {
        if (!_session.HasImage)
            return;

        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var oldZoom = _zoom;
        if (Math.Abs(zoom - oldZoom) < 0.0001)
        {
            UpdateZoomLabel();
            return;
        }

        var anchor =
            viewportAnchor
            ?? new Point(PreviewScroll.Viewport.Width / 2, PreviewScroll.Viewport.Height / 2);
        var offset = PreviewScroll.Offset;
        var contentX = (offset.X + anchor.X) / oldZoom;
        var contentY = (offset.Y + anchor.Y) / oldZoom;

        _zoom = zoom;
        _fitPending = false;
        ApplyZoomTransform();

        UpdateContentHostSize();

        // Defer offset until layout applies the new scale.
        Dispatcher.UIThread.Post(
            () =>
            {
                PreviewScroll.Offset = new Vector(
                    Math.Max(0, contentX * _zoom - anchor.X),
                    Math.Max(0, contentY * _zoom - anchor.Y)
                );
            },
            DispatcherPriority.Loaded
        );

        UpdateZoomLabel();
    }

    private void FitToWindow()
    {
        if (!_session.HasImage || _session.Preview is null)
            return;

        var viewport = PreviewScroll.Viewport;
        if (viewport.Width <= 1 || viewport.Height <= 1)
        {
            _fitPending = true;
            return;
        }

        var imgW = _session.Preview.PixelSize.Width;
        var imgH = _session.Preview.PixelSize.Height;
        if (imgW <= 0 || imgH <= 0)
            return;

        // Small margin so the frame isn't flush against scroll edges.
        const double margin = 16;
        var zoomX = (viewport.Width - margin) / imgW;
        var zoomY = (viewport.Height - margin) / imgH;
        var zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);

        _zoom = zoom;
        _fitPending = false;
        ApplyZoomTransform();
        UpdateContentHostSize();
        Dispatcher.UIThread.Post(
            () =>
            {
                PreviewScroll.Offset = new Vector(0, 0);
                CenterIfFits();
            },
            DispatcherPriority.Loaded
        );
        UpdateZoomLabel();
    }

    private void ApplyZoomTransform()
    {
        _zoomTransform.ScaleX = _zoom;
        _zoomTransform.ScaleY = _zoom;
    }

    /// <summary>
    /// Makes the scroll content at least as large as the viewport so Center alignment works.
    /// </summary>
    private void UpdateContentHostSize()
    {
        var viewport = PreviewScroll.Viewport;
        double contentW = 0;
        double contentH = 0;
        if (_session.Preview is { } bmp)
        {
            contentW = bmp.PixelSize.Width * _zoom;
            contentH = bmp.PixelSize.Height * _zoom;
        }

        PreviewContentHost.MinWidth = Math.Max(viewport.Width, contentW);
        PreviewContentHost.MinHeight = Math.Max(viewport.Height, contentH);
    }

    /// <summary>
    /// When the scaled image is smaller than the viewport, scroll so it sits in the middle.
    /// </summary>
    private void CenterIfFits()
    {
        var viewport = PreviewScroll.Viewport;
        var extent = PreviewScroll.Extent;
        PreviewScroll.Offset = new Vector(
            Math.Max(0, (extent.Width - viewport.Width) / 2),
            Math.Max(0, (extent.Height - viewport.Height) / 2)
        );
    }

    private void UpdateZoomLabel()
    {
        ZoomText.Text = _session.HasImage ? $"{_zoom * 100:0.#}%" : "—";
    }

    private void UpdateMeta()
    {
        if (!_session.HasImage)
        {
            MetaText.Text = "";
            Title = "ChromaLab";
            return;
        }

        var dirty = _session.IsDirty ? "• " : "";
        var layer = string.IsNullOrEmpty(_session.LayerLabel) ? "" : $" · {_session.LayerLabel}";
        MetaText.Text = $"{dirty}{_session.DisplayName} · {_session.SizeLabel}{layer}";
        Title = $"{dirty}{_session.DisplayName} — ChromaLab";
    }

    private void EditLayers(Action<Layers.LayerDocument> edit)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelPreviewWork();
        try
        {
            _session.EditDocument(edit);
            StatusText.Text = _session.LayerLabel;
            UpdateMeta();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Layers: {ex.Message}";
        }
    }

    private void RefreshUi()
    {
        var has = _session.HasImage;
        var idle = !_busy;
        var canEdit = has && idle;
        var multiLayer = has && (_session.Document?.Layers.Count ?? 0) > 1;

        SetEnabled(idle, OpenMenuItem, OpenButton, ExitMenuItem);
        SetEnabled(canEdit, SaveMenuItem, SaveButton, SaveAsMenuItem, ExportMenuItem, ExportButton);
        SetEnabled(canEdit && _session.CanUndo, UndoMenuItem, UndoButton);
        SetEnabled(canEdit && _session.CanRedo, RedoMenuItem, RedoButton);
        SetEnabled(canEdit, ResetMenuItem, ResetButton);
        SetEnabled(
            canEdit,
            ZoomInMenuItem,
            ZoomInButton,
            ZoomOutMenuItem,
            ZoomOutButton,
            ActualSizeMenuItem,
            ActualSizeButton,
            FitMenuItem,
            FitButton
        );
        SetEnabled(
            canEdit,
            LayerAddMenuItem,
            LayerDuplicateMenuItem,
            LayerPrevMenuItem,
            LayerNextMenuItem,
            LayerToggleVisibleMenuItem
        );
        SetEnabled(canEdit && multiLayer, LayerDeleteMenuItem);
        SetEnabled(canEdit, _filterControls.ToArray());
        Inspector.SetCanApply(canEdit);

        EmptyHint.IsVisible = !has;
        ZoomHost.IsVisible = has;
        PreviewImage.Source = _session.Preview;
        ApplyZoomTransform();
        UpdateContentHostSize();
        UpdateZoomLabel();
        UpdateMeta();

        if (!has)
        {
            _zoom = 1.0;
            if (!_busy && (StatusText.Text is null || StatusText.Text.Length == 0))
                StatusText.Text = "Ready";
        }
    }

    private static void SetEnabled(bool enabled, params Control[] controls)
    {
        foreach (var control in controls)
            control.IsEnabled = enabled;
    }
}
