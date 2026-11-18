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
using Avalonia.Threading;
using ChromaLab.Filters;

namespace ChromaLab.Controls;

public partial class FilterInspector : UserControl
{
    private const int VariantThumbSize = 48;

    private FilterEntry? _entry;
    private FilterValues? _values;
    private bool _canApply;
    private bool _suppressValuesChanged;
    private readonly List<WriteableBitmap> _variantBitmaps = [];
    private CancellationTokenSource? _variantsCts;
    private int _variantsGeneration;

    public FilterInspector()
    {
        InitializeComponent();
        ApplyButton.Click += (_, _) =>
        {
            if (_entry is null || _values is null)
                return;
            ApplyRequested?.Invoke(this, (_entry, _values.Clone()));
        };
        CancelButton.Click += (_, _) =>
        {
            Clear();
            CancelRequested?.Invoke(this, EventArgs.Empty);
        };
        ResetParamsButton.Click += (_, _) => ResetToDefaults();
        RandomizeButton.Click += (_, _) => RandomizeParams();
        RefreshVariantsButton.Click += (_, _) => RebuildVariants(force: true);
        DetachedFromVisualTree += (_, _) =>
        {
            CancelVariantWork();
            ClearVariants();
        };
    }

    /// <summary>
    /// Optional factory for variant thumbnails (active-layer downscale + filter).
    /// </summary>
    public Func<FilterEntry, FilterValues, WriteableBitmap?>? VariantPreviewFactory { get; set; }

    /// <summary>
    /// Raised when the user clicks Apply with the selected filter and current values.
    /// </summary>
    public event EventHandler<(FilterEntry Entry, FilterValues Values)>? ApplyRequested;

    /// <summary>
    /// Raised when the user cancels the inspector (drops preview).
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Raised when parameter values change (including after Select / Reset).
    /// </summary>
    public event EventHandler<(FilterEntry Entry, FilterValues Values)>? ValuesChanged;

    /// <summary>
    /// Gets the currently inspected filter, if any.
    /// </summary>
    public FilterEntry? SelectedEntry => _entry;

    /// <summary>
    /// Gets a clone of the current parameter bag when a filter is selected.
    /// </summary>
    public FilterValues? CurrentValues => _values?.Clone();

    /// <summary>
    /// Re-raises <see cref="ValuesChanged"/> so the host can restore a live preview.
    /// </summary>
    public void NotifyValuesChanged() => RaiseValuesChanged();

    /// <summary>
    /// Loads a parametric filter into the inspector (resets values to defaults).
    /// </summary>
    public void Select(FilterEntry entry)
    {
        if (!entry.HasParams)
            throw new ArgumentException("Inspector requires a parametric filter.", nameof(entry));

        _entry = entry;
        _values = entry.CreateDefaultValues();
        TitleText.Text = entry.DisplayName;
        HintText.IsVisible = false;
        ParamsHost.IsVisible = true;
        CenterHintText.IsVisible = FilterExplorer.HasNormalizedCenter(entry);
        RebuildEditors();
        RebuildVariants(force: true);
        UpdateButtons();
        RaiseValuesChanged();
    }

    /// <summary>
    /// Clears the inspector back to the empty state.
    /// </summary>
    public void Clear()
    {
        CancelVariantWork();
        _entry = null;
        _values = null;
        ParamsHost.Children.Clear();
        ParamsHost.IsVisible = false;
        CenterHintText.IsVisible = false;
        ClearVariants();
        TitleText.Text = "No filter selected";
        HintText.IsVisible = true;
        HintText.Text =
            "Pick a filter from the left rail. Parametric effects preview live; Apply commits to undo.";
        UpdateButtons();
    }

    /// <summary>
    /// Enables or disables Apply based on session readiness (image loaded, not busy).
    /// </summary>
    public void SetCanApply(bool canApply)
    {
        _canApply = canApply;
        UpdateButtons();
        // Do not rebuild variants on busy→idle; only on Select / Refresh / layer change.
    }

    /// <summary>
    /// Sets normalized center (0–1) when the filter exposes <c>cx</c>/<c>cy</c>.
    /// </summary>
    public bool TrySetNormalizedCenter(float x, float y)
    {
        if (_entry is null || _values is null)
            return false;
        if (!FilterExplorer.TrySetCenter(_entry, _values, x, y))
            return false;
        UpdateEditorValues();
        RaiseValuesChanged();
        return true;
    }

    private void ResetToDefaults()
    {
        if (_entry is null)
            return;
        _values = _entry.CreateDefaultValues();
        UpdateEditorValues();
        RaiseValuesChanged();
    }

    private void UpdateButtons()
    {
        var has = _entry is not null;
        ApplyButton.IsEnabled = has && _canApply;
        CancelButton.IsEnabled = has;
        ResetParamsButton.IsEnabled = has;
        RandomizeButton.IsVisible = has;
        RandomizeButton.IsEnabled = has && _canApply;
        RefreshVariantsButton.IsVisible = has;
        RefreshVariantsButton.IsEnabled = has && _canApply;
        VariantsPanel.IsVisible = has;
    }

