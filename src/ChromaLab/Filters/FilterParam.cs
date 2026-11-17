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
/// Kind of inspector control for a filter parameter.
/// </summary>
public enum FilterParamKind
{
    Float,
    Int,
    Bool,
    Choice,
}

/// <summary>
/// One choice option for <see cref="FilterParamKind.Choice"/>.
/// </summary>
public sealed record FilterChoice(string Id, string Label);

/// <summary>
/// Declares a tunable parameter shown in the filter inspector.
/// </summary>
public sealed record FilterParam(
    string Id,
    string Label,
    FilterParamKind Kind,
    double Default = 0,
    double Min = 0,
    double Max = 1,
    double Step = 0.01,
    bool DefaultBool = false,
    string? DefaultChoiceId = null,
    IReadOnlyList<FilterChoice>? Choices = null
)
{
    public static FilterParam Float(
        string id,
        string label,
        double defaultValue,
        double min,
        double max,
        double step = 0.01
    ) => new(id, label, FilterParamKind.Float, defaultValue, min, max, step);

    public static FilterParam Int(
        string id,
        string label,
        int defaultValue,
        int min,
        int max,
        int step = 1
    ) => new(id, label, FilterParamKind.Int, defaultValue, min, max, step);

    public static FilterParam Bool(string id, string label, bool defaultValue = false) =>
        new(id, label, FilterParamKind.Bool, DefaultBool: defaultValue);

    public static FilterParam Choice(
        string id,
        string label,
        string defaultChoiceId,
        params FilterChoice[] choices
    ) =>
        new(
            id,
            label,
            FilterParamKind.Choice,
            DefaultChoiceId: defaultChoiceId,
            Choices: choices
        );
}

/// <summary>
/// Runtime values for a filter's parameters (from defaults or the inspector).
/// </summary>
public sealed class FilterValues
{
    private readonly Dictionary<string, object> _map;

    private FilterValues(Dictionary<string, object> map) => _map = map;

    public static FilterValues FromDefaults(IReadOnlyList<FilterParam> parameters)
    {
        var map = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var p in parameters)
        {
            map[p.Id] = p.Kind switch
            {
                FilterParamKind.Float => p.Default,
                FilterParamKind.Int => (int)Math.Round(p.Default),
                FilterParamKind.Bool => p.DefaultBool,
                FilterParamKind.Choice => p.DefaultChoiceId
                    ?? p.Choices?.FirstOrDefault()?.Id
                    ?? "",
                _ => p.Default,
            };
        }

        return new FilterValues(map);
    }

    public float GetFloat(string id) => Convert.ToSingle(_map[id]);

    public int GetInt(string id) => Convert.ToInt32(_map[id]);

    public bool GetBool(string id) => Convert.ToBoolean(_map[id]);

    public string GetChoice(string id) => Convert.ToString(_map[id]) ?? "";

    public T GetEnum<T>(string id)
        where T : struct, Enum => Enum.Parse<T>(GetChoice(id), ignoreCase: true);

    public void Set(string id, object value) => _map[id] = value;

    public FilterValues Clone() => new(new Dictionary<string, object>(_map, StringComparer.Ordinal));

    /// <summary>
    /// Snapshot for JSON / macro serialization (JSON-friendly primitives).
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var copy = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in _map)
        {
            copy[key] = value switch
            {
                float f => f,
                double d => d,
                int i => i,
                long l => l,
                bool b => b,
                string s => s,
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                    ?? "",
            };
        }

        return copy;
    }

    /// <summary>
    /// Rebuilds values from a dictionary, coerced to each parameter's kind (missing → defaults).
    /// </summary>
    public static FilterValues FromDictionary(
        IReadOnlyList<FilterParam> parameters,
        IReadOnlyDictionary<string, object?>? raw
    )
    {
        var values = FromDefaults(parameters);
        if (raw is null || raw.Count == 0)
            return values;

        foreach (var param in parameters)
        {
            if (!raw.TryGetValue(param.Id, out var boxed) || boxed is null)
                continue;

            values.Set(
                param.Id,
                param.Kind switch
                {
                    FilterParamKind.Float => Convert.ToSingle(
                        boxed,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    FilterParamKind.Int => Convert.ToInt32(
                        boxed,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    FilterParamKind.Bool => Convert.ToBoolean(
                        boxed,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    FilterParamKind.Choice => Convert.ToString(
                        boxed,
                        System.Globalization.CultureInfo.InvariantCulture
                    ) ?? "",
                    _ => boxed,
                }
            );
        }

        return values;
    }
}
