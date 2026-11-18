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

using ChromaFx.Core.Colors;
using ChromaLab.Layers;
using ChromaLab.Services;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Services;

public class DocumentHistoryTests
{
    private static LayerDocument Doc(string name) =>
        LayerDocument.FromImage(TestImages.Solid(4, 4, TestImages.Red), name);

    [Fact]
    public void Undo_Redo_RoundTrip()
    {
        var history = new DocumentHistory();
        var a = Doc("A");
        var b = Doc("B");
        var c = Doc("C");

        history.PushUndo(a);
        history.PushUndo(b);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        var undone = history.TryUndo(c);
        Assert.Same(b, undone);
        Assert.True(history.CanRedo);

        var redone = history.TryRedo(undone!);
        Assert.Same(c, redone);
    }

    [Fact]
    public void PushUndo_Clears_Redo_And_Caps_At_MaxHistory()
    {
        var history = new DocumentHistory();
        for (var i = 0; i < ImageSession.MaxHistory + 5; i++)
            history.PushUndo(Doc($"n{i}"));

        Assert.True(history.CanUndo);

        // Drain undo stack; should not exceed MaxHistory snapshots.
        var count = 0;
        var current = Doc("current");
        while (history.TryUndo(current) is { } prev)
        {
            count++;
            current = prev;
        }

        Assert.Equal(ImageSession.MaxHistory, count);

        history.PushUndo(Doc("after-undo"));
        history.TryUndo(Doc("tip"));
        Assert.True(history.CanRedo);
        history.PushUndo(Doc("branch"));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_Resets_Stacks()
    {
        var history = new DocumentHistory();
        history.PushUndo(Doc("A"));
        history.TryUndo(Doc("B"));
        history.Clear();
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}
