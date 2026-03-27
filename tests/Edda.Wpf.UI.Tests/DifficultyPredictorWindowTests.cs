using System.Text.RegularExpressions;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class DifficultyPredictorWindowTests {
        private const string PredictorButtonId = "btnPredict";
        private const string PredictorResultsPanelId = "PanelPredictionResults";
        private const string PredictorWarningPanelId = "PanelPredictionWarning";
        private const string PredictorDifficultyButton0Id = "btnDifficulty0";
        private const string PredictorDifficultyButton1Id = "btnDifficulty1";
        private const string PredictorDifficultyLabel1Id = "lblDifficultyRank1";

        private const string PredictorPkBeamRadioId = "PKBeamAlgoRadioButton";
        private const string PredictorNytildeRadioId = "NytildeAlgoRadioButton";
        private const string PredictorMelchiorRadioId = "MelchiorAlgoRadioButton";
        private const string PredictorShowPreciseCheckboxId = "CheckShowPreciseValues";
        private const string PredictorShowInMapStatsCheckboxId = "CheckShowInMapStats";
        private const string MainDifficultyPredictionLabelId = "difficultyPrediction";
        private const string PredictorWindowTitle = "Difficulty Predictor";

        [Fact]
        public void DifficultyPredictorPredictShowsResultsForLoadedDifficulties() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                Assert.False(driver.IsVisible(PredictorResultsPanelId));
                driver.ClickButton(PredictorButtonId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(PredictorResultsPanelId));
                Assert.True(driver.IsEnabled(PredictorDifficultyButton0Id));
                Assert.False(driver.IsEnabled(PredictorDifficultyButton1Id));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PredictorDifficultyLabel1Id)));
                Assert.False(driver.IsVisible(PredictorWarningPanelId));
            });
        }

        [Fact(Skip = "All currently shipped predictors return values (AlwaysPredict); null path is not reachable without app-level seams.")]
        public void DifficultyPredictorNullPredictionShowsWarningAndQuestionMarks() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();
                driver.ClickButton(PredictorButtonId);
                driver.WaitForIdle();
            });
        }

        [Fact]
        public void DifficultyPredictorWindowIsSingleInstanceFromMenu() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(PredictorWindowTitle));

                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(PredictorWindowTitle));
            });
        }

        [Fact]
        public void DifficultyPredictorAlgorithmAndTogglesPersistAcrossReopen() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                driver.ClickButton(PredictorMelchiorRadioId);
                driver.ToggleCheckbox(PredictorShowPreciseCheckboxId, true);
                driver.ToggleCheckbox(PredictorShowInMapStatsCheckboxId, true);
                driver.WaitForIdle();

                driver.SendKeyboardShortcutToWindow("Alt+F4", PredictorWindowTitle);
                driver.WaitForIdle();
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                Assert.True(driver.IsChecked(PredictorMelchiorRadioId));
                Assert.True(driver.IsChecked(PredictorShowPreciseCheckboxId));
                Assert.True(driver.IsChecked(PredictorShowInMapStatsCheckboxId));
            });
        }

        [Fact]
        public void DifficultyPredictorPreciseToggleChangesDisplayedPredictionFormat() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                driver.ClickButton(PredictorPkBeamRadioId);
                driver.ToggleCheckbox(PredictorShowPreciseCheckboxId, false);
                driver.ClickButton(PredictorButtonId);
                driver.WaitForIdle();
                var nonPrecise = driver.GetText(PredictorDifficultyLabel1Id);

                driver.ToggleCheckbox(PredictorShowPreciseCheckboxId, true);
                driver.ClickButton(PredictorButtonId);
                driver.WaitForIdle();
                var precise = driver.GetText(PredictorDifficultyLabel1Id);

                Assert.DoesNotMatch(new Regex(@"\."), nonPrecise);
                Assert.Matches(new Regex(@"^-?\d+\.\d{2}$"), precise);
            });
        }

        [Fact]
        public void DifficultyPredictorMapStatsToggleUpdatesMainWindowPredictionVisibility() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                driver.ClickButton(PredictorNytildeRadioId);
                driver.WaitForIdle();

                driver.ToggleCheckbox(PredictorShowInMapStatsCheckboxId, true);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(MainDifficultyPredictionLabelId));

                driver.ToggleCheckbox(PredictorShowInMapStatsCheckboxId, false);
                driver.WaitForIdle();
                Assert.False(driver.IsVisible(MainDifficultyPredictionLabelId));
            });
        }
    }
}
