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

using ChromaFx.Core;
using ChromaFx.IO;
using ChromaFx.IO.Encoders;
using ChromaLab.Layers;
using ChromaLab.Services.Ora;

namespace ChromaLab.Services;

/// <summary>
/// File open/save helpers for layer documents and flattened stills.
/// </summary>
internal static class DocumentStorage
{
    public static (LayerDocument Doc, Animation? Anim) Load(string path)
    {
        if (OraCodec.IsOraPath(path))
            return (OraCodec.Read(path), null);

        var image = Image.Load(path);
        var doc = LayerDocument.FromImage(image);
        Animation? anim = null;
        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                anim = Animation.Load(path);
            }
            catch
            {
                // Still document remains usable if animation decode fails.
            }
        }

        return (doc, anim);
    }

    public static void Write(
        LayerDocument document,
        Image? composite,
        string path,
        IImageEncoder? encoder
    )
    {
        if (OraCodec.IsOraPath(path))
        {
            OraCodec.Write(path, document);
            return;
        }

        if (composite is null)
            throw new ArgumentNullException(nameof(composite));
        WriteFile(composite, path, encoder);
    }

    public static void WriteFile(Image image, string path, IImageEncoder? encoder)
    {
        if (encoder is null)
            image.Save(path);
        else
            image.Save(path, encoder);
    }
}
