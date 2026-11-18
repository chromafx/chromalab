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

using ChromaLab.Controls;
using ChromaLab.Filters;

namespace ChromaLab.Tests.Filters;

public class FilterCatalogTests
{
    [Fact]
    public void All_Ids_Are_Unique()
    {
        var ids = FilterCatalog.All.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_Declared_Group_Is_NonEmpty()
    {
        var present = FilterCatalog.All.Select(e => e.Group).ToHashSet(StringComparer.Ordinal);
        foreach (var group in FilterCatalog.GroupOrder)
            Assert.True(present.Contains(group), $"Group '{group}' has no entries.");
    }

    [Fact]
    public void GroupsByOrder_Follows_GroupOrder()
    {
        var groups = FilterCatalog.GroupsByOrder().Select(g => g.Key).ToList();
        var known = FilterCatalog.GroupOrder.Where(g => groups.Contains(g)).ToList();
        Assert.Equal(known, groups.Where(g => FilterCatalog.GroupOrder.Contains(g)).ToList());
    }

    [Fact]
    public void ToolbarFavorites_Icons_Exist_In_LucideIconData()
    {
        var favorites = FilterCatalog.ToolbarFavorites.ToList();
        Assert.NotEmpty(favorites);
        foreach (var entry in favorites)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.ToolbarIcon));
            Assert.True(
                LucideIconData.Paths.ContainsKey(entry.ToolbarIcon!),
                $"Missing Lucide kind '{entry.ToolbarIcon}' for filter '{entry.Id}'."
            );
        }
    }
}
