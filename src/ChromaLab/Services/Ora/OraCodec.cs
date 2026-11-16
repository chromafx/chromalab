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

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ChromaFx.Core;
using ChromaFx.IO;
using ChromaFx.IO.Encoders;
using ChromaFx.Processing.Filters.Overlays;
using ChromaLab.Layers;

namespace ChromaLab.Services.Ora;

/// <summary>
/// OpenRaster (.ora) read/write for <see cref="LayerDocument"/> (lab-only; not ChromaFx.IO).
/// Nested &lt;stack&gt; groups are flattened on import. Adjustment/filter layers are skipped.
/// </summary>
public static class OraCodec
{
    public const string MimeType = "image/openraster";

    private static readonly PngEncoder Png = new();

    public static bool IsOraPath(string path) =>
        path.EndsWith(".ora", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads an OpenRaster file into a flat layer document.</summary>
    public static LayerDocument Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static LayerDocument Read(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var mime = zip.GetEntry("mimetype");
        if (mime is not null)
        {
            using var ms = mime.Open();
            using var reader = new StreamReader(ms);
            var text = reader.ReadToEnd().Trim();
            if (!string.Equals(text, MimeType, StringComparison.Ordinal))
                throw new InvalidDataException($"Unexpected ORA mimetype: '{text}'.");
        }

        var stackEntry =
            zip.GetEntry("stack.xml")
            ?? throw new InvalidDataException("ORA archive is missing stack.xml.");

        XDocument xml;
        using (var stackStream = stackEntry.Open())
            xml = XDocument.Load(stackStream);

        var imageEl =
            xml.Root
            ?? throw new InvalidDataException("stack.xml has no root.");
        if (
            !string.Equals(imageEl.Name.LocalName, "image", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidDataException("stack.xml root must be <image>.");

        var width = ReadRequiredPositiveInt(imageEl, "w");
        var height = ReadRequiredPositiveInt(imageEl, "h");
        var doc = new LayerDocument(width, height);

        var rootStack =
            imageEl.Elements().FirstOrDefault(e => e.Name.LocalName == "stack")
            ?? throw new InvalidDataException("stack.xml is missing a root <stack>.");

        // OpenRaster: first child = uppermost. Collect top→bottom then reverse to bottom→top.
        var flat = new List<RasterLayer>();
        CollectLayers(zip, rootStack, flat);
        flat.Reverse();
        foreach (var layer in flat)
            doc.AddLayerInternal(layer);

        if (doc.Layers.Count == 0)
        {
            // Viewing fallback: mergedimage.png
            var merged = zip.GetEntry("mergedimage.png");
            if (merged is null)
                throw new InvalidDataException("ORA has no layers and no mergedimage.png.");
            using var mergedStream = merged.Open();
            using var ms = new MemoryStream();
            mergedStream.CopyTo(ms);
            ms.Position = 0;
            var image = Image.Load(ms);
            return LayerDocument.FromImage(image, "Merged");
        }

        doc.SetActiveIndexInternal(doc.Layers.Count - 1);
        return doc;
    }

    /// <summary>Writes a flat OpenRaster archive (mimetype first, uncompressed).</summary>
    public static void Write(string path, LayerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var stream = File.Create(path);
        Write(stream, document);
    }

    public static void Write(Stream stream, LayerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        // Spec: mimetype must be the first entry and stored (no compression).
        var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var mimeStream = mimeEntry.Open())
        using (var writer = new StreamWriter(mimeStream))
            writer.Write(MimeType);

        var layerEls = new List<XElement>();
        // stack.xml wants uppermost first; our list is bottom→top.
        for (var i = document.Layers.Count - 1; i >= 0; i--)
        {
            var layer = document.Layers[i];
            var fileName = $"data/layer{i:D4}.png";
            var entry = zip.CreateEntry(fileName, CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
                layer.Pixels.Save(entryStream, Png);

            layerEls.Add(
                new XElement(
                    "layer",
                    new XAttribute("name", layer.Name),
                    new XAttribute("src", fileName),
                    new XAttribute("x", layer.X),
                    new XAttribute("y", layer.Y),
                    new XAttribute(
                        "opacity",
                        layer.Opacity.ToString("0.###", CultureInfo.InvariantCulture)
                    ),
                    new XAttribute("visibility", layer.Visible ? "visible" : "hidden"),
                    new XAttribute("composite-op", ToCompositeOp(layer.BlendMode))
                )
            );
        }

        var stackXml = new XDocument(
            new XElement(
                "image",
                new XAttribute("version", "0.0.3"),
                new XAttribute("w", document.Width),
                new XAttribute("h", document.Height),
                new XElement("stack", layerEls)
            )
        );

        var stackEntry = zip.CreateEntry("stack.xml", CompressionLevel.Optimal);
        using (var stackStream = stackEntry.Open())
            stackXml.Save(stackStream);

        var merged = document.Composite();
        var mergedEntry = zip.CreateEntry("mergedimage.png", CompressionLevel.Optimal);
        using (var mergedStream = mergedEntry.Open())
            merged.Save(mergedStream, Png);
    }

    private static void CollectLayers(ZipArchive zip, XElement stack, List<RasterLayer> into)
    {
        // First element = uppermost; walk in document order.
        foreach (var child in stack.Elements())
        {
            var local = child.Name.LocalName;
            if (local.Equals("stack", StringComparison.OrdinalIgnoreCase))
            {
                CollectLayers(zip, child, into);
                continue;
            }

            if (!local.Equals("layer", StringComparison.OrdinalIgnoreCase))
                continue; // skip text / filter proposals

            var src =
                (string?)child.Attribute("src")
                ?? throw new InvalidDataException("ORA layer is missing src.");
            var entry =
                zip.GetEntry(src)
                ?? throw new InvalidDataException($"ORA layer data missing: {src}");

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            ms.Position = 0;
            var pixels = Image.Load(ms);

            var name = (string?)child.Attribute("name") ?? Path.GetFileNameWithoutExtension(src);
            var layer = new RasterLayer(name, pixels)
            {
                X = ReadInt(child, "x", 0),
                Y = ReadInt(child, "y", 0),
                Opacity = ReadFloat(child, "opacity", 1f),
                Visible = ReadVisibility(child),
                BlendMode = FromCompositeOp((string?)child.Attribute("composite-op")),
            };
            into.Add(layer);
        }
    }

    private static int ReadRequiredPositiveInt(XElement el, string name)
    {
        var attr = el.Attribute(name) ?? throw new InvalidDataException($"Missing {name}.");
        if (
            !int.TryParse(
                attr.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var v
            )
            || v < 1
        )
            throw new InvalidDataException($"Invalid {name}: {attr.Value}");
        return v;
    }

    private static int ReadInt(XElement el, string name, int fallback)
    {
        var attr = el.Attribute(name);
        if (attr is null)
            return fallback;
        return int.TryParse(attr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    private static float ReadFloat(XElement el, string name, float fallback)
    {
        var attr = el.Attribute(name);
        if (attr is null)
            return fallback;
        return float.TryParse(
            attr.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var v
        )
            ? Math.Clamp(v, 0f, 1f)
            : fallback;
    }

    private static bool ReadVisibility(XElement el)
    {
        var v = (string?)el.Attribute("visibility");
        if (string.IsNullOrEmpty(v))
            return true;
        return !v.Equals("hidden", StringComparison.OrdinalIgnoreCase)
            && !v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToCompositeOp(BlendMode mode) =>
        mode switch
        {
            BlendMode.Multiply => "svg:multiply",
            BlendMode.Screen => "svg:screen",
            BlendMode.Overlay => "svg:overlay",
            BlendMode.SoftLight => "svg:soft-light",
            _ => "svg:src-over",
        };

    private static BlendMode FromCompositeOp(string? op)
    {
        if (string.IsNullOrEmpty(op))
            return BlendMode.Normal;

        // Accept with or without svg: prefix.
        var key = op.Contains(':') ? op[(op.IndexOf(':') + 1)..] : op;
        return key.ToLowerInvariant() switch
        {
            "multiply" => BlendMode.Multiply,
            "screen" => BlendMode.Screen,
            "overlay" => BlendMode.Overlay,
            "soft-light" => BlendMode.SoftLight,
            _ => BlendMode.Normal,
        };
    }
}
