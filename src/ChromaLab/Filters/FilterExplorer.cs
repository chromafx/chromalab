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

namespace ChromaLab.Filters;

/// <summary>
/// Explorer helpers: randomize params, normalized center pick, variant bags.
/// </summary>
public static class FilterExplorer
{
    public const int VariantCount = 5;

    /// <summary>
    /// True when the entry exposes normalized <c>cx</c>/<c>cy</c> (0–1) center knobs.
    /// </summary>
    public static bool HasNormalizedCenter(FilterEntry entry) =>
        FindCenterIds(entry) is not null;

    /// <summary>
    /// Sets normalized center (0–1) when the entry has <c>cx</c>/<c>cy</c>.
    /// </summary>
    public static bool TrySetCenter(FilterEntry entry, FilterValues values, float x, float y)
    {
        var ids = FindCenterIds(entry);
        if (ids is null)
            return false;

        values.Set(ids.Value.Cx, Math.Clamp(x, 0f, 1f));
        values.Set(ids.Value.Cy, Math.Clamp(y, 0f, 1f));
        return true;
    }

    /// <summary>
    /// Randomizes float/int/bool/choice params within declared ranges (E0 / generative).
    /// </summary>
    public static void Randomize(FilterEntry entry, FilterValues values, System.Random? rng = null)
    {
        rng ??= System.Random.Shared;
        foreach (var param in entry.Params)
        {
            switch (param.Kind)
            {
                case FilterParamKind.Float:
                {
                    var min = param.Min;
                    var max = param.Max;
                    if (max < min)
                        (min, max) = (max, min);
                    var t = rng.NextDouble();
                    var v = min + (max - min) * t;
                    if (param.Step > 0)
                        v = Math.Round(v / param.Step) * param.Step;
                    values.Set(param.Id, (float)Math.Clamp(v, min, max));
                    break;
                }
                case FilterParamKind.Int:
                {
                    var min = (int)Math.Round(param.Min);
                    var max = (int)Math.Round(param.Max);
                    if (max < min)
                        (min, max) = (max, min);
                    values.Set(param.Id, rng.Next(min, max + 1));
                    break;
                }
                case FilterParamKind.Bool:
                    values.Set(param.Id, rng.Next(2) == 1);
                    break;
                case FilterParamKind.Choice:
                {
                    var choices = param.Choices;
                    if (choices is null || choices.Count == 0)
                        break;
                    values.Set(param.Id, choices[rng.Next(choices.Count)].Id);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Builds <paramref name="count"/> independent randomized value bags (E3).
    /// </summary>
    public static FilterValues[] CreateVariants(
        FilterEntry entry,
        int count = VariantCount,
        System.Random? rng = null
    )
    {
        rng ??= System.Random.Shared;
        var result = new FilterValues[count];
        for (var i = 0; i < count; i++)
        {
            var values = entry.CreateDefaultValues();
            Randomize(entry, values, rng);
            result[i] = values;
        }

        return result;
    }

    private static (string Cx, string Cy)? FindCenterIds(FilterEntry entry)
    {
        string? cx = null;
        string? cy = null;
        foreach (var p in entry.Params)
        {
            if (p.Kind != FilterParamKind.Float)
                continue;
            if (p.Id.Equals("cx", StringComparison.OrdinalIgnoreCase)
                || p.Id.Equals("centerX", StringComparison.OrdinalIgnoreCase))
                cx = p.Id;
            else if (p.Id.Equals("cy", StringComparison.OrdinalIgnoreCase)
                     || p.Id.Equals("centerY", StringComparison.OrdinalIgnoreCase))
                cy = p.Id;
        }

        return cx is not null && cy is not null ? (cx, cy) : null;
    }
}
