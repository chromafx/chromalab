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
using ChromaFx.Processing;
using ChromaLab.Filters;

namespace ChromaLab.Macros;

/// <summary>
/// Resolves catalog ids and applies macro steps to an image processing context / layer.
/// </summary>
public static class MacroRunner
{
    /// <summary>
    /// Applies every step to <paramref name="image"/> in order (mutates in place).
    /// </summary>
    public static void Apply(Image image, MacroDefinition macro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(macro);

        foreach (var step in macro.Steps)
        {
            ct.ThrowIfCancellationRequested();
            var entry =
                FilterCatalog.All.FirstOrDefault(e => e.Id == step.Id)
                ?? throw new InvalidOperationException($"Unknown filter id '{step.Id}'.");
            var values = FilterValues.FromDictionary(entry.Params, step.Params);
            image.Mutate(ctx => entry.Apply(ctx, values));
        }
    }

    /// <summary>
    /// Validates that every step id exists in the catalog.
    /// </summary>
    public static IReadOnlyList<string> MissingIds(MacroDefinition macro) =>
        macro
            .Steps.Select(s => s.Id)
            .Where(id => FilterCatalog.All.All(e => e.Id != id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns null when valid; otherwise an error message listing unknown ids.
    /// </summary>
    public static string? Validate(MacroDefinition macro)
    {
        var missing = MissingIds(macro);
        return missing.Count == 0
            ? null
            : "Unknown filter id(s): " + string.Join(", ", missing);
    }

    /// <summary>
    /// Throws when the macro references unknown catalog ids.
    /// </summary>
    public static void ValidateOrThrow(MacroDefinition macro)
    {
        var error = Validate(macro);
        if (error is not null)
            throw new InvalidOperationException(error);
    }
}
