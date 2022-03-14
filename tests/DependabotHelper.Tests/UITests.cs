// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Playwright;
using static MartinCostello.DependabotHelper.GitHubFixtures;

namespace MartinCostello.DependabotHelper;

[Collection(HttpServerCollection.Name)]
public class UITests : IntegrationTests<HttpServerFixture>
{
    public UITests(HttpServerFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    public static IEnumerable<object?[]> Browsers()
    {
        yield return new[] { BrowserType.Chromium, null };
        yield return new[] { BrowserType.Chromium, "chrome" };

        if (!OperatingSystem.IsLinux())
        {
            yield return new[] { BrowserType.Chromium, "msedge" };
        }

        yield return new[] { BrowserType.Firefox, null };

        if (OperatingSystem.IsMacOS())
        {
            yield return new[] { BrowserType.Webkit, null };
        }
    }

    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_Sign_In_And_Out(string browserType, string? browserChannel)
    {
        // Arrange
        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        var browser = new BrowserFixture(options, OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            // Load the application
            await page.GotoAsync(Fixture.ServerAddress);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var app = new ManagePage(page);

            // Act - Sign in
            await app.SignInAsync();

            // Assert
            await app.WaitForSignedInAsync();
            await app.UserNameAsync().ShouldBe("John Smith");

            // Arrange - Wait for the page to be ready
            await app.WaitForNoRepositoriesAsync();

            // Act - Sign out
            await app.SignOutAsync();

            // Assert
            await app.WaitForSignedOutAsync();
        });
    }

    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_Manage_Updates(string browserType, string? browserChannel)
    {
        // Arrange
        RegisterGetUserOrganizations(() => new[]
        {
            CreateUser("dotnet", id: 9011267),
            CreateUser("github", id: 67483024),
        });

        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        var browser = new BrowserFixture(options, OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            // Load the application
            await page.GotoAsync(Fixture.ServerAddress);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var managePage = new ManagePage(page);

            // Act - Sign in
            await managePage.SignInAsync();

            // Assert
            await managePage.WaitForSignedInAsync();

            // Arrange - Wait for the page to be ready
            await managePage.WaitForNoRepositoriesAsync();

            var configurePage = await managePage.ConfigureAsync();

            await configurePage.WaitForOwnerListAsync();
        });
    }

    public override async Task InitializeAsync()
    {
        InstallPlaywright();
        await base.InitializeAsync();
    }

    private static void InstallPlaywright()
    {
        int exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright exited with code {exitCode}");
        }
    }
}
