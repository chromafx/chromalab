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

namespace ChromaLab.Macros;

/// <summary>
/// Collects committed catalog Applies into a macro buffer.
/// </summary>
public sealed class MacroRecorder
{
    private readonly List<MacroStep> _steps = [];

    public bool IsRecording { get; private set; }
    public int StepCount => _steps.Count;

    public void Start()
    {
        _steps.Clear();
        IsRecording = true;
    }

    public void Stop() => IsRecording = false;

    /// <summary>
    /// Appends a step when recording. No-op when idle.
    /// </summary>
    public void Record(FilterEntry entry, FilterValues values)
    {
        if (!IsRecording)
            return;

        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(values);

        var parms = values
            .ToDictionary()
            .ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal);
        _steps.Add(new MacroStep(entry.Id, parms));
    }

    public MacroDefinition ToDefinition(string name = "Untitled") =>
        new()
        {
            Version = MacroDefinition.CurrentVersion,
            Name = name,
            Steps = _steps.ToList(),
        };
}
