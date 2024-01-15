// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using GitHub;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        GitHubClient client,
        ILogger<GitHubRateLimitMiddleware> logger)
    {
        context.Response.OnStarting(async () =>
        {
            var rateLimit = await client.Rate_limit.GetAsync(cancellationToken: context.RequestAborted);

            if (rateLimit?.Rate is { } limits)
            {
                var headers = context.Response.Headers;

                if (limits.Limit is { } limit)
                {
                    headers["x-ratelimit-limit"] = limit.ToString(CultureInfo.InvariantCulture);
                }

                if (limits.Remaining is { } remaining)
                {
                    headers["x-ratelimit-remaining"] = remaining.ToString(CultureInfo.InvariantCulture);
                }

                DateTimeOffset? resetsAt = null;

                if (limits.Reset is { } reset)
                {
                    headers["x-ratelimit-reset"] = reset.ToString(CultureInfo.InvariantCulture);
                    resetsAt = DateTimeOffset.FromUnixTimeSeconds(reset);
                }

                logger.LogInformation(
                    "GitHub API rate limit {Remaining}/{Limit}. Rate limit resets at {Reset:u}.",
                    limits.Remaining,
                    limits.Limit,
                    resetsAt);
            }
        });

        await next(context);
    }
}
