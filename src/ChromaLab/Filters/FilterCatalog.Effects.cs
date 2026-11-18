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
    private static FilterEntry[] BuildEffects() =>
        [
            Parametric(
                "posterize",
                "Effects",
                "Posterize",
                [Int("divisions", "Divisions", 4, 2, 32)],
                (c, v) => c.Posterize(v.GetInt("divisions"))
            ),
            Instant("solarize", "Effects", "Solarize", c => c.Solarize()),
            Parametric(
                "pixellate",
                "Effects",
                "Pixellate",
                [Int("size", "Pixel size", 8, 2, 64)],
                (c, v) => c.Pixellate(v.GetInt("size"))
            ),
            Parametric(
                "noise",
                "Effects",
                "Noise",
                [Int("amount", "Amount", 32, 0, 128)],
                (c, v) => c.Noise((byte)Math.Clamp(v.GetInt("amount"), 0, 255))
            ),
            Parametric(
                "jitter",
                "Effects",
                "Jitter",
                [Int("amount", "Amount", 5, 1, 64)],
                (c, v) => c.Jitter(v.GetInt("amount"))
            ),
            Instant("vignette", "Effects", "Vignette", c => c.Vignette(Color.Black)),
            Instant("glow", "Effects", "Glow", c => c.Glow(Color.White)),
            Parametric(
                "quantize",
                "Effects",
                "Quantize",
                [
                    Int("colors", "Colors", 16, 2, 256),
                    Choice("dither", "Dither", nameof(DitherMethod.Bayer4x4), QuantizeDitherChoices),
                    Float("strength", "Strength", 0.5, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.Quantize(v.GetInt("colors"), v.GetEnum<DitherMethod>("dither"), v.GetFloat("strength"))
            ),
            Parametric(
                "oil-paint",
                "Effects",
                "Oil paint",
                [Int("radius", "Radius", 3, 1, 12), Int("levels", "Levels", 20, 2, 64)],
                (c, v) => c.OilPaint(v.GetInt("radius"), v.GetInt("levels"))
            )
        ];
}
