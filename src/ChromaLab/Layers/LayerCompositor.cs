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

using ChromaFx.Core;
using ChromaFx.Processing.Filters.Overlays;
using ChromaFx.Processing.Numerics;

namespace ChromaLab.Layers;

/// <summary>
/// Flattens a layer stack bottom→top onto a transparent canvas.
/// </summary>
public static class LayerCompositor
{
    /// <summary>
    /// Composites <paramref name="document"/> into a new image (document size).
    /// </summary>
    public static Image Composite(LayerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (TryFastPathSingleLayer(document, out var fast))
            return fast;

        var canvas = new Image(document.Width, document.Height);
        var first = true;

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible || layer.Opacity <= 0f)
                continue;

            if (
                first
                && layer.BlendMode == BlendMode.Normal
                && layer.Opacity >= 1f
                && layer.X == 0
                && layer.Y == 0
                && layer.Pixels.Width == document.Width
                && layer.Pixels.Height == document.Height
            )
            {
                canvas = layer.Pixels.Clone();
                first = false;
                continue;
            }

            BlendLayer(canvas, layer);
            first = false;
        }

        return canvas;
    }

    private static bool TryFastPathSingleLayer(LayerDocument document, out Image result)
    {
        result = null!;
        RasterLayer? only = null;
        foreach (var layer in document.Layers)
        {
            if (!layer.Visible || layer.Opacity <= 0f)
                continue;
            if (only is not null)
                return false;
            only = layer;
        }

        if (only is null)
        {
            result = new Image(document.Width, document.Height);
            return true;
        }

        if (
            only.BlendMode == BlendMode.Normal
            && only.Opacity >= 1f
            && only.X == 0
            && only.Y == 0
            && only.Pixels.Width == document.Width
            && only.Pixels.Height == document.Height
        )
        {
            result = only.Pixels.Clone();
            return true;
        }

        return false;
    }

    private static void BlendLayer(Image canvas, RasterLayer layer)
    {
        var opacity = Math.Clamp(layer.Opacity, 0f, 1f);
        var src = layer.Pixels;

        var destX = layer.X;
        var destY = layer.Y;
        var srcX = 0;
        var srcY = 0;
        var w = src.Width;
        var h = src.Height;

        if (destX < 0)
        {
            srcX = -destX;
            w += destX;
            destX = 0;
        }

        if (destY < 0)
        {
            srcY = -destY;
            h += destY;
            destY = 0;
        }

        if (destX + w > canvas.Width)
            w = canvas.Width - destX;
        if (destY + h > canvas.Height)
            h = canvas.Height - destY;

        if (w <= 0 || h <= 0)
            return;

        var sourceRect = new Rectangle(srcX, srcY, w, h);
        var targetRect = new Rectangle(destX, destY, w, h);
        new Blend(src, opacity, layer.BlendMode, sourceRect).Apply(canvas, targetRect);
    }
}
