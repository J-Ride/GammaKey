# Colour Balance and Press-Again-To-Restore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Warm/cool, Red, Green, Blue and Black floor sliders to every profile, and make a profile's hotkey restore the original screen when that profile is already active.

**Architecture:** The five values join the existing `ColorValues` record as init properties with default 0, so every current caller and every saved `profiles.json` keeps working. `CurveBuilder` gets a per-channel stage after the existing single-curve maths. `AppController` remembers the active profile id and turns a repeat hotkey into a restore. `MainForm` gains one slider group with five rows.

**Tech Stack:** C# .NET 8 WinForms, xUnit. No NuGet beyond xUnit.

**Spec:** `docs/superpowers/specs/2026-11-09-colour-balance-and-toggle-design.md`

## Global Constraints

- All sliders at normal must produce the identity gamma table exactly (`ramp[i] == i * 257`).
- Each channel of the table must stay non-decreasing (Windows rejects a falling table).
- Channel gain is limited to plus or minus 30 percent; combined gain is clamped to 0.4 .. 1.6.
- Black floor maps to at most half scale (BlackFloor 100 means floor 0.5).
- Profile file `version` stays 1. Missing fields load as 0 (normal).
- Press-again-to-restore is always on. No setting, no new window.
- No new NuGet packages. `dotnet format --verify-no-changes` must pass on touched files.
- Commit messages are descriptive and carry no attribution trailers.
- Work on branch `feature/colour-balance` (already created from master). Run git with `git -C C:\Dev\Projects\GammaKey`.
- Run tests with `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~<TestClass>"`.

---

## File map

| File | Change | Responsibility |
|---|---|---|
| `src/GammaKey/Models/ColorValues.cs` | Modify | Add Temperature, Red, Green, Blue, BlackFloor; clamp them |
| `src/GammaKey/Models/Profile.cs` | Modify | Persist the five fields; map them in `Values` |
| `src/GammaKey/Color/CurveBuilder.cs` | Modify | Per-channel gain and black floor stage |
| `src/GammaKey/App/ActiveProfile.cs` | Create | Pure tracker: which profile id is on screen |
| `src/GammaKey/App/AppController.cs` | Modify | Track active profile; repeat hotkey restores |
| `src/GammaKey/UI/MainForm.cs` | Modify | Five slider rows in a new "Balance" group |
| `tests/GammaKey.Tests/ProfileStoreTests.cs` | Modify | Load defaults and clamping for new fields |
| `tests/GammaKey.Tests/CurveBuilderTests.cs` | Modify | Curve behaviour of new fields |
| `tests/GammaKey.Tests/ActiveProfileTests.cs` | Create | Toggle rules |

---

### Task 1: Model fields and persistence

**Files:**
- Modify: `src/GammaKey/Models/ColorValues.cs`
- Modify: `src/GammaKey/Models/Profile.cs`
- Test: `tests/GammaKey.Tests/ProfileStoreTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ColorValues` init properties `int Temperature`, `int Red`, `int Green`, `int Blue`, `int BlackFloor` (all default 0). `ColorValues.Clamped()` clamps them to -100..100 (BlackFloor 0..100). `Profile` properties with the same names, round-tripped by `Profile.Values`. `ColorValues.Normal` keeps all five at 0.

- [ ] **Step 1: Write the failing tests**

Add to `tests/GammaKey.Tests/ProfileStoreTests.cs` inside the class:

```csharp
    [Fact]
    public void MissingBalanceFieldsLoadAsNormal()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Old", "gamma": 1.2, "brightness": 5, "contrast": 0, "shadows": 0, "highlights": 0, "vibrance": 50 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(new ColorValues(1.2, 5, 0, 0, 0, 50), p.Values);
    }

    [Fact]
    public void OutOfRangeBalanceFieldsAreClampedOnLoad()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, """
            { "version": 1, "profiles": [ { "name": "Wild", "temperature": 500, "red": -500, "green": 500, "blue": -500, "blackFloor": 500 } ] }
            """);
        var store = new ProfileStore(path);

        var p = Assert.Single(store.Load().Profiles);

        Assert.Equal(ColorValues.Normal with { Temperature = 100, Red = -100, Green = 100, Blue = -100, BlackFloor = 100 }, p.Values);
    }

    [Fact]
    public void BalanceFieldsRoundTripThroughSave()
    {
        var path = PathFor("profiles.json");
        var store = new ProfileStore(path);
        var settings = ProfileStore.Defaults();
        settings.Profiles[0].Values = ColorValues.Normal with { Temperature = 30, Red = -10, Green = 5, Blue = 20, BlackFloor = 40 };
        store.Save(settings);

        var p = store.Load().Profiles[0];

        Assert.Equal(ColorValues.Normal with { Temperature = 30, Red = -10, Green = 5, Blue = 20, BlackFloor = 40 }, p.Values);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~ProfileStoreTests"`
