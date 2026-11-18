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
using Avalonia.Platform.Storage;
using ChromaLab.Dialogs;
using ChromaLab.Macros;
using ChromaLab.Services;

namespace ChromaLab;

public partial class MainWindow
{
    private void WireMacroMenus()
    {
        MacroRecordMenuItem.Click += (_, _) => ToggleMacroRecording();
        MacroSaveMenuItem.Click += async (_, _) => await SaveMacroAsync();
        MacroRunMenuItem.Click += async (_, _) => await OpenAndRunMacroAsync();
        MacroBulkMenuItem.Click += async (_, _) =>
        {
            if (_busy)
                return;
            await BulkProcessDialog.ShowAsync(this);
        };
    }

    private void RefreshMacroMenuUi(bool idle, bool canEdit)
    {
        MacroRecordMenuItem.Header = _macroRecorder.IsRecording
            ? "Stop _Recording"
            : "Start _Recording";
        MacroRecordMenuItem.IsEnabled = idle;
        MacroSaveMenuItem.IsEnabled = idle && _macroRecorder.StepCount > 0;
        MacroRunMenuItem.IsEnabled = canEdit;
        MacroBulkMenuItem.IsEnabled = idle;
        RecordingBadge.IsVisible = _macroRecorder.IsRecording;
        if (_macroRecorder.IsRecording)
            ToolTip.SetTip(
                RecordingBadge,
                $"Recording · {_macroRecorder.StepCount} step(s)"
            );
    }

    private void ToggleMacroRecording()
    {
        if (_busy)
            return;

        if (_macroRecorder.IsRecording)
        {
            _macroRecorder.Stop();
            StatusText.Text =
                _macroRecorder.StepCount == 0
                    ? "Recording stopped (no steps)."
                    : $"Recording stopped · {_macroRecorder.StepCount} step(s) — Save Macro… to keep";
        }
        else
        {
            _macroRecorder.Start();
            StatusText.Text = "Recording · apply filters to capture steps";
        }

        RefreshMacroMenuUi(!_busy, _session.HasImage && !_busy);
    }

    private async Task SaveMacroAsync()
    {
        if (_busy || _macroRecorder.StepCount == 0)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save macro",
                SuggestedFileName = "macro.chromafx-macro.json",
                DefaultExtension = "chromafx-macro.json",
                FileTypeChoices = [MacroFileTypes.PickerType],
            }
        );
        if (file?.TryGetLocalPath() is not { } path)
            return;

        if (!path.EndsWith(MacroJson.Extension, StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path += MacroJson.Extension;

        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith(".chromafx-macro", StringComparison.OrdinalIgnoreCase))
                name = name[..^".chromafx-macro".Length];
            var macro = _macroRecorder.ToDefinition(string.IsNullOrWhiteSpace(name) ? "Untitled" : name);
            MacroJson.Write(path, macro);
            StatusText.Text = $"Saved macro · {macro.Steps.Count} step(s) → {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save macro failed: {ex.Message}";
        }
    }

    private async Task OpenAndRunMacroAsync()
    {
        if (_busy || !_session.HasImage)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open & run macro",
                AllowMultiple = false,
                FileTypeFilter = [MacroFileTypes.PickerType],
            }
        );
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;

        MacroDefinition macro;
        try
        {
            macro = MacroJson.Read(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Macro load failed: {ex.Message}";
            return;
        }

        if (MacroRunner.Validate(macro) is { } error)
        {
            StatusText.Text = error;
            return;
        }

        CancelTransientModes(clearInspector: true);
        SetBusy(true, $"Macro · {macro.Name}…");
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _session.ApplyMacroAsync(macro);
            sw.Stop();
            StatusText.Text =
                $"Macro · {macro.Name} · {macro.Steps.Count} step(s) · {sw.ElapsedMilliseconds} ms";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Macro failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }
}
