// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JustEat.HttpClientInterception;
using MartinCostello.DependabotHelper.Infrastructure;
using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static MartinCostello.DependabotHelper.Builders.GitHubFixtures;

namespace MartinCostello.DependabotHelper;

[Collection<AppCollection>]
public sealed class ApiTests(AppFixture fixture, ITestOutputHelper outputHelper) : IntegrationTests<AppFixture>(fixture, outputHelper)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task Can_Approve_Pull_Request()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterPostReview(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/github/repos/{user.Login}/{repository.Name}/pulls/{pullRequest.Number}/approve",
            new { },
            CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_No_Repositories()
    {
        // Arrange
        var user = CreateUser();

        RegisterGetUser(user);
        RegisterGetUserRepositories(user);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{user.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_Get_Repositories_For_Self()
    {
        // Arrange
        var user = CreateUser(CurrentUserLogin, CurrentUserId);
        var repository = user.CreateRepository();
        repository.Visibility = "internal";

        RegisterGetUser(user);
        RegisterGetRepositoriesForCurrentUser(repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{user.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}");
        actualRepository.Id.ShouldBe(repository.Id);
        actualRepository.IsFork.ShouldBeFalse();
        actualRepository.IsPrivate.ShouldBeTrue();
        actualRepository.Name.ShouldBe(repository.Name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_Organization_Has_Repositories()
    {
        // Arrange
        var organization = CreateUser(userType: "organization");
        var repository = organization.CreateRepository();
        repository.IsPrivate = true;

        RegisterGetUser(organization);
        RegisterGetOrganizationRepositories(organization, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{organization.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{organization.Login}/{repository.Name}");
        actualRepository.Id.ShouldBe(repository.Id);
        actualRepository.IsFork.ShouldBeFalse();
        actualRepository.IsPrivate.ShouldBeTrue();
        actualRepository.Name.ShouldBe(repository.Name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_User_Has_Repositories()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetUser(user);
        RegisterGetUserRepositories(user, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{user.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}");
        actualRepository.Id.ShouldBe(repository.Id);
        actualRepository.IsFork.ShouldBeFalse();
        actualRepository.IsPrivate.ShouldBeFalse();
        actualRepository.Name.ShouldBe(repository.Name);
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

        var user = CreateUser();

        var repository = user.CreateRepository();
        repository.IsFork = isFork;

        RegisterGetUser(user);
        RegisterGetUserRepositories(user, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{user.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(expectedCount);

        if (expectedCount > 0)
        {
            var actualRepository = actual[0];

            actualRepository.HtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}");
            actualRepository.Id.ShouldBe(repository.Id);
            actualRepository.IsFork.ShouldBe(isFork);
            actualRepository.IsPrivate.ShouldBeFalse();
            actualRepository.Name.ShouldBe(repository.Name);
        }
    }

    [Theory]
    [InlineData(false, false, null, 1)]
    [InlineData(false, false, "internal", 0)]
    [InlineData(false, false, "public", 1)]
    [InlineData(false, true, null, 0)]
    [InlineData(true, false, null, 1)]
    [InlineData(true, false, "internal", 1)]
    [InlineData(true, true, null, 1)]
    public async Task Can_Filter_Repositories_That_Are_Private(
        bool includePrivate,
        bool isPrivate,
        string? visibility,
        int expectedCount)
    {
        // Arrange
        Fixture.OverrideConfiguration(
            "Dependabot:IncludePrivate",
            includePrivate.ToString(CultureInfo.InvariantCulture));

        var user = CreateUser();
        var repository = user.CreateRepository();

        repository.IsPrivate = isPrivate;
        repository.Visibility = visibility;

        RegisterGetUser(user);
        RegisterGetUserRepositories(user, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{user.Login}", CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(expectedCount);
    }

    [Theory]
    [InlineData(null, false, false, true)]
    [InlineData(null, false, true, true)]
    [InlineData(null, true, false, true)]
    [InlineData(null, true, true, true)]
    [InlineData("Invalid", true, true, true)]
    [InlineData("Merge", true, true, true)]
    [InlineData("Rebase", true, true, true)]
    [InlineData("Squash", true, true, true)]
    public async Task Can_Merge_Pull_Requests(
        string? mergeMethod,
        bool allowMergeCommit,
        bool allowRebaseMerge,
        bool allowSquashMerge)
    {
        // Arrange
        var user = CreateUser();

        var repository = user.CreateRepository();
        repository.AllowMergeCommit = allowMergeCommit;
        repository.AllowRebaseMerge = allowRebaseMerge;
        repository.AllowSquashMerge = allowSquashMerge;

        var pullRequest1 = repository.CreatePullRequest();
        var pullRequest2 = repository.CreatePullRequest();
        repository.CreatePullRequest();
        var pullRequest4 = repository.CreatePullRequest();
        var pullRequest5 = repository.CreatePullRequest();

        pullRequest2.IsDraft = true;
        pullRequest4.IsMergeable = false;

        RegisterGetRepository(repository);
        RegisterPutPullRequestMerge(pullRequest1, mergeable: true);
        RegisterPutPullRequestMerge(pullRequest5, mergeable: false);
        RegisterEnableAutomerge(pullRequest5);
        RegisterNoIssues(repository);

        RegisterGetIssues(
            repository,
            DependabotBotName,
            pullRequest1.CreateIssue(),
            pullRequest2.CreateIssue());

        RegisterGetIssues(
            repository,
            GitHubActionsBotName,
            repository.CreateIssue(),
            pullRequest4.CreateIssue(),
            pullRequest5.CreateIssue());

        RegisterGetPullRequest(pullRequest1);
        RegisterGetPullRequest(pullRequest2);
        RegisterGetPullRequest(pullRequest4);
        RegisterGetPullRequest(pullRequest5);

        using var client = await CreateAuthenticatedClientAsync();

        string requestUri = $"/github/repos/{user.Login}/{repository.Name}/pulls/merge";

        if (mergeMethod is not null)
        {
            requestUri += $"?mergeMethod={mergeMethod}";
        }

        // Act
        using var response = await client.PostAsJsonAsync(requestUri, new { }, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var actual = await response.Content.ReadFromJsonAsync<MergePullRequestsResponse>(CancellationToken);

        actual.ShouldNotBeNull();
        actual.Numbers.ShouldNotBeNull();
        actual.Numbers.ToArray().ShouldBe([pullRequest1.Number]);
    }

    [Fact]
    public async Task Cannot_Get_Repository_That_Does_Not_Exist()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status404NotFound);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{user.Login}/{repository.Name}/pulls", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.5");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_401_If_Token_Invalid()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status401Unauthorized);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{user.Login}/{repository.Name}/pulls", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.Title.ShouldBe("Unauthorized");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.2");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_403_If_Token_Forbidden()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status403Forbidden);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{user.Login}/{repository.Name}/pulls", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Title.ShouldBe("Forbidden");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.4");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Api_Returns_Http_429_If_Api_Rate_Limits_Exceeded()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(
            repository,
            statusCode: StatusCodes.Status403Forbidden,
            configure: (builder) =>
            {
                builder.WithSystemTextJsonContent(new { message = "API rate limit exceeded" })
                       .WithResponseHeader("x-ratelimit-limit", "60")
                       .WithResponseHeader("x-ratelimit-remaining", "0")
                       .WithResponseHeader("x-ratelimit-reset", "1377013266");
            });

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{user.Login}/{repository.Name}/pulls", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe((HttpStatusCode)StatusCodes.Status429TooManyRequests);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

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
        using var response = await client.SendAsync(request, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problem.Title.ShouldBe("An error occurred while processing your request.");
        problem.Detail.ShouldBeNull();
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.6.1");
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
        using var response = await client.PostAsJsonAsync(requestUri, new { }, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);

        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldBe("Invalid CSRF token specified.");
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.1");
        problem.Instance.ShouldBeNull();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_None_Open()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterNoIssues(repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}/network/updates");
        actual.HtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}/pulls");
        actual.Id.ShouldBe(repository.Id);
        actual.IsFork.ShouldBeFalse();
        actual.IsPrivate.ShouldBeFalse();
        actual.Name.ShouldBe(repository.Name);

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
        var user = CreateUser();
        var repository = user.CreateRepository();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository, statusCode: StatusCodes.Status404NotFound);
        RegisterNoIssues(repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Draft()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();

        var pullRequest = repository.CreatePullRequest();
        pullRequest.IsDraft = true;

        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetRepository(repository);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        actual.Pending.ShouldNotBeNull();
        actual.Pending.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Pending[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.HtmlUrl.ShouldBe($"https://github.com/{user.Login}/{repository.Name}/pull/{pullRequest.Number}");
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.HasConflicts.ShouldBeFalse();
        actualPullRequest.IsApproved.ShouldBeTrue();
        actualPullRequest.Number.ShouldBe(pullRequest.Number);
        actualPullRequest.RepositoryName.ShouldBe(repository.Name);
        actualPullRequest.RepositoryOwner.ShouldBe(user.Login);
        actualPullRequest.Status.ShouldBe(ChecksStatus.Pending);
        actualPullRequest.Title.ShouldBe(pullRequest.Title);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_No_Approvals()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Can_Get_Pull_Requests_With_No_Required_Approvals(int requiredApprovingReviewCount)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredApprovingReviewCount);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("CHANGES_REQUESTED", null)]
    [InlineData("CHANGES_REQUESTED", 0)]
    [InlineData("CHANGES_REQUESTED", 1)]
    [InlineData("COMMENTED", null)]
    [InlineData("COMMENTED", 0)]
    [InlineData("COMMENTED", 1)]
    public async Task Can_Get_Pull_Requests_When_Not_Approved(string state, int? requiredReviewers)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        if (requiredReviewers is { } count)
        {
            var protection = CreateBranchProtection(count);
            RegisterGetBranchProtection(pullRequest, protection);
        }

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", state));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("john-smith", false)]
    [InlineData("octocat", true)]
    public async Task Can_Get_Pull_Requests_When_Approved_By_Self_Or_Another(
        string approver,
        bool expectedCanApprove)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview(approver, "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBe(expectedCanApprove);
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Theory]
    [InlineData("john-smith", false)]
    [InlineData("octocat", true)]
    public async Task Can_Get_Pull_Requests_When_Approved_By_Self_Or_Another_But_Not_Enough_Reviews(
        string approver,
        bool expectedCanApprove)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(2);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview(approver, "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBe(expectedCanApprove);
        actualPullRequest.IsApproved.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved_By_Self_And_Another_And_Enough_Reviews()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(2);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("john-smith", "APPROVED"),
            CreateReview("octokit", "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeFalse();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Can_Get_Pull_Requests_When_Approved_By_Others_And_Enough_Reviews(
        int requiredApprovingReviewCount)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredApprovingReviewCount);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var reviews = Enumerable.Repeat(() => CreateReview(Guid.NewGuid().ToString(), "APPROVED"), requiredApprovingReviewCount)
            .Select((p) => p())
            .ToArray();

        RegisterGetReviews(
            pullRequest,
            reviews);

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved_And_Commented()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("octodog", "COMMENTED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Approved_And_Changes_Requested()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("octodog", "CHANGES_REQUESTED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var submittedAt = DateTimeOffset.UtcNow;

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", firstState, submittedAt: submittedAt),
            CreateReview("octocat", secondState, submittedAt: submittedAt.AddMinutes(5)));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBe(expected);
    }

    [Theory]
    [InlineData("COLLABORATOR")]
    [InlineData("MEMBER")]
    [InlineData("OWNER")]
    public async Task Can_Get_Pull_Requests_Project_Member_Can_Approve(string authorAssociation)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_Project_Bot_Can_Approve()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var review = CreateReview("octocat", "APPROVED", "NONE");
        review.User.UserType = "Bot";

        RegisterGetReviews(pullRequest, review);

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Theory]
    [InlineData("COLLABORATOR")]
    [InlineData("MEMBER")]
    [InlineData("OWNER")]
    public async Task Can_Get_Pull_Requests_Project_Member_Can_Request_Changes(string authorAssociation)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "CHANGES_REQUESTED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("notoctocat", "APPROVED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("notoctocat", "CHANGES_REQUESTED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.CanApprove.ShouldBeTrue();
        actualPullRequest.IsApproved.ShouldBeTrue();
    }

    [Theory]
    [InlineData("in_progress", null, false)]
    [InlineData("in_progress", null, true)]
    [InlineData("queued", null, true)]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Pending(
        string status,
        string? conclusion,
        bool hasCheckRun)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var checkSuite = CreateCheckSuite(status, conclusion);

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(checkSuite));

        RegisterGetCheckRuns(
            repository,
            checkSuite.Id,
            hasCheckRun ? [CreateCheckRun(status, conclusion)] : []);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci", "lint"]);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var checkSuite = CreateCheckSuite("completed", conclusion);

        RegisterGetCheckRuns(
            repository,
            checkSuite.Id,
            CreateCheckRun("completed", conclusion, name: "ci"));

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(checkSuite));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
    [InlineData("completed", "neutral")]
    [InlineData("completed", "skipped")]
    [InlineData("completed", "success")]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Success_When_No_Required_Statuses(string status, string? conclusion)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(CreateCheckSuite(status, conclusion)));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Success_With_All_Required_Statuses()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci"]);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var checkSuite = CreateCheckSuite("completed", "success");

        RegisterGetCheckRuns(
            repository,
            checkSuite.Id,
            CreateCheckRun("completed", "success", name: "ci"));

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(checkSuite));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Success_With_Required_Status_Missing()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci", "lint"]);

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        var checkSuite = CreateCheckSuite("completed", "success");

        RegisterGetCheckRuns(
            repository,
            checkSuite.Id,
            CreateCheckRun("completed", "success", name: "ci"));

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(checkSuite));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
    [InlineData(new[] { "completed", "success" }, true, new[] { "completed", "failure" }, true, ChecksStatus.Error)]
    [InlineData(new[] { "completed", "success" }, true, new[] { "completed", "success" }, true, ChecksStatus.Success)]
    [InlineData(new[] { "in_progress", null }, true, new[] { "completed", "success" }, true, ChecksStatus.Pending)]
    [InlineData(new[] { "in_progress", null }, true, new[] { "in_progress", null }, true, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, false, new[] { "completed", "failure" }, true, ChecksStatus.Error)]
    [InlineData(new[] { "queued", null }, false, new[] { "completed", "success" }, false, ChecksStatus.Success)]
    [InlineData(new[] { "queued", null }, true, new[] { "completed", "success" }, true, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, false, new[] { "in_progress", null }, true, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, true, new[] { "in_progress", null }, true, ChecksStatus.Pending)]
    [InlineData(new[] { "queued", null }, false, new[] { "queued", null }, false, ChecksStatus.Success)]
    [InlineData(new[] { "queued", null }, true, new[] { "queued", null }, true, ChecksStatus.Pending)]
    public async Task Can_Get_Pull_Requests_With_Multiple_Check_Suites(
        string?[] first,
        bool firstHasCheckRun,
        string?[] second,
        bool secondHasCheckRun,
        ChecksStatus expected)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(
            repository,
            GitHubActionsBotName,
            pullRequest.CreateIssue());

        var firstSuite = CreateCheckSuite(first[0]!, first[1]);
        var secondSuite = CreateCheckSuite(second[0]!, second[1]);

        RegisterGetCheckRuns(
            repository,
            firstSuite.Id,
            firstHasCheckRun ? [CreateCheckRun(first[0]!)] : []);

        RegisterGetCheckRuns(
            repository,
            secondSuite.Id,
            secondHasCheckRun ? [CreateCheckRun(second[0]!)] : []);

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(firstSuite, secondSuite));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("pending", CreateStatus("pending")));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci", "lint"]);

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(state, CreateStatus(state)));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
    public async Task Can_Get_Pull_Requests_When_Status_Success_When_No_Required_Statuses()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("success", CreateStatus("success")));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Status_Success_With_All_Required_Statuses()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci"]);

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("success", CreateStatus("success", "ci")));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Status_Success_With_All_Required_Statuses_And_Optional_Statuses()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci"]);

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(
                "success",
                CreateStatus("success", "lint"),
                CreateStatus("success", "ci")));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Status_Success_With_Required_Status_Missing()
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        var protection = CreateBranchProtection(requiredStatusCheckContexts: ["ci", "lint"]);

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetBranchProtection(pullRequest, protection);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("success", CreateStatus("success", "ci")));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(
                overallState,
                CreateStatus(firstState),
                CreateStatus(secondState)));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

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
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(
                CreateCheckSuite(checkSuiteStatus, checkSuiteConclusion)));

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(state, CreateStatus(state)));

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.All.ShouldNotBeNull();
        actual.All.Count.ShouldBe(1);

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.Status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("behind", false)]
    [InlineData("blocked", false)]
    [InlineData("clean", false)]
    [InlineData("dirty", true)]
    [InlineData("draft", false)]
    [InlineData("has_hooks", false)]
    [InlineData("unknown", false)]
    [InlineData("unstable", false)]
    public async Task Can_Get_If_Pull_Request_Has_Conflicts(string? mergeableState, bool expected)
    {
        // Arrange
        var user = CreateUser();
        var repository = user.CreateRepository();
        var pullRequest = repository.CreatePullRequest();
        pullRequest.MergeableState = mergeableState!;

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetPullRequest(pullRequest);
        RegisterNoIssues(repository);

        RegisterGetIssues(repository, GitHubActionsBotName, pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{user.Login}/{repository.Name}/pulls",
            SerializerOptions,
            CancellationToken);

        // Assert
        actual.ShouldNotBeNull();

        var actualPullRequest = actual.All[0];

        actualPullRequest.ShouldBeSameAs(actual.Pending[0]);

        actualPullRequest.ShouldNotBeNull();
        actualPullRequest.HasConflicts.ShouldBe(expected);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }
}
