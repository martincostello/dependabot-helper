// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1516

using MartinCostello.DependabotHelper;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGitHubAuthentication();
builder.Services.AddGitHubClient();
builder.Services.AddRazorPages();

builder.Services.AddOptions();
builder.Services.Configure<DependabotOptions>(builder.Configuration.GetSection("Dependabot"));
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));

builder.Services.Configure<JsonOptions>((options) =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

if (string.Equals(builder.Configuration["CODESPACES"], "true", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<ForwardedHeadersOptions>(
        options => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
}

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseMiddleware<CustomHttpHeadersMiddleware>();

app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthenticationRoutes();

app.MapGitHubRoutes(app.Logger);
app.UseMiddleware<GitHubRateLimitMiddleware>();

app.MapRazorPages();

app.Run();

namespace MartinCostello.DependabotHelper
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
