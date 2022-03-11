// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace MartinCostello.DependabotHelper;

/// <summary>
/// A class containing the HTTP endpoints and extension methods for GitHub.
/// </summary>
public static class GitHubEndpoints
{
    /// <summary>
    /// Maps the endpoints for GitHub.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>
    /// A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.
    /// </returns>
    public static IEndpointRouteBuilder MapGitHubRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("/github/is-authenticated", (ClaimsPrincipal user) => new { user.Identity?.IsAuthenticated });

        builder.MapGet("/github/rate-limits", async (GitHubService service) =>
        {
            return await service.GetRateLimitsAsync();
        }).RequireAuthorization();

        builder.MapGet("/github/repos/{owner}/{name}/pulls", async (string owner, string name, GitHubService service) =>
        {
            return await service.GetPullRequestsAsync(owner, name);
        }).RequireAuthorization();

        builder.MapPost("/github/repos/{owner}/{name}/pulls/merge", async (
            string owner,
            string name,
            GitHubService service,
            IAntiforgery antiforgery,
            HttpContext httpContext) =>
        {
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                return Results.Problem("Invalid CSRF token specified.", statusCode: StatusCodes.Status400BadRequest);
            }

            await service.MergePullRequestsAsync(owner, name);

            return Results.NoContent();
        }).RequireAuthorization();

        return builder;
    }
}
