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
using ChromaLab.Services;

namespace ChromaLab;

public partial class MainWindow
{
    private void OnPreviewWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_session.HasImage || _busy)
            return;

        var factor = e.Delta.Y > 0 ? PreviewViewport.ZoomStep : 1.0 / PreviewViewport.ZoomStep;
        ZoomBy(factor, e.GetPosition(PreviewScroll));
        e.Handled = true;
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_session.HasImage || _busy)
            return;

        var point = e.GetCurrentPoint(PreviewFrame);
        if (
            point.Properties.IsLeftButtonPressed
            && e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && TryPickFilterCenter(e.GetPosition(PreviewImage))
        )
        {
            e.Handled = true;
            return;
        }

        if (_canvasTool == CanvasTool.Crop && point.Properties.IsLeftButtonPressed)
        {
            var start = e.GetPosition(PreviewImage);
            _crop.Begin(start);
            UpdateCropOverlay(new Rect(start, new Size(0, 0)));
            e.Pointer.Capture(PreviewFrame);
            e.Handled = true;
            return;
        }

        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _viewport.BeginPan(e.GetPosition(PreviewScroll));
            e.Pointer.Capture(PreviewFrame);
            e.Handled = true;
        }
    }

    private bool TryPickFilterCenter(Point imagePoint)
    {
        var size = PreviewPixelSize();
        if (size is null || size.Value.Width <= 0 || size.Value.Height <= 0)
            return false;

        var nx = (float)Math.Clamp(imagePoint.X / size.Value.Width, 0, 1);
        var ny = (float)Math.Clamp(imagePoint.Y / size.Value.Height, 0, 1);
        if (!Inspector.TrySetNormalizedCenter(nx, ny))
            return false;

        StatusText.Text = $"Center · {nx:0.00}, {ny:0.00}";
        return true;
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_crop.IsDragging)
        {
            var rect = _crop.Update(e.GetPosition(PreviewImage));
            UpdateCropOverlay(rect);
            var w = Math.Max(0, (int)Math.Round(rect.Width));
            var h = Math.Max(0, (int)Math.Round(rect.Height));
            StatusText.Text = $"Crop {w}×{h}";
            e.Handled = true;
            return;
        }

        if (!_viewport.IsPanning)
            return;

        _viewport.MovePan(e.GetPosition(PreviewScroll));
        e.Handled = true;
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_crop.IsDragging)
        {
            var imageLocal = _crop.Update(e.GetPosition(PreviewImage));
            _crop.EndDrag();
            e.Pointer.Capture(null);
            CommitCropRect(imageLocal);
            e.Handled = true;
            return;
        }

        if (!_viewport.IsPanning)
            return;

        _viewport.EndPan();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ZoomBy(double factor, Point? viewportAnchor = null) =>
        SetZoom(_viewport.Zoom * factor, viewportAnchor);

    private void SetZoom(double zoom, Point? viewportAnchor = null)
    {
        _viewport.SetZoom(zoom, _session.HasImage, PreviewPixelSize(), viewportAnchor);
        UpdateZoomLabel();
    }

    private void FitToWindow()
    {
        _viewport.FitToWindow(_session.HasImage, PreviewPixelSize());
        UpdateZoomLabel();
    }

    private void UpdateCropOverlay(Rect imageRect)
    {
        var has = imageRect.Width > 0 && imageRect.Height > 0;
        CropRect.IsVisible = has;
        Canvas.SetLeft(CropRect, imageRect.X);
        Canvas.SetTop(CropRect, imageRect.Y);
        CropRect.Width = imageRect.Width;
        CropRect.Height = imageRect.Height;
        UpdateCropShade(imageRect, has);
    }

    private void UpdateCropShade(Rect keep, bool hasKeep)
    {
        var size = PreviewPixelSize();
        if (!hasKeep || size is null)
        {
            CropShadeTop.IsVisible = false;
            CropShadeBottom.IsVisible = false;
            CropShadeLeft.IsVisible = false;
            CropShadeRight.IsVisible = false;
            return;
        }

        var fullW = size.Value.Width;
        var fullH = size.Value.Height;
        var x = Math.Clamp(keep.X, 0, fullW);
        var y = Math.Clamp(keep.Y, 0, fullH);
        var r = Math.Clamp(keep.Right, 0, fullW);
        var b = Math.Clamp(keep.Bottom, 0, fullH);

        SetShade(CropShadeTop, 0, 0, fullW, y);
        SetShade(CropShadeBottom, 0, b, fullW, fullH - b);
        SetShade(CropShadeLeft, 0, y, x, b - y);
        SetShade(CropShadeRight, r, y, fullW - r, b - y);
    }

    private static void SetShade(
        Avalonia.Controls.Shapes.Rectangle rect,
        double x,
        double y,
        double w,
        double h
    )
    {
        var visible = w > 0.5 && h > 0.5;
        rect.IsVisible = visible;
        if (!visible)
            return;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = w;
        rect.Height = h;
    }

    private void CommitCropRect(Rect imageLocal)
    {
        if (_session.Preview is null)
            return;

        var bmpW = _session.Preview.PixelSize.Width;
        var bmpH = _session.Preview.PixelSize.Height;
        _crop.Commit(imageLocal, bmpW, bmpH);
        if (!_crop.HasRect)
        {
            ClearCropSelection();
            return;
        }

        UpdateCropOverlay(_crop.Rect!.Value);
        ApplyCropButton.IsEnabled = true;
        CancelCropButton.IsEnabled = true;
        var w = (int)_crop.Rect.Value.Width;
        var h = (int)_crop.Rect.Value.Height;
        StatusText.Text = $"Crop {w}×{h} — Apply Crop or Enter";
    }

    private void ClearCropSelection()
    {
        _crop.Clear();
        CropRect.IsVisible = false;
        UpdateCropShade(default, false);
        ApplyCropButton.IsEnabled = false;
        CancelCropButton.IsEnabled = _canvasTool == CanvasTool.Crop;
    }

    private void CancelCropOrExit()
    {
        if (_crop.HasRect || _crop.IsDragging)
        {
            ClearCropSelection();
            if (_canvasTool == CanvasTool.Crop)
                StatusText.Text = "Crop — drag a rectangle on the active layer";
            return;
        }

        if (_canvasTool == CanvasTool.Crop)
        {
            SetCanvasTool(CanvasTool.Hand);
            if (!_busy && _session.HasImage)
                StatusText.Text = "Ready";
        }
    }

    private void ApplyCrop()
    {
        if (_busy || !_session.HasImage || !_crop.HasRect)
            return;

        var rect = _crop.Rect!.Value;
        ClearCropSelection();
        SetBusy(true, "Crop…");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var size = _session.CropActiveLayer(
                (int)rect.X,
                (int)rect.Y,
                (int)rect.Width,
                (int)rect.Height
            );
            sw.Stop();
            StatusText.Text = size is null
                ? "Crop missed the active layer."
                : $"Crop · {size.Value.Width}×{size.Value.Height} · {sw.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Crop failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }
}
