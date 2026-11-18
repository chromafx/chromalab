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

/// <summary>
/// Static catalog of dogfoodable filters. Menus/toolbar/inspector are built from this list.
/// Parametric entries open the right-hand inspector; param-less entries apply immediately.
/// </summary>
public static class FilterCatalog
{
    private static readonly FilterChoice[] DitherChoices =
    [
        new(nameof(OrderedDitherMethod.None), "None"),
        new(nameof(OrderedDitherMethod.Bayer4x4), "Bayer 4×4"),
        new(nameof(OrderedDitherMethod.Bayer8x8), "Bayer 8×8"),
    ];

    private static readonly FilterChoice[] QuantizeDitherChoices =
    [
        new(nameof(DitherMethod.None), "None"),
        new(nameof(DitherMethod.Bayer4x4), "Bayer 4×4"),
        new(nameof(DitherMethod.Bayer8x8), "Bayer 8×8"),
        new(nameof(DitherMethod.FloydSteinberg), "Floyd–Steinberg"),
    ];

    private static readonly FilterChoice[] CgaModeChoices =
    [
        new(nameof(CgaPaletteMode.Palette1High), "Palette 1 High"),
        new(nameof(CgaPaletteMode.Palette1Low), "Palette 1 Low"),
        new(nameof(CgaPaletteMode.Palette2High), "Palette 2 High"),
        new(nameof(CgaPaletteMode.Palette2Low), "Palette 2 Low"),
        new(nameof(CgaPaletteMode.Standard16), "Standard 16"),
        new(nameof(CgaPaletteMode.Monochrome2), "Monochrome"),
    ];

    private static readonly FilterChoice[] BevelStyleChoices =
    [
        new(nameof(BevelStyle.Raised), "Raised"),
        new(nameof(BevelStyle.Pressed), "Pressed"),
    ];

    private static readonly FilterChoice[] AwbChoices =
    [
        new(nameof(AutoWhiteBalanceMethod.GrayWorld), "Gray World"),
        new(nameof(AutoWhiteBalanceMethod.WhitePatch), "White Patch"),
    ];

    private static readonly FilterChoice[] ResampleChoices =
    [
        new(nameof(ResamplingFilter.NearestNeighbor), "Nearest"),
        new(nameof(ResamplingFilter.Bilinear), "Bilinear"),
        new(nameof(ResamplingFilter.Bicubic), "Bicubic"),
        new(nameof(ResamplingFilter.Lanczos3), "Lanczos3"),
        new(nameof(ResamplingFilter.Mitchell), "Mitchell"),
    ];

    /// <summary>
    /// Display order for filter groups in the Filters menu.
    /// </summary>
    public static IReadOnlyList<string> GroupOrder { get; } =
        [
            "Color",
            "Adjust",
            "Blur",
            "Sharpen & edges",
            "Effects",
            "Film",
            "Transform",
            "Retro",
            "Pixel art",
        ];

    /// <summary>
    /// All registered filters.
    /// </summary>
    public static IReadOnlyList<FilterEntry> All { get; } = Build();

    /// <summary>
    /// Entries that should appear on the toolbar.
    /// </summary>
    public static IEnumerable<FilterEntry> ToolbarFavorites => All.Where(e => e.IsToolbarFavorite);

    /// <summary>
    /// Groups entries in <see cref="GroupOrder"/> sequence (unknown groups last).
    /// </summary>
    public static IEnumerable<IGrouping<string, FilterEntry>> GroupsByOrder()
    {
        var rank = GroupOrder
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.Ordinal);

        return All.GroupBy(e => e.Group)
            .OrderBy(g => rank.TryGetValue(g.Key, out var i) ? i : int.MaxValue)
            .ThenBy(g => g.Key, StringComparer.Ordinal);
    }

    private static FilterEntry[] Build() =>
        [
            // —— Color ——
            Instant(
                "greyscale",
                "Color",
                "_Greyscale",
                c => c.Greyscale(),
                "M2,2 H14 V14 H2 Z M8,2 V14",
                "Greyscale"
            ),
            Instant(
                "invert",
                "Color",
                "_Invert",
                c => c.Invert(),
                "M8,2 A6,6 0 1 0 8,14 A6,6 0 1 0 8,2 M8,2 V14",
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
            ),
            // —— Adjust ——
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
            ),
            // —— Blur ——
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
            ),
            // —— Sharpen & edges ——
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
            ),
            // —— Effects ——
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
            ),
            // —— Film ——
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
            Instant("lomograph", "Film", "Lomograph", c => c.Lomograph()),
            // —— Transform ——
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
            ),
            // —— Retro ——
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
                "M2,2 H6 V6 H2 Z M10,2 H14 V6 H10 Z M2,10 H6 V14 H2 Z M10,10 H14 V14 H10 Z",
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
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.EgaQuantize(v.GetEnum<OrderedDitherMethod>("dither"), v.GetFloat("strength"))
            ),
            Parametric(
                "vga",
                "Retro",
                "VGA 6-bit",
                [
                    Choice("dither", "Dither", nameof(OrderedDitherMethod.Bayer4x4), DitherChoices),
                    Float("strength", "Strength", 0.4, 0, 1, 0.05),
                ],
                (c, v) =>
                    c.VgaQuantize(v.GetEnum<OrderedDitherMethod>("dither"), v.GetFloat("strength"))
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
                ],
                (c, v) => c.Fire(v.GetFloat("strength"), v.GetInt("seed"), v.GetFloat("cooling"))
            ),
            // —— Pixel art ——
            Instant("scale2x", "Pixel art", "Scale_2x", c => c.Scale2x()),
            Instant("scale3x", "Pixel art", "Scale3x", c => c.Scale3x()),
            Instant("scale4x", "Pixel art", "Scale4x", c => c.Scale4x()),
            Instant("hq2x", "Pixel art", "Hq2x", c => c.Hq2x()),
            Instant("hq3x", "Pixel art", "Hq3x", c => c.Hq3x()),
            Instant("hq4x", "Pixel art", "Hq4x", c => c.Hq4x()),
            Instant("eagle", "Pixel art", "Eagle", c => c.Eagle()),
            Instant("super-eagle", "Pixel art", "Super Eagle", c => c.SuperEagle()),
            Instant("scale2xsai", "Pixel art", "Scale2xSaI", c => c.Scale2xSaI()),
            Instant("super2xsai", "Pixel art", "Super2xSaI", c => c.Super2xSaI()),
        ];

    private static FilterEntry Instant(
        string id,
        string group,
        string header,
        Action<IImageProcessingContext> apply,
        string? toolbarIcon = null,
        string? toolbarTip = null
    ) =>
        new(
            id,
            group,
            header,
            (c, _) => apply(c),
            [],
            toolbarIcon,
            toolbarTip
        );

    private static FilterEntry Parametric(
        string id,
        string group,
        string header,
        IReadOnlyList<FilterParam> parameters,
        Action<IImageProcessingContext, FilterValues> apply,
        string? toolbarIcon = null,
        string? toolbarTip = null
    ) => new(id, group, header, apply, parameters, toolbarIcon, toolbarTip);
}
