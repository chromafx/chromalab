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
using Avalonia.Media;
using ChromaLab.Filters;

namespace ChromaLab.Controls;

public partial class FilterCatalogView : UserControl
{
    private readonly List<Control> _entryControls = [];
    private bool _enabled = true;
    private string _query = "";

    public FilterCatalogView()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) =>
        {
            _query = SearchBox.Text?.Trim() ?? "";
            Rebuild();
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
        foreach (var control in _entryControls)
            control.IsEnabled = enabled;
    }

    private void Rebuild()
    {
        GroupsHost.Children.Clear();
        _entryControls.Clear();

        var query = _query;
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
                var button = new Button
                {
                    Content = entry.DisplayName,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Padding = new Avalonia.Thickness(8, 6),
                    Classes = { "catalog-entry" },
                    IsEnabled = _enabled,
                    [ToolTip.TipProperty] = entry.HasParams
                        ? $"{entry.DisplayName} — adjust in inspector"
                        : entry.DisplayName,
                };
                var captured = entry;
                button.Click += (_, _) => FilterChosen?.Invoke(this, captured);
                block.Children.Add(button);
                _entryControls.Add(button);
            }

            GroupsHost.Children.Add(block);
        }
    }
}
