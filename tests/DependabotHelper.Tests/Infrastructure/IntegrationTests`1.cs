// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using JustEat.HttpClientInterception;
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

    protected static void ConfigureRateLimit(HttpRequestInterceptionBuilder builder)
    {
        string oneHourFromNowEpoch = DateTimeOffset.UtcNow
            .AddHours(1)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        builder.WithResponseHeader("x-ratelimit-limit", "5000")
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

    protected void RegisterGetRepository(
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

    protected void RegisterGetIssues(
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

    protected void RegisterGetPullRequest(
        string owner,
        string name,
        int number,
        bool isDraft = false,
        Func<Builders.PullRequestBuilder>? response = null)
    {
        response ??= () => CreatePullRequest(owner, name, number, isDraft);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response().Build());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetOrganizationRepositories(string login, Func<object[]>? response = null)
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

    protected void RegisterGetUserOrganizations(Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/user/orgs")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUserRepositories(string login, Func<object[]>? response = null)
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

    protected void RegisterGetRepositoriesForCurrentUser(Func<object[]>? response = null)
    {
        response ??= Array.Empty<object>;

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl("https://api.github.com/user/repos?type=owner")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetUser(
        string login,
        string userType = "user",
        int? id = null,
        Func<object>? response = null)
    {
        response ??= () => CreateUser(login, userType, id);

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

    protected void RegisterGetStatuses(
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

    protected void RegisterGetCheckRuns(
        string owner,
        string name,
        int id,
        Func<object>? response = null)
    {
        response ??= () => CreateCheckRuns();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/check-suites/{id}/check-runs?filter=latest")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    protected void RegisterGetCheckSuites(
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

    protected void RegisterGetReviews(
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

    protected void RegisterPostReview(string owner, string name, int number)
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

    protected void RegisterPutPullRequestMerge(string owner, string name, int number, bool mergeable = true)
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
