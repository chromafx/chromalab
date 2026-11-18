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
using Avalonia.VisualTree;
using ChromaLab.Macros;
using ChromaLab.Services;

namespace ChromaLab;

public partial class MainWindow : Window
{
    private enum LeftRailMode
    {
        Tools,
        Filters,
    }

    private enum CanvasTool
    {
        Hand,
        Crop,
    }

    private const int PreviewDebounceMs = 100;

    private readonly ImageSession _session = new();
    private readonly PreviewViewport _viewport = new();
    private readonly CropController _crop = new();
    private readonly MacroRecorder _macroRecorder = new();
    private readonly List<Control> _filterMenuControls = [];
    private readonly List<Control> _alwaysWhenIdle = [];
    private readonly List<Control> _whenCanEdit = [];
    private readonly List<Control> _whenCanZoom = [];
    private CancellationTokenSource? _previewCts;
    private bool _busy;
    private bool _forceClose;
    private LeftRailMode _railMode = LeftRailMode.Tools;
    private CanvasTool _canvasTool = CanvasTool.Hand;

    public MainWindow()
    {
        InitializeComponent();
        _viewport.Attach(PreviewScroll, ZoomHost, PreviewContentHost, Checkerboard, CropOverlay);
        ApplyMenuGestures();
        ApplyToolbarTooltips();
        BuildFilterUi();
        CollectCommandGroups();
        WireShell();
        SetRailMode(LeftRailMode.Tools);
        SetCanvasTool(CanvasTool.Hand);
        RefreshUi();
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
        StopAnimationPlayback(restoreDocument: false);
        CancelPreviewWork();
        _session.Dispose();
        base.OnClosed(e);
    }

