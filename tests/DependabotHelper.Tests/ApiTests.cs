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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterPostReview(pullRequest);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls/{pullRequest.Number}/approve",
            new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_No_Repositories()
    {
        // Arrange
        var owner = CreateUser();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(owner);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner.Login}");

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_Get_Repositories_For_Self()
    {
        // Arrange
        var owner = CreateUser("john-smith", id: 1);
        var repository = owner.CreateRepository();
        repository.Visibility = "internal";

        RegisterGetUser(owner);
        RegisterGetRepositoriesForCurrentUser(repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner.Login}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}");
        actualRepository.Id.ShouldBe(repository.Id);
        actualRepository.IsFork.ShouldBeFalse();
        actualRepository.IsPrivate.ShouldBeTrue();
        actualRepository.Name.ShouldBe(repository.Name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_Organization_Has_Repositories()
    {
        // Arrange
        var owner = CreateUser(userType: "organization");
        var repository = owner.CreateRepository();
        repository.IsPrivate = true;

        RegisterGetUser(owner);
        RegisterGetOrganizationRepositories(owner, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner.Login}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}");
        actualRepository.Id.ShouldBe(repository.Id);
        actualRepository.IsFork.ShouldBeFalse();
        actualRepository.IsPrivate.ShouldBeTrue();
        actualRepository.Name.ShouldBe(repository.Name);
    }

    [Fact]
    public async Task Can_Get_Repositories_If_User_Has_Repositories()
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(owner, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner.Login}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var actualRepository = actual[0];

        actualRepository.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}");
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

        var owner = CreateUser();
        var repository = owner.CreateRepository();
        repository.IsFork = isFork;

        RegisterGetUser(owner);
        RegisterGetUserRepositories(owner, repository);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Repository>>($"/github/repos/{owner.Login}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(expectedCount);

        if (expectedCount > 0)
        {
            var actualRepository = actual[0];

            actualRepository.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}");
            actualRepository.Id.ShouldBe(repository.Id);
            actualRepository.IsFork.ShouldBe(isFork);
            actualRepository.IsPrivate.ShouldBeFalse();
            actualRepository.Name.ShouldBe(repository.Name);
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
        var owner = CreateUser();

        var repository = owner.CreateRepository();
        repository.AllowMergeCommit = allowMergeCommit;
        repository.AllowRebaseMerge = allowRebaseMerge;

        var pullRequest1 = repository.CreatePullRequest();

        var pullRequest2 = repository.CreatePullRequest();
        pullRequest2.IsDraft = true;

        var pullRequest3 = repository.CreatePullRequest();

        var pullRequest4 = repository.CreatePullRequest();
        pullRequest4.IsMergeable = false;

        var pullRequest5 = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetPullRequest(pullRequest1);
        RegisterGetPullRequest(pullRequest2);
        RegisterGetPullRequest(pullRequest4);
        RegisterGetPullRequest(pullRequest5);
        RegisterPutPullRequestMerge(pullRequest1, mergeable: true);
        RegisterPutPullRequestMerge(pullRequest5, mergeable: false);

        RegisterGetIssues(
            repository,
            "app/dependabot",
            pullRequest1.CreateIssue(),
            pullRequest2.CreateIssue());

        RegisterGetIssues(
            repository,
            "app/github-actions",
            repository.CreateIssue(),
            pullRequest4.CreateIssue(),
            pullRequest5.CreateIssue());

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.PostAsJsonAsync(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls/merge",
            new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cannot_Get_Repository_That_Does_Not_Exist()
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status404NotFound);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner.Login}/{repository.Name}/pulls");

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
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status401Unauthorized);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner.Login}/{repository.Name}/pulls");

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
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetRepository(repository, statusCode: StatusCodes.Status403Forbidden);

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        using var response = await client.GetAsync($"/github/repos/{owner.Login}/{repository.Name}/pulls");

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
        var owner = CreateUser();
        var repository = owner.CreateRepository();

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
        using var response = await client.GetAsync($"/github/repos/{owner.Login}/{repository.Name}/pulls");

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
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetIssues(repository, "app/github-actions");

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls");

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}/network/updates");
        actual.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}/pulls");
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository, statusCode: StatusCodes.Status404NotFound);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetIssues(repository, "app/github-actions");

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls");

        // Assert
        actual.ShouldNotBeNull();
        actual.DependabotHtmlUrl.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_When_Draft()
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();

        var pullRequest = repository.CreatePullRequest();
        pullRequest.IsDraft = true;

        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetRepository(repository);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls");

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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        actualPullRequest.HtmlUrl.ShouldBe($"https://github.com/{owner.Login}/{repository.Name}/pull/{pullRequest.Number}");
        actualPullRequest.IsApproved.ShouldBeTrue();
        actualPullRequest.Number.ShouldBe(pullRequest.Number);
        actualPullRequest.RepositoryName.ShouldBe(repository.Name);
        actualPullRequest.RepositoryOwner.ShouldBe(owner.Login);
        actualPullRequest.Status.ShouldBe(ChecksStatus.Pending);
        actualPullRequest.Title.ShouldBe(pullRequest.Title);
    }

    [Fact]
    public async Task Can_Get_Pull_Requests_No_Approvals()
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", state));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("octodog", "COMMENTED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("octodog", "CHANGES_REQUESTED"));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        var submittedAt = DateTimeOffset.UtcNow;

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", firstState, submittedAt: submittedAt),
            CreateReview("octocat", secondState, submittedAt: submittedAt.AddMinutes(5)));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "CHANGES_REQUESTED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("notoctocat", "APPROVED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetReviews(
            pullRequest,
            CreateReview("octocat", "APPROVED"),
            CreateReview("notoctocat", "CHANGES_REQUESTED", authorAssociation));

        RegisterGetStatuses(pullRequest);
        RegisterGetCheckSuites(pullRequest);

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
    [InlineData("in_progress", null, false)]
    [InlineData("in_progress", null, true)]
    [InlineData("queued", null, true)]
    [InlineData("completed", "skipped", false)]
    [InlineData("completed", "skipped", true)]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Pending(
        string status,
        string? conclusion,
        bool hasCheckRun)
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        var checkSuite = CreateCheckSuite(status, conclusion);

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(checkSuite));

        if (hasCheckRun)
        {
            RegisterGetCheckRuns(repository, checkSuite.Id, CreateCheckRun(status, conclusion));
        }
        else
        {
            RegisterGetCheckRuns(repository, checkSuite.Id);
        }

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(CreateCheckSuite("completed", conclusion)));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
    [InlineData("completed", "neutral")]
    [InlineData("completed", "success")]
    public async Task Can_Get_Pull_Requests_When_Check_Suite_Success(string status, string? conclusion)
    {
        // Arrange
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(CreateCheckSuite(status, conclusion)));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);
        RegisterGetStatuses(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        var firstSuite = CreateCheckSuite(first[0]!, first[1]);
        var secondSuite = CreateCheckSuite(second[0]!, second[1]);

        if (firstHasCheckRun)
        {
            RegisterGetCheckRuns(repository, firstSuite.Id, CreateCheckRun(first[0]!));
        }
        else
        {
            RegisterGetCheckRuns(repository, firstSuite.Id);
        }

        if (secondHasCheckRun)
        {
            RegisterGetCheckRuns(repository, secondSuite.Id, CreateCheckRun(second[0]!));
        }
        else
        {
            RegisterGetCheckRuns(repository, secondSuite.Id);
        }

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(firstSuite, secondSuite));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("pending", CreateStatus("pending")));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(state, CreateStatus(state)));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses("success", CreateStatus("success")));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetCheckSuites(pullRequest);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        var firstStatus = CreateStatus(firstState);
        var secondStatus = CreateStatus(secondState);

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(overallState, firstStatus, secondStatus));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
        var owner = CreateUser();
        var repository = owner.CreateRepository();
        var pullRequest = repository.CreatePullRequest();

        RegisterGetRepository(repository);
        RegisterGetDependabotContent(repository);
        RegisterGetIssues(repository, "app/dependabot");
        RegisterGetPullRequest(pullRequest);
        RegisterGetReviews(pullRequest);

        RegisterGetIssues(
            repository,
            "app/github-actions",
            pullRequest.CreateIssue());

        RegisterGetCheckSuites(
            pullRequest,
            CreateCheckSuites(CreateCheckSuite(checkSuiteStatus, checkSuiteConclusion)));

        RegisterGetStatuses(
            pullRequest,
            CreateStatuses(state, CreateStatus(state)));

        var options = CreateSerializerOptions();
        using var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<RepositoryPullRequests>(
            $"/github/repos/{owner.Login}/{repository.Name}/pulls",
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
