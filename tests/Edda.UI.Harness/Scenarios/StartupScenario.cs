namespace Edda.UI.Harness.Scenarios {
    public class StartupScenario : UIScenario {
        public override string Name => "Startup";

        public override void Run(IUIDriver driver) {
            try {
                driver.Launch();

                driver.WaitForIdle();

                if (!driver.IsVisible("MainWindow")) {
                    throw new Exception("Main window did not appear after startup.");
                }

                if (!driver.IsEnabled("btnLoadSong")) {
                    throw new Exception("Load Song button should be enabled on startup.");
                }
            } finally {
                driver.Shutdown();
            }
        }
    }
}