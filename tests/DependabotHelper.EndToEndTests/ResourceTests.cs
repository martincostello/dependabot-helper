﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Mime;

namespace MartinCostello.DependabotHelper;

public class ResourceTests(AppFixture fixture, ITestOutputHelper outputHelper) : EndToEndTest(fixture, outputHelper)
{
    [Theory]
    [InlineData("/", MediaTypeNames.Text.Html)]
    [InlineData("/bad-request.html", MediaTypeNames.Text.Html)]
    [InlineData("/configure", MediaTypeNames.Text.Html)]
    [InlineData("/error.html", MediaTypeNames.Text.Html)]
    [InlineData("/favicon.png", "image/png")]
    [InlineData("/humans.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/not-found.html", MediaTypeNames.Text.Html)]
    [InlineData("/robots.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/robots933456.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/sign-in", MediaTypeNames.Text.Html)]
    [InlineData("/static/css/main.css", "text/css")]
    [InlineData("/static/css/main.css.map", MediaTypeNames.Text.Plain)]
    [InlineData("/static/js/main.js", "text/javascript")]
    [InlineData("/static/js/main.js.map", MediaTypeNames.Text.Plain)]
    [InlineData("/version", MediaTypeNames.Application.Json)]
    public async Task Can_Load_Resource_As_Get(string requestUri, string contentType)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.ShouldNotBeNull();
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe(contentType);
    }

    [Fact]
    public async Task Response_Headers_Contains_Expected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        [
            "Content-Security-Policy",
            "Cross-Origin-Embedder-Policy",
            "Cross-Origin-Opener-Policy",
            "Cross-Origin-Resource-Policy",
            "Expect-CT",
            "Permissions-Policy",
            "Referrer-Policy",
            "X-Content-Type-Options",
            "X-Download-Options",
            "X-Frame-Options",
            "X-Request-Id",
            "X-XSS-Protection",
        ];

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/", CancellationToken);

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeTrue($"The '{expected}' response header was not found.");
        }
    }

    [Fact]
    public async Task Response_Headers_Does_Not_Contain_Unexpected_Headers()
    {
        // Arrange
        string[] expectedHeaders =
        [
            "Server",
            "X-Powered-By",
        ];

        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync("/", CancellationToken);

        // Assert
        foreach (string expected in expectedHeaders)
        {
            response.Headers.Contains(expected).ShouldBeFalse($"The '{expected}' response header was found.");
        }
    }
}
