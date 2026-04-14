using System;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class AboutWindowTests {
    const string AboutVersionTextId = "TxtVersionNumber";
    const string AboutGithubLinkId = "TxtGithubLink";
    const string AboutCommunityLinkId = "TxtRagnacustomsLink";
    const string AboutWindowTitle = "About Edda";

    [Fact]
    public void AboutWindowShowsVersionAndLinkText() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Help>About Edda");
            driver.WaitForIdle();

            Assert.StartsWith("version ", driver.GetText(AboutVersionTextId), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("github.com", driver.GetText(AboutGithubLinkId), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ragnacustoms.com", driver.GetText(AboutCommunityLinkId), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AboutWindowOpensAsSingleInstance() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Help>About Edda");
            driver.WaitForIdle();

            driver.ClickButton(AboutGithubLinkId);
            driver.WaitForIdle();
            Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));

            driver.ClickButton(AboutCommunityLinkId);
            driver.WaitForIdle();
            Assert.Equal(1, driver.CountWindowsByTitle(AboutWindowTitle));
        });
    }
}
