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

        using var frame = bitmap.Lock();
        var rowBytes = image.Width * 4;
        var scratch = new byte[rowBytes];
        for (var y = 0; y < image.Height; y++)
        {
            MemoryMarshal.AsBytes(image.GetRowSpan(y)).CopyTo(scratch);
            Marshal.Copy(scratch, 0, frame.Address + y * frame.RowBytes, rowBytes);
        }

        return bitmap;
    }
}
