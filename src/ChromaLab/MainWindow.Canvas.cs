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
            _cropDragging = true;
            _cropStart = e.GetPosition(PreviewImage);
            _cropRectImage = null;
            UpdateCropOverlay(new Rect(_cropStart, new Size(0, 0)));
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
        if (_cropDragging)
        {
            UpdateCropOverlay(NormalizeRect(_cropStart, e.GetPosition(PreviewImage)));
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
        if (_cropDragging)
        {
            _cropDragging = false;
            e.Pointer.Capture(null);
            CommitCropRect(NormalizeRect(_cropStart, e.GetPosition(PreviewImage)));
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

    private static Rect NormalizeRect(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private void UpdateCropOverlay(Rect imageRect)
    {
        CropRect.IsVisible = imageRect.Width > 0 && imageRect.Height > 0;
        Canvas.SetLeft(CropRect, imageRect.X);
        Canvas.SetTop(CropRect, imageRect.Y);
        CropRect.Width = imageRect.Width;
        CropRect.Height = imageRect.Height;
    }

    private void CommitCropRect(Rect imageLocal)
    {
        if (_session.Preview is null)
            return;

        var bmpW = _session.Preview.PixelSize.Width;
        var bmpH = _session.Preview.PixelSize.Height;
        var x = (int)Math.Floor(Math.Clamp(imageLocal.X, 0, bmpW));
        var y = (int)Math.Floor(Math.Clamp(imageLocal.Y, 0, bmpH));
        var w = (int)Math.Ceiling(Math.Clamp(imageLocal.Right, 0, bmpW)) - x;
        var h = (int)Math.Ceiling(Math.Clamp(imageLocal.Bottom, 0, bmpH)) - y;
        if (w < 1 || h < 1)
        {
            ClearCropSelection();
            return;
        }

        _cropRectImage = new Rect(x, y, w, h);
        UpdateCropOverlay(_cropRectImage.Value);
        ApplyCropButton.IsEnabled = true;
        CancelCropButton.IsEnabled = true;
        StatusText.Text = $"Crop {w}×{h} — Apply Crop or Enter";
    }

    private void ClearCropSelection()
    {
        _cropRectImage = null;
        _cropDragging = false;
        CropRect.IsVisible = false;
        ApplyCropButton.IsEnabled = false;
        CancelCropButton.IsEnabled = _canvasTool == CanvasTool.Crop;
    }

    private void ApplyCrop()
    {
        if (_busy || !_session.HasImage || _cropRectImage is null)
            return;

        var rect = _cropRectImage.Value;
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
            UpdateMeta();
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
