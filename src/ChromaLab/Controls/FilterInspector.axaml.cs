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
using ChromaLab.Filters;

namespace ChromaLab.Controls;

public partial class FilterInspector : UserControl
{
    private FilterEntry? _entry;
    private FilterValues? _values;
    private bool _canApply;
    private bool _suppressValuesChanged;

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
        RandomizeButton.Click += (_, _) => RandomizeSeeds();
    }

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
        RebuildEditors();
        UpdateButtons();
        RaiseValuesChanged();
    }

    /// <summary>
    /// Clears the inspector back to the empty state.
    /// </summary>
    public void Clear()
    {
        _entry = null;
        _values = null;
        ParamsHost.Children.Clear();
        ParamsHost.IsVisible = false;
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
    }

    private void ResetToDefaults()
    {
        if (_entry is null)
            return;
        _values = _entry.CreateDefaultValues();
        RebuildEditors();
        RaiseValuesChanged();
    }

    private void UpdateButtons()
    {
        var has = _entry is not null;
        ApplyButton.IsEnabled = has && _canApply;
        CancelButton.IsEnabled = has;
        ResetParamsButton.IsEnabled = has;
        var hasSeed = has && _entry!.Params.Any(p =>
            p.Kind == FilterParamKind.Int
            && p.Id.Contains("seed", StringComparison.OrdinalIgnoreCase));
        RandomizeButton.IsVisible = hasSeed;
        RandomizeButton.IsEnabled = hasSeed && _canApply;
    }

    private void RandomizeSeeds()
    {
        if (_entry is null || _values is null)
            return;

        var rng = Random.Shared;
        foreach (var param in _entry.Params)
        {
            if (param.Kind != FilterParamKind.Int)
                continue;
            if (!param.Id.Contains("seed", StringComparison.OrdinalIgnoreCase))
                continue;
            var min = (int)param.Min;
            var max = (int)param.Max;
            if (max < min)
                (min, max) = (max, min);
            _values.Set(param.Id, rng.Next(min, max + 1));
        }

        RebuildEditors();
        RaiseValuesChanged();
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
                var block = new StackPanel { Spacing = 4 };
                block.Children.Add(
                    new TextBlock
                    {
                        Text = param.Label,
                        FontSize = 12,
                        Opacity = 0.85,
                    }
                );

                Control editor = param.Kind switch
                {
                    FilterParamKind.Float => BuildSlider(param, integer: false),
                    FilterParamKind.Int => BuildSlider(param, integer: true),
                    FilterParamKind.Bool => BuildCheckBox(param),
                    FilterParamKind.Choice => BuildCombo(param),
                    _ => new TextBlock { Text = "(unsupported)" },
                };

                block.Children.Add(editor);
                ParamsHost.Children.Add(block);
            }
        }
        finally
        {
            _suppressValuesChanged = false;
        }
    }

    private Control BuildSlider(FilterParam param, bool integer)
    {
        var valueLabel = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.7,
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

        var row = new DockPanel();
        DockPanel.SetDock(valueLabel, Dock.Top);
        row.Children.Add(valueLabel);
        row.Children.Add(slider);
        return row;
    }

    private Control BuildCheckBox(FilterParam param)
    {
        var box = new CheckBox
        {
            Content = "Enabled",
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

        return combo;
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
