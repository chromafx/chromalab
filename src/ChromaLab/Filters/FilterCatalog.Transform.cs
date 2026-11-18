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
    private static FilterEntry[] BuildTransform() =>
        [
            Instant(
                "flip-h",
                "Transform",
                "Flip horizontal",
                c => c.Flip(FlipDirection.Horizontal)
            ),
            Instant("flip-v", "Transform", "Flip vertical", c => c.Flip(FlipDirection.Vertical)),
            Instant("rotate-90", "Transform", "Rotate 90° CW", c => c.Rotate(90f)),
            Instant("rotate-180", "Transform", "Rotate 180°", c => c.Rotate(180f)),
            Instant("rotate-270", "Transform", "Rotate 90° CCW", c => c.Rotate(270f)),
            Parametric(
                "resize",
                "Transform",
                "Resize",
                [
                    Float("scale", "Scale", 1.0, 0.1, 4.0, 0.05),
                    Choice(
                        "filter",
                        "Filter",
                        nameof(ResamplingFilter.Bilinear),
                        ResampleChoices
                    ),
                ],
                (c, v) =>
                {
                    var scale = v.GetFloat("scale");
                    var w = Math.Max(1, (int)Math.Round(c.Image.Width * scale));
                    var h = Math.Max(1, (int)Math.Round(c.Image.Height * scale));
                    c.Resize(w, h, v.GetEnum<ResamplingFilter>("filter"));
                }
            ),
            Parametric(
                "rotate",
                "Transform",
                "Rotate",
                [Float("degrees", "Degrees", 15, -180, 180, 1)],
                (c, v) => c.Rotate(v.GetFloat("degrees"))
            ),
            Parametric(
                "skew",
                "Transform",
                "Skew",
                [
                    Float("x", "X angle °", 10, -45, 45, 1),
                    Float("y", "Y angle °", 0, -45, 45, 1),
                    Choice(
                        "filter",
                        "Filter",
                        nameof(ResamplingFilter.Bilinear),
                        ResampleChoices
                    ),
                ],
                (c, v) =>
                    c.Skew(
                        v.GetFloat("x"),
                        v.GetFloat("y"),
                        v.GetEnum<ResamplingFilter>("filter")
                    )
            ),
            Parametric(
                "perspective",
                "Transform",
                "Perspective",
                [
                    Float("horizontal", "Horizontal", 0.15, -0.45, 0.45, 0.01),
                    Float("vertical", "Vertical", 0, -0.45, 0.45, 0.01),
                ],
                (c, v) => c.Perspective(v.GetFloat("horizontal"), v.GetFloat("vertical"))
            )
        ];
}
