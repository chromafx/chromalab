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
using ChromaFx.Core.Colors;
using ChromaFx.IO;
using ChromaFx.IO.Encoders;

namespace ChromaLab.Tests.Helpers;

internal static class TestImages
{
    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color Green = new(0, 255, 0);
    public static readonly Color Blue = new(0, 0, 255);
    public static readonly Color Cyan = new(0, 255, 255);
    public static readonly Color Magenta = new(255, 0, 255);
    public static readonly Color Yellow = new(255, 255, 0);
    public static readonly Color Orange = new(255, 165, 0);
    public static readonly Color Purple = new(128, 0, 128);
    public static readonly Color Teal = new(0, 128, 128);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);

    public static Image Solid(int width, int height, Color color)
    {
        var data = new Color[width * height];
        Array.Fill(data, color);
        return new Image(width, height, data);
    }

    public static Image SolidRgb(int width, int height, byte r, byte g, byte b, byte a = 255) =>
        Solid(width, height, new Color(r, g, b, a));

    public static string SavePng(Image image, string? directory = null)
    {
        var dir = directory ?? Path.Combine(Path.GetTempPath(), "chromalab-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "image.png");
        image.Save(path, new PngEncoder());
        return path;
    }

    public static string SaveGif(Image image, string? directory = null)
    {
        var dir = directory ?? Path.Combine(Path.GetTempPath(), "chromalab-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "image.gif");
        image.Save(path, new GifEncoder());
        return path;
    }

    public sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "chromalab-tests",
                Guid.NewGuid().ToString("N")
            );

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
