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

namespace ChromaLab.Layers;

/// <summary>
/// Flat raster layer stack (bottom→top). Nested ORA groups are flattened on import.
/// </summary>
public sealed class LayerDocument
{
    private readonly List<RasterLayer> _layers = [];
    private int _activeIndex;

    public LayerDocument(int width, int height)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Document size must be at least 1×1.");
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Layers from bottom (index 0) to top.</summary>
    public IReadOnlyList<RasterLayer> Layers => _layers;

    /// <summary>Index of the layer filters / edits target.</summary>
    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            if (_layers.Count == 0)
                throw new InvalidOperationException("Document has no layers.");
            if (value < 0 || value >= _layers.Count)
                throw new ArgumentOutOfRangeException(nameof(value));
            _activeIndex = value;
        }
    }

    public RasterLayer ActiveLayer => _layers[ActiveIndex];

    public string LayerStatusLabel =>
        _layers.Count == 0
            ? "No layers"
            : $"Layer {ActiveIndex + 1}/{_layers.Count} · {ActiveLayer.Name}";

    /// <summary>Single-layer document from a still image (takes ownership of <paramref name="image"/>).</summary>
    public static LayerDocument FromImage(Image image, string layerName = "Background")
    {
        ArgumentNullException.ThrowIfNull(image);
        var doc = new LayerDocument(image.Width, image.Height);
        doc._layers.Add(new RasterLayer(layerName, image));
        doc._activeIndex = 0;
        return doc;
    }

    public Image Composite() => LayerCompositor.Composite(this);

    public LayerDocument Clone()
    {
        var copy = new LayerDocument(Width, Height);
        foreach (var layer in _layers)
            copy._layers.Add(layer.Clone());
        copy._activeIndex = Math.Clamp(_activeIndex, 0, Math.Max(0, copy._layers.Count - 1));
        return copy;
    }

    public void AddLayer(RasterLayer layer, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.Add(layer);
        if (makeActive)
            _activeIndex = _layers.Count - 1;
    }

    public RasterLayer AddEmptyLayer(string? name = null)
    {
        var layer = new RasterLayer(name ?? $"Layer {_layers.Count + 1}", new Image(Width, Height));
        AddLayer(layer);
        return layer;
    }

    public RasterLayer DuplicateActive()
    {
        var clone = ActiveLayer.Clone();
        clone.Name = ActiveLayer.Name + " copy";
        _layers.Insert(ActiveIndex + 1, clone);
        _activeIndex++;
        return clone;
    }

    /// <summary>Removes the active layer. Keeps at least one layer.</summary>
    public bool TryDeleteActive()
    {
        if (_layers.Count <= 1)
            return false;

        _layers.RemoveAt(_activeIndex);
        if (_activeIndex >= _layers.Count)
            _activeIndex = _layers.Count - 1;
        return true;
    }

    public void SelectPrevious()
    {
        if (_layers.Count == 0)
            return;
        _activeIndex = (_activeIndex + _layers.Count - 1) % _layers.Count;
    }

    public void SelectNext()
    {
        if (_layers.Count == 0)
            return;
        _activeIndex = (_activeIndex + 1) % _layers.Count;
    }

    public void ToggleActiveVisible() => ActiveLayer.Visible = !ActiveLayer.Visible;

    /// <summary>Used by <see cref="Services.Ora.OraCodec"/> when building from stack.xml (bottom→top).</summary>
    internal void AddLayerInternal(RasterLayer layer) => _layers.Add(layer);

    internal void SetActiveIndexInternal(int index) =>
        _activeIndex = Math.Clamp(index, 0, Math.Max(0, _layers.Count - 1));
}
