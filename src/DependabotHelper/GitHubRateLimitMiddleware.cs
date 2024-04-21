// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IGitHubClient client,
        ILogger<GitHubRateLimitMiddleware> logger)
    {
        context.Response.OnStarting(() =>
        {
            var rateLimit = client.GetLastApiInfo()?.RateLimit;

            if (rateLimit is { } limits)
            {
                logger.LogDebug(
                    "GitHub API rate limit {Remaining}/{Limit}. Rate limit resets at {Reset:u}.",
                    limits.Remaining,
                    limits.Limit,
                    limits.Reset);

                string limit = limits.Limit.ToString(CultureInfo.InvariantCulture);
                string remaining = limits.Remaining.ToString(CultureInfo.InvariantCulture);
                string reset = limits.ResetAsUtcEpochSeconds.ToString(CultureInfo.InvariantCulture);

                var headers = context.Response.Headers;
                headers["x-ratelimit-limit"] = limit;
                headers["x-ratelimit-remaining"] = remaining;
                headers["x-ratelimit-reset"] = reset;
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
