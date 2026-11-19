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

using ChromaFx.Core.Quantization;
using ChromaFx.Processing;
using ChromaFx.Processing.Filters;
using ChromaFx.Processing.Filters.ColorMatrix;
using ChromaFx.Processing.Filters.Resampling;
using ChromaFx.Retro;
using ChromaFx.Retro.Filters;

namespace ChromaLab.Filters;

/// <summary>
/// Static catalog of dogfoodable filters. Menus/toolbar/inspector are built from this list.
/// Parametric entries open the right-hand inspector; param-less entries apply immediately.
/// </summary>
public static partial class FilterCatalog
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

    private static readonly FilterChoice[] EgaModeChoices =
    [
        new(nameof(EgaPaletteMode.Full64), "Full 64"),
        new(nameof(EgaPaletteMode.Adaptive16), "Adaptive 16"),
    ];

    private static readonly FilterChoice[] VgaModeChoices =
    [
        new(nameof(VgaPaletteMode.DacLevels), "DAC levels"),
        new(nameof(VgaPaletteMode.Adaptive256), "Adaptive 256"),
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
            ..BuildColor(),
            ..BuildAdjust(),
            ..BuildBlur(),
            ..BuildSharpenedges(),
            ..BuildEffects(),
            ..BuildFilm(),
            ..BuildTransform(),
            ..BuildRetro(),
            ..BuildPixelart()
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
