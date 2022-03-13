// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

/// <summary>
/// A class containing extension methods for the <see cref="IResultExtensions"/> interface. This class cannot be inherited.
/// </summary>
internal static class ResultsExtensions
{
    /// <summary>
    /// Returns an <see cref="IResult"/> representing an exception.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/> being extended.</param>
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <returns>
    /// The <see cref="IResult"/> representing the response.
    /// </returns>
    public static IResult Exception(this IResultExtensions resultExtensions, Exception exception, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);

        if (exception is NotFoundException)
        {
            logger.LogInformation(exception, "Not found.");
            return Results.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        else if (exception is AuthorizationException)
        {
            logger.LogInformation(exception, "Unauthorized.");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized);
        }
        else if (exception is ForbiddenException)
        {
            logger.LogInformation(exception, "Forbidden.");
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
        }
        else if (exception is RateLimitExceededException)
        {
            logger.LogWarning(exception, "Rate limit exceeded.");
            return Results.Problem("Rate limit exceeded.", statusCode: StatusCodes.Status429TooManyRequests);
        }
        else
        {
            logger.LogError(exception, "Failed to handle request.");
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Returns an <see cref="IResult"/> representing an antiforgery validation failure.
    /// </summary>
    /// <param name="resultExtensions">The <see cref="IResultExtensions"/> being extended.</param>
    /// <returns>
    /// The <see cref="IResult"/> representing the response.
    /// </returns>
    public static IResult AntiforgeryValidationFailed(this IResultExtensions resultExtensions)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions);
        return Results.Problem("Invalid CSRF token specified.", statusCode: StatusCodes.Status400BadRequest);
    }
}
