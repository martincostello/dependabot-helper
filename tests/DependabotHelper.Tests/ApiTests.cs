// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using static MartinCostello.DependabotHelper.GitHubFixtures;

namespace MartinCostello.DependabotHelper;

[Collection(AppCollection.Name)]
public sealed class ApiTests : IDisposable
{
    private const string AuthorizationHeader = "Token gho_secret-access-token";

    private readonly IDisposable _scope;

    public ApiTests(AppFixture fixture, ITestOutputHelper outputHelper)
    {
        Fixture = fixture;
        OutputHelper = outputHelper;
        Fixture.SetOutputHelper(OutputHelper);
        _scope = Fixture.Interceptor.BeginScope();

        // TODO Fix scope disposal removing the existing bundle
        Fixture.Interceptor.RegisterBundle(Path.Combine("Bundles", "oauth-http-bundle.json"));
    }

    private AppFixture Fixture { get; }

    private ITestOutputHelper OutputHelper { get; }

    [Fact]
    public async Task Can_Approve_Pull_Request()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();
        int number = RandomNumber();

        RegisterPostReview(owner, name, number);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/github/repos/{owner}/{name}/pulls/{number}/approve",
            new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_No_Repositories()
    {
        // Arrange
        string owner = RandomString();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(owner);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_Get_Repositories_For_Self()
    {
        // Arrange
        string owner = "john-smith";
        string name = RandomString();
        int id = RandomNumber();

        RegisterGetUser(owner);
        RegisterGetRepositoriesForCurrentUser(
            response: () => new[] { CreateRepository(owner, name, id, visibility: "internal") });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var repository = actual[0];

        repository.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}");
        repository.Id.ShouldBe(id);
        repository.IsFork.ShouldBeFalse();
        repository.IsPrivate.ShouldBeTrue();
        repository.Name.ShouldBe(name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_Organization_Has_Repositories()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();
        int id = RandomNumber();

        RegisterGetUser(owner, userType: "organization");
        RegisterGetOrganizationRepositories(
            owner,
            response: () => new[] { CreateRepository(owner, name, id, isPrivate: true) });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var repository = actual[0];

        repository.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}");
        repository.Id.ShouldBe(id);
        repository.IsFork.ShouldBeFalse();
        repository.IsPrivate.ShouldBeTrue();
        repository.Name.ShouldBe(name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_User_Has_Repositories()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();
        int id = RandomNumber();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(
            owner,
            response: () => new[] { CreateRepository(owner, name, id) });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var repository = actual[0];

        repository.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}");
        repository.Id.ShouldBe(id);
        repository.IsFork.ShouldBeFalse();
        repository.IsPrivate.ShouldBeFalse();
        repository.Name.ShouldBe(name);
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    public async Task Can_Filter_Repositories_That_Are_Forks(
        bool includeForks,
        bool isFork,
        int expectedCount)
    {
        // Arrange
        Fixture.OverrideConfiguration(
            "Dependabot:IncludeForks",
            includeForks.ToString(CultureInfo.InvariantCulture));

        string owner = RandomString();
        string name = RandomString();
        int id = RandomNumber();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(
            owner,
            response: () => new[] { CreateRepository(owner, name, id, isFork: isFork) });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(expectedCount);

        if (expectedCount > 0)
        {
            var repository = actual[0];

            repository.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}");
            repository.Id.ShouldBe(id);
            repository.IsFork.ShouldBe(isFork);
            repository.IsPrivate.ShouldBeFalse();
            repository.Name.ShouldBe(name);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Can_Merge_Pull_Requests(bool allowMergeCommit, bool allowRebaseMerge)
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        int pullRequest1 = RandomNumber();
        int pullRequest2 = RandomNumber();
        int pullRequest3 = RandomNumber();
        int pullRequest4 = RandomNumber();

        RegisterGetRepository(owner, name, allowMergeCommit: allowMergeCommit, allowRebaseMerge: allowRebaseMerge);
        RegisterGetPullRequest(owner, name, pullRequest1);
        RegisterGetPullRequest(owner, name, pullRequest2, isDraft: true);
        RegisterGetPullRequest(owner, name, pullRequest4, response: () => CreatePullRequest(owner, name, pullRequest4, isMergeable: false));
        RegisterPutPullRequestMerge(owner, name, pullRequest1, mergeable: true);
        RegisterPutPullRequestMerge(owner, name, pullRequest2, mergeable: false);

        RegisterGetIssues(
            owner,
            name,
            "app/dependabot",
            () => new[]
            {
                CreateIssue(owner, name, pullRequest1, CreatePullRequest(owner, name, pullRequest1, isDraft: false)),
                CreateIssue(owner, name, pullRequest2, CreatePullRequest(owner, name, pullRequest2, isDraft: true)),
            });

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[]
            {
                CreateIssue(owner, name, pullRequest3, pullRequest: null),
                CreateIssue(owner, name, pullRequest4, CreatePullRequest(owner, name, pullRequest4, isMergeable: false)),
            });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/github/repos/{owner}/{name}/pulls/merge",
            new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cannot_Get_Repository_That_Does_Not_Exist()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, statusCode: StatusCodes.Status404NotFound);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.4");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_401_If_Token_Invalid()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, statusCode: StatusCodes.Status401Unauthorized);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.Title.ShouldBe("Unauthorized");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7235#section-3.1");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_403_If_Token_Forbidden()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, statusCode: StatusCodes.Status403Forbidden);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Title.ShouldBe("Forbidden");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.3");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_429_If_Api_Rate_Limits_Exceeded()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(
            owner,
            name,
            statusCode: StatusCodes.Status403Forbidden,
            response: () => new { message = "API rate limit exceeded" },
            configure: (builder) =>
            {
                builder.WithResponseHeader("x-ratelimit-limit", "60")
                       .WithResponseHeader("x-ratelimit-remaining", "0")
                       .WithResponseHeader("x-ratelimit-reset", "1377013266");
            });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)StatusCodes.Status429TooManyRequests);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status429TooManyRequests);
        problem.Title.ShouldBe("Too Many Requests");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc6585#section-4");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_500_If_An_Error_Occurs()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, statusCode: StatusCodes.Status500InternalServerError);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Title.ShouldBe("An error occurred while processing your request.");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.6.1");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_None_Open()
    {
        // Arrange
        int id = RandomNumber();
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, id);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetIssues(owner, name, "app/github-actions");

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>($"/github/repos/{owner}/{name}/pulls");

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe($"https://github.com/{owner}/{name}/network/updates");
        actual.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}/pulls");
        actual.Id.ShouldBe(id);
        actual.IsFork.ShouldBeFalse();
        actual.IsPrivate.ShouldBeFalse();
        actual.Name.ShouldBe(name);

        actual.All.ShouldNotBeNull();
        actual.All.ShouldBeEmpty();
        actual.Error.ShouldNotBeNull();
        actual.Error.ShouldBeEmpty();
        actual.Pending.ShouldNotBeNull();
        actual.Pending.ShouldBeEmpty();
        actual.Success.ShouldNotBeNull();
        actual.Success.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Dependabot_Not_Enabled()
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name);
        RegisterGetDependabotContent(owner, name, statusCode: StatusCodes.Status404NotFound, Array.Empty<byte>);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetIssues(owner, name, "app/github-actions");

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>($"/github/repos/{owner}/{name}/pulls");

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Draft()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, isDraft: true);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, CreatePullRequest(owner, name, number)) });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>($"/github/repos/{owner}/{name}/pulls");

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.ShouldBeEmpty();
        actual.Error.ShouldNotBeNull();
        actual.Error.ShouldBeEmpty();
        actual.Pending.ShouldNotBeNull();
        actual.Pending.ShouldBeEmpty();
        actual.Success.ShouldNotBeNull();
        actual.Success.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[] { CreateReview("octocat", "APPROVED") });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Pending[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}/pull/{number}");
        actualPullRequest.IsApproved.ShouldBeTrue();
        actualPullRequest.Number.ShouldBe(number);
        actualPullRequest.RepositoryName.ShouldBe(name);
        actualPullRequest.RepositoryOwner.ShouldBe(owner);
        actualPullRequest.Status.ShouldBe(ChecksStatus.Pending);
        actualPullRequest.Title.ShouldBe(title);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_No_Approvals()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetReviews(
            owner,
            name,
            number);

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("CHANGES_REQUESTED")]
    [InlineData("COMMENTED")]
    public async Task Can_Get_Pull_Requests_When_Not_Approved(string state)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[] { CreateReview("octocat", state) });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved_And_Commented()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[]
            {
                CreateReview("octocat", "APPROVED"),
                CreateReview("octodog", "COMMENTED"),
            });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved_And_Changes_Requested()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[]
            {
                CreateReview("octocat", "APPROVED"),
                CreateReview("octodog", "CHANGES_REQUESTED"),
            });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("APPROVED", "CHANGES_REQUESTED", false)]
    [InlineData("APPROVED", "APPROVED", true)]
    [InlineData("CHANGES_REQUESTED", "APPROVED", true)]
    [InlineData("CHANGES_REQUESTED", "CHANGES_REQUESTED", false)]
    [InlineData("CHANGES_REQUESTED", "COMMENTED", false)]
    [InlineData("COMMENTED", "COMMENTED", false)]
    [InlineData("COMMENTED", "APPROVED", true)]
    public async Task Can_Get_Pull_Requests_When_Review_Superseded(
        string firstState,
        string secondState,
        bool expected)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        var submittedAt = DateTimeOffset.UtcNow;

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[]
            {
                CreateReview("octocat", firstState, submittedAt: submittedAt),
                CreateReview("octocat", secondState, submittedAt: submittedAt.AddMinutes(5)),
            });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBe(expected);
    }

    [Theory]
    [InlineData("CONTRIBUTOR")]
    [InlineData("FIRST_TIMER")]
    [InlineData("FIRST_TIME_CONTRIBUTOR")]
    [InlineData("NONE")]
    public async Task Can_Get_Pull_Requests_External_Reviewer_Cannot_Approve(string authorAssociation)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        var submittedAt = DateTimeOffset.UtcNow;

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[] { CreateReview("notoctocat", "APPROVED", authorAssociation) });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("CONTRIBUTOR")]
    [InlineData("FIRST_TIMER")]
    [InlineData("FIRST_TIME_CONTRIBUTOR")]
    [InlineData("NONE")]
    public async Task Can_Get_Pull_Requests_External_Reviewer_Cannot_Request_Changes(string authorAssociation)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        var submittedAt = DateTimeOffset.UtcNow;

        RegisterGetReviews(
            owner,
            name,
            number,
            () => new[]
            {
                CreateReview("octocat", "APPROVED"),
                CreateReview("notoctocat", "CHANGES_REQUESTED", authorAssociation),
            });

        RegisterGetStatuses(owner, name, commit);
        RegisterGetCheckSuites(owner, name, commit);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner}/{name}/pulls",
            options);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    public void Dispose()
    {
        _scope?.Dispose();
        Fixture.ClearConfigurationOverrides();
    }

    private static void ConfigureRateLimit(HttpRequestInterceptionBuilder builder)
    {
        string oneHourFromNowEpoch = DateTimeOffset.UtcNow
            .AddHours(1)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        builder.WithResponseHeader("x-ratelimit-limit", "5000")
               .WithResponseHeader("x-ratelimit-remaining", "4999")
               .WithResponseHeader("x-ratelimit-reset", oneHourFromNowEpoch);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        AntiforgeryTokens anonymousTokens = await Fixture.GetAntiforgeryTokensAsync();

        var redirectHandler = new RedirectHandler(Fixture.ClientOptions.MaxAutomaticRedirections);

        var anonymousCookieHandler = new CookieContainerHandler();
        anonymousCookieHandler.Container.Add(
            Fixture.Server.BaseAddress,
            new Cookie(anonymousTokens.CookieName, anonymousTokens.CookieValue));

        using var anonymousClient = Fixture.CreateDefaultClient(redirectHandler, anonymousCookieHandler);
        anonymousClient.DefaultRequestHeaders.Add(anonymousTokens.HeaderName, anonymousTokens.RequestToken);

        var parameters = Array.Empty<KeyValuePair<string?, string?>>();
        using var content = new FormUrlEncodedContent(parameters);

        using var response = await anonymousClient.PostAsync("/sign-in", content);
        response.IsSuccessStatusCode.ShouldBeTrue();

        var authenticatedTokens = await Fixture.GetAntiforgeryTokensAsync(() => anonymousClient);

        var authenticatedCookieHandler = new CookieContainerHandler(anonymousCookieHandler.Container);

        var authenticatedClient = Fixture.CreateDefaultClient(authenticatedCookieHandler);
        authenticatedClient.DefaultRequestHeaders.Add(authenticatedTokens.HeaderName, authenticatedTokens.RequestToken);

        return authenticatedClient;
    }

    private void RegisterGetDependabotContent(
        string owner,
        string name,
        int statusCode = StatusCodes.Status200OK,
        Func<byte[]>? response = null)
    {
        response ??= CreateDependabotYaml;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/contents/.github/dependabot.yml")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(statusCode)
            .WithContent(response);

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetRepository(
        string owner,
        string name,
        int? id = null,
        bool allowMergeCommit = true,
        bool allowRebaseMerge = true,
        int statusCode = StatusCodes.Status200OK,
        Func<object>? response = null,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        response ??= () => CreateRepository(
            owner,
            name,
            id,
            allowMergeCommit: allowMergeCommit,
            allowRebaseMerge: allowRebaseMerge);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(statusCode)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetIssues(
        string owner,
        string name,
        string creator,
        Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        string encodedCreator = Uri.EscapeDataString(creator);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/issues?creator={encodedCreator}&filter=created&state=open&labels=dependencies&sort=created&direction=desc")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetPullRequest(
        string owner,
        string name,
        int number,
        bool isDraft = false,
        Func<object>? response = null)
    {
        response ??= () => CreatePullRequest(owner, name, number, isDraft);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetOrganizationRepositories(string login, Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/orgs/{login}/repos")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetUserRepositories(string login, Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/users/{login}/repos")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetRepositoriesForCurrentUser(Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/user/repos")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetUser(string login, string userType = "user", Func<object>? response = null)
    {
        response ??= () => CreateUser(login, userType);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/users/{login}")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetStatuses(
        string owner,
        string name,
        string reference,
        Func<object>? response = null)
    {
        response ??= () => CreateStatuses("success");

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/commits/{reference}/status")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetCheckSuites(
        string owner,
        string name,
        string reference,
        Func<object>? response = null)
    {
        response ??= () => CreateCheckSuites();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/commits/{reference}/check-suites")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetReviews(
        string owner,
        string name,
        int number,
        Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}/reviews")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterPostReview(string owner, string name, int number)
    {
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForPost()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}/reviews")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterPutPullRequestMerge(string owner, string name, int number, bool mergeable = true)
    {
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForPut()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}/merge")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(mergeable ? StatusCodes.Status200OK : StatusCodes.Status405MethodNotAllowed)
            .WithSystemTextJsonContent(new { });

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }
}
