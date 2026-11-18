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

namespace ChromaLab.Services;

/// <summary>
/// Why <see cref="ImageSession"/> raised <see cref="ImageSession.Changed"/>.
/// </summary>
public enum SessionChangeKind
{
    /// <summary>Document pixels/structure/dirty changed — full shell refresh.</summary>
    Full,

    /// <summary>Display bitmap only (live preview, compare, animation frame).</summary>
    PreviewOnly,

    /// <summary>Active layer selection changed (no pixel mutation).</summary>
    Selection,
}
