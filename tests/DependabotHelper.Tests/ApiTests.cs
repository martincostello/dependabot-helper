// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MartinCostello.DependabotHelper.Infrastructure;
using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static MartinCostello.DependabotHelper.Infrastructure.GitHubFixtures;

namespace MartinCostello.DependabotHelper;

[Collection(AppCollection.Name)]
public sealed class ApiTests : IntegrationTests<AppFixture>
{
    public ApiTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

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
        int pullRequest5 = RandomNumber();

        RegisterGetRepository(owner, name, allowMergeCommit: allowMergeCommit, allowRebaseMerge: allowRebaseMerge);
        RegisterGetPullRequest(owner, name, pullRequest1);
        RegisterGetPullRequest(owner, name, pullRequest2, isDraft: true);
        RegisterGetPullRequest(owner, name, pullRequest4, response: () => CreatePullRequest(owner, name, pullRequest4, isMergeable: false));
        RegisterGetPullRequest(owner, name, pullRequest5);
        RegisterPutPullRequestMerge(owner, name, pullRequest1, mergeable: true);
        RegisterPutPullRequestMerge(owner, name, pullRequest5, mergeable: false);

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
                CreateIssue(owner, name, pullRequest5, CreatePullRequest(owner, name, pullRequest5)),
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

    [Theory]
    [InlineData("GET", "/github/repos/owner-name")]
    [InlineData("GET", "/github/repos/owner-name/repo-name/pulls")]
    [InlineData("POST", "/github/repos/owner-name/repo-name/pulls/merge")]
    [InlineData("POST", "/github/repos/owner-name/repo-name/pulls/42/approve")]
    public async Task Api_Returns_Http_500_If_An_Error_Occurs(
        string httpMethod,
        string requestUri)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync();
        using var request = new HttpRequestMessage(new(httpMethod), requestUri);

        // Act
        using var response = await client.SendAsync(request);

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

    [Theory]
    [InlineData("/github/repos/owner-name/repo-name/pulls/merge")]
    [InlineData("/github/repos/owner-name/repo-name/pulls/42/approve")]
    public async Task Api_Returns_Http_400_If_Antiforgery_Token_Missing(string requestUri)
    {
        // Arrange
        using var client = await CreateAuthenticatedClientAsync(setAntiforgeryTokenHeader: false);

        // Act
        using var response = await client.PostAsJsonAsync(requestUri, new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldBe("Invalid CSRF token specified.");
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
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
    [InlineData("COLLABORATOR")]
    [InlineData("MEMBER")]
    [InlineData("OWNER")]
    public async Task Can_Get_Pull_Requests_Project_Member_Can_Approve(string authorAssociation)
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
            () => new[] { CreateReview("octocat", "APPROVED", authorAssociation) });

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

    [Theory]
    [InlineData("COLLABORATOR")]
    [InlineData("MEMBER")]
    [InlineData("OWNER")]
    public async Task Can_Get_Pull_Requests_Project_Member_Can_Request_Changes(string authorAssociation)
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
                CreateReview("octocat", "CHANGES_REQUESTED", authorAssociation),
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

    [Theory]
    [InlineData("in_progress", null)]
    [InlineData("completed", "skipped")]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Pending(string status, string? conclusion)
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
        RegisterGetReviews(owner, name, number);
        RegisterGetStatuses(owner, name, commit);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetCheckSuites(
            owner,
            name,
            commit,
            () => CreateCheckSuites(new[] { CreateCheckSuite(status, conclusion) }));

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
        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(0);
        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(0);

        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Pending[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Pending);
    }

