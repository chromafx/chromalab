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

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChromaLab.Dialogs;
using ChromaLab.Services;

namespace ChromaLab;

public partial class MainWindow
{
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (!await ConfirmDiscardIfNeededAsync())
            return;

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open image",
                AllowMultiple = false,
                FileTypeFilter = [ImageFileTypes.PickerType, FilePickerFileTypes.All],
            }
        );
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            StatusText.Text = "Could not resolve a local path for that file.";
            return;
        }

        await OpenPathAsync(path);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;
        await SaveDocumentAsync();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;
        await SaveAsAsync();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;
        await ExportAsync();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage)
            return;

        try
        {
            CancelPreviewWork();
            ClearCropSelection();
            _session.Reset();
            StatusText.Text = $"Reset · {_session.SizeLabel}";
            UpdateMeta();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Reset failed: {ex.Message}";
        }
    }

    private async void OnCanvasSizeClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_session.HasImage || _session.Document is null)
            return;

        StopAnimationPlayback(restoreDocument: true);
        CancelPreviewWork();
        ClearCropSelection();

        var doc = _session.Document;
        var result = await CanvasSizeDialog.ShowAsync(this, doc.Width, doc.Height);
        if (result is null)
            return;

        try
        {
            _session.ResizeCanvas(result.Value.Width, result.Value.Height);
            StatusText.Text = $"Canvas · {_session.SizeLabel}";
            UpdateMeta();
            FitToWindow();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Canvas size failed: {ex.Message}";
        }
    }

    private void TryUndo()
    {
        if (_busy)
            return;
        CancelPreviewWork();
        ClearCropSelection();
        if (_session.Undo())
        {
            StatusText.Text = $"Undo · {_session.SizeLabel}";
            UpdateMeta();
        }
    }

    private void TryRedo()
    {
        if (_busy)
            return;
        CancelPreviewWork();
        ClearCropSelection();
        if (_session.Redo())
        {
            StatusText.Text = $"Redo · {_session.SizeLabel}";
            UpdateMeta();
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetDroppedImagePath(e) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_busy)
            return;

        var path = GetDroppedImagePath(e);
        if (path is null)
            return;

        if (!await ConfirmDiscardIfNeededAsync())
            return;

        await OpenPathAsync(path);
    }

    private static string? GetDroppedImagePath(DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File))
            return null;
        var path = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
        return ImageFileTypes.IsSupportedPath(path) ? path : null;
    }

    private async Task OpenPathAsync(string path)
    {
        CancelInspectorPreview();
        Inspector.Clear();
        ClearCropSelection();
        SetBusy(true, "Opening…");
        try
        {
            await _session.OpenAsync(path);
            _viewport.FitPending = true;
            StatusText.Text = $"Opened {_session.DisplayName}";
            UpdateMeta();
            Dispatcher.UIThread.Post(FitToWindow, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Open failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> SaveDocumentAsync()
    {
        if (_session.Path is null)
            return await SaveAsAsync();

        var encoder = ExportFormatCatalog.CreateEncoderForPath(_session.Path);
        SetBusy(true, "Saving…");
        try
        {
            await _session.SaveAsync(_session.Path, encoder);
            StatusText.Text = $"Saved → {_session.DisplayName}";
            UpdateMeta();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> SaveAsAsync()
    {
        var result = await ExportDialog.ShowAsync(
            this,
            ExportDialogMode.SaveAs,
            _session.Path,
            _session.DisplayName
        );
        if (result is null)
            return false;

        SetBusy(true, "Saving…");
        try
        {
            await _session.SaveAsync(result.Path, result.Encoder);
            StatusText.Text = $"Saved → {_session.DisplayName} ({result.Format.DisplayName})";
            UpdateMeta();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExportAsync()
    {
        var result = await ExportDialog.ShowAsync(
            this,
            ExportDialogMode.Export,
            SuggestExportPath(),
            _session.DisplayName
        );
        if (result is null)
            return;

        SetBusy(true, "Exporting…");
        try
        {
            await _session.ExportAsync(result.Path, result.Encoder);
            StatusText.Text =
                $"Exported → {Path.GetFileName(result.Path)} ({result.Format.DisplayName})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private string? SuggestExportPath()
    {
        if (_session.Path is null)
            return null;
        var dir = Path.GetDirectoryName(_session.Path) ?? "";
        var name = Path.GetFileNameWithoutExtension(_session.Path);
        return Path.Combine(dir, name + "-export.png");
    }

    private async Task<bool> ConfirmDiscardIfNeededAsync()
    {
        if (!_session.IsDirty)
            return true;

        var result = await ConfirmDialog.ShowAsync(
            this,
            $"Save changes to “{_session.DisplayName}” before continuing?"
        );

        return result switch
        {
            ConfirmResult.Discard => true,
            ConfirmResult.Cancel => false,
            ConfirmResult.Save => await SaveDocumentAsync(),
            _ => false,
        };
    }

    private async Task HandleDirtyCloseAsync()
    {
        var result = await ConfirmDialog.ShowAsync(
            this,
            $"Save changes to “{_session.DisplayName}” before quitting?"
        );

        switch (result)
        {
            case ConfirmResult.Cancel:
                return;
            case ConfirmResult.Discard:
                _forceClose = true;
                Close();
                return;
            case ConfirmResult.Save:
                if (await SaveDocumentAsync())
                {
                    _forceClose = true;
                    Close();
                }
                return;
        }
    }
}
