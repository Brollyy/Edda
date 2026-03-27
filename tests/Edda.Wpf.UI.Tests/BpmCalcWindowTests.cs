using System.Text.RegularExpressions;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class BpmCalcWindowTests {
        private const string BpmFinderInputCounterId = "lblInputCounter";
        private const string BpmFinderAverageBpmId = "lblAvgBPM";
        private const string BpmFinderPreciseAverageBpmId = "lblUnroundedAvgBPM";
        private const string BpmFinderResetButtonId = "btnReset";
        private const string BpmFinderWindowTitle = "BPM Finder";

        [Fact]
        public void BpmFinderStartsWithZeroedValues() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>BPM Finder");
                driver.WaitForIdle();

                Assert.Equal("0", driver.GetText(BpmFinderInputCounterId));
                Assert.Equal("0", driver.GetText(BpmFinderAverageBpmId));
                Assert.Equal("(0.00)", driver.GetText(BpmFinderPreciseAverageBpmId));
            });
        }

        [Fact]
        public void BpmFinderFirstTapStartsTimerWithoutIncrementingInputCounter() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>BPM Finder");
                driver.WaitForIdle();

                driver.SendKeyboardShortcutToWindow("1", BpmFinderWindowTitle);
                driver.WaitForIdle();

                Assert.Equal("0", driver.GetText(BpmFinderInputCounterId));
                Assert.Equal("0", driver.GetText(BpmFinderAverageBpmId));
                Assert.Equal("(0.00)", driver.GetText(BpmFinderPreciseAverageBpmId));
            });
        }

        [Fact]
        public void BpmFinderSecondAndLaterTapsUpdateCounterAndBpmFormats() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
                Assert.Matches(new Regex(@"^\(\d+\.\d{2}\)$"), driver.GetText(BpmFinderPreciseAverageBpmId));
            });
        }

        [Fact]
        public void BpmFinderResetReturnsDefaultStateAfterCapturedTaps() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
                Assert.Equal("(0.00)", driver.GetText(BpmFinderPreciseAverageBpmId));
            });
        }
    }
}
