// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DependabotHelper.Infrastructure;
using MartinCostello.DependabotHelper.Pages;
using Microsoft.Playwright;
using static MartinCostello.DependabotHelper.Infrastructure.GitHubFixtures;

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
        string currentUser = "john-smith";
        string organization1 = "dotnet";
        string organization2 = "github";

        RegisterGetUserOrganizations(() => new[]
        {
            CreateUser(organization1, id: 9011267),
            CreateUser(organization2, id: 67483024),
        });

        RegisterGetUser(currentUser);

        RegisterGetRepositoriesForCurrentUser(response: () => new[]
        {
            CreateRepository(currentUser, "website"),
            CreateRepository(currentUser, "awesome-project", isPrivate: true),
            CreateRepository(currentUser, "blog"),
            CreateRepository(currentUser, "aspnetcore", isFork: true),
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

            // Act - Navigate to the configure page
            var configurePage = await managePage.ConfigureAsync();
            await configurePage.WaitForOwnerListAsync();

            // Assert
            var owners = await configurePage.GetOwnersAsync();
            owners.Count.ShouldBe(3);

            await owners[0].NameAsync().ShouldBe(currentUser);
            await owners[1].NameAsync().ShouldBe(organization1);
            await owners[2].NameAsync().ShouldBe(organization2);

            // Act
            var modal = await owners[0].ConfigureAsync();
            await modal.WaitForRepositoryListAsync();

            // Assert
            var repositories = await modal.GetRepositoriesAsync();
            repositories.Count.ShouldBe(4);

            await repositories[0].NameAsync().ShouldBe("aspnetcore");
            await repositories[0].IsForkAsync().ShouldBeTrue();
            await repositories[0].IsPrivateAsync().ShouldBeFalse();
            await repositories[0].IsSelectedAsync().ShouldBeFalse();

            await repositories[1].NameAsync().ShouldBe("awesome-project");
            await repositories[1].IsForkAsync().ShouldBeFalse();
            await repositories[1].IsPrivateAsync().ShouldBeTrue();
            await repositories[1].IsSelectedAsync().ShouldBeFalse();

            await repositories[2].NameAsync().ShouldBe("blog");
            await repositories[2].IsForkAsync().ShouldBeFalse();
            await repositories[2].IsPrivateAsync().ShouldBeFalse();
            await repositories[2].IsSelectedAsync().ShouldBeFalse();

            await repositories[3].NameAsync().ShouldBe("website");
            await repositories[3].IsForkAsync().ShouldBeFalse();
            await repositories[3].IsPrivateAsync().ShouldBeFalse();
            await repositories[3].IsSelectedAsync().ShouldBeFalse();

            // Act - Select some of the repositories and save the changes
            await repositories[1].ToggleAsync();
            await repositories[3].ToggleAsync();
            configurePage = await modal.SaveChangesAsync();

            // Assert - Check the selected repositories are enabled
            modal = await owners[0].ConfigureAsync();
            await modal.WaitForRepositoryListAsync();

            repositories = await modal.GetRepositoriesAsync();
            repositories.Count.ShouldBe(4);

            await repositories[0].IsSelectedAsync().ShouldBeFalse();
            await repositories[1].IsSelectedAsync().ShouldBeTrue();
            await repositories[2].IsSelectedAsync().ShouldBeFalse();
            await repositories[3].IsSelectedAsync().ShouldBeTrue();

            // Act - Change the selection and dismiss the modal
            await repositories[1].ToggleAsync();
            await repositories[3].ToggleAsync();
            configurePage = await modal.CloseAsync();

            // Assert - The selected repositories remains the same
            modal = await owners[0].ConfigureAsync();
            await modal.WaitForRepositoryListAsync();

            repositories = await modal.GetRepositoriesAsync();
            repositories.Count.ShouldBe(4);

            await repositories[0].IsSelectedAsync().ShouldBeFalse();
            await repositories[1].IsSelectedAsync().ShouldBeTrue();
            await repositories[2].IsSelectedAsync().ShouldBeFalse();
            await repositories[3].IsSelectedAsync().ShouldBeTrue();
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
