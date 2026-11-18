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
using ChromaLab.Layers;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Layers;

public class LayerCompositorTests
{
    [Fact]
    public void Single_Visible_Full_Layer_Uses_Fast_Path_Size()
    {
        var doc = LayerDocument.FromImage(TestImages.Solid(12, 9, TestImages.Cyan));
        var composite = LayerCompositor.Composite(doc);
        Assert.Equal(12, composite.Width);
        Assert.Equal(9, composite.Height);
        Assert.Equal(TestImages.Cyan, composite.PixelSpan[0]);
    }

    [Fact]
    public void Hidden_Layer_Is_Skipped()
    {
        var doc = LayerDocument.FromImage(TestImages.Solid(4, 4, TestImages.Red));
        doc.AddLayer(new RasterLayer("hidden", TestImages.Solid(4, 4, TestImages.Blue)) { Visible = false });
        var composite = LayerCompositor.Composite(doc);
        Assert.Equal(TestImages.Red, composite.PixelSpan[0]);
    }

    [Fact]
    public void Offset_Layer_Composites_Within_Canvas()
    {
        var doc = new LayerDocument(8, 8);
        doc.AddLayer(new RasterLayer("bg", TestImages.Solid(8, 8, TestImages.Black)));
        doc.AddLayer(
            new RasterLayer("badge", TestImages.Solid(2, 2, TestImages.White)) { X = 3, Y = 3 }
        );
        var composite = LayerCompositor.Composite(doc);
        Assert.Equal(TestImages.Black, composite.PixelSpan[0]);
        Assert.Equal(TestImages.White, composite.PixelSpan[3 + 3 * 8]);
    }
}
