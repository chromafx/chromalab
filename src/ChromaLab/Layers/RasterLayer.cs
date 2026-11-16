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

namespace ChromaLab.Layers;

/// <summary>
/// One raster layer in a <see cref="LayerDocument"/> (owned pixel buffer).
/// </summary>
public sealed class RasterLayer
{
    public RasterLayer(string name, Image pixels)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(pixels);
        Name = name;
        Pixels = pixels;
    }

    /// <summary>Display name (stack.xml <c>name</c>).</summary>
    public string Name { get; set; }

    /// <summary>When false, skipped during composite.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Layer opacity 0–1 (multiplied with pixel alpha).</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>Horizontal offset on the document canvas.</summary>
    public int X { get; set; }

    /// <summary>Vertical offset on the document canvas.</summary>
    public int Y { get; set; }

    /// <summary>Blend mode when compositing over layers below.</summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    /// <summary>Owned pixel data for this layer.</summary>
    public Image Pixels { get; set; }

    /// <summary>Deep copy (new <see cref="Image"/>).</summary>
    public RasterLayer Clone() =>
        new(Name, Pixels.Clone())
        {
            Visible = Visible,
            Opacity = Opacity,
            X = X,
            Y = Y,
            BlendMode = BlendMode,
        };
}
