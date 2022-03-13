// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using JustEat.HttpClientInterception;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync($"/github/repos/{owner}/{name}/pulls/{number}/approve", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Can_Get_Rate_Limits()
    {
        // Arrange
        var utcNow = DateTimeOffset.UtcNow;

        long oneHourFromNowEpoch = utcNow.AddHours(1).ToUnixTimeSeconds();
        string oneHourFromNowEpochString = oneHourFromNowEpoch.ToString(CultureInfo.InvariantCulture);

        RegisterGetRateLimit(
            response: () => new
            {
                resources = new
                {
                    core = new
                    {
                        limit = 5000,
                        remaining = 4999,
                        reset = oneHourFromNowEpoch,
                        used = 1,
                        resource = "core",
                    },
                },
                rate = new
                {
                    limit = 5000,
                    remaining = 4999,
                    reset = oneHourFromNowEpoch,
                    used = 1,
                    resource = "core",
                },
            },
            configure: (builder) =>
            {
                builder.WithResponseHeader("X-RateLimit-Limit", "5000")
                       .WithResponseHeader("X-RateLimit-Remaining", "4999")
                       .WithResponseHeader("X-RateLimit-Reset", oneHourFromNowEpochString);
            });

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<Models.RateLimits>($"/github/rate-limits");

        // Assert
        actual.ShouldNotBeNull();
        actual.Limit.ShouldBe(5000);
        actual.Remaining.ShouldBe(4999);
        actual.Resets.ShouldNotBeNull();
        actual.Resets.Value.ShouldBe(utcNow.AddHours(1), TimeSpan.FromSeconds(2));
        actual.ResetsText.ShouldBe("59 minutes from now");
    }

    [Fact]
    public async Task Can_Get_Repositories_If_No_Repositories()
    {
        // Arrange
        string owner = RandomString();

        RegisterGetUser(owner);
        RegisterGetUserRepositories(owner);

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Models.Repository>>($"/github/repos/{owner}");

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
            response: () => new[]
            {
                new
                {
                    id,
                    name,
                    fork = false,
                    @private = false,
                    visibility = "internal",
                    html_url = $"https://github.com/{owner}/{name}",
                },
            });

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Models.Repository>>($"/github/repos/{owner}");

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
            response: () => new[]
            {
                new
                {
                    id,
                    name,
                    fork = true,
                    @private = true,
                    html_url = $"https://github.com/{owner}/{name}",
                },
            });

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Models.Repository>>($"/github/repos/{owner}");

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(1);

        var repository = actual[0];

        repository.HtmlUrl.ShouldBe($"https://github.com/{owner}/{name}");
        repository.Id.ShouldBe(id);
        repository.IsFork.ShouldBeTrue();
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
            response: () => new[]
            {
                new
                {
                    id,
                    name,
                    fork = false,
                    @private = false,
                    html_url = $"https://github.com/{owner}/{name}",
                },
            });

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var actual = await client.GetFromJsonAsync<IList<Models.Repository>>($"/github/repos/{owner}");

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
    [InlineData(false)]
    [InlineData(true)]
    public async Task Can_Merge_Pull_Requests(bool allowMergeCommit)
    {
        // Arrange
        string owner = RandomString();
        string name = RandomString();

        int pullRequest1 = RandomNumber();
        int pullRequest2 = RandomNumber();
        int pullRequest3 = RandomNumber();

        RegisterGetRepository(owner, name, allowMergeCommit);
        RegisterGetPullRequest(owner, name, pullRequest1);
        RegisterGetPullRequest(owner, name, pullRequest2);
        RegisterPutPullRequestMerge(owner, name, pullRequest1, mergeable: true);
        RegisterPutPullRequestMerge(owner, name, pullRequest2, mergeable: false);

        RegisterGetIssues(
            owner,
            name,
            "app/dependabot",
            () => new[]
            {
                new
                {
                    number = pullRequest1,
                    draft = false,
                    pull_request = new
                    {
                    },
                },
                new
                {
                    number = pullRequest2,
                    draft = true,
                    pull_request = new
                    {
                    },
                },
            });

        RegisterGetIssues(
            owner,
            name,
            "app/github-actions",
            () => new[]
            {
                new
                {
                    number = pullRequest3,
                    draft = true,
                },
            });

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync($"/github/repos/{owner}/{name}/pulls/merge", new { });

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

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

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync($"/github/repos/{owner}/{name}/pulls");

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

    public void Dispose()
    {
        _scope?.Dispose();
    }

    private static int RandomNumber() => RandomNumberGenerator.GetInt32(int.MaxValue);

    private static string RandomString() => Guid.NewGuid().ToString();

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

    private void RegisterGetRateLimit(
        Func<object>? response = null,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        response ??= () => new
        {
            resources = new
            {
                core = new
                {
                    limit = 5000,
                    remaining = 4999,
                    reset = 1377013266,
                    used = 1,
                    resource = "core",
                },
            },
        };

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl("https://api.github.com/rate_limit")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        configure?.Invoke(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetRepository(
        string owner,
        string name,
        bool allowMergeCommit = true,
        int statusCode = StatusCodes.Status200OK,
        Func<object>? response = null,
        Action<HttpRequestInterceptionBuilder>? configure = null)
    {
        response ??= () => new
        {
            name,
            full_name = $"{owner}/{name}",
            allow_merge_commit = allowMergeCommit,
        };

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
        Func<object>? response = null)
    {
        response ??= () => Array.Empty<object>();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/issues?creator={Uri.EscapeDataString(creator)}&filter=created&state=open&labels=dependencies&sort=created&direction=desc")
            .ForRequestHeader("Authorization", AuthorizationHeader)
            .Responds()
            .WithStatus(StatusCodes.Status200OK)
            .WithSystemTextJsonContent(response());

        ConfigureRateLimit(builder);

        builder.RegisterWith(Fixture.Interceptor);
    }

    private void RegisterGetPullRequest(string owner, string name, int number, Func<object>? response = null)
    {
        response ??= () => new { number };

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

    private void RegisterGetOrganizationRepositories(string login, Func<object>? response = null)
    {
        response ??= () => Array.Empty<object>();

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

    private void RegisterGetUserRepositories(string login, Func<object>? response = null)
    {
        response ??= () => Array.Empty<object>();

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

    private void RegisterGetRepositoriesForCurrentUser(Func<object>? response = null)
    {
        response ??= () => Array.Empty<object>();

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
        response ??= () => new
        {
            login,
            type = userType,
        };

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
