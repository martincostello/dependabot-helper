// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

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
        builder.MapGet("/github/rate-limits", async (GitHubService service) => await service.GetRateLimitsAsync())
               .RequireAuthorization();

        builder.MapGet("/github/repos/{owner}/{name}/pulls", async (string owner, string name, GitHubService service) =>
        {
            return await service.GetRepositoriesAsync();
        }).RequireAuthorization();

        builder.MapPost("/github/repos/{owner}/{name}/pulls/merge", async (string owner, string name, GitHubService service) =>
        {
            await service.MergePullRequestsAsync(owner, name);
        }).RequireAuthorization();

        return builder;
    }
}
