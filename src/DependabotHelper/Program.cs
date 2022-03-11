// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1516

using MartinCostello.DependabotHelper;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGitHubAuthentication();
builder.Services.AddGitHubClient();
builder.Services.AddMemoryCache();
builder.Services.AddRazorPages();

builder.Services.AddOptions();
builder.Services.Configure<DependabotOptions>(builder.Configuration.GetSection("Dependabot"));

if (string.Equals(builder.Configuration["CODESPACES"], "true", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<ForwardedHeadersOptions>(
        options => options.ForwardedHeaders |= ForwardedHeaders.XForwardedHost);
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

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

app.MapRazorPages();

app.Run();

namespace MartinCostello.DependabotHelper
{
    public partial class Program
    {
        // Expose the Program class for use with WebApplicationFactory<T>
    }
}
