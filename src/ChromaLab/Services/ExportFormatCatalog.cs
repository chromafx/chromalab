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

using Avalonia.Controls;
using ChromaFx.IO.Encoders;
using ChromaFx.IO.Formats.Tiff;
using ChromaFx.Native.Encoders;
using ChromaLab.Controls;

namespace ChromaLab.Services;

/// <summary>
/// Mutable encoder option values remembered across Save As / Export.
/// </summary>
public sealed class ExportOptionsState
{
    public int JpegQuality { get; set; } = 90;
    public TiffCompression TiffCompression { get; set; } = TiffCompression.Deflate;
    public int WebPQuality { get; set; } = WebPEncoder.DefaultQuality;
    public bool WebPLossless { get; set; }
    public int AvifQuality { get; set; } = AvifEncoder.DefaultQuality;
    public bool AvifLossless { get; set; }
    public int JxlQuality { get; set; } = JxlEncoder.DefaultQuality;
    public bool JxlLossless { get; set; }
}

/// <summary>
/// One exportable format and how to build its encoder / options UI.
/// </summary>
public sealed class ExportFormat
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string DefaultExtension { get; init; }
    public required IReadOnlyList<string> Extensions { get; init; }
    public bool IsNative { get; init; }

    /// <summary>
    /// Layer-document format (e.g. OpenRaster). Save As only — not flattened Export.
    /// </summary>
    public bool IsDocumentFormat { get; init; }

    public required Func<ExportOptionsState, Control?> CreateOptionsPanel { get; init; }
    public required Func<ExportOptionsState, IImageEncoder> CreateEncoder { get; init; }
}

/// <summary>
/// Catalog of lab export formats (managed + optional native).
/// </summary>
public static class ExportFormatCatalog
{
    /// <summary>
    /// Session-wide last-used encoder options.
    /// </summary>
    public static ExportOptionsState SharedOptions { get; } = new();

    /// <summary>
    /// All formats the lab can write.
    /// </summary>
    public static IReadOnlyList<ExportFormat> All { get; } =
        [
            new ExportFormat
            {
                Id = "ora",
                DisplayName = "OpenRaster",
                DefaultExtension = "ora",
                Extensions = [".ora"],
                IsDocumentFormat = true,
                CreateOptionsPanel = _ => null,
                // Unused: ImageSession writes layers via OraCodec when the path is .ora.
                CreateEncoder = _ => new PngEncoder(),
            },
            Plain("png", "PNG", "png", [".png"], () => new PngEncoder()),
            new ExportFormat
            {
                Id = "jpeg",
                DisplayName = "JPEG",
                DefaultExtension = "jpg",
                Extensions = [".jpg", ".jpeg"],
                CreateOptionsPanel = state =>
                    ExportOptionsPanel.QualityPanel(
                        "Quality",
                        state.JpegQuality,
                        1,
                        100,
                        v => state.JpegQuality = v
                    ),
                CreateEncoder = state => new JpegEncoder { Quality = state.JpegQuality },
            },
            Plain("bmp", "BMP", "bmp", [".bmp"], () => new BmpEncoder()),
            Plain("gif", "GIF", "gif", [".gif"], () => new GifEncoder()),
            Plain("tga", "TGA", "tga", [".tga"], () => new TgaEncoder()),
            new ExportFormat
            {
                Id = "tiff",
                DisplayName = "TIFF",
                DefaultExtension = "tif",
                Extensions = [".tif", ".tiff"],
                CreateOptionsPanel = ExportOptionsPanel.TiffCompressionPanel,
                CreateEncoder = state => new TiffEncoder { Compression = state.TiffCompression },
            },
            Plain("ico", "ICO", "ico", [".ico"], () => new IcoEncoder()),
            Native(
                "webp",
                "WebP",
                "webp",
                [".webp"],
                state => state.WebPQuality,
                state => state.WebPLossless,
                (state, q) => state.WebPQuality = q,
                (state, l) => state.WebPLossless = l,
                state => new WebPEncoder
                {
                    Quality = state.WebPQuality,
                    Lossless = state.WebPLossless,
                }
            ),
            Native(
                "avif",
                "AVIF",
                "avif",
                [".avif"],
                state => state.AvifQuality,
                state => state.AvifLossless,
                (state, q) => state.AvifQuality = q,
                (state, l) => state.AvifLossless = l,
                state => new AvifEncoder
                {
                    Quality = state.AvifQuality,
                    Lossless = state.AvifLossless,
                }
            ),
            Native(
                "jxl",
                "JPEG XL",
                "jxl",
                [".jxl"],
                state => state.JxlQuality,
                state => state.JxlLossless,
                (state, q) => state.JxlQuality = q,
                (state, l) => state.JxlLossless = l,
                state => new JxlEncoder
                {
                    Quality = state.JxlQuality,
                    Lossless = state.JxlLossless,
                }
            ),
        ];

    /// <summary>
    /// Finds a format by file extension (including the dot), or null.
    /// </summary>
    public static ExportFormat? FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return All.FirstOrDefault(f =>
            f.Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase))
        );
    }

    /// <summary>
    /// Builds an encoder for <paramref name="path"/> using shared options.
    /// </summary>
    public static IImageEncoder CreateEncoderForPath(string path)
    {
        var format = FromExtension(System.IO.Path.GetExtension(path));
        return format?.CreateEncoder(SharedOptions) ?? new PngEncoder();
    }

    private static ExportFormat Plain(
        string id,
        string name,
        string ext,
        string[] extensions,
        Func<IImageEncoder> create
    ) =>
        new()
        {
            Id = id,
            DisplayName = name,
            DefaultExtension = ext,
            Extensions = extensions,
            CreateOptionsPanel = _ => null,
            CreateEncoder = _ => create(),
        };

    private static ExportFormat Native(
        string id,
        string name,
        string ext,
        string[] extensions,
        Func<ExportOptionsState, int> getQuality,
        Func<ExportOptionsState, bool> getLossless,
        Action<ExportOptionsState, int> setQuality,
        Action<ExportOptionsState, bool> setLossless,
        Func<ExportOptionsState, IImageEncoder> create
    ) =>
        new()
        {
            Id = id,
            DisplayName = name,
            DefaultExtension = ext,
            Extensions = extensions,
            IsNative = true,
            CreateOptionsPanel = state =>
                ExportOptionsPanel.NativeQualityPanel(
                    getQuality(state),
                    getLossless(state),
                    q => setQuality(state, q),
                    l => setLossless(state, l)
                ),
            CreateEncoder = create,
        };
}
