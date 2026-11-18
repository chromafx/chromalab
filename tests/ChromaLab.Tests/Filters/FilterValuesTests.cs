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
using static ChromaLab.Filters.FilterParam;

namespace ChromaLab.Tests.Filters;

public class FilterValuesTests
{
    private static readonly IReadOnlyList<FilterParam> SampleParams =
    [
        Float("amount", "Amount", 0.35, -1, 1, 0.05),
        Int("size", "Size", 8, 2, 64),
        Bool("enabled", "Enabled", true),
        Choice("mode", "Mode", "a", new FilterChoice("a", "A"), new FilterChoice("b", "B")),
    ];

    [Fact]
    public void FromDefaults_Uses_Declared_Defaults()
    {
        var values = FilterValues.FromDefaults(SampleParams);
        Assert.Equal(0.35f, values.GetFloat("amount"), 3);
        Assert.Equal(8, values.GetInt("size"));
        Assert.True(values.GetBool("enabled"));
        Assert.Equal("a", values.GetChoice("mode"));
    }

    [Fact]
    public void Dictionary_RoundTrip_Preserves_Values()
    {
        var original = FilterValues.FromDefaults(SampleParams);
        original.Set("amount", 0.5f);
        original.Set("size", 16);
        original.Set("enabled", false);
        original.Set("mode", "b");

        var dict = original.ToDictionary();
        var restored = FilterValues.FromDictionary(
            SampleParams,
            dict.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
        );

        Assert.Equal(0.5f, restored.GetFloat("amount"), 3);
        Assert.Equal(16, restored.GetInt("size"));
        Assert.False(restored.GetBool("enabled"));
        Assert.Equal("b", restored.GetChoice("mode"));
    }

    [Fact]
    public void FilterEntry_DisplayName_Strips_Mnemonics()
    {
        var entry = new FilterEntry(
            "invert",
            "Color",
            "_Invert",
            (_, _) => { },
            []
        );
        Assert.Equal("Invert", entry.DisplayName);
        Assert.False(entry.HasParams);
        Assert.False(entry.IsToolbarFavorite);
    }

    [Fact]
    public void FromDictionary_Missing_Keys_Keep_Defaults()
    {
        var restored = FilterValues.FromDictionary(
            SampleParams,
            new Dictionary<string, object?> { ["size"] = 32 }
        );
        Assert.Equal(0.35f, restored.GetFloat("amount"), 3);
        Assert.Equal(32, restored.GetInt("size"));
    }
}
