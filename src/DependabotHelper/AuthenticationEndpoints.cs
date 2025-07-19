// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace MartinCostello.DependabotHelper;

/// <summary>
/// A class containing the HTTP endpoints and extension methods for authentication.
/// </summary>
public static class AuthenticationEndpoints
{
    private const string ApplicationName = "dependabothelper";
    private const string CookiePrefix = ".dependabothelper.";
    private const string DeniedPath = "/denied";
    private const string RootPath = "/";
    private const string SignInPath = "/sign-in";
    private const string SignOutPath = "/sign-out";

    private const string GitHubAvatarClaim = "urn:github:avatar";
    private const string GitHubProfileClaim = "urn:github:profile";

    /// <summary>
    /// Adds GitHub authentication to the application.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/>.</param>
    /// <returns>
    /// A <see cref="IServiceCollection"/> that can be used to further configure the application.
    /// </returns>
    public static IServiceCollection AddGitHubAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie((options) =>
            {
                options.AccessDeniedPath = DeniedPath;
                options.Cookie.Name = CookiePrefix + "authentication";
                options.ExpireTimeSpan = TimeSpan.FromDays(60);
                options.LoginPath = SignInPath;
                options.LogoutPath = SignOutPath;
                options.SlidingExpiration = true;
            })
            .AddGitHub();

        services
            .AddOptions<GitHubAuthenticationOptions>(GitHubAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<GitHubOptions>>((options, configuration) =>
            {
                options.AccessDeniedPath = DeniedPath;
                options.CallbackPath = SignInPath + "-github";
                options.ClientId = configuration.Value.ClientId;
                options.ClientSecret = configuration.Value.ClientSecret;
                options.CorrelationCookie.Name = CookiePrefix + "correlation";
                options.EnterpriseDomain = configuration.Value.EnterpriseDomain;
                options.SaveTokens = true;
                options.UsePkce = true;

                foreach (string scope in configuration.Value.Scopes)
                {
                    options.Scope.Add(scope);
                }

                options.ClaimActions.MapJsonKey(GitHubProfileClaim, "html_url");

                if (string.IsNullOrEmpty(options.EnterpriseDomain))
                {
                    options.ClaimActions.MapJsonKey(GitHubAvatarClaim, "avatar_url");
                }

                options.Events.OnTicketReceived = (context) =>
                {
                    var timeProvider = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();

                    context.Properties!.ExpiresUtc = timeProvider.GetUtcNow().AddDays(60);
                    context.Properties.IsPersistent = true;

                    var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
                    antiforgery.SetCookieTokenAndHeader(context.HttpContext);

                    return Task.CompletedTask;
                };
            })
            .ValidateOnStart();

        services.AddAntiforgery((options) =>
        {
            options.Cookie.Name = CookiePrefix + "antiforgery";
            options.FormFieldName = "__antiforgery";
            options.HeaderName = "x-antiforgery";
        });

        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName(ApplicationName);

        if (configuration["ConnectionStrings:AzureBlobStorage"] is { Length: > 0 })
        {
#pragma warning disable CA1308
            dataProtection.PersistKeysToAzureBlobStorage(static (provider) =>
            {
                var client = provider.GetRequiredService<BlobServiceClient>();
                var environment = provider.GetRequiredService<IHostEnvironment>();

                string containerName = "data-protection";
                string blobName = $"{environment.EnvironmentName.ToLowerInvariant()}/keys.xml";

                if (environment.IsDevelopment() && !client.GetBlobContainers().Any((p) => p.Name == containerName))
                {
                    client.CreateBlobContainer(containerName);
                }

                return client.GetBlobContainerClient(containerName).GetBlobClient(blobName);
            });
#pragma warning restore CA1308
        }

        return services;
    }

    /// <summary>
    /// Gets the user's GitHub access token as an asynchronous operation.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that returns the GitHub access token for the current user.
    /// </returns>
    public static async Task<string> GetAccessTokenAsync(this HttpContext context)
        => (await context.GetTokenAsync("access_token"))!;

    /// <summary>
    /// Gets the user's GitHub avatar URL.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>
    /// The GitHub avatar URL for the current user, if any.
    /// </returns>
    public static string GetAvatarUrl(this ClaimsPrincipal user)
        => user.FindFirst(GitHubAvatarClaim)?.Value ?? string.Empty;

    /// <summary>
    /// Gets the user's GitHub profile URL.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>
    /// The GitHub profile URL for the current user.
    /// </returns>
    public static string GetProfileUrl(this ClaimsPrincipal user)
        => user.FindFirst(GitHubProfileClaim)!.Value;

    /// <summary>
    /// Gets the user's user Id.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>
    /// The user Id for the current user.
    /// </returns>
    public static string GetUserId(this ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Gets the user's login.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>
    /// The login for the current user.
    /// </returns>
    public static string GetUserLogin(this ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Name)!.Value;

    /// <summary>
    /// Gets the user's user name.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <returns>
    /// The user name for the current user, if available; otherwise the login is returned.
    /// </returns>
    public static string GetUserName(this ClaimsPrincipal user)
        => user.FindFirst(GitHubAuthenticationConstants.Claims.Name)?.Value ?? user.GetUserLogin();

    /// <summary>
    /// Signs out the current user and challenges them to authenticate with GitHub as an asynchronous operation.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that challenges the current user to re-authenticate.
    /// </returns>
    public static async Task ReauthenticateAsync(this HttpContext context)
    {
        await context.SignOutAsync();
        await context.ChallengeAsync(GitHubAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Maps the endpoints for authentication.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>
    /// A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.
    /// </returns>
    public static IEndpointRouteBuilder MapAuthenticationRoutes(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(DeniedPath, () => Results.Redirect(SignInPath + "?denied=true"));

        builder.MapGet(SignOutPath, () => Results.Redirect(RootPath));

        builder.MapPost(SignInPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                antiforgery.SetCookieTokenAndHeader(context);
                return Results.Redirect(RootPath);
            }

            return Results.Challenge(
                new() { RedirectUri = RootPath },
                [GitHubAuthenticationDefaults.AuthenticationScheme]);
        });

        builder.MapPost(SignOutPath, async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.Redirect(RootPath);
            }

            return Results.SignOut(
                new() { RedirectUri = RootPath },
                [CookieAuthenticationDefaults.AuthenticationScheme]);
        });

        return builder;
    }
}
