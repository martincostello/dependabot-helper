﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.Json.Nodes;
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
    /// <param name="logger">The <see cref="ILogger"/> to use.</param>
    /// <returns>
    /// A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.
    /// </returns>
    public static IEndpointRouteBuilder MapGitHubRoutes(this IEndpointRouteBuilder builder, ILogger logger)
    {
        builder.MapGet("/github/repos/{owner}", async (
            string owner,
            ClaimsPrincipal user,
            GitHubService service) =>
        {
            try
            {
                return Results.Json(
                    await service.GetRepositoriesAsync(user, owner),
                    ApplicationJsonSerializerContext.Default.IListRepository);
            }
            catch (Exception ex)
            {
                return Results.Extensions.Exception(ex, logger);
            }
        }).RequireAuthorization();

        builder.MapGet("/github/repos/{owner}/{name}/pulls", async (
            string owner,
            string name,
            ClaimsPrincipal user,
            GitHubService service) =>
        {
            try
            {
                return Results.Json(
                    await service.GetPullRequestsAsync(user, owner, name),
                    ApplicationJsonSerializerContext.Default.RepositoryPullRequests);
            }
            catch (Exception ex)
            {
                return Results.Extensions.Exception(ex, logger);
            }
        }).RequireAuthorization();

        builder.MapPost("/github/repos/{owner}/{name}/pulls/merge", async (
            string owner,
            string name,
            string? mergeMethod,
            ClaimsPrincipal user,
            GitHubService service,
            IAntiforgery antiforgery,
            HttpContext httpContext) =>
        {
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                return Results.Extensions.AntiforgeryValidationFailed();
            }

            try
            {
                return Results.Json(
                    await service.MergePullRequestsAsync(user, owner, name, mergeMethod),
                    ApplicationJsonSerializerContext.Default.MergePullRequestsResponse);
            }
            catch (Exception ex)
            {
                return Results.Extensions.Exception(ex, logger);
            }
        }).RequireAuthorization();

        builder.MapPost("/github/repos/{owner}/{name}/pulls/{number:int}/approve", async (
            string owner,
            string name,
            int number,
            ClaimsPrincipal user,
            GitHubService service,
            IAntiforgery antiforgery,
            HttpContext httpContext) =>
        {
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                return Results.Extensions.AntiforgeryValidationFailed();
            }

            try
            {
                await service.ApprovePullRequestAsync(owner, name, number);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return Results.Extensions.Exception(ex, logger);
            }
        }).RequireAuthorization();

        builder.MapGet("/version", static () =>
        {
            return new JsonObject()
            {
                ["applicationVersion"] = GitMetadata.Version,
                ["frameworkDescription"] = RuntimeInformation.FrameworkDescription,
                ["operatingSystem"] = new JsonObject()
                {
                    ["description"] = RuntimeInformation.OSDescription,
                    ["architecture"] = RuntimeInformation.OSArchitecture.ToString(),
                    ["version"] = Environment.OSVersion.VersionString,
                    ["is64Bit"] = Environment.Is64BitOperatingSystem,
                },
                ["process"] = new JsonObject()
                {
                    ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                    ["is64BitProcess"] = Environment.Is64BitProcess,
                    ["isNativeAoT"] = !RuntimeFeature.IsDynamicCodeSupported,
                    ["isPrivilegedProcess"] = Environment.IsPrivilegedProcess,
                },
                ["dotnetVersions"] = new JsonObject()
                {
                    ["runtime"] = GetVersion<object>(),
                    ["aspNetCore"] = GetVersion<HttpContext>(),
                },
            };

            static string GetVersion<T>()
                => typeof(T).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        }).AllowAnonymous();

        return builder;
    }
}
