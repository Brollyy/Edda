using System;
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

                Assert.DoesNotMatch(new Regex(@"[.,]"), nonPrecise);
                Assert.Matches(new Regex(@"^-?\d+[.,]\d{2}$"), precise);
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

        [Fact]
        public void DifficultyPredictorKeepsAlgorithmOptionsAndPredictedRanksInStableRows() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Difficulty Predictor");
                driver.WaitForIdle();

                var pkBeam = driver.GetElementBounds(PredictorPkBeamRadioId);
                var nytilde = driver.GetElementBounds(PredictorNytildeRadioId);
                var melchior = driver.GetElementBounds(PredictorMelchiorRadioId);
                var precise = driver.GetElementBounds(PredictorShowPreciseCheckboxId);
                var mapStats = driver.GetElementBounds(PredictorShowInMapStatsCheckboxId);
                var predict = driver.GetElementBounds(PredictorButtonId);

                Assert.InRange(Math.Abs(pkBeam.left - nytilde.left), 0, 18);
                Assert.InRange(Math.Abs(nytilde.left - melchior.left), 0, 18);
                Assert.True(precise.top > melchior.top, $"Expected Show precise values below the algorithm list, but checkbox top {precise.top:0.##} was not below Melchior top {melchior.top:0.##}.");
                Assert.True(mapStats.top > precise.top, $"Expected Show in map stats below Show precise values, but checkbox top {mapStats.top:0.##} was not below precise top {precise.top:0.##}.");

                driver.ClickButton(PredictorButtonId);
                driver.WaitForIdle();

                var difficulty0 = driver.GetElementBounds(PredictorDifficultyButton0Id);
                var difficulty1 = driver.GetElementBounds(PredictorDifficultyButton1Id);
                var difficulty2 = driver.GetElementBounds("btnDifficulty2");

                Assert.InRange(Math.Abs(difficulty0.top - difficulty1.top), 0, 6);
                Assert.InRange(Math.Abs(difficulty1.top - difficulty2.top), 0, 6);
                Assert.InRange(Math.Abs(difficulty0.width - difficulty1.width), 0, 4);
                Assert.InRange(Math.Abs(difficulty1.width - difficulty2.width), 0, 4);
                Assert.True(difficulty0.left < difficulty1.left && difficulty1.left < difficulty2.left, "Expected predicted difficulty buttons to stay ordered left-to-right.");
                Assert.True(predict.top > difficulty0.top, $"Expected Predict button docked below the results row, but button top {predict.top:0.##} was not below result row top {difficulty0.top:0.##}.");
            });
        }
    }
}
