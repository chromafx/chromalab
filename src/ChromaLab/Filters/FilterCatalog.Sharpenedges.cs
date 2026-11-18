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

using ChromaFx.Core.Colors;
using ChromaFx.Core.Quantization;
using ChromaFx.PixelArt;
using ChromaFx.Processing;
using ChromaFx.Processing.Filters;
using ChromaFx.Processing.Filters.ColorMatrix;
using ChromaFx.Processing.Filters.Resampling;
using ChromaFx.Retro;
using ChromaFx.Retro.Filters;
using static ChromaLab.Filters.FilterParam;

namespace ChromaLab.Filters;

public static partial class FilterCatalog
{
    private static FilterEntry[] BuildSharpenedges() =>
        [
            Instant("sharpen", "Sharpen & edges", "Sharpen", c => c.Sharpen()),
            Instant("unsharp", "Sharpen & edges", "Unsharp mask", c => c.Unsharp()),
            Instant("emboss", "Sharpen & edges", "Emboss", c => c.Emboss()),
            Instant("sobel-emboss", "Sharpen & edges", "Sobel emboss", c => c.SobelEmboss()),
            Instant("canny", "Sharpen & edges", "Canny edges", c => c.Canny()),
            Instant("edge-detect", "Sharpen & edges", "Edge detect", c => c.EdgeDetect()),
            Parametric(
                "threshold",
                "Sharpen & edges",
                "Threshold",
                [Float("value", "Value", 0.5, 0, 1, 0.01)],
                (c, v) => c.Threshold(v.GetFloat("value"))
            ),
            Instant(
                "adaptive-threshold",
                "Sharpen & edges",
                "Adaptive threshold",
                c => c.AdaptiveThreshold()
            ),
            Instant("dilate", "Sharpen & edges", "Dilate", c => c.Dilate()),
            Instant("erode", "Sharpen & edges", "Erode", c => c.Erode()),
            Parametric(
                "morph-open",
                "Sharpen & edges",
                "Morph open",
                [Int("radius", "Radius", 1, 1, 16)],
                (c, v) => c.MorphOpen(v.GetInt("radius"))
            ),
            Parametric(
                "morph-close",
                "Sharpen & edges",
                "Morph close",
                [Int("radius", "Radius", 1, 1, 16)],
                (c, v) => c.MorphClose(v.GetInt("radius"))
            )
        ];
}
