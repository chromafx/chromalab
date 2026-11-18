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
    private static FilterEntry[] BuildFilm() =>
        [
            Parametric(
                "film-grain",
                "Film",
                "Film grain",
                [
                    Float("amount", "Amount", 0.35, 0, 1, 0.05),
                    Float("size", "Size", 1.25, 0.25, 8, 0.25),
                    Float("color", "Color", 0.12, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.FilmGrain(v.GetFloat("amount"), v.GetFloat("size"), v.GetFloat("color"))
            ),
            Instant("kodachrome", "Film", "Kodachrome", c => c.Kodachrome()),
            Instant("polaroid", "Film", "Polaroid", c => c.Polaroid()),
            Instant("lomograph", "Film", "Lomograph", c => c.Lomograph())
        ];
}