    private void RandomizeParams()
    {
        if (_entry is null || _values is null)
            return;

        FilterExplorer.Randomize(_entry, _values);
        UpdateEditorValues();
        RaiseValuesChanged();
    }

    private void RebuildVariants(bool force = false)
    {
        if (_entry is null)
        {
            ClearVariants();
            VariantsPanel.IsVisible = false;
            return;
        }

        VariantsPanel.IsVisible = true;
        if (!_canApply && !force)
            return;

        CancelVariantWork();
        _variantsCts = new CancellationTokenSource();
        var ct = _variantsCts.Token;
        var generation = ++_variantsGeneration;
        var entry = _entry;
        var factory = VariantPreviewFactory;
        _ = BuildVariantsAsync(entry, factory, generation, ct);
    }

    private async Task BuildVariantsAsync(
        FilterEntry entry,
        Func<FilterEntry, FilterValues, WriteableBitmap?>? factory,
        int generation,
        CancellationToken cancellationToken
    )
    {
        ClearVariants(keepPanelVisible: true);
        var variants = FilterExplorer.CreateVariants(entry);

        // Yield so Select/layout stays responsive, then build thumbs with cancellation.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (cancellationToken.IsCancellationRequested || generation != _variantsGeneration)
            return;

        for (var i = 0; i < variants.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested || generation != _variantsGeneration)
                return;

            var values = variants[i];
            WriteableBitmap? thumb = null;
            try
            {
                thumb = factory?.Invoke(entry, values);
            }
            catch
            {
                // Keep a blank button if a variant fails.
            }

            if (thumb is not null)
                _variantBitmaps.Add(thumb);

            var button = new Button
            {
                Width = VariantThumbSize,
                Height = VariantThumbSize,
                Padding = new Avalonia.Thickness(2),
                [ToolTip.TipProperty] = $"Variant {i + 1}",
                Content = thumb is null
                    ? new TextBlock
                    {
                        Text = $"{i + 1}",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    }
                    : new Image
                    {
                        Source = thumb,
                        Stretch = Stretch.UniformToFill,
                        Width = VariantThumbSize - 4,
                        Height = VariantThumbSize - 4,
                    },
            };
            var captured = values;
            button.Click += (_, _) => ApplyVariant(captured);
            VariantsHost.Children.Add(button);

            if (i < variants.Length - 1)
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    private void ApplyVariant(FilterValues values)
    {
        if (_entry is null)
            return;
        _values = values.Clone();
        UpdateEditorValues();
        RaiseValuesChanged();
    }

    private void CancelVariantWork()
    {
        if (_variantsCts is null)
            return;
        _variantsCts.Cancel();
        _variantsCts.Dispose();
        _variantsCts = null;
    }

    private void ClearVariants(bool keepPanelVisible = false)
    {
        VariantsHost.Children.Clear();
        foreach (var bmp in _variantBitmaps)
            bmp.Dispose();
        _variantBitmaps.Clear();
        if (!keepPanelVisible)
            VariantsPanel.IsVisible = false;
    }

    private void RaiseValuesChanged()
    {
        if (_suppressValuesChanged || _entry is null || _values is null)
            return;
        ValuesChanged?.Invoke(this, (_entry, _values.Clone()));
    }

    private void OnValueEdited() => RaiseValuesChanged();

    private void RebuildEditors()
    {
        ParamsHost.Children.Clear();
        if (_entry is null || _values is null)
            return;

        _suppressValuesChanged = true;
        try
        {
            foreach (var param in _entry.Params)
            {
                Control editor = param.Kind switch
                {
                    FilterParamKind.Float => BuildSlider(param, integer: false),
                    FilterParamKind.Int => BuildSlider(param, integer: true),
                    FilterParamKind.Bool => BuildCheckBox(param),
                    FilterParamKind.Choice => BuildCombo(param),
                    _ => new TextBlock { Text = "(unsupported)" },
                };
                ParamsHost.Children.Add(editor);
            }
        }
        finally
        {
            _suppressValuesChanged = false;
        }
    }

    /// <summary>
    /// Updates existing editor control values in place (preserves focus / scroll).
    /// </summary>
    private void UpdateEditorValues()
    {
        if (_entry is null || _values is null)
        {
            RebuildEditors();
            return;
        }

        if (ParamsHost.Children.Count != _entry.Params.Count)
        {
            RebuildEditors();
            return;
        }

        _suppressValuesChanged = true;
        try
        {
            for (var i = 0; i < _entry.Params.Count; i++)
            {
                var param = _entry.Params[i];
                var control = ParamsHost.Children[i];
                switch (param.Kind)
                {
                    case FilterParamKind.Float:
                    case FilterParamKind.Int:
                    {
                        if (control is not DockPanel sliderHost
                            || sliderHost.Children.OfType<Slider>().FirstOrDefault() is not { } slider)
                        {
                            RebuildEditors();
                            return;
                        }

                        var header = sliderHost.Children.OfType<DockPanel>().FirstOrDefault();
                        var valueLabel = header?.Children.OfType<TextBlock>().FirstOrDefault();
                        var integer = param.Kind == FilterParamKind.Int;
                        var current = integer
                            ? _values.GetInt(param.Id)
                            : _values.GetFloat(param.Id);
                        slider.Value = current;
                        if (valueLabel is not null)
                            valueLabel.Text = FormatValue(current, integer, param.Step);
                        break;
                    }
                    case FilterParamKind.Bool:
                        if (control is CheckBox box)
                            box.IsChecked = _values.GetBool(param.Id);
                        else
                        {
                            RebuildEditors();
                            return;
                        }
                        break;
                    case FilterParamKind.Choice:
                    {
                        var combo = control as ComboBox
                            ?? (control as Panel)?.Children.OfType<ComboBox>().FirstOrDefault();
                        if (combo is null)
                        {
                            RebuildEditors();
                            return;
                        }

                        var choices = param.Choices ?? [];
                        var currentId = _values.GetChoice(param.Id);
                        combo.SelectedItem =
                            choices.FirstOrDefault(c => c.Id == currentId)
                            ?? choices.FirstOrDefault();
                        break;
                    }
                }
            }
        }
        finally
        {
            _suppressValuesChanged = false;
        }
    }

    private Control BuildSlider(FilterParam param, bool integer)
    {
        var label = new TextBlock
        {
            Text = param.Label,
            FontSize = 12,
            Opacity = 0.85,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valueLabel = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var slider = new Slider
        {
            Minimum = param.Min,
            Maximum = param.Max,
            TickFrequency = param.Step,
            IsSnapToTickEnabled = param.Step > 0,
            MinHeight = 24,
        };

        var current = integer ? _values!.GetInt(param.Id) : _values!.GetFloat(param.Id);
        slider.Value = current;
        valueLabel.Text = FormatValue(current, integer, param.Step);

        slider.ValueChanged += (_, e) =>
        {
            if (_values is null || _suppressValuesChanged)
                return;
            if (integer)
            {
                var i = (int)Math.Round(e.NewValue);
                _values.Set(param.Id, i);
                valueLabel.Text = FormatValue(i, true, param.Step);
            }
            else
            {
                var f = (float)e.NewValue;
                _values.Set(param.Id, f);
                valueLabel.Text = FormatValue(f, false, param.Step);
            }

            OnValueEdited();
        };

        var header = new DockPanel { Margin = new Avalonia.Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(valueLabel, Dock.Right);
        header.Children.Add(valueLabel);
        header.Children.Add(label);

        var row = new DockPanel { Tag = param.Id };
        DockPanel.SetDock(header, Dock.Top);
        row.Children.Add(header);
        row.Children.Add(slider);
        return row;
    }

    private Control BuildCheckBox(FilterParam param)
    {
        var box = new CheckBox
        {
            Content = param.Label,
            IsChecked = _values!.GetBool(param.Id),
            FontSize = 12,
        };
        box.IsCheckedChanged += (_, _) =>
        {
            if (_values is null || _suppressValuesChanged)
                return;
            _values.Set(param.Id, box.IsChecked == true);
            OnValueEdited();
        };
        return box;
    }

    private Control BuildCombo(FilterParam param)
    {
        var choices = param.Choices ?? [];
        var block = new StackPanel { Spacing = 4 };
        block.Children.Add(
            new TextBlock
            {
                Text = param.Label,
                FontSize = 12,
                Opacity = 0.85,
            }
        );

        var combo = new ComboBox
        {
            ItemsSource = choices,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 32,
        };
        combo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FilterChoice>(
            (choice, _) =>
                new TextBlock { Text = choice.Label, TextTrimming = TextTrimming.CharacterEllipsis }
        );

        var currentId = _values!.GetChoice(param.Id);
        combo.SelectedItem = choices.FirstOrDefault(c => c.Id == currentId) ?? choices.FirstOrDefault();

        combo.SelectionChanged += (_, _) =>
        {
            if (_values is null || _suppressValuesChanged || combo.SelectedItem is not FilterChoice choice)
                return;
            _values.Set(param.Id, choice.Id);
            OnValueEdited();
        };

        // For UpdateEditorValues, expose ComboBox as direct child when Kind is Choice —
        // wrap breaks in-place update; keep combo as the returned control with label above via StackPanel
        // and teach UpdateEditorValues to find nested ComboBox.
        block.Children.Add(combo);
        block.Tag = "choice";
        return block;
    }

    private static string FormatValue(double value, bool integer, double step)
    {
        if (integer)
            return ((int)Math.Round(value)).ToString();
        if (step >= 1)
            return value.ToString("0.#");
        if (step >= 0.1)
            return value.ToString("0.##");
        return value.ToString("0.###");
    }
}
