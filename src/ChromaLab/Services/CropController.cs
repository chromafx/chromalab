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

namespace ChromaLab.Services;

/// <summary>
/// Tracks crop drag state in image-local coordinates.
/// </summary>
public sealed class CropController
{
    private bool _dragging;
    private Point _start;
    private Rect? _rect;

    public bool IsDragging => _dragging;
    public Rect? Rect => _rect;
    public bool HasRect => _rect is { Width: > 0, Height: > 0 };

    public void Begin(Point imagePoint)
    {
        _dragging = true;
        _start = imagePoint;
        _rect = null;
    }

    public Rect Update(Point imagePoint) => Normalize(_start, imagePoint);

    public void EndDrag() => _dragging = false;

    public void Commit(Rect imageLocal, int bitmapWidth, int bitmapHeight)
    {
        var x = (int)Math.Floor(Math.Clamp(imageLocal.X, 0, bitmapWidth));
        var y = (int)Math.Floor(Math.Clamp(imageLocal.Y, 0, bitmapHeight));
        var w = (int)Math.Ceiling(Math.Clamp(imageLocal.Right, 0, bitmapWidth)) - x;
        var h = (int)Math.Ceiling(Math.Clamp(imageLocal.Bottom, 0, bitmapHeight)) - y;
        _rect = w < 1 || h < 1 ? null : new Rect(x, y, w, h);
        _dragging = false;
    }

    public void Clear()
    {
        _rect = null;
        _dragging = false;
    }

    public static Rect Normalize(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}
