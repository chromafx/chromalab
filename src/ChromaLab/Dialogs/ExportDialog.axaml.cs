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
using ChromaFx.IO.Encoders;
using ChromaLab.Controls;
using ChromaLab.Services;

namespace ChromaLab.Dialogs;

/// <summary>
/// Save As (rebind document) vs Export (write a copy).
/// </summary>
public enum ExportDialogMode
{
    SaveAs,
    Export,
}

/// <summary>
/// Result of a confirmed Save As / Export dialog.
/// </summary>
public sealed record ExportDialogResult(string Path, IImageEncoder Encoder, ExportFormat Format);

/// <summary>
/// Format + encoder options + destination path.
/// </summary>
public partial class ExportDialog : Window
{
    private readonly ExportDialogMode _mode;
    private readonly ExportOptionsState _options = ExportFormatCatalog.SharedOptions;
    private ExportDialogResult? _result;
    private bool _suppressFormatChange;

    public ExportDialog()
        : this(ExportDialogMode.Export, null, "image.png") { }

    public ExportDialog(ExportDialogMode mode, string? suggestedPath, string fallbackFileName)
    {
        _mode = mode;
        InitializeComponent();

        Title = mode == ExportDialogMode.SaveAs ? "Save As" : "Export";
        PrimaryButton.Content = mode == ExportDialogMode.SaveAs ? "Save" : "Export";
        SubtitleText.Text =
            mode == ExportDialogMode.SaveAs
                ? "Choose a format and options. This becomes the document path."
                : "Write a copy with format options. The open document is unchanged.";

        var formats =
            mode == ExportDialogMode.SaveAs
                ? ExportFormatCatalog.All
                : ExportFormatCatalog.All.Where(f => !f.IsDocumentFormat).ToList();

        FormatCombo.ItemsSource = formats;
        FormatCombo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ExportFormat>(
            (f, _) =>
                new TextBlock
                {
                    Text = f.IsNative
                        ? $"{f.DisplayName} (native)"
                        : f.IsDocumentFormat
                            ? $"{f.DisplayName} (layers)"
                            : f.DisplayName,
                }
        );

        var fromExt = ExportFormatCatalog.FromExtension(
            System.IO.Path.GetExtension(suggestedPath ?? "")
        );
        var initial =
            fromExt is not null && formats.Contains(fromExt)
                ? fromExt
                : formats.First(f => f.Id == "png");

        _suppressFormatChange = true;
        FormatCombo.SelectedItem = initial;
        _suppressFormatChange = false;

        PathBox.Text = BuildSuggestedPath(suggestedPath, fallbackFileName, initial);

        FormatCombo.SelectionChanged += (_, _) => OnFormatChanged();
        PathBox.LostFocus += (_, _) => SyncFormatFromPath();
        BrowseButton.Click += async (_, _) => await BrowseAsync();
        PrimaryButton.Click += async (_, _) => await TryAcceptAsync();
        CancelButton.Click += (_, _) => Close();

        RebuildOptions();
        UpdateNativeHint();
    }

    /// <summary>
    /// Shows the dialog. Returns null when cancelled.
    /// </summary>
    public static async Task<ExportDialogResult?> ShowAsync(
        Window owner,
        ExportDialogMode mode,
        string? suggestedPath,
        string fallbackFileName
    )
    {
        var dialog = new ExportDialog(mode, suggestedPath, fallbackFileName);
        await dialog.ShowDialog(owner);
        return dialog._result;
    }

    private void OnFormatChanged()
    {
        if (_suppressFormatChange || FormatCombo.SelectedItem is not ExportFormat format)
            return;

        RebuildOptions();
        UpdateNativeHint();
        PathBox.Text = EnsureExtension(PathBox.Text, format);
    }

    private void RebuildOptions() =>
        ExportOptionsPanel.Rebuild(
            OptionsHost,
            FormatCombo.SelectedItem as ExportFormat,
            _options
        );

    private void UpdateNativeHint()
    {
        NativeHint.IsVisible =
            FormatCombo.SelectedItem is ExportFormat { IsNative: true };
    }

    private async Task BrowseAsync()
    {
        if (FormatCombo.SelectedItem is not ExportFormat format)
            return;

        var patterns = format.Extensions.Select(e => "*" + e).ToArray();
        var fileType = new FilePickerFileType(format.DisplayName) { Patterns = patterns };
        var suggested = System.IO.Path.GetFileName(PathBox.Text);
        if (string.IsNullOrWhiteSpace(suggested))
            suggested = "image." + format.DefaultExtension;

        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = _mode == ExportDialogMode.SaveAs ? "Save As" : "Export",
                SuggestedFileName = suggested,
                FileTypeChoices = [fileType],
                DefaultExtension = format.DefaultExtension,
            }
        );
        if (file?.TryGetLocalPath() is { } path)
            PathBox.Text = EnsureExtension(path, format);
    }

    private void SyncFormatFromPath()
    {
        if (_suppressFormatChange)
            return;

        var path = PathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var match = ExportFormatCatalog.FromExtension(System.IO.Path.GetExtension(path));
        if (match is null || FormatCombo.SelectedItem is ExportFormat current && current.Id == match.Id)
            return;

        if (FormatCombo.ItemsSource is not System.Collections.IEnumerable items)
            return;

        ExportFormat? available = null;
        foreach (var item in items)
        {
            if (item is ExportFormat f && f.Id == match.Id)
            {
                available = f;
                break;
            }
        }

        if (available is null)
            return;

        _suppressFormatChange = true;
        FormatCombo.SelectedItem = available;
        _suppressFormatChange = false;
        RebuildOptions();
        UpdateNativeHint();
    }

    private async Task TryAcceptAsync()
    {
        SyncFormatFromPath();
        if (FormatCombo.SelectedItem is not ExportFormat format)
            return;

        var path = PathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            PathBox.Focus();
            return;
        }

        path = EnsureExtension(path, format);
        PathBox.Text = path;

        if (File.Exists(path))
        {
            var overwrite = await ConfirmDialog.AskYesNoAsync(
                this,
                $"“{System.IO.Path.GetFileName(path)}” already exists. Overwrite it?"
            );
            if (!overwrite)
                return;
        }

        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch
        {
            // Encode will surface a clearer error.
        }

        _result = new ExportDialogResult(path, format.CreateEncoder(_options), format);
        Close();
    }

    private static string BuildSuggestedPath(
        string? suggestedPath,
        string fallbackFileName,
        ExportFormat format
    )
    {
        if (!string.IsNullOrWhiteSpace(suggestedPath))
            return EnsureExtension(suggestedPath, format);

        var name = System.IO.Path.GetFileNameWithoutExtension(fallbackFileName);
        if (string.IsNullOrWhiteSpace(name) || name is "Untitled")
            name = "chromalab";
        return name + "." + format.DefaultExtension;
    }

    private static string EnsureExtension(string? path, ExportFormat format)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "chromalab." + format.DefaultExtension;

        var ext = System.IO.Path.GetExtension(path);
        if (
            format.Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase))
        )
            return path;

        var without = string.IsNullOrEmpty(ext)
            ? path
            : path[..^ext.Length];
        return without + "." + format.DefaultExtension;
    }
}