    private void WireShell()
    {
        Inspector.ApplyRequested += (_, args) => TryMutate(args.Entry, args.Values);
        Inspector.ValuesChanged += (_, args) => SchedulePreview(args.Entry, args.Values);
        Inspector.CancelRequested += (_, _) =>
        {
            FilterBrowser.SetSelectedEntry(null);
            CancelInspectorPreview();
        };
        Inspector.VariantPreviewFactory = (entry, values) =>
            _session.CreateFilterVariantThumb(entry, values);
        FilterBrowser.FilterChosen += (_, entry) => OnFilterChosen(entry);
        _session.Changed += (_, kind) => OnSessionChanged(kind);

        UiHelpers.Wire(OnOpenClick, OpenMenuItem, OpenButton, EmptyOpenButton);
        UiHelpers.Wire(OnSaveClick, SaveMenuItem, SaveButton);
        UiHelpers.Wire(OnSaveAsClick, SaveAsMenuItem);
        UiHelpers.Wire(OnExportClick, ExportMenuItem, ExportButton);
        UiHelpers.Wire((_, _) => TryUndo(), UndoMenuItem, UndoButton);
        UiHelpers.Wire((_, _) => TryRedo(), RedoMenuItem, RedoButton);
        UiHelpers.Wire(OnCanvasSizeClick, CanvasSizeMenuItem);
        UiHelpers.Wire(OnResetClick, ResetMenuItem, ResetButton);
        UiHelpers.Wire((_, _) => ZoomBy(PreviewViewport.ZoomStep), ZoomInMenuItem, ZoomInButton);
        UiHelpers.Wire(
            (_, _) => ZoomBy(1.0 / PreviewViewport.ZoomStep),
            ZoomOutMenuItem,
            ZoomOutButton
        );
        UiHelpers.Wire((_, _) => SetZoom(1.0), ActualSizeMenuItem, ActualSizeButton);
        UiHelpers.Wire((_, _) => FitToWindow(), FitMenuItem, FitButton);
        UiHelpers.Wire((_, _) => EditLayers(d => d.AddEmptyLayer()), LayerAddMenuItem);
        UiHelpers.Wire((_, _) => EditLayers(d => d.DuplicateActive()), LayerDuplicateMenuItem);
        UiHelpers.Wire((_, _) => EditLayers(d => d.TryDeleteActive()), LayerDeleteMenuItem);
        UiHelpers.Wire((_, _) => SelectAdjacentLayer(-1), LayerPrevMenuItem);
        UiHelpers.Wire((_, _) => SelectAdjacentLayer(1), LayerNextMenuItem);
        UiHelpers.Wire((_, _) => EditLayers(d => d.ToggleActiveVisible()), LayerToggleVisibleMenuItem);
        UiHelpers.Wire((_, _) => EditLayers(d => d.TryMoveActiveUp()), LayerMoveUpMenuItem);
        UiHelpers.Wire((_, _) => EditLayers(d => d.TryMoveActiveDown()), LayerMoveDownMenuItem);
        WireMacroMenus();
        WireAnimationUi();
        ExitMenuItem.Click += (_, _) => Close();

        ToolsTabButton.Click += (_, _) => SetRailMode(LeftRailMode.Tools);
        FiltersTabButton.Click += (_, _) => SetRailMode(LeftRailMode.Filters);
        HandToolButton.Click += (_, _) => SetCanvasTool(CanvasTool.Hand);
        CropToolButton.Click += (_, _) => SetCanvasTool(CanvasTool.Crop);
        ApplyCropButton.Click += (_, _) => ApplyCrop();
        CancelCropButton.Click += (_, _) => CancelCropOrExit();
        Rotate90Button.Click += (_, _) => ApplyCatalogId("rotate-90");
        Rotate180Button.Click += (_, _) => ApplyCatalogId("rotate-180");
        FlipHButton.Click += (_, _) => ApplyCatalogId("flip-h");
        FlipVButton.Click += (_, _) => ApplyCatalogId("flip-v");
        ResizeToolButton.Click += (_, _) => ApplyCatalogId("resize", stayOnTools: true);
        RotateToolButton.Click += (_, _) => ApplyCatalogId("rotate", stayOnTools: true);
        SkewToolButton.Click += (_, _) => ApplyCatalogId("skew", stayOnTools: true);

        LayersPane.AddRequested += (_, _) => EditLayers(d => d.AddEmptyLayer());
        LayersPane.DuplicateRequested += (_, _) => EditLayers(d => d.DuplicateActive());
        LayersPane.DeleteRequested += (_, _) => EditLayers(d => d.TryDeleteActive());
        LayersPane.MoveUpRequested += (_, _) => EditLayers(d => d.TryMoveActiveUp());
        LayersPane.MoveDownRequested += (_, _) => EditLayers(d => d.TryMoveActiveDown());
        LayersPane.LayerSelected += (_, index) => SelectLayerIndex(index);
        LayersPane.VisibilityChanged += (_, args) =>
            EditLayers(d => d.SetVisible(args.Index, args.Visible));

        CompareButton.AddHandler(
            PointerPressedEvent,
            (_, e) =>
            {
                BeginCompare();
                e.Handled = true;
            },
            RoutingStrategies.Tunnel
        );
        CompareButton.AddHandler(
            PointerReleasedEvent,
            (_, e) =>
            {
                EndCompare();
                e.Handled = true;
            },
            RoutingStrategies.Tunnel
        );
        CompareButton.PointerCaptureLost += (_, _) => EndCompare();

        PreviewScroll.PointerWheelChanged += OnPreviewWheel;
        PreviewFrame.PointerPressed += OnPreviewPointerPressed;
        PreviewFrame.PointerMoved += OnPreviewPointerMoved;
        PreviewFrame.PointerReleased += OnPreviewPointerReleased;
        PreviewFrame.PointerCaptureLost += (_, _) =>
        {
            _viewport.EndPan();
            _crop.EndDrag();
        };
        PreviewScroll.SizeChanged += (_, _) =>
        {
            _viewport.UpdateContentHostSize(PreviewPixelSize());
            if (_viewport.FitPending && _session.HasImage)
                FitToWindow();
        };

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
    }

    private void CollectCommandGroups()
    {
        _alwaysWhenIdle.AddRange(
            [OpenMenuItem, OpenButton, EmptyOpenButton, ExitMenuItem, MacroBulkMenuItem]
        );
        _whenCanEdit.AddRange(
            [
                SaveMenuItem,
                SaveButton,
                SaveAsMenuItem,
                ExportMenuItem,
                ExportButton,
                ResetMenuItem,
                ResetButton,
                CanvasSizeMenuItem,
                LayerAddMenuItem,
                LayerDuplicateMenuItem,
                LayerPrevMenuItem,
                LayerNextMenuItem,
                LayerToggleVisibleMenuItem,
                Rotate90Button,
                Rotate180Button,
                FlipHButton,
                FlipVButton,
                ResizeToolButton,
                RotateToolButton,
                SkewToolButton,
                HandToolButton,
                CropToolButton,
                MacroRunMenuItem,
            ]
        );
        _whenCanZoom.AddRange(
            [
                ZoomInMenuItem,
                ZoomInButton,
                ZoomOutMenuItem,
                ZoomOutButton,
                ActualSizeMenuItem,
                ActualSizeButton,
                FitMenuItem,
                FitButton,
                CompareButton,
                CompareMenuItem,
            ]
        );
    }

