﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
using MartinCostello.DependabotHelper.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using static MartinCostello.DependabotHelper.Builders.GitHubFixtures;

namespace MartinCostello.DependabotHelper.Infrastructure;

public abstract class IntegrationTests<T> : IAsyncLifetime
    where T : AppFixture
{
    private readonly IDisposable _scope;

    protected IntegrationTests(T fixture, ITestOutputHelper outputHelper)
    {
        Fixture = fixture;
        OutputHelper = outputHelper;
        Fixture.SetOutputHelper(OutputHelper);
        _scope = Fixture.Interceptor.BeginScope();

        // TODO Fix scope disposal removing the existing bundle
        Fixture.Interceptor.RegisterBundle(Path.Combine("Bundles", "oauth-http-bundle.json"));
    }

    protected T Fixture { get; }

    protected ITestOutputHelper OutputHelper { get; }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync()
    {
        _scope?.Dispose();
        Fixture.ClearConfigurationOverrides();
        return Task.CompletedTask;
    }

    protected static HttpRequestInterceptionBuilder ConfigureRateLimit(HttpRequestInterceptionBuilder builder)
    {
        string oneHourFromNowEpoch = DateTimeOffset.UtcNow
            .AddHours(1)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        return builder
            .WithResponseHeader("x-ratelimit-limit", "5000")
            .WithResponseHeader("x-ratelimit-remaining", "4999")
            .WithResponseHeader("x-ratelimit-reset", oneHourFromNowEpoch);
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(bool setAntiforgeryTokenHeader = true)
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

        if (setAntiforgeryTokenHeader)
        {
            authenticatedClient.DefaultRequestHeaders.Add(authenticatedTokens.HeaderName, authenticatedTokens.RequestToken);
        }

        return authenticatedClient;
    }

    protected void RegisterGetDependabotContent(
        RepositoryBuilder repository,
        int statusCode = StatusCodes.Status200OK)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/contents/.github/dependabot.yml")
            .Responds()
            .WithStatus(statusCode)
            .WithContent(CreateDependabotYaml)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetRepository(
        RepositoryBuilder repository,
        int statusCode = StatusCodes.Status200OK,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}")
            .Responds()
            .WithStatus(statusCode)
            .WithJsonContent(repository);

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetIssues(
        RepositoryBuilder repository,
        string creator,
        params IssueBuilder[] response)
    {
        string encodedCreator = Uri.EscapeDataString(creator);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/issues")
            .ForQuery($"creator={encodedCreator}&filter=created&state=open&labels=dependencies&sort=created&direction=desc")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetPullRequest(PullRequestBuilder pullRequest)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}")
            .Responds()
            .WithJsonContent(pullRequest)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetOrganizationRepositories(UserBuilder user, params RepositoryBuilder[] response)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/orgs/{user.Login}/repos")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUserOrganizations(params UserBuilder[] organizations)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath("/user/orgs")
            .Responds()
            .WithJsonContent(organizations)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUserRepositories(UserBuilder user, params RepositoryBuilder[] response)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/users/{user.Login}/repos")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetRepositoriesForCurrentUser(params RepositoryBuilder[] response)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath("/user/repos")
            .ForQuery("type=owner")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUser(UserBuilder user)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/users/{user.Login}")
            .Responds()
            .WithJsonContent(user)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetStatuses(
        PullRequestBuilder pullRequest,
        CombinedCommitStatusBuilder? response = null)
    {
        response ??= CreateStatuses("success");

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/commits/{pullRequest.Sha}/status")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCheckRuns(
        RepositoryBuilder repository,
        int id,
        params CheckRunBuilder[] checkRuns)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{repository.Owner.Login}/{repository.Name}/check-suites/{id}/check-runs")
            .ForQuery("filter=latest")
            .Responds()
            .WithJsonContent(CreateCheckRuns(checkRuns))
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCheckSuites(
        PullRequestBuilder pullRequest,
        CheckSuitesResponseBuilder? response = null)
    {
        response ??= CreateCheckSuites();

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/commits/{pullRequest.Sha}/check-suites")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetReviews(
        PullRequestBuilder pullRequest,
        params PullRequestReviewBuilder[] response)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}/reviews")
            .Responds()
            .WithJsonContent(response)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterPostReview(PullRequestBuilder pullRequest)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}/reviews")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { })
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterPutPullRequestMerge(PullRequestBuilder pullRequest, bool mergeable = true)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPut()
            .ForPath($"/repos/{pullRequest.Repository.Owner.Login}/{pullRequest.Repository.Name}/pulls/{pullRequest.Number}/merge")
            .Responds()
            .WithStatus(mergeable ? StatusCodes.Status200OK : StatusCodes.Status405MethodNotAllowed)
            .WithSystemTextJsonContent(new { })
            .RegisterWith(Fixture.Interceptor);
    }

    private static HttpRequestInterceptionBuilder CreateDefaultBuilder()
    {
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForHttps()
            .ForHost("api.github.com")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .ForGet()
            .Responds()
            .WithStatus(StatusCodes.Status200OK);

        return ConfigureRateLimit(builder);
    }
}
