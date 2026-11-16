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
using ChromaFx.Processing;
using ChromaLab.Layers;
using ChromaLab.Services;
using FxImage = ChromaFx.Core.Image;

namespace ChromaLab.Controls;

public partial class LayersPanel : UserControl
{
    private const int ThumbSize = 40;
    private readonly List<WriteableBitmap> _thumbs = [];
    private bool _enabled;
    private bool _suppressToggle;

    public LayersPanel()
    {
        InitializeComponent();
        AddButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        DupButton.Click += (_, _) => DuplicateRequested?.Invoke(this, EventArgs.Empty);
        DeleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        UpButton.Click += (_, _) => MoveUpRequested?.Invoke(this, EventArgs.Empty);
        DownButton.Click += (_, _) => MoveDownRequested?.Invoke(this, EventArgs.Empty);
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

    public void Refresh(LayerDocument? document, bool canEdit)
    {
        ClearThumbs();
        LayersHost.Children.Clear();

        if (document is null || document.Layers.Count == 0)
        {
            SetEnabled(false);
            return;
        }

        _enabled = canEdit;
        var multi = document.Layers.Count > 1;
        AddButton.IsEnabled = canEdit;
        DupButton.IsEnabled = canEdit;
        DeleteButton.IsEnabled = canEdit && multi;
        UpButton.IsEnabled = canEdit && document.ActiveIndex < document.Layers.Count - 1;
        DownButton.IsEnabled = canEdit && document.ActiveIndex > 0;

        _suppressToggle = true;
        try
        {
            // Top of list = top of stack (reverse of storage order).
            for (var display = document.Layers.Count - 1; display >= 0; display--)
                AddRow(document, display);
        }
        finally
        {
            _suppressToggle = false;
        }
    }

    private void AddRow(LayerDocument document, int index)
    {
        var layer = document.Layers[index];
        var active = index == document.ActiveIndex;

        var row = new Border
        {
            Background = active
                ? (IBrush?)(
                    this.FindResource("EditorAccentMutedBrush") as IBrush
                    ?? new SolidColorBrush(Color.Parse("#2A3F55"))
                )
                : Brushes.Transparent,
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
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

        var thumb = BuildThumb(layer.Pixels);
        var name = new TextBlock
        {
            Text = layer.Name,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(8, 0, 4, 0),
            FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal,
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
            Child = new Avalonia.Controls.Image
            {
                Source = thumb,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
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
            if (e.Source is CheckBox)
                return;
            LayerSelected?.Invoke(this, index);
        };

        LayersHost.Children.Add(row);
    }

    private WriteableBitmap? BuildThumb(FxImage pixels)
    {
        try
        {
            var clone = pixels.Clone();
            var scale = Math.Min(
                (double)ThumbSize / Math.Max(1, clone.Width),
                (double)ThumbSize / Math.Max(1, clone.Height)
            );
            var w = Math.Max(1, (int)Math.Round(clone.Width * scale));
            var h = Math.Max(1, (int)Math.Round(clone.Height * scale));
            if (w != clone.Width || h != clone.Height)
                clone.Mutate(c => c.Resize(w, h));
            var bmp = AvaloniaImageBridge.ToBitmap(clone);
            _thumbs.Add(bmp);
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private void ClearThumbs()
    {
        foreach (var thumb in _thumbs)
            thumb.Dispose();
        _thumbs.Clear();
    }
}