Expected: build error `'ColorValues' does not contain a definition for 'Temperature'`.

- [ ] **Step 3: Extend `ColorValues`**

Replace the whole file `src/GammaKey/Models/ColorValues.cs` with:

```csharp
namespace GammaKey.Models;

public sealed record ColorValues(
    double Gamma,
    int Brightness,
    int Contrast,
    int Shadows,
    int Highlights,
    int Vibrance)
{
    public const double GammaMin = 0.3;
    public const double GammaMax = 2.8;
    public const int VibranceMin = 50;
    public const int VibranceMax = 100;

    // Colour balance. Init properties, not positional, so old callers and old profile files
    // keep working; a missing field is 0, which is normal.
    public int Temperature { get; init; }
    public int Red { get; init; }
    public int Green { get; init; }
    public int Blue { get; init; }
    public int BlackFloor { get; init; }

    public static readonly ColorValues Normal = new(1.0, 0, 0, 0, 0, VibranceMin);

    public ColorValues Clamped() => this with
    {
        Gamma = Math.Clamp(Gamma, GammaMin, GammaMax),
        Brightness = Math.Clamp(Brightness, -100, 100),
        Contrast = Math.Clamp(Contrast, -100, 100),
        Shadows = Math.Clamp(Shadows, -100, 100),
        Highlights = Math.Clamp(Highlights, -100, 100),
        Vibrance = Math.Clamp(Vibrance, VibranceMin, VibranceMax),
        Temperature = Math.Clamp(Temperature, -100, 100),
        Red = Math.Clamp(Red, -100, 100),
        Green = Math.Clamp(Green, -100, 100),
        Blue = Math.Clamp(Blue, -100, 100),
        BlackFloor = Math.Clamp(BlackFloor, 0, 100),
    };
}
```

- [ ] **Step 4: Extend `Profile`**

In `src/GammaKey/Models/Profile.cs`, after the line `public int Vibrance { get; set; } = ColorValues.VibranceMin;` add:

```csharp
    public int Temperature { get; set; }
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
    public int BlackFloor { get; set; }
```

Replace the `Values` property with:

```csharp
    [JsonIgnore]
    public ColorValues Values
    {
        get => new(Gamma, Brightness, Contrast, Shadows, Highlights, Vibrance)
        {
            Temperature = Temperature,
            Red = Red,
            Green = Green,
            Blue = Blue,
            BlackFloor = BlackFloor,
        };
        set
        {
            Gamma = value.Gamma;
            Brightness = value.Brightness;
            Contrast = value.Contrast;
            Shadows = value.Shadows;
            Highlights = value.Highlights;
            Vibrance = value.Vibrance;
            Temperature = value.Temperature;
            Red = value.Red;
            Green = value.Green;
            Blue = value.Blue;
            BlackFloor = value.BlackFloor;
        }
    }
```

`Clone()` already copies through `Values`, so it needs no change.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~ProfileStoreTests"`
Expected: all ProfileStoreTests pass, including the three new ones.

- [ ] **Step 6: Lint and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes --include src/GammaKey/Models/ColorValues.cs src/GammaKey/Models/Profile.cs tests/GammaKey.Tests/ProfileStoreTests.cs`
Expected: no output, exit 0.

```bash
git -C C:\Dev\Projects\GammaKey add src/GammaKey/Models/ColorValues.cs src/GammaKey/Models/Profile.cs tests/GammaKey.Tests/ProfileStoreTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Add temperature, RGB gain and black floor fields to profiles"
```

---

### Task 2: Per-channel curve stage

**Files:**
- Modify: `src/GammaKey/Color/CurveBuilder.cs`
- Test: `tests/GammaKey.Tests/CurveBuilderTests.cs`

**Interfaces:**
- Consumes: `ColorValues.Temperature`, `Red`, `Green`, `Blue`, `BlackFloor` from Task 1.
- Produces: `CurveBuilder.Build(ColorValues)` unchanged signature; the returned 768-entry table now differs per channel when the balance fields are non-zero. Layout stays red `[0..255]`, green `[256..511]`, blue `[512..767]`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/GammaKey.Tests/CurveBuilderTests.cs` inside the class:

