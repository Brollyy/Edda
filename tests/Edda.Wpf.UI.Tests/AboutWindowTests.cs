using System;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class AboutWindowTests {
        private const string AboutVersionTextId = "TxtVersionNumber";
        private const string AboutGithubLinkId = "TxtGithubLink";
        private const string AboutCommunityLinkId = "TxtRagnacustomsLink";
        private const string AboutWindowTitle = "About Edda";

        [Fact]
        public void AboutWindowShowsVersionAndLinkText() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Help>About Edda");
                driver.WaitForIdle();

                Assert.StartsWith("version ", driver.GetText(AboutVersionTextId), StringComparison.OrdinalIgnoreCase);
                Assert.Contains("github.com", driver.GetText(AboutGithubLinkId), StringComparison.OrdinalIgnoreCase);
                Assert.Contains("ragnacustoms.com", driver.GetText(AboutCommunityLinkId), StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void AboutWindowOpensAsSingleInstance() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Help>About Edda");
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));

                driver.SelectMenuItem("Help>About Edda");
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));
                Assert.StartsWith("version ", driver.GetText(AboutVersionTextId), StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void AboutWindowLinksCanBeClickedWithoutClosingWindow() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Help>About Edda");
                driver.WaitForIdle();

                driver.MoveMouseWithinElement(AboutGithubLinkId, 0.5, 0.5);
                driver.WaitForIdle();
                Assert.Equal("Hand", driver.GetCurrentCursorKind());

                driver.MoveMouseWithinElement(AboutVersionTextId, 0.5, 0.5);
                driver.WaitForIdle();
                Assert.NotEqual("Hand", driver.GetCurrentCursorKind());

                driver.ClickButton(AboutGithubLinkId);
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));

                driver.ClickButton(AboutCommunityLinkId);
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));
            });
        }
    }
}
