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
using ChromaFx.IO.Formats.Tiff;
using ChromaLab.Services;

namespace ChromaLab.Controls;

/// <summary>
/// Shared encoder-options UI for Save As / Export / Bulk dialogs.
/// </summary>
public static class ExportOptionsPanel
{
    public static void Rebuild(Panel host, ExportFormat? format, ExportOptionsState options)
    {
        host.Children.Clear();
        if (format is null)
            return;

        var panel = format.CreateOptionsPanel(options);
        if (panel is null)
        {
            host.Children.Add(
                new TextBlock
                {
                    Text = "No encoder options for this format.",
                    FontSize = 12,
                    Opacity = 0.55,
                }
            );
        }
        else
        {
            host.Children.Add(panel);
        }
    }

    public static Control QualityPanel(string label, int value, int min, int max, Action<int> set)
    {
        var valueText = new TextBlock
        {
            Text = value.ToString(),
            FontSize = 11,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            MinHeight = 24,
        };
        slider.ValueChanged += (_, e) =>
        {
            var v = (int)Math.Round(e.NewValue);
            set(v);
            valueText.Text = v.ToString();
        };

        var header = new DockPanel { Margin = new Avalonia.Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(valueText, Dock.Right);
        header.Children.Add(valueText);
        header.Children.Add(
            new TextBlock
            {
                Text = label,
                FontSize = 12,
                Opacity = 0.85,
                VerticalAlignment = VerticalAlignment.Center,
            }
        );

        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        body.Children.Add(header);
        body.Children.Add(slider);
        return body;
    }

    public static Control NativeQualityPanel(
        int quality,
        bool lossless,
        Action<int> setQuality,
        Action<bool> setLossless
    )
    {
        var panel = new StackPanel { Spacing = 10 };
        var qualityBlock = QualityPanel("Quality", quality, 0, 100, setQuality);
        var losslessBox = new CheckBox
        {
            Content = "Lossless",
            IsChecked = lossless,
            FontSize = 12,
        };
        losslessBox.IsCheckedChanged += (_, _) =>
        {
            var on = losslessBox.IsChecked == true;
            setLossless(on);
            qualityBlock.IsEnabled = !on;
        };
        qualityBlock.IsEnabled = !lossless;
        panel.Children.Add(losslessBox);
        panel.Children.Add(qualityBlock);
        return panel;
    }

    public static Control TiffCompressionPanel(ExportOptionsState state)
    {
        var none = new ComboBoxItem { Content = "None", Tag = TiffCompression.None };
        var deflate = new ComboBoxItem { Content = "Deflate", Tag = TiffCompression.Deflate };
        var combo = new ComboBox
        {
            ItemsSource = new[] { none, deflate },
            SelectedItem = state.TiffCompression == TiffCompression.None ? none : deflate,
            MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: TiffCompression c })
                state.TiffCompression = c;
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(
            new TextBlock
            {
                Text = "Compression",
                FontSize = 12,
                Opacity = 0.85,
            }
        );
        stack.Children.Add(combo);
        return stack;
    }
}