    private void SetRailMode(LeftRailMode mode)
    {
        _railMode = mode;
        var tools = mode == LeftRailMode.Tools;
        ToolsPane.IsVisible = tools;
        FilterBrowser.IsVisible = !tools;
        UiHelpers.SetSelectedClass(ToolsTabButton, tools);
        UiHelpers.SetSelectedClass(FiltersTabButton, !tools);
        if (!tools && _canvasTool == CanvasTool.Crop)
            SetCanvasTool(CanvasTool.Hand);
        UpdateAnimationUi();
    }

    private void SetCanvasTool(CanvasTool tool)
    {
        _canvasTool = tool;
        UiHelpers.SetSelectedClass(HandToolButton, tool == CanvasTool.Hand);
        UiHelpers.SetSelectedClass(CropToolButton, tool == CanvasTool.Crop);
        var cropping = tool == CanvasTool.Crop;
        CropHint.IsVisible = cropping;
        CropActions.IsVisible = cropping;
        CropOverlay.IsVisible = cropping;
        PreviewFrame.Cursor = cropping
            ? new Cursor(StandardCursorType.Cross)
            : new Cursor(StandardCursorType.SizeAll);
        if (!cropping)
            ClearCropSelection();
        else
            StatusText.Text = "Crop — drag a rectangle on the active layer";
    }

    private void ApplyMenuGestures()
    {
        var primary = PrimaryModifier();
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
        CompareMenuItem.InputGesture = new KeyGesture(Key.Space);
    }

    private void ApplyToolbarTooltips()
    {
        var mod = OperatingSystem.IsMacOS() ? "⌘" : "Ctrl+";
        ToolTip.SetTip(OpenButton, $"Open ({mod}O)");
        ToolTip.SetTip(SaveButton, $"Save ({mod}S)");
        ToolTip.SetTip(ExportButton, $"Export… ({mod}E)");
        ToolTip.SetTip(UndoButton, $"Undo ({mod}Z)");
        ToolTip.SetTip(RedoButton, $"Redo ({mod}Y)");
        ToolTip.SetTip(ResetButton, "Reset to original");
        ToolTip.SetTip(ZoomOutButton, $"Zoom out ({mod}-)");
        ToolTip.SetTip(ZoomInButton, $"Zoom in ({mod}=)");
        ToolTip.SetTip(ActualSizeButton, $"Actual size 100% ({mod}0)");
        ToolTip.SetTip(FitButton, $"Fit to window ({mod}1)");
        ToolTip.SetTip(CompareButton, "Hold to compare with original (Space)");
    }

    private static KeyModifiers PrimaryModifier() =>
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private static bool HasPrimaryModifier(KeyEventArgs e) =>
        e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

    private static bool IsTextInputFocused(object? source)
    {
        for (var current = source as Avalonia.Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox or ComboBox)
                return true;
        }

