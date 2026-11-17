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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromaLab.Macros;

/// <summary>
/// Load/save <c>*.chromafx-macro.json</c>.
/// </summary>
public static class MacroJson
{
    public const string Extension = ".chromafx-macro.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonElementToObjectConverter() },
    };

    public static void Write(string path, MacroDefinition macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        var dto = new MacroFileDto
        {
            Version = macro.Version <= 0 ? MacroDefinition.CurrentVersion : macro.Version,
            Name = macro.Name,
            Steps = macro
                .Steps.Select(s => new MacroStepDto
                {
                    Id = s.Id,
                    Params = s.Params.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value,
                        StringComparer.Ordinal
                    ),
                })
                .ToList(),
        };
        var json = JsonSerializer.Serialize(dto, Options);
        File.WriteAllText(path, json);
    }

    public static MacroDefinition Read(string path)
    {
        var json = File.ReadAllText(path);
        var dto =
            JsonSerializer.Deserialize<MacroFileDto>(json, Options)
            ?? throw new InvalidDataException("Macro file is empty.");
        if (dto.Version != MacroDefinition.CurrentVersion)
            throw new InvalidDataException(
                $"Unsupported macro version {dto.Version} (expected {MacroDefinition.CurrentVersion})."
            );

        var steps = (dto.Steps ?? [])
            .Select(s =>
            {
                if (string.IsNullOrWhiteSpace(s.Id))
                    throw new InvalidDataException("Macro step is missing id.");
                IReadOnlyDictionary<string, object?> parms =
                    s.Params ?? new Dictionary<string, object?>(StringComparer.Ordinal);
                return new MacroStep(s.Id, parms);
            })
            .ToList();

        return new MacroDefinition
        {
            Version = dto.Version,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name.Trim(),
            Steps = steps,
        };
    }

    private sealed class MacroFileDto
    {
        public int Version { get; set; }
        public string? Name { get; set; }
        public List<MacroStepDto>? Steps { get; set; }
    }

    private sealed class MacroStepDto
    {
        public string? Id { get; set; }
        public Dictionary<string, object?>? Params { get; set; }
    }

    /// <summary>
    /// Deserializes JSON object values as CLR primitives instead of <see cref="JsonElement"/>.
    /// </summary>
    private sealed class JsonElementToObjectConverter : JsonConverter<object?>
    {
        public override object? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        ) => ReadValue(ref reader);

        public override void Write(
            Utf8JsonWriter writer,
            object? value,
            JsonSerializerOptions options
        )
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }

        private static object? ReadValue(ref Utf8JsonReader reader) =>
            reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number when reader.TryGetInt32(out var i) => i,
                JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.StartObject => ReadObject(ref reader),
                JsonTokenType.StartArray => ReadArray(ref reader),
                _ => throw new JsonException($"Unexpected token {reader.TokenType}."),
            };

        private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return map;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name.");
                var name = reader.GetString() ?? "";
                reader.Read();
                map[name] = ReadValue(ref reader);
            }

            throw new JsonException("Unterminated object.");
        }

        private static List<object?> ReadArray(ref Utf8JsonReader reader)
        {
            var list = new List<object?>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;
                list.Add(ReadValue(ref reader));
            }

            throw new JsonException("Unterminated array.");
        }
    }
}
