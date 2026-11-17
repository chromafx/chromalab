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

namespace ChromaLab.Dialogs;

/// <summary>
/// Result of <see cref="CanvasSizeDialog"/> when the user confirms.
/// </summary>
public readonly record struct CanvasSizeResult(int Width, int Height);

/// <summary>
/// Modal dialog to change document canvas width/height.
/// </summary>
public partial class CanvasSizeDialog : Window
{
    private CanvasSizeResult? _result;

    public CanvasSizeDialog()
    {
        InitializeComponent();
        OkButton.Click += (_, _) =>
        {
            var w = (int)Math.Round(WidthBox.Value ?? 1);
            var h = (int)Math.Round(HeightBox.Value ?? 1);
            if (w < 1 || h < 1)
                return;
            _result = new CanvasSizeResult(w, h);
            Close();
        };
        CancelButton.Click += (_, _) => Close();
    }

    /// <summary>
    /// Shows the dialog; returns null if cancelled.
    /// </summary>
    public static async Task<CanvasSizeResult?> ShowAsync(Window owner, int width, int height)
    {
        var dialog = new CanvasSizeDialog();
        dialog.WidthBox.Value = width;
        dialog.HeightBox.Value = height;
        await dialog.ShowDialog(owner);
        return dialog._result;
    }
}