    [Theory]
    [InlineData("action_required")]
    [InlineData("cancelled")]
    [InlineData("failure")]
    [InlineData("timed_out")]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Failure(string conclusion)
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
        RegisterGetReviews(owner, name, number);
        RegisterGetStatuses(owner, name, commit);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetCheckSuites(
            owner,
            name,
            commit,
            () => CreateCheckSuites(new[] { CreateCheckSuite("completed", conclusion) }));

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
        actual.Pending.Count.ShouldBe(0);
        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(0);

        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Error[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Error);
    }

    [Theory]
    [InlineData("queued", null)]
    [InlineData("completed", "neutral")]
    [InlineData("completed", "success")]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Success(string status, string? conclusion)
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
        RegisterGetReviews(owner, name, number);
        RegisterGetStatuses(owner, name, commit);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetCheckSuites(
            owner,
            name,
            commit,
            () => CreateCheckSuites(new[] { CreateCheckSuite(status, conclusion) }));

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
        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(0);
        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(0);

        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Success[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Success);
    }

    [Theory]
    [InlineData(new[] { "completed", "success" }, new[] { "completed", "success" }, ChecksStatus.Success)]
    [InlineData(new[] { "queued", null }, new[] { "completed", "success" }, ChecksStatus.Success)]
    [InlineData(new[] { "in_progress", null }, new[] { "completed", "success" }, ChecksStatus.Pending)]
    [InlineData(new[] { "in_progress", null }, new[] { "in_progress", null }, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, new[] { "in_progress", null }, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, new[] { "queued", null }, ChecksStatus.Success)]
    [InlineData(new[] { "queued", null }, new[] { "completed", "failure" }, ChecksStatus.Error)]
    [InlineData(new[] { "completed", "success" }, new[] { "completed", "failure" }, ChecksStatus.Error)]
    public async Task Can_Get_Pull_Requests_With_Multiple_Check_Suites(
        string?[] first,
        string?[] second,
        ChecksStatus expected)
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
        RegisterGetReviews(owner, name, number);
        RegisterGetStatuses(owner, name, commit);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        var firstSuite = CreateCheckSuite(first[0]!, first[1]);
        var secondSuite = CreateCheckSuite(second[0]!, second[1]);

        RegisterGetCheckSuites(
            owner,
            name,
            commit,
            () => CreateCheckSuites(new[] { firstSuite, secondSuite }));

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
        actualPullRequest.Status.ShouldBe(expected);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Status_Pending()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetCheckSuites(owner, name, commit);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);
        RegisterGetReviews(owner, name, number);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetStatuses(
            owner,
            name,
            commit,
            () => CreateStatuses("pending", new[] { CreateStatus("pending") }));

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
        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(0);
        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(0);

        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Pending[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Pending);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("failure")]
    public async Task Can_Get_Pull_Requests_When_Status_Failure(string state)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetCheckSuites(owner, name, commit);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);
        RegisterGetReviews(owner, name, number);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetStatuses(
            owner,
            name,
            commit,
            () => CreateStatuses(state, new[] { CreateStatus(state) }));

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
        actual.Pending.Count.ShouldBe(0);
        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(0);

        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Error[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Error);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Status_Success()
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetCheckSuites(owner, name, commit);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);
        RegisterGetReviews(owner, name, number);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetStatuses(
            owner,
            name,
            commit,
            () => CreateStatuses("success", new[] { CreateStatus("success") }));

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
        actual.Error.ShouldNotBeNull();
        actual.Error.Count.ShouldBe(0);
        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(0);

        actual.Success.ShouldNotBeNull();
        actual.Success.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Success[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(ChecksStatus.Success);
    }

    [Theory]
    [InlineData("success", "success", "success", ChecksStatus.Success)]
    [InlineData("pending", "pending", "success", ChecksStatus.Pending)]
    [InlineData("pending", "pending", "pending", ChecksStatus.Pending)]
    [InlineData("failure", "pending", "failure", ChecksStatus.Error)]
    [InlineData("failure", "success", "failure", ChecksStatus.Error)]
    public async Task Can_Get_Pull_Requests_With_Multiple_Statuses(
        string overallState,
        string firstState,
        string secondState,
        ChecksStatus expected)
    {
        // Arrange
        int number = RandomNumber();
        string owner = RandomString();
        string name = RandomString();
        string commit = RandomString();
        string title = RandomString();

        var pullRequest = CreatePullRequest(owner, name, number, commitSha: commit, title: title);

        RegisterGetRepository(owner, name, number);
        RegisterGetCheckSuites(owner, name, commit);
        RegisterGetDependabotContent(owner, name);
        RegisterGetIssues(owner, name, "app/dependabot");
        RegisterGetPullRequest(owner, name, number, response: () => pullRequest);
        RegisterGetReviews(owner, name, number);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        var firstStatus = CreateStatus(firstState);
        var secondStatus = CreateStatus(secondState);

        RegisterGetStatuses(
            owner,
            name,
            commit,
            () => CreateStatuses(overallState, new[] { firstStatus, secondStatus }));

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
        actualPullRequest.Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData("failure", "completed", "failure", ChecksStatus.Error)]
    [InlineData("failure", "completed", "success", ChecksStatus.Error)]
    [InlineData("failure", "in_progress", null, ChecksStatus.Error)]
    [InlineData("pending", "completed", "success", ChecksStatus.Pending)]
    [InlineData("pending", "in_progress", null, ChecksStatus.Pending)]
    [InlineData("pending", "completed", "failure", ChecksStatus.Error)]
    [InlineData("success", "completed", "failure", ChecksStatus.Error)]
    [InlineData("success", "completed", "success", ChecksStatus.Success)]
    [InlineData("success", "in_progress", null, ChecksStatus.Pending)]
    public async Task Can_Get_Pull_Requests_With_Check_Suites_And_Statuses(
        string state,
        string checkSuiteStatus,
        string? checkSuiteConclusion,
        ChecksStatus expected)
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
        RegisterGetReviews(owner, name, number);

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[] { CreateIssue(owner, name, number, pullRequest, title: title) });

        RegisterGetCheckSuites(
            owner,
            name,
            commit,
            () => CreateCheckSuites(new[] { CreateCheckSuite(checkSuiteStatus, checkSuiteConclusion) }));

        RegisterGetStatuses(
            owner,
            name,
            commit,
            () => CreateStatuses(state, new[] { CreateStatus(state) }));

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
        actualPullRequest.Status.ShouldBe(expected);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
