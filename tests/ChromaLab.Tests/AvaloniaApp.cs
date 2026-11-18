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

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using ChromaLab;

namespace ChromaLab.Tests;

/// <summary>
/// Headless Avalonia bootstrap for WriteableBitmap used by <see cref="Services.ImageSession"/>.
/// Clears the UI SynchronizationContext so <c>ConfigureAwait(true)</c> does not deadlock.
/// </summary>
internal static class AvaloniaApp
{
    private static int _init;

    [ModuleInitializer]
    internal static void EnsureInitialized() => Init();

    internal static void Init()
    {
        if (Interlocked.Exchange(ref _init, 1) == 1)
            return;

        AppBuilder
            .Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        SynchronizationContext.SetSynchronizationContext(null);
    }
}
