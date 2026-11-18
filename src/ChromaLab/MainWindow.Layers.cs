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

namespace ChromaLab;

public partial class MainWindow
{
    private void EditLayers(Action<LayerDocument> edit)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelTransientModes(clearInspector: false);
        try
        {
            _session.EditDocument(edit);
            StatusText.Text = _session.LayerLabel;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Layers: {ex.Message}";
        }
    }

    private void SelectLayerIndex(int index)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelTransientModes(clearInspector: true);
        _session.ClearPreview();
        try
        {
            _session.SelectLayer(index);
            StatusText.Text = _session.LayerLabel;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Layers: {ex.Message}";
        }
    }

    private void SelectAdjacentLayer(int delta)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelTransientModes(clearInspector: true);
        _session.ClearPreview();
        _session.SelectAdjacentLayer(delta);
        StatusText.Text = _session.LayerLabel;
    }
}
