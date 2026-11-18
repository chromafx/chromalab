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
using Avalonia.Input;
using Avalonia.Media;
using ChromaLab.Filters;
using ChromaLab.Services;

namespace ChromaLab.Controls;

public partial class FilterCatalogView : UserControl
{
    private readonly List<(Button Button, string Id)> _entryControls = [];
    private bool _enabled = true;
    private string _query = "";
    private string? _selectedId;

    public FilterCatalogView()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) =>
        {
            _query = SearchBox.Text?.Trim() ?? "";
            ClearSearchButton.IsVisible = !string.IsNullOrEmpty(_query);
            Rebuild();
        };
        ClearSearchButton.Click += (_, _) => ClearSearch();
        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            if (string.IsNullOrEmpty(SearchBox.Text))
                return;
            ClearSearch();
            e.Handled = true;
        };
        Rebuild();
    }

    /// <summary>
    /// Raised when the user picks a catalog entry.
    /// </summary>
    public event EventHandler<FilterEntry>? FilterChosen;

    /// <summary>
    /// Enables or disables all filter entry buttons.
    /// </summary>
    public void SetEntriesEnabled(bool enabled)
    {
        _enabled = enabled;
        foreach (var (control, _) in _entryControls)
            control.IsEnabled = enabled;
    }

    /// <summary>
    /// Marks the currently inspected parametric filter (or clears selection).
    /// </summary>
    public void SetSelectedEntry(string? id)
    {
        _selectedId = id;
        foreach (var (button, entryId) in _entryControls)
            UiHelpers.SetSelectedClass(button, entryId == _selectedId);
    }

    /// <summary>
    /// Focuses the search box (e.g. Ctrl/Cmd+F).
    /// </summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>
    /// Clears the search query and rebuilds the catalog list.
    /// </summary>
    public void ClearSearch()
    {
        SearchBox.Text = "";
        _query = "";
        ClearSearchButton.IsVisible = false;
        Rebuild();
    }

    private void Rebuild()
    {
        GroupsHost.Children.Clear();
        _entryControls.Clear();

        var query = _query;
        var any = false;
        foreach (var group in FilterCatalog.GroupsByOrder())
        {
            var entries = string.IsNullOrEmpty(query)
                ? group.ToList()
                : group
                    .Where(e =>
                        e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || e.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || e.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            if (entries.Count == 0)
                continue;

            any = true;
            var block = new StackPanel { Spacing = 2 };
            block.Children.Add(
                new TextBlock
                {
                    Text = group.Key.ToUpperInvariant(),
                    FontSize = 10,
                    FontWeight = FontWeight.SemiBold,
                    Opacity = 0.55,
                    LetterSpacing = 0.6,
                    Margin = new Avalonia.Thickness(4, 4, 4, 2),
                }
            );

            foreach (var entry in entries)
            {
                var label = entry.HasParams ? entry.DisplayName + "…" : entry.DisplayName;
                var button = new Button
                {
                    Content = label,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Padding = new Avalonia.Thickness(8, 6),
                    Classes = { "catalog-entry" },
                    IsEnabled = _enabled,
                    [ToolTip.TipProperty] = entry.HasParams
                        ? $"{entry.DisplayName} — adjust in inspector"
                        : entry.DisplayName,
                };
                UiHelpers.SetSelectedClass(button, entry.Id == _selectedId);
                var captured = entry;
                button.Click += (_, _) => FilterChosen?.Invoke(this, captured);
                block.Children.Add(button);
                _entryControls.Add((button, entry.Id));
            }

            GroupsHost.Children.Add(block);
        }

        if (!any && !string.IsNullOrEmpty(query))
        {
            GroupsHost.Children.Add(
                new TextBlock
                {
                    Text = "No matching filters",
                    FontSize = 12,
                    Opacity = 0.55,
                    Margin = new Avalonia.Thickness(8, 12, 8, 0),
                    TextWrapping = TextWrapping.Wrap,
                }
            );
        }
    }
}
