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

using ChromaLab.Filters;

namespace ChromaLab.Tests.Filters;

public class FilterExplorerTests
{
    [Fact]
    public void Randomize_Keeps_Values_In_Declared_Ranges()
    {
        var entry = FilterCatalog.All.First(e => e.Id == "posterize");
        var values = entry.CreateDefaultValues();
        var rng = new System.Random(42);

        for (var i = 0; i < 40; i++)
        {
            FilterExplorer.Randomize(entry, values, rng);
            var divisions = values.GetInt("divisions");
            var param = entry.Params[0];
            Assert.InRange(divisions, (int)Math.Round(param.Min), (int)Math.Round(param.Max));
        }
    }

    [Fact]
    public void TrySetCenter_Clamps_And_Requires_CxCy()
    {
        var entry = FilterCatalog.All.First(e => e.Id == "lens-flare");
        Assert.True(FilterExplorer.HasNormalizedCenter(entry));

        var values = entry.CreateDefaultValues();
        Assert.True(FilterExplorer.TrySetCenter(entry, values, -0.5f, 1.5f));
        Assert.Equal(0f, values.GetFloat("cx"), 5);
        Assert.Equal(1f, values.GetFloat("cy"), 5);

        var noCenter = FilterCatalog.All.First(e => e.Id == "invert");
        Assert.False(FilterExplorer.HasNormalizedCenter(noCenter));
        Assert.False(FilterExplorer.TrySetCenter(noCenter, noCenter.CreateDefaultValues(), 0.5f, 0.5f));
    }

    [Fact]
    public void CreateVariants_Returns_Requested_Count()
    {
        var entry = FilterCatalog.All.First(e => e.Id == "posterize");
        var variants = FilterExplorer.CreateVariants(entry, count: 3, rng: new System.Random(7));
        Assert.Equal(3, variants.Length);
        Assert.Equal(FilterExplorer.VariantCount, FilterExplorer.CreateVariants(entry).Length);
    }
}
