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

public class LayerDocumentTests
{
    [Fact]
    public void FromImage_Creates_Single_Background_Layer()
    {
        var image = TestImages.Solid(16, 12, TestImages.Red);
        var doc = LayerDocument.FromImage(image);
        Assert.Equal(16, doc.Width);
        Assert.Equal(12, doc.Height);
        Assert.Single(doc.Layers);
        Assert.Equal(0, doc.ActiveIndex);
        Assert.Equal("Background", doc.ActiveLayer.Name);
    }

    [Fact]
    public void Add_Duplicate_Delete_And_Reorder()
    {
        var doc = LayerDocument.FromImage(TestImages.Solid(8, 8, TestImages.Blue));
        doc.AddEmptyLayer("Top");
        Assert.Equal(2, doc.Layers.Count);
        Assert.Equal(1, doc.ActiveIndex);

        doc.DuplicateActive();
        Assert.Equal(3, doc.Layers.Count);
        Assert.Equal("Top copy", doc.ActiveLayer.Name);

        Assert.True(doc.TryMoveActiveDown());
        Assert.True(doc.TryDeleteActive());
        Assert.Equal(2, doc.Layers.Count);
        Assert.True(doc.TryDeleteActive());
        Assert.Single(doc.Layers);
        Assert.False(doc.TryDeleteActive());
    }

    [Fact]
    public void SetCanvasSize_Keeps_Layer_Pixels_And_Offsets()
    {
        var doc = LayerDocument.FromImage(TestImages.Solid(10, 10, TestImages.Green));
        doc.ActiveLayer.X = 3;
        doc.ActiveLayer.Y = 4;
        var w = doc.ActiveLayer.Pixels.Width;
        var h = doc.ActiveLayer.Pixels.Height;

        doc.SetCanvasSize(20, 30);
        Assert.Equal(20, doc.Width);
        Assert.Equal(30, doc.Height);
        Assert.Equal(w, doc.ActiveLayer.Pixels.Width);
        Assert.Equal(h, doc.ActiveLayer.Pixels.Height);
        Assert.Equal(3, doc.ActiveLayer.X);
        Assert.Equal(4, doc.ActiveLayer.Y);
    }
}
