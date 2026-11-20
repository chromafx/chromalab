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
    private static FilterEntry[] BuildRetro() =>
        [
            Parametric(
                "websafe",
                "Retro",
                "_Web-safe",
                [
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.WebSafeQuantize(
                        v.GetEnum<OrderedDitherMethod>("dither"),
                        v.GetFloat("strength")
                    ),
                "grid-2x2",
                "Web-safe quantize"
            ),
            Parametric(
                "cga",
                "Retro",
                "CGA palette",
                [
                    Choice("mode", "Mode", nameof(CgaPaletteMode.Palette1High), CgaModeChoices),
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.CgaQuantize(
                        v.GetEnum<CgaPaletteMode>("mode"),
                        v.GetEnum<OrderedDitherMethod>("dither"),
                        v.GetFloat("strength")
                    )
            ),
            Parametric(
                "ega",
                "Retro",
                "EGA palette",
                [
                    Choice("mode", "Mode", nameof(EgaPaletteMode.Full64), EgaModeChoices),
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.EgaQuantize(
                        v.GetEnum<EgaPaletteMode>("mode"),
                        v.GetEnum<OrderedDitherMethod>("dither"),
                        v.GetFloat("strength")
                    )
            ),
            Parametric(
                "vga",
                "Retro",
                "VGA 6-bit",
                [
                    Choice("mode", "Mode", nameof(VgaPaletteMode.DacLevels), VgaModeChoices),
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.VgaQuantize(
                        v.GetEnum<VgaPaletteMode>("mode"),
                        v.GetEnum<OrderedDitherMethod>("dither"),
                        v.GetFloat("strength")
                    )
            ),
            Parametric(
                "bad-web",
                "Retro",
                "Bad web scale",
                [
                    Float("factor", "Factor", 0.25, 0.05, 0.95, 0.05),
                    Bool("webSafe", "Web-safe on small", true),
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.None), DitherChoices),
                    Float("strength", "Strength", 0.5, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.BadWebScale(
                        v.GetFloat("factor"),
                        v.GetBool("webSafe"),
                        v.GetEnum<OrderedDitherMethod>("dither"),
                        v.GetFloat("strength")
                    )
            ),
            Parametric(
                "bevel",
                "Retro",
                "Bevel",
                [
                    Int("depth", "Depth", 2, 0, 32),
                    Choice("style", "Style", nameof(BevelStyle.Raised), BevelStyleChoices),
                    Bool("expand", "Expand canvas", true),
                    Float("strength", "Strength", 0.65, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.Bevel(
                        v.GetInt("depth"),
                        v.GetEnum<BevelStyle>("style"),
                        v.GetBool("expand"),
                        v.GetFloat("strength")
                    )
            ),
            Parametric(
                "retro-mode",
                "Retro",
                "Retro mode",
                [Int("width", "Width", 320, 16, 1280), Int("height", "Height", 200, 16, 1024)],
                (c, v) => c.RetroMode(v.GetInt("width"), v.GetInt("height"))
            ),
            Parametric(
                "halftone",
                "Retro",
                "Halftone",
                [
                    Int("cell", "Cell size", 6, 2, 64),
                    Float("angle", "Angle °", 45, 0, 180, 1),
                    Float("strength", "Strength", 1, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.Halftone(v.GetInt("cell"), v.GetFloat("angle"), v.GetFloat("strength"))
            ),
            Parametric(
                "chroma-ab",
                "Retro",
                "Chromatic aberration",
                [
                    Float("amount", "Amount", 3, 0, 32, 0.5),
                    Float("strength", "Strength", 1, 0, 1, 0.05),
                ],
                (c, v) => c.ChromaticAberration(v.GetFloat("amount"), v.GetFloat("strength"))
            ),
            Parametric(
                "ccd-bloom",
                "Retro",
                "CCD bloom",
                [
                    Float("threshold", "Threshold", 0.85, 0.5, 1, 0.01),
                    Int("radius", "Radius", 12, 1, 64),
                    Float("strength", "Strength", 0.65, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.CcdBloom(v.GetFloat("threshold"), v.GetInt("radius"), v.GetFloat("strength"))
            ),
            Parametric(
                "lens-flare",
                "Retro",
                "Lens flare",
                [
                    Float("cx", "Center X", 0.32, 0, 1, 0.01),
                    Float("cy", "Center Y", 0.28, 0, 1, 0.01),
                    Float("strength", "Strength", 0.85, 0, 1, 0.05),
                ],
                (c, v) => c.LensFlare(v.GetFloat("cx"), v.GetFloat("cy"), v.GetFloat("strength"))
            ),
            Parametric(
                "liquid-chrome",
                "Retro",
                "Liquid chrome",
                [Float("strength", "Strength", 1, 0, 1, 0.05)],
                (c, v) => c.LiquidChrome(v.GetFloat("strength"))
            ),
            Parametric(
                "plastic-wrap",
                "Retro",
                "Plastic wrap",
                [Float("strength", "Strength", 0.75, 0, 1, 0.05)],
                (c, v) => c.PlasticWrap(v.GetFloat("strength"))
            ),
            Parametric(
                "jpeg-blocks",
                "Retro",
                "JPEG blocks",
                [
                    Int("block", "Block size", 8, 2, 32),
                    Float("strength", "Strength", 0.85, 0, 1, 0.05),
                ],
                (c, v) => c.JpegBlocks(v.GetInt("block"), v.GetFloat("strength"))
            ),
            Parametric(
                "page-curl",
                "Retro",
                "Page curl",
                [
                    Float("amount", "Amount", 0.55, 0, 1, 0.05),
                    Bool("fromRight", "From right", true),
                ],
                (c, v) => c.PageCurl(v.GetFloat("amount"), v.GetBool("fromRight"))
            ),
            Parametric(
                "warp-twirl",
                "Retro",
                "Warp twirl",
                [
                    Float("amount", "Amount", 1.2, -4, 4, 0.05),
                    Float("radius", "Radius", 0.85, 0.05, 1.5, 0.05),
                    Float("cx", "Center X", 0.5, 0, 1, 0.01),
                    Float("cy", "Center Y", 0.5, 0, 1, 0.01),
                ],
                (c, v) =>
                    c.Warp(
                        WarpMode.Twirl,
                        v.GetFloat("amount"),
                        v.GetFloat("radius"),
                        v.GetFloat("cx"),
                        v.GetFloat("cy")
                    )
            ),
            Parametric(
                "warp-pinch",
                "Retro",
                "Warp pinch",
                [
                    Float("amount", "Amount", 0.8, -2, 2, 0.05),
                    Float("radius", "Radius", 0.85, 0.05, 1.5, 0.05),
                ],
                (c, v) => c.Warp(WarpMode.Pinch, v.GetFloat("amount"), v.GetFloat("radius"))
            ),
            Parametric(
                "warp-sphere",
                "Retro",
                "Warp sphere",
                [
                    Float("amount", "Amount", 0.9, 0, 2, 0.05),
                    Float("radius", "Radius", 0.85, 0.05, 1.5, 0.05),
                ],
                (c, v) => c.Warp(WarpMode.Sphere, v.GetFloat("amount"), v.GetFloat("radius"))
            ),
            Parametric(
                "glass-lens",
                "Retro",
                "Glass lens",
                [
                    Float("refraction", "Refraction", 0.65, 0, 2, 0.05),
                    Float("radius", "Radius", 0.45, 0.05, 1.2, 0.05),
                    Float("rim", "Rim", 0.7, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.GlassLens(v.GetFloat("refraction"), v.GetFloat("radius"), rim: v.GetFloat("rim"))
            ),
            Parametric(
                "kaleidoscope",
                "Retro",
                "Kaleidoscope",
                [Int("segments", "Segments", 8, 2, 24)],
                (c, v) => c.Kaleidoscope(v.GetInt("segments"))
            ),
            Parametric(
                "vortex-tile",
                "Retro",
                "Vortex tile",
                [
                    Float("twist", "Twist", 2, -8, 8, 0.1),
                    Float("zoom", "Zoom", 1, 0.25, 4, 0.05),
                ],
                (c, v) => c.VortexTile(v.GetFloat("twist"), v.GetFloat("zoom"))
            ),
            Parametric(
                "lightning",
                "Retro",
                "Lightning",
                [
                    Int("seed", "Seed", 42, 0, 9999),
                    Float("strength", "Strength", 0.9, 0, 1, 0.05),
                    Int("bolts", "Bolts", 2, 1, 6),
                ],
                (c, v) => c.Lightning(v.GetInt("seed"), v.GetFloat("strength"), v.GetInt("bolts"))
            ),
            Parametric(
                "fractal-noise",
                "Retro",
                "Fractal noise",
                [
                    Float("strength", "Strength", 0.55, 0, 1, 0.05),
                    Float("frequency", "Frequency", 0.02, 0.001, 0.2, 0.001),
                    Int("seed", "Seed", 1337, 0, 99999),
                    Int("octaves", "Octaves", 4, 1, 8),
                ],
                (c, v) =>
                    c.FractalNoise(
                        v.GetFloat("strength"),
                        v.GetFloat("frequency"),
                        v.GetInt("seed"),
                        v.GetInt("octaves")
                    )
            ),
            Parametric(
                "plasma",
                "Retro",
                "Plasma",
                [
                    Float("strength", "Strength", 1, 0, 1, 0.05),
                    Float("scale", "Scale", 0.08, 0.01, 0.5, 0.01),
                    Float("time", "Time", 0, 0, 20, 0.1),
                ],
                (c, v) => c.Plasma(v.GetFloat("strength"), v.GetFloat("scale"), v.GetFloat("time"))
            ),
            Parametric(
                "fire",
                "Retro",
                "Fire",
                [
                    Float("strength", "Strength", 0.85, 0, 1, 0.05),
                    Int("seed", "Seed", 7, 0, 9999),
                    Float("cooling", "Cooling", 1, 0.5, 2, 0.05),
                    Float("imageInfluence", "Image influence", 0.75, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.Fire(
                        v.GetFloat("strength"),
                        v.GetInt("seed"),
                        v.GetFloat("cooling"),
                        v.GetFloat("imageInfluence")
                    )
            )
        ];
}
