using Xunit;

namespace Edda.Avalonia.UI.Tests {
    public class AvaloniaStartupTests {
        private const string StartWindowId = "StartWindow";
        private const string NewMapButtonId = "ButtonNewMap";
        private const string ImportMapButtonId = "ButtonImportMap";
        private const string OpenMapButtonId = "ButtonOpenMap";

        [Fact]
        public void DriverCanBeCreated() {
            var driver = new AvaloniaUIDriver();
            Assert.NotNull(driver);
        }

        [Fact(Skip = "Enable once the Avalonia app shell and driver launch flow are implemented.")]
        public void StartupWindowIsVisibleOnLaunch() {
            var driver = new AvaloniaUIDriver();

            try {
                driver.Launch();
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(StartWindowId));
            } finally {
                driver.Shutdown();
            }
        }

        [Fact(Skip = "Enable once the Avalonia app shell and driver launch flow are implemented.")]
        public void StartupActionsAreEnabled() {
            var driver = new AvaloniaUIDriver();

            try {
                driver.Launch();
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(NewMapButtonId));
                Assert.True(driver.IsEnabled(ImportMapButtonId));
                Assert.True(driver.IsEnabled(OpenMapButtonId));
            } finally {
                driver.Shutdown();
            }
        }
    }
}