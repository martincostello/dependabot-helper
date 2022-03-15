﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DependabotHelper.Infrastructure;
using MartinCostello.DependabotHelper.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static MartinCostello.DependabotHelper.Builders.GitHubFixtures;

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

    public override async Task InitializeAsync()
    {
        InstallPlaywright();
        await base.InitializeAsync();
    }

    public override Task DisposeAsync()
    {
        var cache = Fixture.Services.GetRequiredService<IMemoryCache>();

        if (cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(100);
        }

        return base.DisposeAsync();
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
            await app.WaitForNoOwners();

            // Act - Sign out
            await app.SignOutAsync();

            // Assert
            await app.WaitForSignedOutAsync();
        });
    }

    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_Configure_Repositories(string browserType, string? browserChannel)
    {
        // Arrange
        var currentUser = CreateUser("john-smith", id: 1);
        string organization1 = "dotnet";
        string organization2 = "github";

        RegisterGetUserOrganizations(
            CreateUser(organization1, id: 9011267),
            CreateUser(organization2, id: 67483024));

        RegisterGetUser(currentUser);

        RegisterGetRepositoriesForCurrentUser(
            currentUser.CreateRepository("website"),
            currentUser.CreateRepository("awesome-project", isPrivate: true),
            currentUser.CreateRepository("blog"),
            currentUser.CreateRepository("aspnetcore", isFork: true));

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
            await managePage.WaitForNoOwners();

            // Act - Navigate to the configure page
            var configurePage = await managePage.ConfigureAsync();
            await configurePage.WaitForOwnerListAsync();

            // Assert
            var owners = await configurePage.GetOwnersAsync();
            owners.Count.ShouldBe(3);

            await owners[0].NameAsync().ShouldBe(currentUser.Login);
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
            await modal.WaitForRepositoriesAsync();

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

    [Theory]
    [MemberData(nameof(Browsers))]
    public async Task Can_Manage_Updates(string browserType, string? browserChannel)
    {
        // Arrange
        var owner = CreateUser("john-smith", id: 1);

        var repository1 = owner.CreateRepository();
        repository1.Name = "a-" + repository1.Name;

        var repository2 = owner.CreateRepository();
        repository2.Name = "z-" + repository1.Name;

        RegisterGetDependabotContent(repository1);
        RegisterGetDependabotContent(repository2, StatusCodes.Status404NotFound);
        RegisterGetIssues(repository1, "app/github-actions");
        RegisterGetIssues(repository2, "app/github-actions");
        RegisterGetRepository(repository1);
        RegisterGetRepository(repository2);
        RegisterGetRepositoriesForCurrentUser(repository1, repository2);
        RegisterGetUser(owner);
        RegisterGetUserOrganizations();

        var failedPullRequest = repository1.CreatePullRequest();

        RegisterGetCheckSuites(failedPullRequest, CreateCheckSuites(CreateCheckSuite("completed", "failure")));
        RegisterGetPullRequest(failedPullRequest);
        RegisterGetReviews(failedPullRequest);
        RegisterGetStatuses(failedPullRequest);
        RegisterGetIssues(
            repository1,
            "app/dependabot",
            failedPullRequest.CreateIssue());

        var pendingPullRequest = repository2.CreatePullRequest();

        RegisterGetCheckSuites(pendingPullRequest, CreateCheckSuites(CreateCheckSuite("in_progress", null)));
        RegisterGetPullRequest(pendingPullRequest);
        RegisterGetStatuses(pendingPullRequest);

        var successPullRequest = repository2.CreatePullRequest();

        RegisterGetCheckSuites(successPullRequest, CreateCheckSuites(CreateCheckSuite("completed", "success")));
        RegisterGetPullRequest(successPullRequest);
        RegisterGetStatuses(successPullRequest);

        RegisterGetIssues(
            repository2,
            "app/dependabot",
            pendingPullRequest.CreateIssue(),
            successPullRequest.CreateIssue());

        RegisterGetReviews(pendingPullRequest);
        RegisterGetReviews(successPullRequest, CreateReview("octocat", "APPROVED"));

        var options = new BrowserFixtureOptions()
        {
            BrowserType = browserType,
            BrowserChannel = browserChannel,
        };

        var browser = new BrowserFixture(options, OutputHelper);
        await browser.WithPageAsync(async page =>
        {
            await page.GotoAsync(Fixture.ServerAddress);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var managePage = new ManagePage(page);
            await managePage.SignInAsync();
            await managePage.WaitForSignedInAsync();
            await managePage.WaitForNoOwners();

            var configurePage = await managePage.ConfigureAsync();
            await configurePage.WaitForOwnerListAsync();

            var owners = await configurePage.GetOwnersAsync();
            owners.Count.ShouldBe(1);

            var modal = await owners[0].ConfigureAsync();
            await modal.WaitForRepositoryListAsync();

            var repositories = await modal.GetRepositoriesAsync();
            repositories.Count.ShouldBe(2);

            await repositories[0].ToggleAsync();
            await repositories[1].ToggleAsync();

            configurePage = await modal.SaveChangesAsync();
            managePage = await configurePage.ManageAsync();

            // Act
            await managePage.WaitForOwnerListAsync();

            // Assert
            var repoOwners = await managePage.GetOwnersAsync();

            repoOwners.ShouldNotBeNull();

            var repoOwner = repoOwners.ShouldHaveSingleItem();
            await repoOwner.WaitForRepositoryListAsync();

            var ownerRepos = await repoOwner.GetRepsitoriesAsync();

            ownerRepos.ShouldNotBeNull();
            ownerRepos.Count.ShouldBe(2);

            await ownerRepos[0].NameAsync().ShouldBe(repository1.Name);
            await ownerRepos[0].IsDependabotEnabledAsync().ShouldBeTrue();
            await ownerRepos[0].ApprovedCountAsync().ShouldBe(0);
            await ownerRepos[0].ErrorCountAsync().ShouldBe(1);
            await ownerRepos[0].PendingCountAsync().ShouldBe(0);
            await ownerRepos[0].SuccessCountAsync().ShouldBe(0);

            await ownerRepos[1].NameAsync().ShouldBe(repository2.Name);
            await ownerRepos[1].IsDependabotEnabledAsync().ShouldBeFalse();
            await ownerRepos[1].ApprovedCountAsync().ShouldBe(1);
            await ownerRepos[1].ErrorCountAsync().ShouldBe(0);
            await ownerRepos[1].PendingCountAsync().ShouldBe(1);
            await ownerRepos[1].SuccessCountAsync().ShouldBe(1);
        });
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
