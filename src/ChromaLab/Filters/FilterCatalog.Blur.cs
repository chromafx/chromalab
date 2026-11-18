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
    private static FilterEntry[] BuildBlur() =>
        [
            Parametric(
                "box-blur",
                "Blur",
                "Box blur",
                [Int("size", "Size", 5, 1, 64)],
                (c, v) => c.BoxBlur(v.GetInt("size"))
            ),
            Parametric(
                "gaussian-blur",
                "Blur",
                "Gaussian blur",
                [Float("radius", "Radius", 2, 0.5, 32, 0.5)],
                (c, v) => c.GaussianBlur(v.GetFloat("radius"))
            ),
            Parametric(
                "median",
                "Blur",
                "Median",
                [Int("radius", "Radius", 1, 1, 16)],
                (c, v) => c.Median(v.GetInt("radius"))
            ),
            Instant("kuwahara", "Blur", "Kuwahara", c => c.Kuwahara()),
            Instant("snn-blur", "Blur", "SNN blur", c => c.SnnBlur()),
            Parametric(
                "motion-blur",
                "Blur",
                "Motion blur",
                [
                    Int("length", "Length", 9, 1, 64),
                    Float("angle", "Angle °", 0, 0, 360, 1),
                ],
                (c, v) => c.MotionBlur(v.GetInt("length"), v.GetFloat("angle"))
            )
        ];
}
