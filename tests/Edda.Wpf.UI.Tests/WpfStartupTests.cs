using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class WpfStartupTests {
        private const string StartWindowId = "StartWindow";
        private const string NewMapButtonId = "ButtonNewMap";
        private const string ImportMapButtonId = "ButtonImportMap";
        private const string OpenMapButtonId = "ButtonOpenMap";

        [Fact]
        public void DriverCanBeCreated() {
            var driver = new WpfUIDriver();
            Assert.NotNull(driver);
        }

        [Fact(Skip = "Enable once WpfUIDriver launch/wait/element lookup is implemented.")]
        public void StartupWindowIsVisibleOnLaunch() {
            var driver = new WpfUIDriver();

            try {
                driver.Launch();
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(StartWindowId));
            } finally {
                driver.Shutdown();
            }
        }

        [Fact(Skip = "Enable once WpfUIDriver launch/wait/element lookup is implemented.")]
        public void StartupActionsAreEnabled() {
            var driver = new WpfUIDriver();

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
