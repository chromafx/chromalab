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
    private static FilterEntry[] BuildColor() =>
        [
            Instant(
                "greyscale",
                "Color",
                "_Greyscale",
                c => c.Greyscale(),
                "blend",
                "Greyscale"
            ),
            Instant(
                "invert",
                "Color",
                "_Invert",
                c => c.Invert(),
                "contrast",
                "Invert"
            ),
            Instant("sepia", "Color", "_Sepia", c => c.Sepia()),
            Instant("red-channel", "Color", "Red channel", c => c.Apply(new RedChannel())),
            Instant("green-channel", "Color", "Green channel", c => c.Apply(new GreenChannel())),
            Instant("blue-channel", "Color", "Blue channel", c => c.Apply(new BlueChannel())),
            Parametric(
                "saturation",
                "Color",
                "Saturation",
                [Float("amount", "Amount", 0.35, -1, 1, 0.05)],
                (c, v) => c.Saturation(v.GetFloat("amount"))
            ),
            Parametric(
                "hue",
                "Color",
                "Hue",
                [Float("degrees", "Degrees", 30, 0, 360, 1)],
                (c, v) => c.Hue(v.GetFloat("degrees"))
            ),
            Parametric(
                "tint",
                "Color",
                "Tint",
                [Float("amount", "Green ↔ magenta", 0.2, -1, 1, 0.05)],
                (c, v) => c.Tint(v.GetFloat("amount"))
            ),
            Parametric(
                "kelvin",
                "Color",
                "Kelvin tint",
                [Float("kelvin", "Kelvin", 4500, 1000, 40000, 100)],
                (c, v) => c.KelvinTint(v.GetFloat("kelvin"))
            ),
            Parametric(
                "awb",
                "Color",
                "Auto white balance",
                [Choice("method", "Method", nameof(AutoWhiteBalanceMethod.GrayWorld), AwbChoices)],
                (c, v) => c.AutoWhiteBalance(v.GetEnum<AutoWhiteBalanceMethod>("method"))
            )
        ];
}
