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
    private static FilterEntry[] BuildAdjust() =>
        [
            Parametric(
                "brightness",
                "Adjust",
                "Brightness",
                [Float("amount", "Amount", 0.15, -1, 1, 0.05)],
                (c, v) => c.Brightness(v.GetFloat("amount"))
            ),
            Parametric(
                "contrast",
                "Adjust",
                "Contrast",
                [Float("amount", "Amount", 1.25, 0, 3, 0.05)],
                (c, v) => c.Contrast(v.GetFloat("amount"))
            ),
            Parametric(
                "gamma",
                "Adjust",
                "Gamma",
                [Float("amount", "Amount", 1.2, 0.1, 3, 0.05)],
                (c, v) => c.Gamma(v.GetFloat("amount"))
            ),
            Instant("stretch-contrast", "Adjust", "Stretch contrast", c => c.StretchContrast()),
            Instant("equalize", "Adjust", "Equalize", c => c.Equalize()),
            Instant("adaptive-equalize", "Adjust", "Adaptive equalize", c => c.AdaptiveEqualize()),
            Instant("logarithm", "Adjust", "Logarithm", c => c.Logarithm()),
            Parametric(
                "levels",
                "Adjust",
                "Levels",
                [
                    Float("inBlack", "Input black", 0, 0, 1, 0.01),
                    Float("inWhite", "Input white", 1, 0, 1, 0.01),
                    Float("gamma", "Gamma", 1, 0.1, 3, 0.05),
                    Float("outBlack", "Output black", 0, 0, 1, 0.01),
                    Float("outWhite", "Output white", 1, 0, 1, 0.01),
                ],
                (c, v) =>
                    c.Levels(
                        v.GetFloat("inBlack"),
                        v.GetFloat("inWhite"),
                        v.GetFloat("gamma"),
                        v.GetFloat("outBlack"),
                        v.GetFloat("outWhite")
                    )
            ),
            Parametric(
                "curves",
                "Adjust",
                "Curves",
                [
                    Float("black", "Black", 0, 0, 1, 0.01),
                    Float("mid", "Mid", 0.5, 0, 1, 0.01),
                    Float("white", "White", 1, 0, 1, 0.01),
                ],
                (c, v) => c.Curves(v.GetFloat("black"), v.GetFloat("mid"), v.GetFloat("white"))
            )
        ];
}