        return false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_busy || e.Handled)
            return;

        if (
            e.Key == Key.Space
            && !HasPrimaryModifier(e)
            && _session.HasImage
            && !IsTextInputFocused(e.Source)
        )
        {
            BeginCompare();
            e.Handled = true;
            return;
        }

        if (_canvasTool == CanvasTool.Crop && _crop.HasRect)
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                ApplyCrop();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ClearCropSelection();
                e.Handled = true;
                return;
            }
        }

        if (!HasPrimaryModifier(e))
            return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.F when !shift:
                SetRailMode(LeftRailMode.Filters);
                FilterBrowser.FocusSearch();
                e.Handled = true;
                break;
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
                ZoomBy(PreviewViewport.ZoomStep);
                e.Handled = true;
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomBy(1.0 / PreviewViewport.ZoomStep);
                e.Handled = true;
                break;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || IsTextInputFocused(e.Source))
            return;
        EndCompare();
        e.Handled = true;
    }

    private void BeginCompare()
    {
        if (_busy || !_session.HasImage || _session.IsComparing)
            return;
        StopAnimationPlayback(restoreDocument: true);
        CancelPreviewWork();
        _session.BeginCompare();
        var metrics = _session.SimilarityVsOriginalLabel();
        StatusText.Text = metrics is null
            ? "Comparing with original…"
            : $"Comparing · {metrics}";
    }

    private void EndCompare()
    {
        if (!_session.IsComparing)
            return;
        _session.EndCompare();
        if (_busy || !_session.HasImage)
            return;

        if (Inspector.SelectedEntry is not null)
        {
            StatusText.Text = $"{Inspector.SelectedEntry.DisplayName} — preview; Apply to commit";
            Inspector.NotifyValuesChanged();
            return;
        }

        StatusText.Text = "Ready";
    }

    private void SetBusy(bool busy, string? status = null)
    {
        if (busy)
            StopAnimationPlayback(restoreDocument: true);
        _busy = busy;
        if (status is not null)
            StatusText.Text = status;
        Cursor = busy ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
        BusyProgress.IsVisible = busy;
        RefreshCommandState();
        RefreshPreviewUi();
    }

    private void OnSessionChanged(SessionChangeKind kind)
    {
        if (!_session.HasAnimation && _animationPlaying)
            StopAnimationPlayback(restoreDocument: false);

        switch (kind)
        {
            case SessionChangeKind.PreviewOnly:
                RefreshPreviewUi();
                break;
            case SessionChangeKind.Selection:
                RefreshCommandState();
                LayersPane.RefreshSelection(_session.Document, _session.HasImage && !_busy);
                UpdateMeta();
                break;
            default:
                RefreshUi();
                break;
        }
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

    private void UpdateZoomLabel() =>
        ZoomText.Text = _session.HasImage ? _viewport.ZoomLabel : "—";

    private PixelSize? PreviewPixelSize() =>
        _session.Preview is { } bmp
            ? new PixelSize(bmp.PixelSize.Width, bmp.PixelSize.Height)
            : null;

    private void RefreshPreviewUi()
    {
        var has = _session.HasImage;
        EmptyHint.IsVisible = !has;
        ZoomHost.IsVisible = has;
        PreviewImage.Source = _session.Preview;
        _viewport.ApplyTransform();
        _viewport.UpdateContentHostSize(PreviewPixelSize());
        UpdateZoomLabel();
        UpdateAnimationUi();
    }

    private void RefreshCommandState()
    {
        var has = _session.HasImage;
        var idle = !_busy;
        var canEdit = has && idle;
        var multiLayer = has && (_session.Document?.Layers.Count ?? 0) > 1;
        var doc = _session.Document;

        UiHelpers.SetEnabled(idle, _alwaysWhenIdle);
        UiHelpers.SetEnabled(canEdit, _whenCanEdit);
        UiHelpers.SetEnabled(canEdit, _whenCanZoom);
        UiHelpers.SetEnabled(canEdit && _session.CanUndo, UndoMenuItem, UndoButton);
        UiHelpers.SetEnabled(canEdit && _session.CanRedo, RedoMenuItem, RedoButton);
        UiHelpers.SetEnabled(
            canEdit && multiLayer && doc is not null && doc.ActiveIndex < doc.Layers.Count - 1,
            LayerMoveUpMenuItem
        );
        UiHelpers.SetEnabled(
            canEdit && multiLayer && doc is not null && doc.ActiveIndex > 0,
            LayerMoveDownMenuItem
        );
        UiHelpers.SetEnabled(canEdit && multiLayer, LayerDeleteMenuItem);
        UiHelpers.SetEnabled(canEdit, _filterMenuControls);
        FilterBrowser.SetEntriesEnabled(canEdit);
        Inspector.SetCanApply(canEdit);
        RefreshMacroMenuUi(idle, canEdit);
        UpdateMeta();
    }

    private void RefreshUi()
    {
        var has = _session.HasImage;
        var canEdit = has && !_busy;
        var doc = _session.Document;

        RefreshCommandState();
        LayersPane.Refresh(doc, canEdit);
        RefreshPreviewUi();

        if (!has)
        {
            _viewport.Reset();
            ClearCropSelection();
            StopAnimationPlayback(restoreDocument: false);
            if (!_busy && string.IsNullOrEmpty(StatusText.Text))
                StatusText.Text = "Ready";
        }
    }
}
