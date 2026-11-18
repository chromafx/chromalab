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
    private static FilterEntry[] BuildPixelart() =>
        [
            Instant("scale2x", "Pixel art", "Scale_2x", c => c.Scale2x()),
            Instant("scale3x", "Pixel art", "Scale3x", c => c.Scale3x()),
            Instant("scale4x", "Pixel art", "Scale4x", c => c.Scale4x()),
            Instant("hq2x", "Pixel art", "Hq2x", c => c.Hq2x()),
            Instant("hq3x", "Pixel art", "Hq3x", c => c.Hq3x()),
            Instant("hq4x", "Pixel art", "Hq4x", c => c.Hq4x()),
            Instant("eagle", "Pixel art", "Eagle", c => c.Eagle()),
            Instant("super-eagle", "Pixel art", "Super Eagle", c => c.SuperEagle()),
            Instant("scale2xsai", "Pixel art", "Scale2xSaI", c => c.Scale2xSaI()),
            Instant("super2xsai", "Pixel art", "Super2xSaI", c => c.Super2xSaI())
        ];
}