```csharp
    [Fact]
    public void WarmTemperatureRaisesRedMidGrey()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.True(ramp[128] > 128 * 257);
    }

    [Fact]
    public void WarmTemperatureLowersBlueMidGrey()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.True(ramp[512 + 128] < 128 * 257);
    }

    [Fact]
    public void WarmTemperatureLeavesGreenUnchanged()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Temperature = 100 });

        Assert.Equal((ushort)(128 * 257), ramp[256 + 128]);
    }

    [Fact]
    public void RedGainChangesOnlyTheRedChannel()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 50 });

        Assert.True(ramp[128] > 128 * 257);
        Assert.Equal((ushort)(128 * 257), ramp[256 + 128]);
        Assert.Equal((ushort)(128 * 257), ramp[512 + 128]);
    }

    [Fact]
    public void GreenGainChangesOnlyTheGreenChannel()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Green = -50 });

        Assert.Equal((ushort)(128 * 257), ramp[128]);
        Assert.True(ramp[256 + 128] < 128 * 257);
        Assert.Equal((ushort)(128 * 257), ramp[512 + 128]);
    }

    [Fact]
    public void BlackFloorAtMaxLiftsBlackToHalfScale()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { BlackFloor = 100 });

        Assert.Equal((ushort)32768, ramp[0]);
    }

    [Fact]
    public void BlackFloorKeepsWhiteAtFullScale()
    {
        var ramp = CurveBuilder.Build(ColorValues.Normal with { BlackFloor = 100 });

        Assert.Equal((ushort)65535, ramp[255]);
    }

    [Fact]
    public void MaxGainNeverPushesMidGreyAboveLimit()
    {
        // Red 100 plus Temperature 100 would be gain 1.6 unclamped; the limit is 1.6, so the
        // 30 percent rule holds: entry 128 (128/255 of full scale) times 1.6.
        var ramp = CurveBuilder.Build(ColorValues.Normal with { Red = 100, Temperature = 100 });

        Assert.Equal((ushort)Math.Round(128 / 255.0 * 1.6 * 65535), ramp[128]);
    }

    [Theory]
    [InlineData(100, 100, 100, 100, 100)]
    [InlineData(-100, -100, -100, -100, 100)]
    [InlineData(100, -100, 100, -100, 0)]
    public void ExtremeBalanceValuesStayNonDecreasingPerChannel(int temperature, int red, int green, int blue, int floor)
    {
        var ramp = CurveBuilder.Build(new ColorValues(0.3, 100, 100, 100, 100, 50)
        {
            Temperature = temperature, Red = red, Green = green, Blue = blue, BlackFloor = floor,
        });

        for (int c = 0; c < 3; c++)
            for (int i = 1; i < 256; i++)
                Assert.True(ramp[c * 256 + i] >= ramp[c * 256 + i - 1], $"channel {c} entry {i} decreased");
    }
```

Note the existing test `TableHasThreeEqualChannels` stays valid: it uses only Gamma, so the channels stay equal.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~CurveBuilderTests"`
Expected: the nine new tests fail (channels are still identical and black is 0); the old tests pass.

- [ ] **Step 3: Add the per-channel stage**

Replace the whole file `src/GammaKey/Color/CurveBuilder.cs` with:

