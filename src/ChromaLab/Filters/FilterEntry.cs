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

using ChromaFx.Processing;

namespace ChromaLab.Filters;

/// <summary>
/// One lab filter: menu identity, optional inspector params, and apply delegate.
/// Param-less filters apply immediately; parametric ones open the inspector first.
/// </summary>
public sealed record FilterEntry(
    string Id,
    string Group,
    string Header,
    Action<IImageProcessingContext, FilterValues> Apply,
    IReadOnlyList<FilterParam> Params,
    string? ToolbarIcon = null,
    string? ToolbarTip = null
)
{
    /// <summary>
    /// Gets whether this entry appears on the toolbar.
    /// </summary>
    public bool IsToolbarFavorite => ToolbarIcon is not null;

    /// <summary>
    /// Gets whether the inspector should open before apply.
    /// </summary>
    public bool HasParams => Params.Count > 0;

    /// <summary>
    /// Display name without menu mnemonics.
    /// </summary>
    public string DisplayName => Header.Replace("_", "", StringComparison.Ordinal);

    /// <summary>
    /// Creates a values bag seeded from parameter defaults.
    /// </summary>
    public FilterValues CreateDefaultValues() => FilterValues.FromDefaults(Params);
}
