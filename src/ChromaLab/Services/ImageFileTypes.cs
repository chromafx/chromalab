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

using Avalonia.Platform.Storage;

namespace ChromaLab.Services;

/// <summary>
/// Shared open/drop file filters for still images and OpenRaster.
/// </summary>
public static class ImageFileTypes
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tga",
        ".tif",
        ".tiff",
        ".ico",
        ".webp",
        ".avif",
        ".jxl",
        ".ora",
    };

    public static readonly FilePickerFileType PickerType = new("Images")
    {
        Patterns =
        [
            "*.png",
            "*.jpg",
            "*.jpeg",
            "*.gif",
            "*.bmp",
            "*.tga",
            "*.tif",
            "*.tiff",
            "*.ico",
            "*.webp",
            "*.avif",
            "*.jxl",
            "*.ora",
        ],
    };

    public static bool IsSupportedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        return Extensions.Contains(Path.GetExtension(path));
    }
}
