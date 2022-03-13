// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
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
    }

    private AppFixture Fixture { get; }

    private ITestOutputHelper OutputHelper { get; }

    [Fact]
    public async Task Cannot_Get_Repo_That_Does_Not_Exist()
    {
        // Arrange
        var builder = new HttpRequestInterceptionBuilder()
            .Requests()
            .ForGet()
            .ForUrl("https://api.github.com/repos/foo/bar")
            .ForRequestHeader("Authorization", "Token gho_secret-access-token")
            .Responds()
            .WithStatus(StatusCodes.Status404NotFound)
            .WithSystemTextJsonContent(new { });

        builder.RegisterWith(Fixture.Interceptor);

        var client = await CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/github/repos/foo/bar/pulls");

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

    public void Dispose()
    {
        _scope?.Dispose();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        AntiforgeryTokens tokens = await Fixture.GetAntiforgeryTokensAsync();

        var redirectHandler = new RedirectHandler(Fixture.ClientOptions.MaxAutomaticRedirections);

        var cookieHandler = new CookieContainerHandler();
        cookieHandler.Container.Add(
            Fixture.Server.BaseAddress,
            new Cookie(tokens.CookieName, tokens.CookieValue));

        var client = Fixture.CreateDefaultClient(redirectHandler, cookieHandler);

        client.DefaultRequestHeaders.Add(tokens.HeaderName, tokens.RequestToken);

        var parameters = Array.Empty<KeyValuePair<string?, string?>>();
        using var content = new FormUrlEncodedContent(parameters);

        using var response = await client.PostAsync("/sign-in", content);
        response.IsSuccessStatusCode.ShouldBeTrue();

        tokens = await Fixture.GetAntiforgeryTokensAsync();

        return client;
    }
}
