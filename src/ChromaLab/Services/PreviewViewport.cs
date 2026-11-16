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
using Avalonia.Media;
using Avalonia.Threading;

namespace ChromaLab.Services;

/// <summary>
/// Zoom / pan / fit for the preview <see cref="ScrollViewer"/> + scale host.
/// </summary>
public sealed class PreviewViewport
{
    public const double MinZoom = 0.05;
    public const double MaxZoom = 32.0;
    public const double ZoomStep = 1.25;

    private readonly ScaleTransform _transform = new(1, 1);
    private ScrollViewer? _scroll;
    private LayoutTransformControl? _zoomHost;
    private Panel? _contentHost;
    private Control? _checkerboard;
    private Control? _cropOverlay;
    private bool _panning;
    private Point _panStart;
    private Vector _panOffsetStart;

    public double Zoom { get; private set; } = 1.0;
    public bool FitPending { get; set; }
    public bool IsPanning => _panning;

    public string ZoomLabel => $"{Zoom * 100:0.#}%";

    public void Attach(
        ScrollViewer scroll,
        LayoutTransformControl zoomHost,
        Panel contentHost,
        Control? checkerboard = null,
        Control? cropOverlay = null
    )
    {
        _scroll = scroll;
        _zoomHost = zoomHost;
        _contentHost = contentHost;
        _checkerboard = checkerboard;
        _cropOverlay = cropOverlay;
        zoomHost.LayoutTransform = _transform;
    }

    public void Reset()
    {
        Zoom = 1.0;
        FitPending = false;
        EndPan();
        ApplyTransform();
    }

    public void SetZoom(
        double zoom,
        bool hasImage,
        PixelSize? imageSize,
        Point? viewportAnchor = null
    )
    {
        if (!hasImage || _scroll is null)
            return;

        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var oldZoom = Zoom;
        if (Math.Abs(zoom - oldZoom) < 0.0001)
            return;

        var anchor =
            viewportAnchor
            ?? new Point(_scroll.Viewport.Width / 2, _scroll.Viewport.Height / 2);
        var offset = _scroll.Offset;
        var contentX = (offset.X + anchor.X) / oldZoom;
        var contentY = (offset.Y + anchor.Y) / oldZoom;

        Zoom = zoom;
        FitPending = false;
        ApplyTransform();
        UpdateContentHostSize(imageSize);

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_scroll is null)
                    return;
                _scroll.Offset = new Vector(
                    Math.Max(0, contentX * Zoom - anchor.X),
                    Math.Max(0, contentY * Zoom - anchor.Y)
                );
            },
            DispatcherPriority.Loaded
        );
    }

    public void FitToWindow(bool hasImage, PixelSize? imageSize)
    {
        if (!hasImage || imageSize is null || _scroll is null)
            return;

        var viewport = _scroll.Viewport;
        if (viewport.Width <= 1 || viewport.Height <= 1)
        {
            FitPending = true;
            return;
        }

        var imgW = imageSize.Value.Width;
        var imgH = imageSize.Value.Height;
        if (imgW <= 0 || imgH <= 0)
            return;

        const double margin = 16;
        var zoomX = (viewport.Width - margin) / imgW;
        var zoomY = (viewport.Height - margin) / imgH;
        Zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);
        FitPending = false;
        ApplyTransform();
        UpdateContentHostSize(imageSize);

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_scroll is null)
                    return;
                _scroll.Offset = new Vector(0, 0);
                CenterIfFits();
            },
            DispatcherPriority.Loaded
        );
    }

    public void UpdateContentHostSize(PixelSize? imageSize)
    {
        if (_scroll is null || _contentHost is null)
            return;

        var viewport = _scroll.Viewport;
        double contentW = 0;
        double contentH = 0;
        if (imageSize is { } size)
        {
            contentW = size.Width * Zoom;
            contentH = size.Height * Zoom;
            if (_checkerboard is not null)
            {
                _checkerboard.Width = size.Width;
                _checkerboard.Height = size.Height;
            }

            if (_cropOverlay is not null)
            {
                _cropOverlay.Width = size.Width;
                _cropOverlay.Height = size.Height;
            }
        }

        _contentHost.MinWidth = Math.Max(viewport.Width, contentW);
        _contentHost.MinHeight = Math.Max(viewport.Height, contentH);
    }

    public void BeginPan(Point scrollPosition)
    {
        if (_scroll is null)
            return;
        _panning = true;
        _panStart = scrollPosition;
        _panOffsetStart = _scroll.Offset;
    }

    public void MovePan(Point scrollPosition)
    {
        if (!_panning || _scroll is null)
            return;
        var delta = scrollPosition - _panStart;
        _scroll.Offset = new Vector(
            Math.Max(0, _panOffsetStart.X - delta.X),
            Math.Max(0, _panOffsetStart.Y - delta.Y)
        );
    }

    public void EndPan() => _panning = false;

    public void ApplyTransform()
    {
        _transform.ScaleX = Zoom;
        _transform.ScaleY = Zoom;
    }

    private void CenterIfFits()
    {
        if (_scroll is null)
            return;
        var viewport = _scroll.Viewport;
        var extent = _scroll.Extent;
        _scroll.Offset = new Vector(
            Math.Max(0, (extent.Width - viewport.Width) / 2),
            Math.Max(0, (extent.Height - viewport.Height) / 2)
        );
    }
}
