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
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();
        int number = RandomNumberGenerator.GetInt32(int.MaxValue);

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForPost()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}/pulls/{number}/reviews")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status201Created)
            .WithSystemTextJsonContent(new { });

        builder.RegisterWith(Fixture.Interceptor);

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync($"/github/repos/{owner}/{name}/pulls/{number}/approve", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cannot_Get_Repository_That_Does_Not_Exist()
    {
        // Arrange
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status404NotFound)
            .WithSystemTextJsonContent(new { });

        builder.RegisterWith(Fixture.Interceptor);

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
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status401Unauthorized)
            .WithSystemTextJsonContent(new { });

        builder.RegisterWith(Fixture.Interceptor);

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
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status403Forbidden)
            .WithSystemTextJsonContent(new { });

        builder.RegisterWith(Fixture.Interceptor);

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
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status403Forbidden)
            .WithSystemTextJsonContent(new { message = "API rate limit exceeded" })
            .WithResponseHeader("x-ratelimit-limit", "60")
            .WithResponseHeader("x-ratelimit-remaining", "0")
            .WithResponseHeader("x-ratelimit-reset", "1377013266");

        builder.RegisterWith(Fixture.Interceptor);

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
        string owner = Guid.NewGuid().ToString();
        string name = Guid.NewGuid().ToString();

        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl($"https://api.github.com/repos/{owner}/{name}")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status500InternalServerError)
            .WithSystemTextJsonContent(new { message = "Whoops" });

        builder.RegisterWith(Fixture.Interceptor);

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
}
