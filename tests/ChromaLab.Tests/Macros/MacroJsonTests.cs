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
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Macros;

public class MacroJsonTests
{
    [Fact]
    public void Recorder_RoundTrip_Via_Json()
    {
        using var temp = new TestImages.TempDir();
        var invert = FilterCatalog.All.First(e => e.Id == "invert");
        var posterize = FilterCatalog.All.First(e => e.Id == "posterize");
        var values = posterize.CreateDefaultValues();
        values.Set("divisions", 6);

        var recorder = new MacroRecorder();
        recorder.Start();
        recorder.Record(invert, invert.CreateDefaultValues());
        recorder.Record(posterize, values);
        recorder.Stop();

        var path = Path.Combine(temp.Path, "demo" + MacroJson.Extension);
        MacroJson.Write(path, recorder.ToDefinition("Demo"));
        var loaded = MacroJson.Read(path);

        Assert.Equal(MacroDefinition.CurrentVersion, loaded.Version);
        Assert.Equal("Demo", loaded.Name);
        Assert.Equal(2, loaded.Steps.Count);
        Assert.Equal("invert", loaded.Steps[0].Id);
        Assert.Equal("posterize", loaded.Steps[1].Id);
        Assert.Equal(6, Convert.ToInt32(loaded.Steps[1].Params["divisions"]));
    }

    [Fact]
    public void Read_Rejects_Unsupported_Version()
    {
        using var temp = new TestImages.TempDir();
        var path = Path.Combine(temp.Path, "bad" + MacroJson.Extension);
        File.WriteAllText(
            path,
            """
            {
              "version": 99,
              "name": "Nope",
              "steps": []
            }
            """
        );

        var ex = Assert.Throws<InvalidDataException>(() => MacroJson.Read(path));
        Assert.Contains("Unsupported macro version", ex.Message);
    }

    [Fact]
    public void Record_Is_NoOp_When_Not_Recording()
    {
        var recorder = new MacroRecorder();
        var invert = FilterCatalog.All.First(e => e.Id == "invert");
        recorder.Record(invert, invert.CreateDefaultValues());
        Assert.Equal(0, recorder.StepCount);
        Assert.False(recorder.IsRecording);
    }
}
