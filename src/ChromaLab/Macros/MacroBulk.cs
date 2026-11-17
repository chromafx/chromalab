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

using ChromaFx.Core;
using ChromaFx.IO;
using ChromaFx.IO.Encoders;
using ChromaLab.Services;

namespace ChromaLab.Macros;

/// <summary>
/// Progress callback for folder bulk processing.
/// </summary>
public sealed record MacroBulkProgress(int Done, int Total, string CurrentFile, string? Error);

/// <summary>
/// Applies a macro to every still image in a folder and writes flattened exports.
/// </summary>
public static class MacroBulk
{
    private static readonly HashSet<string> ExcludedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".ora",
            ".gif",
        };

    /// <summary>
    /// Still-image extensions eligible for bulk (catalog minus ora/gif).
    /// </summary>
    public static IEnumerable<string> InputExtensions =>
        ImageFileTypes.Extensions.Where(ext => !ExcludedExtensions.Contains(ext));

    public static IReadOnlyList<string> EnumerateInputs(string inputFolder)
    {
        if (!Directory.Exists(inputFolder))
            return [];

        return Directory
            .EnumerateFiles(inputFolder)
            .Where(path => InputExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildOutputPath(
        string outputFolder,
        string inputPath,
        string extension,
        string suffix = "-macro"
    )
    {
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(outputFolder, stem + suffix + ext);
    }

    /// <summary>
    /// Processes each input file. Continues after per-file errors.
    /// </summary>
    public static async Task<(int Ok, int Failed)> ProcessAsync(
        string inputFolder,
        string outputFolder,
        MacroDefinition macro,
        IImageEncoder encoder,
        string outputExtension,
        string suffix = "-macro",
        IProgress<MacroBulkProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(encoder);

        Directory.CreateDirectory(outputFolder);
        var files = EnumerateInputs(inputFolder);
        var ok = 0;
        var failed = 0;

        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = files[i];
            string? error = null;
            try
            {
                await Task.Run(
                        () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var image = Image.Load(path);
                            MacroRunner.Apply(image, macro, cancellationToken);
                            var outPath = BuildOutputPath(
                                outputFolder,
                                path,
                                outputExtension,
                                suffix
                            );
                            image.Save(outPath, encoder);
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(true);
                ok++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                error = $"{Path.GetFileName(path)}: {ex.Message}";
            }

            progress?.Report(new MacroBulkProgress(i + 1, files.Count, path, error));
        }

        return (ok, failed);
    }
}
