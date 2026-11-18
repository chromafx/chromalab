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
using ChromaLab.Controls;
using ChromaLab.Filters;

namespace ChromaLab;

public partial class MainWindow
{
    private void BuildFilterUi()
    {
        FiltersMenu.Items.Clear();
        FavoriteTools.Children.Clear();
        _filterMenuControls.Clear();

        foreach (var group in FilterCatalog.GroupsByOrder())
        {
            var submenu = new MenuItem { Header = group.Key };
            foreach (var entry in group)
            {
                var item = new MenuItem { Header = entry.Header, IsEnabled = false };
                var captured = entry;
                item.Click += (_, _) => OnFilterChosen(captured);
                submenu.Items.Add(item);
                _filterMenuControls.Add(item);
            }

            FiltersMenu.Items.Add(submenu);
        }

        var favorites = FilterCatalog.ToolbarFavorites.ToList();
        FavoriteToolsSep.IsVisible = favorites.Count > 0;

        foreach (var entry in favorites)
        {
            var icon = new LabIcon
            {
                Kind = entry.ToolbarIcon,
                Size = 16,
                Foreground = ResolveFavoriteBrush(entry.Id),
            };
            var tip = entry.ToolbarTip ?? entry.DisplayName;
            if (entry.HasParams)
                tip += " — adjust in inspector";
            var button = new Button
            {
                Classes = { "tool" },
                Content = icon,
                IsEnabled = false,
                [ToolTip.TipProperty] = tip,
            };
            var captured = entry;
            button.Click += (_, _) => OnFilterChosen(captured);
            FavoriteTools.Children.Add(button);
            _filterMenuControls.Add(button);
        }
    }

    private IBrush ResolveFavoriteBrush(string filterId) =>
        filterId switch
        {
            "invert" => this.FindResource("IconAccentGradient") as IBrush
                ?? Brushes.DodgerBlue,
            "websafe" => this.FindResource("IconWebsafeGradient") as IBrush
                ?? Brushes.SteelBlue,
            _ => this.FindResource("IconMutedBrush") as IBrush ?? Brushes.Silver,
        };

    private void ApplyCatalogId(string id, bool stayOnTools = false)
    {
        var entry = FilterCatalog.All.FirstOrDefault(e => e.Id == id);
        if (entry is not null)
            OnFilterChosen(entry, stayOnTools);
    }

    private void OnFilterChosen(FilterEntry entry, bool stayOnTools = false)
    {
        if (_busy || !_session.HasImage)
            return;

        StopAnimationPlayback(restoreDocument: true);

        if (_canvasTool == CanvasTool.Crop)
            SetCanvasTool(CanvasTool.Hand);

        if (entry.HasParams)
        {
            if (!stayOnTools)
                SetRailMode(LeftRailMode.Filters);
            Inspector.Select(entry);
            FilterBrowser.SetSelectedEntry(entry.Id);
            StatusText.Text = $"{entry.DisplayName} — preview; Apply to commit";
            return;
        }

        CancelInspectorPreview();
        FilterBrowser.SetSelectedEntry(null);
        TryMutate(entry, entry.CreateDefaultValues());
    }

    private void CancelTransientModes(bool clearInspector = false)
    {
        CancelPreviewWork();
        StopAnimationPlayback(restoreDocument: true);
        ClearCropSelection();
        if (clearInspector)
        {
            Inspector.Clear();
            FilterBrowser.SetSelectedEntry(null);
        }
    }

    private void SchedulePreview(FilterEntry entry, FilterValues values)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelPreviewWork();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        _ = RunPreviewAsync(entry, values.Clone(), ct);
    }

    private async Task RunPreviewAsync(
        FilterEntry entry,
        FilterValues values,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Task.Delay(PreviewDebounceMs, cancellationToken).ConfigureAwait(true);
            await _session
                .PreviewAsync(ctx => entry.Apply(ctx, values), cancellationToken)
                .ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested)
                StatusText.Text = $"Preview · {entry.DisplayName}";
        }
        catch (OperationCanceledException)
        {
            // Superseded.
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
                StatusText.Text = $"Preview failed: {ex.Message}";
        }
    }

    private void CancelInspectorPreview()
    {
        CancelPreviewWork();
        _session.ClearPreview();
        if (!_busy && _session.HasImage && Inspector.SelectedEntry is null)
            StatusText.Text = "Ready";
    }

    private void CancelPreviewWork()
    {
        if (_previewCts is null)
            return;
        _previewCts.Cancel();
        _previewCts.Dispose();
        _previewCts = null;
    }

    private async void TryMutate(FilterEntry entry, FilterValues values)
    {
        if (_busy || !_session.HasImage)
            return;

        CancelPreviewWork();
        var label = entry.DisplayName;
        SetBusy(true, $"{label}…");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _session.MutateAsync(ctx => entry.Apply(ctx, values));
            sw.Stop();
            _macroRecorder.Record(entry, values);
            StatusText.Text = _macroRecorder.IsRecording
                ? $"Recording · {_macroRecorder.StepCount} · {label} · {sw.ElapsedMilliseconds} ms"
                : $"{label} · {_session.SizeLabel} · {sw.ElapsedMilliseconds} ms";
            RefreshMacroMenuUi(!_busy, _session.HasImage && !_busy);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{label} failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }
}
