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
using ChromaLab.Macros;
using ChromaLab.Services;

namespace ChromaLab.Dialogs;

/// <summary>
/// Folder bulk: pick macro + folders + export format, run with cancel.
/// </summary>
public partial class BulkProcessDialog : Window
{
    private readonly ExportOptionsState _options = ExportFormatCatalog.SharedOptions;
    private CancellationTokenSource? _cts;
    private bool _running;

    public BulkProcessDialog()
    {
        InitializeComponent();

        var formats = ExportFormatCatalog.All.Where(f => !f.IsDocumentFormat).ToList();
        FormatCombo.ItemsSource = formats;
        FormatCombo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ExportFormat>(
            (f, _) =>
                new TextBlock
                {
                    Text = f.IsNative ? $"{f.DisplayName} (native)" : f.DisplayName,
                }
        );
        FormatCombo.SelectedItem = formats.First(f => f.Id == "png");
        FormatCombo.SelectionChanged += (_, _) => RebuildOptions();
        RebuildOptions();

        BrowseMacroButton.Click += async (_, _) => await BrowseMacroAsync();
        BrowseInputButton.Click += async (_, _) => await BrowseFolderAsync(InputFolderBox);
        BrowseOutputButton.Click += async (_, _) => await BrowseFolderAsync(OutputFolderBox);
        RunButton.Click += async (_, _) => await RunAsync();
        CancelButton.Click += (_, _) => OnCloseOrCancel();
        Closing += (_, e) =>
        {
            if (_running)
            {
                e.Cancel = true;
                _cts?.Cancel();
            }
        };
    }

    public static Task ShowAsync(Window owner)
    {
        var dialog = new BulkProcessDialog();
        return dialog.ShowDialog(owner);
    }

    private void RebuildOptions()
    {
        OptionsHost.Children.Clear();
        if (FormatCombo.SelectedItem is not ExportFormat format)
            return;

        var panel = format.CreateOptionsPanel(_options);
        if (panel is null)
        {
            OptionsHost.Children.Add(
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
            OptionsHost.Children.Add(panel);
        }
    }

    private async Task BrowseMacroAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open macro",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ChromaFx macro")
                    {
                        Patterns = ["*.chromafx-macro.json", "*.json"],
                    },
                ],
            }
        );
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            MacroPathBox.Text = path;
    }

    private async Task BrowseFolderAsync(TextBox target)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose folder", AllowMultiple = false }
        );
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            target.Text = path;
    }

    private void OnCloseOrCancel()
    {
        if (_running)
        {
            _cts?.Cancel();
            ProgressText.Text = "Cancelling…";
            return;
        }

        Close();
    }

    private async Task RunAsync()
    {
        if (_running)
            return;

        var macroPath = MacroPathBox.Text?.Trim();
        var input = InputFolderBox.Text?.Trim();
        var output = OutputFolderBox.Text?.Trim();
        var suffix = SuffixBox.Text ?? "-macro";
        if (string.IsNullOrWhiteSpace(macroPath) || !File.Exists(macroPath))
        {
            ProgressText.Text = "Choose a valid macro file.";
            return;
        }

        if (string.IsNullOrWhiteSpace(input) || !Directory.Exists(input))
        {
            ProgressText.Text = "Choose a valid input folder.";
            return;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            ProgressText.Text = "Choose an output folder.";
            return;
        }

        if (FormatCombo.SelectedItem is not ExportFormat format)
            return;

        MacroDefinition macro;
        try
        {
            macro = MacroJson.Read(macroPath);
        }
        catch (Exception ex)
        {
            ProgressText.Text = $"Macro load failed: {ex.Message}";
            return;
        }

        var missing = MacroRunner.MissingIds(macro);
        if (missing.Count > 0)
        {
            ProgressText.Text = "Unknown filter id(s): " + string.Join(", ", missing);
            return;
        }

        var files = MacroBulk.EnumerateInputs(input);
        if (files.Count == 0)
        {
            ProgressText.Text = "No supported still images in the input folder.";
            return;
        }

        _running = true;
        _cts = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.Content = "Cancel";
        LogBox.Text = "";
        ProgressText.Text = $"Starting · {files.Count} file(s)…";

        var progress = new Progress<MacroBulkProgress>(p =>
        {
            ProgressText.Text = $"Processing {p.Done}/{p.Total} · {Path.GetFileName(p.CurrentFile)}";
            if (p.Error is not null)
                AppendLog(p.Error);
        });

        try
        {
            var encoder = format.CreateEncoder(_options);
            var (ok, failed) = await MacroBulk.ProcessAsync(
                input,
                output,
                macro,
                encoder,
                format.DefaultExtension,
                suffix,
                progress,
                _cts.Token
            );
            ProgressText.Text = $"Done · {ok} ok · {failed} failed";
            if (failed == 0)
                AppendLog($"Finished {ok} file(s).");
        }
        catch (OperationCanceledException)
        {
            ProgressText.Text = "Cancelled.";
            AppendLog("Cancelled by user.");
        }
        catch (Exception ex)
        {
            ProgressText.Text = $"Bulk failed: {ex.Message}";
            AppendLog(ex.Message);
        }
        finally
        {
            _running = false;
            _cts.Dispose();
            _cts = null;
            RunButton.IsEnabled = true;
            CancelButton.Content = "Close";
        }
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(LogBox.Text))
            LogBox.Text = line;
        else
            LogBox.Text += Environment.NewLine + line;
    }
}