```csharp
using GammaKey.Models;

namespace GammaKey.Color;

public static class CurveBuilder
{
    public const int Size = 256;

    // Shadows and highlights are limited to a quarter of full scale so their slope never
    // cancels the base curve; combined with the running max below this keeps the table
    // non-decreasing, which Windows requires.
    private const double RegionStrength = 0.25;

    // A channel gain of more than 30 percent clips mid tones to white; the clamp keeps the
    // combined Temperature plus RGB slider inside that limit.
    private const double GainStrength = 0.3;
    private const double GainMin = 1.0 - GainStrength * 2;
    private const double GainMax = 1.0 + GainStrength * 2;

    // Black floor stops at half scale so the table stays inside the Windows gamma clamp even
    // with brightness at maximum.
    private const double FloorStrength = 0.5;

    public static ushort[] Build(ColorValues input)
    {
        var v = input.Clamped();
        var ramp = new ushort[Size * 3];
        double contrastFactor = (v.Contrast + 100) / 100.0;
        double brightnessShift = v.Brightness / 200.0;
        double shadows = v.Shadows / 100.0 * RegionStrength;
        double highlights = v.Highlights / 100.0 * RegionStrength;

        double[] gains =
        {
            Gain(v.Red / 100.0 + v.Temperature / 100.0),
            Gain(v.Green / 100.0),
            Gain(v.Blue / 100.0 - v.Temperature / 100.0),
        };
        double floor = v.BlackFloor / 100.0 * FloorStrength;
        var previous = new ushort[3];

        for (int i = 0; i < Size; i++)
        {
            double x = i / 255.0;
            double y = Math.Pow(x, 1.0 / v.Gamma);
            y = (y - 0.5) * contrastFactor + 0.5;
            y += brightnessShift;

            double shadowWeight = Math.Clamp(1.0 - 2.0 * x, 0.0, 1.0);
            y += shadows * shadowWeight * shadowWeight;

            double highlightWeight = Math.Clamp(2.0 * x - 1.0, 0.0, 1.0);
            y += highlights * highlightWeight * highlightWeight;

            for (int c = 0; c < 3; c++)
            {
                double yc = y * gains[c];
                yc = floor + yc * (1.0 - floor);
                var value = (ushort)Math.Round(Math.Clamp(yc, 0.0, 1.0) * 65535.0);
                if (value < previous[c]) value = previous[c];
                previous[c] = value;
                ramp[c * Size + i] = value;
            }
        }

        return ramp;
    }

    private static double Gain(double amount) => Math.Clamp(1.0 + GainStrength * amount, GainMin, GainMax);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~CurveBuilderTests"`
Expected: all CurveBuilderTests pass, including `NormalValuesProduceExactIdentityTable`.

If `BlackFloorAtMaxLiftsBlackToHalfScale` reports 32767 instead of 32768: `Math.Round` uses banker's rounding, and 0.5 * 65535 = 32767.5 rounds to 32768 (even). If the platform differs, change the assertion to `Assert.InRange(ramp[0], (ushort)32767, (ushort)32768)`. Do not change the builder.

- [ ] **Step 5: Lint and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes --include src/GammaKey/Color/CurveBuilder.cs tests/GammaKey.Tests/CurveBuilderTests.cs`
Expected: exit 0.

```bash
git -C C:\Dev\Projects\GammaKey add src/GammaKey/Color/CurveBuilder.cs tests/GammaKey.Tests/CurveBuilderTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Curve builder applies per-channel temperature, RGB gain and black floor"
```

---

### Task 3: Press again to restore

**Files:**
- Create: `src/GammaKey/App/ActiveProfile.cs`
- Modify: `src/GammaKey/App/AppController.cs`
- Test: `tests/GammaKey.Tests/ActiveProfileTests.cs`

**Interfaces:**
- Consumes: `ApplyResult.Accepted` (bool) from `GammaKey.Display`.
- Produces: `ActiveProfile` class with `string? Id { get; }`, `void Applied(string id)`, `void Cleared()`, `bool IsActive(string id)`. `AppController.Reset()` keeps its signature. `AppController.ResetApplied` fires with `"Restored"` on a toggle and `"Reset to original"` on a reset.

`AppController` touches real display hardware, so the toggle rule lives in a tiny pure class the controller owns. The controller wiring is checked by build plus the hand test in Task 5.

- [ ] **Step 1: Write the failing tests**

Create `tests/GammaKey.Tests/ActiveProfileTests.cs`:

```csharp
using GammaKey.App;
using Xunit;

namespace GammaKey.Tests;

public class ActiveProfileTests
{
    [Fact]
    public void NothingIsActiveAtStart()
    {
        var active = new ActiveProfile();

        Assert.False(active.IsActive("a"));
    }

    [Fact]
    public void SameProfileTwiceIsActiveOnSecondPress()
    {
        var active = new ActiveProfile();
        active.Applied("a");

        Assert.True(active.IsActive("a"));
    }

    [Fact]
    public void DifferentProfileSwitchesTheActiveOne()
    {
        var active = new ActiveProfile();
        active.Applied("a");
        active.Applied("b");

        Assert.False(active.IsActive("a"));
    }

