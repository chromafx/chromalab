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
using ChromaLab.Services.Ora;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Services;

public class OraCodecTests
{
    [Fact]
    public void RoundTrip_Two_Layer_Document()
    {
        using var temp = new TestImages.TempDir();
        var path = Path.Combine(temp.Path, "stack.ora");

        var doc = LayerDocument.FromImage(TestImages.Solid(16, 16, TestImages.Red), "Bottom");
        doc.AddLayer(
            new RasterLayer("Top", TestImages.Solid(8, 8, TestImages.Blue))
            {
                X = 4,
                Y = 4,
                Opacity = 0.75f,
            }
        );
        doc.ActiveIndex = 0;

        OraCodec.Write(path, doc);
        Assert.True(OraCodec.IsOraPath(path));

        var loaded = OraCodec.Read(path);
        Assert.Equal(16, loaded.Width);
        Assert.Equal(16, loaded.Height);
        Assert.Equal(2, loaded.Layers.Count);
        Assert.Equal("Bottom", loaded.Layers[0].Name);
        Assert.Equal("Top", loaded.Layers[1].Name);
        Assert.Equal(4, loaded.Layers[1].X);
        Assert.Equal(4, loaded.Layers[1].Y);
        Assert.Equal(0.75f, loaded.Layers[1].Opacity, 3);
        Assert.Equal(8, loaded.Layers[1].Pixels.Width);
        Assert.Equal(8, loaded.Layers[1].Pixels.Height);
    }
}
