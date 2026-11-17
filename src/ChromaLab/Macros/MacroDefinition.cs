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

namespace ChromaLab.Macros;

/// <summary>
/// One catalog Apply step (id + parameter bag).
/// </summary>
public sealed record MacroStep(string Id, IReadOnlyDictionary<string, object?> Params);

/// <summary>
/// Ordered filter macro (versioned JSON document).
/// </summary>
public sealed class MacroDefinition
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public string Name { get; init; } = "Untitled";
    public IReadOnlyList<MacroStep> Steps { get; init; } = [];
}
