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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ChromaLab.Controls;

/// <summary>
/// Renders a vendored Lucide stroke icon (24×24 viewBox) tinted with <see cref="Foreground"/>.
/// </summary>
public sealed class LabIcon : Decorator
{
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<LabIcon, string?>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<LabIcon, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<LabIcon, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<LabIcon, double>(nameof(StrokeWidth), 2);

    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform };
    private readonly Canvas _canvas = new() { Width = 24, Height = 24 };

    public LabIcon()
    {
        _viewbox.Child = _canvas;
        Child = _viewbox;
        Width = Size;
        Height = Size;
    }

    /// <summary>Lucide icon id (e.g. <c>save</c>, <c>folder-open</c>).</summary>
    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Stroke brush (supports solids and gradients).</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Display size in DIPs.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Stroke thickness in Lucide viewBox units (default 2).</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (
            change.Property == KindProperty
            || change.Property == ForegroundProperty
            || change.Property == StrokeWidthProperty
        )
        {
            Rebuild();
        }
        else if (change.Property == SizeProperty)
        {
            Width = Size;
            Height = Size;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        _canvas.Children.Clear();
        var kind = Kind;
        if (string.IsNullOrWhiteSpace(kind))
            return;
        if (!LucideIconData.Paths.TryGetValue(kind, out var parts))
            return;

        var brush = Foreground ?? Brushes.White;
        var thickness = StrokeWidth;
        foreach (var data in parts)
        {
            _canvas.Children.Add(
                new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse(data),
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent,
                }
            );
        }
    }
}
