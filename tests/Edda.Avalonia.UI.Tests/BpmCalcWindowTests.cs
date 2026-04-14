using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class BpmCalcWindowTests {
    const string BpmFinderInputCounterId = "lblInputCounter";
    const string BpmFinderAverageBpmId = "lblAvgBPM";
    const string BpmFinderPreciseAverageBpmId = "lblUnroundedAvgBPM";
    const string BpmFinderResetButtonId = "btnReset";
    const string BpmFinderWindowTitle = "BPM Finder";

    [Fact]
    public void BpmFinderStartsWithZeroedValues() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Tools>BPM Finder");
            driver.WaitForIdle();

            Assert.Equal("0", driver.GetText(BpmFinderInputCounterId));
            Assert.Equal("0", driver.GetText(BpmFinderAverageBpmId));
            Assert.Equal(0, ParsePreciseBpmText(driver.GetText(BpmFinderPreciseAverageBpmId)), 2);
        });
    }

    [Fact]
    public void BpmFinderFirstTapStartsTimerWithoutIncrementingInputCounter() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Tools>BPM Finder");
            driver.WaitForIdle();

            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();

            Assert.Equal("0", driver.GetText(BpmFinderInputCounterId));
            Assert.Equal("0", driver.GetText(BpmFinderAverageBpmId));
            Assert.Equal(0, ParsePreciseBpmText(driver.GetText(BpmFinderPreciseAverageBpmId)), 2);
        });
    }

    [Fact]
    public void BpmFinderSecondAndLaterTapsUpdateCounterAndBpmFormats() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Tools>BPM Finder");
            driver.WaitForIdle();

            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();
            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();
            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();

            Assert.Equal("2", driver.GetText(BpmFinderInputCounterId));
            Assert.NotEqual("0", driver.GetText(BpmFinderAverageBpmId));
            Assert.Matches(new Regex(@"^\(\d+[.,]\d{2}\)$"), driver.GetText(BpmFinderPreciseAverageBpmId));
        });
    }

    [Fact]
    public void BpmFinderResetReturnsDefaultStateAfterCapturedTaps() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Tools>BPM Finder");
            driver.WaitForIdle();

            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();
            driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
            driver.WaitForIdle();
            Assert.Equal("1", driver.GetText(BpmFinderInputCounterId));

            driver.ClickButton(BpmFinderResetButtonId);
            driver.WaitForIdle();

            Assert.Equal("0", driver.GetText(BpmFinderInputCounterId));
            Assert.Equal("0", driver.GetText(BpmFinderAverageBpmId));
            Assert.Equal(0, ParsePreciseBpmText(driver.GetText(BpmFinderPreciseAverageBpmId)), 2);
        });
    }

    static double ParsePreciseBpmText(string text) {
        var normalized = text.Trim().TrimStart('(').TrimEnd(')').Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) {
            return parsed;
        }

        throw new Xunit.Sdk.XunitException($"Expected a precise BPM value, but got '{text}'.");
    }
}
