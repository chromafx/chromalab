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
/// Save / Don't Save / Cancel prompt for unsaved changes.
/// </summary>
public enum ConfirmResult
{
    /// <summary>User chose Save.</summary>
    Save,

    /// <summary>User chose Don't Save.</summary>
    Discard,

    /// <summary>User cancelled.</summary>
    Cancel,
}

/// <summary>
/// Modal confirm dialog for dirty-document flows.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmResult _result = ConfirmResult.Cancel;

    /// <summary>
    /// Creates the dialog (parameterless for Avalonia XAML loader).
    /// </summary>
    public ConfirmDialog()
    {
        InitializeComponent();
        SaveButton.Click += (_, _) => CloseWith(ConfirmResult.Save);
        DiscardButton.Click += (_, _) => CloseWith(ConfirmResult.Discard);
        CancelButton.Click += (_, _) => CloseWith(ConfirmResult.Cancel);
    }

    /// <summary>
    /// Shows the dialog and returns the chosen action.
    /// </summary>
    public static async Task<ConfirmResult> ShowAsync(Window owner, string message)
    {
        var dialog = new ConfirmDialog();
        dialog.MessageText.Text = message;
        await dialog.ShowDialog(owner);
        return dialog._result;
    }

    /// <summary>
    /// Yes/No prompt (hides Don't Save). Returns true when the user confirms.
    /// </summary>
    public static async Task<bool> AskYesNoAsync(
        Window owner,
        string message,
        string yesLabel = "Overwrite",
        string noLabel = "Cancel"
    )
    {
        var dialog = new ConfirmDialog();
        dialog.MessageText.Text = message;
        dialog.SaveButton.Content = yesLabel;
        dialog.DiscardButton.IsVisible = false;
        dialog.CancelButton.Content = noLabel;
        await dialog.ShowDialog(owner);
        return dialog._result == ConfirmResult.Save;
    }

    private void CloseWith(ConfirmResult result)
    {
        _result = result;
        Close();
    }
}
