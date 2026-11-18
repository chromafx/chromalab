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

using ChromaLab.Layers;

namespace ChromaLab.Services;

/// <summary>
/// Undo/redo stacks of <see cref="LayerDocument"/> snapshots.
/// </summary>
internal sealed class DocumentHistory
{
    private readonly List<LayerDocument> _undo = [];
    private readonly List<LayerDocument> _redo = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void PushUndo(LayerDocument snapshot)
    {
        _undo.Add(snapshot);
        while (_undo.Count > ImageSession.MaxHistory)
            _undo.RemoveAt(0);
        _redo.Clear();
    }

    public LayerDocument? TryUndo(LayerDocument current)
    {
        if (_undo.Count == 0)
            return null;
        _redo.Add(current);
        return Pop(_undo);
    }

    public LayerDocument? TryRedo(LayerDocument current)
    {
        if (_redo.Count == 0)
            return null;
        _undo.Add(current);
        while (_undo.Count > ImageSession.MaxHistory)
            _undo.RemoveAt(0);
        return Pop(_redo);
    }

    private static LayerDocument Pop(List<LayerDocument> history)
    {
        var last = history[^1];
        history.RemoveAt(history.Count - 1);
        return last;
    }
}
