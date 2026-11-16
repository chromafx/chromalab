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
using Avalonia.Interactivity;

namespace ChromaLab.Services;

/// <summary>
/// Small Avalonia wiring helpers shared by the editor shell.
/// </summary>
public static class UiHelpers
{
    public static void Wire(EventHandler<RoutedEventArgs> handler, params Control[] controls)
    {
        foreach (var control in controls)
        {
            switch (control)
            {
                case Button button:
                    button.Click += handler;
                    break;
                case MenuItem menuItem:
                    menuItem.Click += handler;
                    break;
            }
        }
    }

    public static void SetEnabled(bool enabled, params Control[] controls)
    {
        foreach (var control in controls)
            control.IsEnabled = enabled;
    }

    public static void SetSelectedClass(Button button, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected"))
                button.Classes.Add("selected");
        }
        else
        {
            button.Classes.Remove("selected");
        }
    }
}
