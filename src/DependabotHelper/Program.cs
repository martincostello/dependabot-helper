// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable CA1852

using System.IO.Compression;
using MartinCostello.DependabotHelper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureApplication();

builder.Services.AddGitHubAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddGitHubClient();
builder.Services.AddRazorPages();
builder.Services.AddResponseCaching();

builder.Services.AddResponseCompression((options) =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddOptions();
builder.Services.Configure<DependabotOptions>(builder.Configuration.GetSection("Dependabot"));
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));

builder.Services.ConfigureHttpJsonOptions((options) =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.Configure<StaticFileOptions>((options) =>
{
    options.OnPrepareResponse = (context) =>
    {
        var maxAge = TimeSpan.FromDays(7);

        if (context.File.Exists)
        {
            string? extension = Path.GetExtension(context.File.PhysicalPath);

            // These files are served with a content hash in the URL so can be cached for longer
            bool isScriptOrStyle =
                string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase);

            if (isScriptOrStyle)
            {
                maxAge = TimeSpan.FromDays(365);
            }
        }

        context.Context.Response.GetTypedHeaders().CacheControl = new() { MaxAge = maxAge };
    };
});

builder.Services.Configure<BrotliCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>((p) => p.Level = CompressionLevel.Fastest);

if (string.Equals(builder.Configuration["CODESPACES"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<ForwardedHeadersOptions>(
        (options) => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
}

builder.WebHost.ConfigureKestrel((p) => p.AddServerHeader = false);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseMiddleware<CustomHttpHeadersMiddleware>();
app.UseMiddleware<GitHubRateLimitMiddleware>();

app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthenticationRoutes();

app.MapGitHubRoutes(app.Logger);

app.MapRazorPages();

app.Run();

namespace MartinCostello.DependabotHelper
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
