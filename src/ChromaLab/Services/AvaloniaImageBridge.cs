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

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ChromaFx.Core;
using ChromaFx.Processing;

namespace ChromaLab.Services;

/// <summary>
/// Converts ChromaFx <see cref="Image"/> pixels to Avalonia <see cref="WriteableBitmap"/>.
/// </summary>
public static class AvaloniaImageBridge
{
    /// <summary>
    /// Creates an RGBA8888 bitmap from a ChromaFx image (logical rows; stride-safe).
    /// </summary>
    public static WriteableBitmap ToBitmap(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("Image has no pixels.", nameof(image));

        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormats.Rgba8888,
            AlphaFormat.Unpremul
        );
        CopyInto(bitmap, image);
        return bitmap;
    }

    /// <summary>
    /// Reuses <paramref name="existing"/> when size matches; otherwise disposes it and returns a new bitmap.
    /// </summary>
    public static WriteableBitmap UpdateOrCreate(WriteableBitmap? existing, Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("Image has no pixels.", nameof(image));

        if (
            existing is not null
            && existing.PixelSize.Width == image.Width
            && existing.PixelSize.Height == image.Height
        )
        {
            CopyInto(existing, image);
            return existing;
        }

        existing?.Dispose();
        return ToBitmap(image);
    }

    /// <summary>
    /// Aspect-fit downscale into a small ChromaFx image (no full-res clone-first path).
    /// </summary>
    public static Image CreateThumbnail(Image source, int maxEdge)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxEdge < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEdge));

        if (source.Width <= 0 || source.Height <= 0)
            throw new ArgumentException("Image has no pixels.", nameof(source));

        var scale = Math.Min(1.0, maxEdge / (double)Math.Max(source.Width, source.Height));
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        if (w == source.Width && h == source.Height)
            return source.Clone();

        var thumb = source.Clone();
        thumb.Mutate(c => c.Resize(w, h));
        return thumb;
    }

    /// <summary>
    /// Downscales then converts to a bitmap for layer/variant thumbs.
    /// </summary>
    public static WriteableBitmap ToThumbnailBitmap(Image source, int maxEdge)
    {
        var thumb = CreateThumbnail(source, maxEdge);
        try
        {
            return ToBitmap(thumb);
        }
        finally
        {
            // CreateThumbnail always returns a new image.
        }
    }

    private static unsafe void CopyInto(WriteableBitmap bitmap, Image image)
    {
        using var frame = bitmap.Lock();
        var rowBytes = image.Width * 4;
        for (var y = 0; y < image.Height; y++)
        {
            var src = MemoryMarshal.AsBytes(image.GetRowSpan(y));
            var dest = new Span<byte>((void*)(frame.Address + y * frame.RowBytes), rowBytes);
            src.CopyTo(dest);
        }
    }
}