    [Fact]
    public void ClearedMeansTheNextPressApplies()
    {
        var active = new ActiveProfile();
        active.Applied("a");
        active.Cleared();

        Assert.False(active.IsActive("a"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~ActiveProfileTests"`
Expected: build error `The type or namespace name 'ActiveProfile' could not be found`.

- [ ] **Step 3: Create `ActiveProfile`**

Create `src/GammaKey/App/ActiveProfile.cs`:

```csharp
namespace GammaKey.App;

// Which profile is on screen right now. Null means the original (baseline) is showing.
public sealed class ActiveProfile
{
    public string? Id { get; private set; }

    public void Applied(string id) => Id = id;

    public void Cleared() => Id = null;

    public bool IsActive(string id) => Id == id;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests --filter "FullyQualifiedName~ActiveProfileTests"`
Expected: 4 passed.

- [ ] **Step 5: Wire the controller**

In `src/GammaKey/App/AppController.cs`:

After the line `private Baseline _baseline = new();` add:

```csharp
    private readonly ActiveProfile _active = new();
```

Replace `ApplyProfile` with:

```csharp
    public ApplyResult ApplyProfile(Profile profile)
    {
        var result = Preview(profile.Values, profile.Target);
        // A refused apply left the old picture on screen, so the old profile stays the active one.
        if (result.Accepted) _active.Applied(profile.Id);
        ProfileApplied?.Invoke(profile, result.Note);
        return result;
    }
```

Replace `Reset()` with:

```csharp
    public void Reset() => Restore("Reset to original");

    private void Restore(string message)
    {
        Display.RestoreBaseline(_baseline);
        Vibrance.RestoreBaseline(_baseline);
        _active.Cleared();
        ResetApplied?.Invoke(message);
    }
```

Replace `OnHotkeyPressed` with:

```csharp
    private void OnHotkeyPressed(int id)
    {
        if (id == ResetHotkeyId)
        {
            Reset();
            return;
        }
        if (!_profileIdsByHotkeyId.TryGetValue(id, out var profileId)) return;
        var profile = Settings.Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null) return;
        // Pressing the active profile's key again puts the original back, like the reset key.
        if (_active.IsActive(profile.Id)) Restore("Restored");
        else ApplyProfile(profile);
    }
```

`Preview` is unchanged: editor previews never change the active profile.

- [ ] **Step 6: Build and run the whole test project**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: Build succeeded, 0 errors.

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests`
Expected: all tests pass.

- [ ] **Step 7: Lint and commit**

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes --include src/GammaKey/App/ActiveProfile.cs src/GammaKey/App/AppController.cs tests/GammaKey.Tests/ActiveProfileTests.cs`
Expected: exit 0.

```bash
git -C C:\Dev\Projects\GammaKey add src/GammaKey/App/ActiveProfile.cs src/GammaKey/App/AppController.cs tests/GammaKey.Tests/ActiveProfileTests.cs
git -C C:\Dev\Projects\GammaKey commit -m "Pressing the active profile's hotkey again restores the original screen"
```

---

### Task 4: Balance slider rows in the editor

**Files:**
- Modify: `src/GammaKey/UI/MainForm.cs` (slider fields near lines 34-39, group build near lines 180-191, load near lines 415-421, `CurrentValues()` near line 432)

**Interfaces:**
- Consumes: `ColorValues` init properties from Task 1; existing `GkSlider`, `GkGroup`, `Slider(string, GkSlider, bool)` helper and `_numbers` map in `MainForm`.
- Produces: nothing used elsewhere.

There are no MainForm unit tests in this project; the editor is covered by build, lint and the hand test in Task 5.

- [ ] **Step 1: Add the slider fields**

In `src/GammaKey/UI/MainForm.cs`, after the line declaring `_vibrance` (the `GkSlider _vibrance = new() { Minimum = 50, Maximum = 100, Origin = 50, ... }` line) add:

```csharp
    private readonly GkSlider _temperature = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _red = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _green = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _blue = new() { Minimum = -100, Maximum = 100, Origin = 0 };
    private readonly GkSlider _blackFloor = new() { Minimum = 0, Maximum = 100, Origin = 0, ScaleFormatter = v => v.ToString() };
```

- [ ] **Step 2: Add the group**

In `BuildLayout`, between `editor.Controls.Add(curveGroup);` and `var colorGroup = new GkGroup("Color", "NVIDIA only");` insert:

```csharp
        var balanceGroup = new GkGroup("Balance", "GDI gamma ramp");
        balanceGroup.Content.Controls.Add(Slider("Warm/cool", _temperature));
        balanceGroup.Content.Controls.Add(Slider("Red", _red));
        balanceGroup.Content.Controls.Add(Slider("Green", _green));
        balanceGroup.Content.Controls.Add(Slider("Blue", _blue));
        balanceGroup.Content.Controls.Add(Slider("Black floor", _blackFloor));
        editor.Controls.Add(balanceGroup);
```

The `Slider` helper already wires each bar to a number box, the value-changed preview and the reset-to-origin behaviour, so nothing else is needed for those.

- [ ] **Step 3: Load the values into the sliders**

In `ShowProfile` (the method containing `_vibrance.Value = Math.Clamp(v.Vibrance, 50, 100);`), after that line add:

```csharp
        _temperature.Value = v.Temperature;
        _red.Value = v.Red;
        _green.Value = v.Green;
        _blue.Value = v.Blue;
        _blackFloor.Value = v.BlackFloor;
```

The `foreach (var (bar, number) in _numbers)` loop that follows already copies each slider value to its number box.

- [ ] **Step 4: Read the values back**

Replace `CurrentValues()` with:

```csharp
    private ColorValues CurrentValues() => new(_gamma.Value / 100.0, _brightness.Value, _contrast.Value, _shadows.Value, _highlights.Value, _vibrance.Value)
    {
        Temperature = _temperature.Value,
        Red = _red.Value,
        Green = _green.Value,
        Blue = _blue.Value,
        BlackFloor = _blackFloor.Value,
    };
```

- [ ] **Step 5: Build, lint, run the app once**

Run: `dotnet build C:\Dev\Projects\GammaKey\GammaKey.sln`
Expected: Build succeeded.

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes --include src/GammaKey/UI/MainForm.cs`
Expected: exit 0.

Run: `dotnet run --project C:\Dev\Projects\GammaKey\src\GammaKey`
Expected: the main window shows a "Balance" group with five rows between "Curve" and "Color". Drag Warm/cool right: the screen turns warm. Drag Black floor right: black areas turn grey. Click "Restore monitor": the screen returns to normal. Quit from the tray.

If the window is now taller than the screen on the gaming PC, widen nothing; report it as a finding for Jordan rather than changing the layout.

- [ ] **Step 6: Commit**

```bash
git -C C:\Dev\Projects\GammaKey add src/GammaKey/UI/MainForm.cs
git -C C:\Dev\Projects\GammaKey commit -m "Editor shows Warm/cool, RGB and Black floor sliders in a Balance group"
```

---

### Task 5: Hand test, graph update and PR

**Files:**
- None modified by code. `graphify-out/` is regenerated.

- [ ] **Step 1: Full test run and lint**

Run: `dotnet test C:\Dev\Projects\GammaKey\tests\GammaKey.Tests`
Expected: all pass.

Run: `dotnet format C:\Dev\Projects\GammaKey\GammaKey.sln --verify-no-changes`
Expected: exit 0, or only the pre-existing whitespace finding in `src/GammaKey/Vibrance/NvApiNative.cs(30,30)`, which is not part of this work.

- [ ] **Step 2: Hand test checklist (run by Jordan on the gaming PC)**

1. Open an old `profiles.json` from before this branch: every profile loads, balance sliders sit at 0.
2. Set a profile to Warm/cool +60, hotkey it: the screen is warm; press the same hotkey again: the screen is back to original and the toast says "Restored".
3. Press profile A's hotkey, then profile B's hotkey: B applies, no restore in between.
4. Press B's hotkey, then the reset hotkey, then B's hotkey again: B applies (reset cleared the active profile).
5. Black floor 100 on a dark game scene: no true black remains.
6. Export a profile with balance values, import it on the laptop: values survive.

- [ ] **Step 3: Update the code graph**

Run: `graphify update C:\Dev\Projects\GammaKey`
Expected: graph regenerated, no error.

- [ ] **Step 4: Push and open the PR**

```bash
git -C C:\Dev\Projects\GammaKey push -u origin feature/colour-balance
gh pr create --repo J-Ride/GammaKey --head feature/colour-balance --base master --title "Colour balance sliders and press-again-to-restore" --body-file <pr-body-path>
```

The PR body lists the five sliders, the toggle rule, the spec path and the six hand checks above as unticked boxes. No attribution trailer.

Publishing the exe (`publish.ps1`) happens after merge, when Jordan has quit the running `GammaKey.exe`. It is not part of this plan.
