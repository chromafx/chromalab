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
using ChromaFx.IO.Encoders;
using ChromaLab.Services;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Services;

public class DocumentStorageTests
{
    [Fact]
    public void Load_Png_Produces_Single_Layer_Document()
    {
        using var temp = new TestImages.TempDir();
        var path = TestImages.SavePng(TestImages.Solid(10, 7, TestImages.Magenta), temp.Path);
        var (doc, anim) = DocumentStorage.Load(path);
        Assert.Equal(10, doc.Width);
        Assert.Equal(7, doc.Height);
        Assert.Single(doc.Layers);
        Assert.Null(anim);
    }

    [Fact]
    public void Load_Gif_Opens_Frame0_Still_And_May_Attach_Animation()
    {
        using var temp = new TestImages.TempDir();
        var path = TestImages.SaveGif(TestImages.Solid(6, 6, TestImages.Yellow), temp.Path);
        var (doc, _) = DocumentStorage.Load(path);
        Assert.Equal(6, doc.Width);
        Assert.Equal(6, doc.Height);
        Assert.Single(doc.Layers);
        // Single-frame GIF may or may not yield a multi-frame Animation; document remains usable.
    }

    [Fact]
    public void WriteFile_RoundTrips_Png()
    {
        using var temp = new TestImages.TempDir();
        var image = TestImages.Solid(5, 5, TestImages.Orange);
        var path = Path.Combine(temp.Path, "out.png");
        DocumentStorage.WriteFile(image, path, new PngEncoder());
        var (doc, _) = DocumentStorage.Load(path);
        Assert.Equal(5, doc.Width);
        Assert.Equal(5, doc.Height);
    }
}
