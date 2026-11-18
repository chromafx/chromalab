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

using ChromaLab.Filters;
using ChromaLab.Macros;
using ChromaLab.Services;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Services;

public class ImageSessionTests
{
    /// <summary>
    /// Runs session work on a thread-pool thread with no sync context so
    /// <see cref="ImageSession"/>'s <c>ConfigureAwait(true)</c> cannot dead-lock.
    /// </summary>
    private static Task RunSession(Func<Task> body) =>
        Task.Run(async () =>
        {
            AvaloniaApp.Init();
            SynchronizationContext.SetSynchronizationContext(null);
            await body().ConfigureAwait(false);
        });

    [Fact]
    public Task Open_Mutate_Undo_Redo_Tracks_Dirty() =>
        RunSession(async () =>
        {
            using var temp = new TestImages.TempDir();
            var path = TestImages.SavePng(TestImages.Solid(16, 16, TestImages.Red), temp.Path);
            using var session = new ImageSession();
            SessionChangeKind? last = null;
            session.Changed += (_, kind) => last = kind;

            await session.OpenAsync(path).ConfigureAwait(false);
            Assert.True(session.HasImage);
            Assert.False(session.IsDirty);
            Assert.Equal(SessionChangeKind.Full, last);

            await session
                .MutateAsync(ctx =>
                {
                    var invert = FilterCatalog.All.First(e => e.Id == "invert");
                    invert.Apply(ctx, invert.CreateDefaultValues());
                })
                .ConfigureAwait(false);
            Assert.True(session.IsDirty);
            Assert.True(session.CanUndo);
            Assert.NotNull(session.Preview);

            Assert.True(session.Undo());
            Assert.True(session.CanRedo);
            Assert.True(session.Redo());
        });

    [Fact]
    public Task EditDocument_Raises_Full_And_SelectLayer_Raises_Selection() =>
        RunSession(async () =>
        {
            using var temp = new TestImages.TempDir();
            var path = TestImages.SavePng(TestImages.Solid(8, 8, TestImages.Blue), temp.Path);
            using var session = new ImageSession();
            var kinds = new List<SessionChangeKind>();
            session.Changed += (_, kind) => kinds.Add(kind);

            await session.OpenAsync(path).ConfigureAwait(false);
            session.EditDocument(d => d.AddEmptyLayer("L2"));
            Assert.Contains(SessionChangeKind.Full, kinds);
            Assert.Equal(2, session.Document!.Layers.Count);

            kinds.Clear();
            session.SelectLayer(0);
            Assert.Equal([SessionChangeKind.Selection], kinds);
            Assert.True(session.IsDirty);
        });

    [Fact]
    public Task ApplyMacroAsync_Is_Single_Undo_Step() =>
        RunSession(async () =>
        {
            using var temp = new TestImages.TempDir();
            var path = TestImages.SavePng(TestImages.Solid(8, 8, TestImages.Green), temp.Path);
            using var session = new ImageSession();
            await session.OpenAsync(path).ConfigureAwait(false);

            var macro = new MacroDefinition
            {
                Name = "Invert twice",
                Steps =
                [
                    new MacroStep("invert", new Dictionary<string, object?>()),
                    new MacroStep("invert", new Dictionary<string, object?>()),
                ],
            };
            await session.ApplyMacroAsync(macro).ConfigureAwait(false);
            Assert.True(session.IsDirty);
            Assert.True(session.CanUndo);
            Assert.True(session.Undo());
            Assert.False(session.CanUndo);
        });

    [Fact]
    public Task ResizeCanvas_And_CropActiveLayer() =>
        RunSession(async () =>
        {
            using var temp = new TestImages.TempDir();
            var path = TestImages.SavePng(TestImages.Solid(20, 20, TestImages.Purple), temp.Path);
            using var session = new ImageSession();
            await session.OpenAsync(path).ConfigureAwait(false);

            session.ResizeCanvas(40, 30);
            Assert.Equal(40, session.Document!.Width);
            Assert.Equal(30, session.Document.Height);
            Assert.Equal(20, session.Document.ActiveLayer.Pixels.Width);

            var crop = session.CropActiveLayer(2, 3, 10, 8);
            Assert.Equal((10, 8), crop);
            Assert.Equal(10, session.Document.ActiveLayer.Pixels.Width);
            Assert.Equal(8, session.Document.ActiveLayer.Pixels.Height);
            Assert.Equal(2, session.Document.ActiveLayer.X);
            Assert.Equal(3, session.Document.ActiveLayer.Y);
        });

    [Fact]
    public Task ClearPreview_After_PreviewAsync_Does_Not_Throw() =>
        RunSession(async () =>
        {
            using var temp = new TestImages.TempDir();
            var path = TestImages.SavePng(TestImages.Solid(8, 8, TestImages.Teal), temp.Path);
            using var session = new ImageSession();
            await session.OpenAsync(path).ConfigureAwait(false);

            var invert = FilterCatalog.All.First(e => e.Id == "invert");
            await session
                .PreviewAsync(ctx => invert.Apply(ctx, invert.CreateDefaultValues()))
                .ConfigureAwait(false);
            Assert.True(session.IsPreviewing);
            session.ClearPreview();
            Assert.False(session.IsPreviewing);
        });

    [Fact]
    public void CropController_Normalize_And_Commit()
    {
        var crop = new CropController();
        var normalized = CropController.Normalize(new Avalonia.Point(10, 2), new Avalonia.Point(4, 8));
        Assert.Equal(4, normalized.X);
        Assert.Equal(2, normalized.Y);
        Assert.Equal(6, normalized.Width);
        Assert.Equal(6, normalized.Height);

        crop.Begin(new Avalonia.Point(1, 1));
        Assert.True(crop.IsDragging);
        crop.Commit(new Avalonia.Rect(2, 3, 5, 7), bitmapWidth: 20, bitmapHeight: 20);
        Assert.False(crop.IsDragging);
        Assert.True(crop.HasRect);
        Assert.Equal(new Avalonia.Rect(2, 3, 5, 7), crop.Rect);
    }
}
