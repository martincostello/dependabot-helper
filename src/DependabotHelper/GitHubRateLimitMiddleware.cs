// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

public class GitHubRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public GitHubRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGitHubClient client)
    {
        context.Response.OnStarting(() =>
        {
            var rateLimit = client.GetLastApiInfo()?.RateLimit;

            if (rateLimit is { } limits)
            {
                string limit = limits.Limit.ToString(CultureInfo.InvariantCulture);
                string remaining = limits.Remaining.ToString(CultureInfo.InvariantCulture);
                string reset = limits.ResetAsUtcEpochSeconds.ToString(CultureInfo.InvariantCulture);

                var headers = context.Response.Headers;
                headers.Add("X-RateLimit-Limit", limit);
                headers.Add("X-RateLimit-Remaining", remaining);
                headers.Add("X-RateLimit-Reset", reset);
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
