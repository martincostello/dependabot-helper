// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using Azure.Identity;
using MartinCostello.DependabotHelper.Models;
using MartinCostello.DependabotHelper.Slices;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace MartinCostello.DependabotHelper;

public static class DependabotHelperBuilder
{
    public static WebApplicationBuilder AddDependabotHelper(this WebApplicationBuilder builder)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
        {
            ExcludeVisualStudioCredential = true,
        });

        if (builder.Configuration["ConnectionStrings:AzureKeyVault"] is { Length: > 0 })
        {
            builder.Configuration.AddAzureKeyVaultSecrets("AzureKeyVault", (p) => p.Credential = credential);
        }

        if (builder.Configuration["ConnectionStrings:AzureBlobStorage"] is { Length: > 0 })
        {
            builder.AddAzureBlobServiceClient("AzureBlobStorage", (p) => p.Credential = credential);
        }

        builder.Services.AddAuthorization();
        builder.Services.AddGitHubAuthentication(builder.Configuration);
        builder.Services.AddGitHubClient();
        builder.Services.AddHsts((options) => options.MaxAge = TimeSpan.FromDays(180));
        builder.Services.AddResponseCaching();
        builder.Services.AddTelemetry(builder.Environment);

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
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApplicationJsonSerializerContext.Default);
            options.SerializerOptions.WriteIndented = true;
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

        builder.Logging.AddTelemetry();

        builder.WebHost.ConfigureKestrel((p) => p.AddServerHeader = false);

        if (builder.Configuration["Sentry:Dsn"] is { Length: > 0 } dsn)
        {
            builder.WebHost.UseSentry(dsn);
        }

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new ApplicationLambdaSerializer());

        return builder;
    }

    public static WebApplication UseDependabotHelper(this WebApplication app)
    {
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

            if (!string.Equals(app.Configuration["ForwardedHeaders_Enabled"], bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                app.UseHttpsRedirection();
            }
        }

        app.UseResponseCompression();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAuthenticationRoutes();

        app.MapGitHubRoutes(app.Logger);

        string[] getAndHead = [HttpMethod.Get.Method, HttpMethod.Head.Method];
        string[] errorMethods = [.. getAndHead, HttpMethod.Post.Method];

        app.MapMethods("/", getAndHead, async (
            HttpContext context,
            IAntiforgery antiforgery,
            GitHubService service,
            IOptionsSnapshot<DependabotOptions> options) =>
        {
            var refreshPeriod = options.Value?.RefreshPeriod;

            try
            {
                await service.VerifyCredentialsAsync();
            }
            catch (Octokit.AuthorizationException)
            {
                await context.ReauthenticateAsync();
            }
            catch (Octokit.RateLimitExceededException)
            {
                // Ignore and let the page load
            }
            catch (Octokit.SecondaryRateLimitExceededException)
            {
                // Ignore and let the page load
            }

            antiforgery.SetCookieTokenAndHeader(context);

            return Results.Extensions.RazorSlice<Home, TimeSpan?>(refreshPeriod);
        }).RequireAuthorization();

        app.MapMethods("/configure", getAndHead, async (
            HttpContext context,
            IAntiforgery antiforgery,
            GitHubService service,
            ClaimsPrincipal user) =>
        {
            IReadOnlyList<Owner> owners = [];

            try
            {
                owners = await service.GetOwnersAsync(user);
            }
            catch (Octokit.AuthorizationException)
            {
                await context.ReauthenticateAsync();
            }
            catch (Octokit.RateLimitExceededException)
            {
                // Ignore and let the page load
            }
            catch (Octokit.SecondaryRateLimitExceededException)
            {
                // Ignore and let the page load
            }

            antiforgery.SetCookieTokenAndHeader(context);

            return Results.Extensions.RazorSlice<Configure, IReadOnlyList<Owner>>(owners);
        }).RequireAuthorization();

        app.MapMethods("/sign-in", getAndHead, (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Redirect("~/");
            }

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Extensions.RazorSlice<SignIn, AntiforgeryTokenSet>(tokens);
        }).AllowAnonymous();

        app.MapMethods("/error", errorMethods, (HttpContext context, int? id = null) =>
        {
            int statusCode = id ?? StatusCodes.Status500InternalServerError;

            if (!Enum.IsDefined(typeof(HttpStatusCode), (HttpStatusCode)statusCode) ||
                id < StatusCodes.Status400BadRequest ||
                id > 599)
            {
                statusCode = StatusCodes.Status500InternalServerError;
            }

            var requestId = Activity.Current?.Id ?? context.TraceIdentifier;

            if (context.Request.IsJson())
            {
                var detail = ReasonPhrases.GetReasonPhrase(statusCode);
                var instance = context.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath ?? context.Request.Path;
                var extensions = new Dictionary<string, object?>(1) { ["correlation"] = requestId };

                return Results.Problem(detail, instance, statusCode, extensions: extensions);
            }

            var model = new ErrorModel(statusCode)
            {
                RequestId = requestId,
                Subtitle = $"Error (HTTP {statusCode})",
            };

            switch (statusCode)
            {
                case StatusCodes.Status400BadRequest:
                    model.Title = "Bad request";
                    model.Subtitle = "Bad request (HTTP 400)";
                    model.Message = "The request was invalid.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status405MethodNotAllowed:
                    model.Title = "Method not allowed";
                    model.Subtitle = "HTTP method not allowed (HTTP 405)";
                    model.Message = "The specified HTTP method was not allowed.";
                    model.IsClientError = true;
                    break;

                case StatusCodes.Status404NotFound:
                    model.Title = "Not found";
                    model.Subtitle = "Page not found (HTTP 404)";
                    model.Message = "The page you requested could not be found.";
                    model.IsClientError = true;
                    break;

                default:
                    break;
            }

            return Results.Extensions.RazorSlice<Error, ErrorModel>(model, statusCode);
        }).AllowAnonymous()
          .DisableAntiforgery()
          .WithMetadata(new ResponseCacheAttribute() { Duration = 0, Location = ResponseCacheLocation.None, NoStore = true });

        return app;
    }
}
