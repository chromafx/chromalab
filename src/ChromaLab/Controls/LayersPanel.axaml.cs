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

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using ChromaLab.Layers;
using ChromaLab.Services;
using FxImage = ChromaFx.Core.Image;

namespace ChromaLab.Controls;

public partial class LayersPanel : UserControl
{
    private const int ThumbSize = 40;
    private readonly Dictionary<int, (FxImage Pixels, int W, int H, WriteableBitmap Bmp)> _thumbCache =
        [];
    private readonly List<WriteableBitmap> _ownedThumbs = [];
    private bool _enabled;
    private bool _suppressToggle;
    private LayerDocument? _document;

    public LayersPanel()
    {
        InitializeComponent();
        AddButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        DupButton.Click += (_, _) => DuplicateRequested?.Invoke(this, EventArgs.Empty);
        DeleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        UpButton.Click += (_, _) => MoveUpRequested?.Invoke(this, EventArgs.Empty);
        DownButton.Click += (_, _) => MoveDownRequested?.Invoke(this, EventArgs.Empty);
        DetachedFromVisualTree += (_, _) => ClearThumbs();
        SetEnabled(false);
    }

    public event EventHandler? AddRequested;
    public event EventHandler? DuplicateRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? MoveUpRequested;
    public event EventHandler? MoveDownRequested;
    public event EventHandler<int>? LayerSelected;
    public event EventHandler<(int Index, bool Visible)>? VisibilityChanged;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        AddButton.IsEnabled = enabled;
        DupButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
        UpButton.IsEnabled = enabled;
        DownButton.IsEnabled = enabled;
    }

    /// <summary>
    /// Full rebuild of layer rows and thumbnails.
    /// </summary>
    public void Refresh(LayerDocument? document, bool canEdit)
    {
        _document = document;
        ClearThumbs();
        LayersHost.Children.Clear();

        if (document is null || document.Layers.Count == 0)
        {
            SetEnabled(false);
            return;
        }

        ApplyChrome(document, canEdit);

        _suppressToggle = true;
        try
        {
            for (var display = document.Layers.Count - 1; display >= 0; display--)
                AddRow(document, display);
        }
        finally
        {
            _suppressToggle = false;
        }
    }

    /// <summary>
    /// Updates active highlight and chrome without rebuilding thumbnails.
    /// </summary>
    public void RefreshSelection(LayerDocument? document, bool canEdit)
    {
        if (document is null || document.Layers.Count == 0)
        {
            Refresh(document, canEdit);
            return;
        }

        if (
            _document != document
            || LayersHost.Children.Count != document.Layers.Count
        )
        {
            Refresh(document, canEdit);
            return;
        }

        _document = document;
        ApplyChrome(document, canEdit);

        // Storage index 0 is bottom; UI rows are top-first.
        for (var display = 0; display < document.Layers.Count; display++)
        {
            var storageIndex = document.Layers.Count - 1 - display;
            if (LayersHost.Children[display] is not Border row)
                continue;

            var active = storageIndex == document.ActiveIndex;
            row.Background = active
                ? (IBrush?)(
                    this.FindResource("EditorAccentMutedBrush") as IBrush
                    ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#2A3F55"))
                )
                : Brushes.Transparent;

            if (row.Child is Grid grid
                && grid.Children.OfType<DockPanel>().FirstOrDefault() is { } mid
                && mid.Children.OfType<TextBlock>().FirstOrDefault() is { } name)
            {
                name.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
                name.Opacity = document.Layers[storageIndex].Visible ? 1 : 0.45;
            }
        }
    }

    private void ApplyChrome(LayerDocument document, bool canEdit)
    {
        _enabled = canEdit;
        var multi = document.Layers.Count > 1;
        AddButton.IsEnabled = canEdit;
        DupButton.IsEnabled = canEdit;
        DeleteButton.IsEnabled = canEdit && multi;
        UpButton.IsEnabled = canEdit && document.ActiveIndex < document.Layers.Count - 1;
        DownButton.IsEnabled = canEdit && document.ActiveIndex > 0;
    }

    private void AddRow(LayerDocument document, int index)
    {
        var layer = document.Layers[index];
        var active = index == document.ActiveIndex;

        var row = new Border
        {
            Classes = { "layer-row" },
            Background = active
                ? (IBrush?)(
                    this.FindResource("EditorAccentMutedBrush") as IBrush
                    ?? new SolidColorBrush(Avalonia.Media.Color.Parse("#2A3F55"))
                )
                : Brushes.Transparent,
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Opacity = layer.Visible ? 1 : 0.85,
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            MinHeight = ThumbSize + 4,
        };

        var eye = new CheckBox
        {
            IsChecked = layer.Visible,
            IsEnabled = _enabled,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 6, 0),
            [ToolTip.TipProperty] = "Visibility",
        };
        eye.IsCheckedChanged += (_, _) =>
        {
            if (_suppressToggle)
                return;
            VisibilityChanged?.Invoke(this, (index, eye.IsChecked == true));
        };
        Grid.SetColumn(eye, 0);

        var thumb = GetOrBuildThumb(index, layer.Pixels);
        var name = new TextBlock
        {
            Text = layer.Name,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(8, 0, 4, 0),
            FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal,
            Opacity = layer.Visible ? 1 : 0.45,
        };

        var mid = new DockPanel();
        var thumbBorder = new Border
        {
            Width = ThumbSize,
            Height = ThumbSize,
            CornerRadius = new Avalonia.CornerRadius(3),
            ClipToBounds = true,
            BorderBrush = this.FindResource("EditorBorderBrush") as IBrush,
            BorderThickness = new Avalonia.Thickness(1),
            Background = this.FindResource("CheckerBrush") as IBrush,
            Child = new Avalonia.Controls.Image
            {
                Source = thumb,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = layer.Visible ? 1 : 0.4,
            },
        };
        DockPanel.SetDock(thumbBorder, Dock.Left);
        mid.Children.Add(thumbBorder);
        mid.Children.Add(name);
        Grid.SetColumn(mid, 1);

        grid.Children.Add(eye);
        grid.Children.Add(mid);
        row.Child = grid;

        row.PointerPressed += (_, e) =>
        {
            if ((e.Source as Avalonia.Visual)?.FindAncestorOfType<CheckBox>(includeSelf: true) is not null)
                return;
            LayerSelected?.Invoke(this, index);
        };

        LayersHost.Children.Add(row);
    }

    private WriteableBitmap? GetOrBuildThumb(int index, FxImage pixels)
    {
        try
        {
            var bmp = AvaloniaImageBridge.ToThumbnailBitmap(pixels, ThumbSize);
            _ownedThumbs.Add(bmp);
            _thumbCache[index] = (pixels, pixels.Width, pixels.Height, bmp);
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private void ClearThumbs()
    {
        foreach (var thumb in _ownedThumbs)
            thumb.Dispose();
        _ownedThumbs.Clear();
        _thumbCache.Clear();
    }
}
