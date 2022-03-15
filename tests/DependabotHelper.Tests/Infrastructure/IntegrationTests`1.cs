// Copyright (c) Martin Costello, 2022. All rights reserved.
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
    private const string AuthorizationHeader = "Token gho_secret-access-token";

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
        string owner,
        string name,
        int statusCode = StatusCodes.Status200OK)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/contents/.github/dependabot.yml")
            .Responds()
            .WithStatus(statusCode)
            .WithContent(CreateDependabotYaml)
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetRepository(
        string owner,
        string name,
        int? id = null,
        bool allowMergeCommit = true,
        bool allowRebaseMerge = true,
        int statusCode = StatusCodes.Status200OK,
        Func<RepositoryBuilder>? response = null,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        response ??= () => CreateRepository(
            owner,
            name,
            id,
            allowMergeCommit: allowMergeCommit,
            allowRebaseMerge: allowRebaseMerge);

        var builder = CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}")
            .Responds()
            .WithStatus(statusCode)
            .WithSystemTextJsonContent(response().Build());

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetIssues(
        string owner,
        string name,
        string creator,
        Func<IssueBuilder[]>? response = null)
    {
        response ??= Array.Empty<IssueBuilder>;

        string encodedCreator = Uri.EscapeDataString(creator);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/issues")
            .ForQuery($"creator={encodedCreator}&filter=created&state=open&labels=dependencies&sort=created&direction=desc")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetPullRequest(
        string owner,
        string name,
        int number,
        bool isDraft = false,
        Func<PullRequestBuilder>? response = null)
    {
        response ??= () => CreatePullRequest(owner, name, number, isDraft);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/pulls/{number}")
            .Responds()
            .WithSystemTextJsonContent(response().Build())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetOrganizationRepositories(string login, Func<RepositoryBuilder[]>? response = null)
    {
        response ??= Array.Empty<RepositoryBuilder>;

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/orgs/{login}/repos")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUserOrganizations(Func<UserBuilder[]>? response = null)
    {
        response ??= Array.Empty<UserBuilder>;

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/user/orgs")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUserRepositories(string login, Func<RepositoryBuilder[]>? response = null)
    {
        response ??= Array.Empty<RepositoryBuilder>;

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/users/{login}/repos")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetRepositoriesForCurrentUser(Func<RepositoryBuilder[]>? response = null)
    {
        response ??= Array.Empty<RepositoryBuilder>;

        CreateDefaultBuilder()
            .Requests()
            .ForPath("/user/repos")
            .ForQuery("type=owner")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUser(
        string login,
        string userType = "user",
        int? id = null,
        Func<UserBuilder>? response = null)
    {
        response ??= () => CreateUser(login, userType, id);

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/users/{login}")
            .Responds()
            .WithSystemTextJsonContent(response().Build())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetStatuses(
        string owner,
        string name,
        string reference,
        Func<CombinedCommitStatusBuilder>? response = null)
    {
        response ??= () => CreateStatuses("success");

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/commits/{reference}/status")
            .Responds()
            .WithSystemTextJsonContent(response().Build())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCheckRuns(
        string owner,
        string name,
        int id,
        params CheckRunBuilder[] checkRuns)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/check-suites/{id}/check-runs")
            .ForQuery("filter=latest")
            .Responds()
            .WithSystemTextJsonContent(CreateCheckRuns(checkRuns).Build())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCheckSuites(
        string owner,
        string name,
        string reference,
        Func<CheckSuitesResponseBuilder>? response = null)
    {
        response ??= () => CreateCheckSuites();

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/commits/{reference}/check-suites")
            .Responds()
            .WithSystemTextJsonContent(response().Build())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetReviews(
        string owner,
        string name,
        int number,
        Func<PullRequestReviewBuilder[]>? response = null)
    {
        response ??= Array.Empty<PullRequestReviewBuilder>;

        CreateDefaultBuilder()
            .Requests()
            .ForPath($"/repos/{owner}/{name}/pulls/{number}/reviews")
            .Responds()
            .WithSystemTextJsonContent(response().Select((p) => p.Build()).ToArray())
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterPostReview(string owner, string name, int number)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPost()
            .ForPath($"/repos/{owner}/{name}/pulls/{number}/reviews")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { })
            .RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterPutPullRequestMerge(string owner, string name, int number, bool mergeable = true)
    {
        CreateDefaultBuilder()
            .Requests()
            .ForPut()
            .ForPath($"/repos/{owner}/{name}/pulls/{number}/merge")
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
            .WithStatus(HttpStatusCode.OK);

        return ConfigureRateLimit(builder);
    }
}
