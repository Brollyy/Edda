using System.Text.RegularExpressions;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class DifficultyPredictorWindowTests {
    const string PredictorButtonId = "btnPredict";
    const string PredictorResultsPanelId = "PanelPredictionResults";
    const string PredictorWarningPanelId = "PanelPredictionWarning";
    const string PredictorDifficultyButton0Id = "btnDifficulty0";
    const string PredictorDifficultyButton1Id = "btnDifficulty1";
    const string PredictorDifficultyLabel1Id = "lblDifficultyRank1";

    const string PredictorPkBeamRadioId = "PKBeamAlgoRadioButton";
    const string PredictorNytildeRadioId = "NytildeAlgoRadioButton";
    const string PredictorMelchiorRadioId = "MelchiorAlgoRadioButton";
    const string PredictorShowPreciseCheckboxId = "CheckShowPreciseValues";
    const string PredictorShowInMapStatsCheckboxId = "CheckShowInMapStats";
    const string MainDifficultyPredictionLabelId = "difficultyPrediction";
    const string PredictorWindowTitle = "Difficulty Predictor";

    [Fact]
    public void DifficultyPredictorPredictShowsResultsForLoadedDifficulties() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
    }

    [Fact]
    public void DifficultyPredictorWindowIsSingleInstanceFromMenu() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
