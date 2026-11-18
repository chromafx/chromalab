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

using ChromaLab.Macros;
using ChromaLab.Tests.Helpers;

namespace ChromaLab.Tests.Macros;

public class MacroRunnerTests
{
    [Fact]
    public void Validate_Returns_Null_For_Known_Ids()
    {
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep("invert", new Dictionary<string, object?>()),
                new MacroStep("posterize", new Dictionary<string, object?> { ["divisions"] = 4 }),
            ],
        };
        Assert.Null(MacroRunner.Validate(macro));
    }

    [Fact]
    public void Validate_Lists_Unknown_Ids()
    {
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep("invert", new Dictionary<string, object?>()),
                new MacroStep("not-a-filter", new Dictionary<string, object?>()),
                new MacroStep("also-missing", new Dictionary<string, object?>()),
            ],
        };
        var error = MacroRunner.Validate(macro);
        Assert.NotNull(error);
        Assert.Contains("not-a-filter", error);
        Assert.Contains("also-missing", error);
    }

    [Fact]
    public void ValidateOrThrow_Throws_On_Unknown()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep("nope", new Dictionary<string, object?>())],
        };
        Assert.Throws<InvalidOperationException>(() => MacroRunner.ValidateOrThrow(macro));
    }

    [Fact]
    public void Apply_Runs_Known_Steps_On_Image()
    {
        var image = TestImages.SolidRgb(8, 8, 200, 100, 50);
        var before = image.PixelSpan[0];
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep("invert", new Dictionary<string, object?>())],
        };
        MacroRunner.Apply(image, macro, TestContext.Current.CancellationToken);
        var after = image.PixelSpan[0];
        Assert.NotEqual(before.Red, after.Red);
    }
}
