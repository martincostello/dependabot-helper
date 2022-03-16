// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Mime;

namespace MartinCostello.DependabotHelper;

public class ResourceTests : EndToEndTest
{
    public ResourceTests(AppFixture fixture, ITestOutputHelper outputHelper)
        : base(fixture, outputHelper)
    {
    }

    [SkippableTheory]
    [InlineData("/", MediaTypeNames.Text.Html)]
    [InlineData("/configure", MediaTypeNames.Text.Html)]
    [InlineData("/sign-in", MediaTypeNames.Text.Html)]
    [InlineData("/css/site.css", "text/css")]
    [InlineData("/static/js/main.js", "application/javascript")]
    [InlineData("/static/js/main.js.map", MediaTypeNames.Text.Plain)]
    [InlineData("/favicon.png", "image/png")]
    [InlineData("/humans.txt", MediaTypeNames.Text.Plain)]
    [InlineData("/manifest.webmanifest", "application/manifest+json")]
    [InlineData("/robots.txt", MediaTypeNames.Text.Plain)]
    public async Task Can_Load_Resource_As_Get(string requestUri, string contentType)
    {
        // Arrange
        using var client = Fixture.CreateClient();

        // Act
        using var response = await client.GetAsync(requestUri);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.ShouldNotBeNull();
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe(contentType);
    }
}
