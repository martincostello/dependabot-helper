// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MartinCostello.DependabotHelper;

public sealed class CustomHttpHeadersMiddleware
{
    private static readonly string ContentSecurityPolicyTemplate = string.Join(
        ';',
        new[]
        {
            "default-src 'self'",
            "script-src 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "script-src-elem 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com {2}",
            "style-src 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com use.fontawesome.com",
            "style-src-elem 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com use.fontawesome.com",
            "img-src 'self' avatars.githubusercontent.com",
            "font-src 'self' cdnjs.cloudflare.com use.fontawesome.com",
            "connect-src 'self' {3}",
            "media-src 'none'",
            "object-src 'none'",
            "child-src 'self'",
            "frame-ancestors 'none'",
            "form-action 'self' {1}",
            "block-all-mixed-content",
            "base-uri 'self'",
            "manifest-src 'self'",
            "upgrade-insecure-requests",
        });

    private readonly RequestDelegate _next;

    public CustomHttpHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(
        HttpContext context,
        IWebHostEnvironment environment,
        IOptions<GitHubOptions> gitHubOptions,
        IOptions<SiteOptions> siteOptions)
    {
        string nonce = GenerateNonce();
        context.SetCspNonce(nonce);

        bool renderAnalytics = !string.IsNullOrEmpty(siteOptions.Value.AnalyticsId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");

            if (environment.IsProduction())
            {
                context.Response.Headers["Content-Security-Policy"] = ContentSecurityPolicy(
                    nonce,
                    gitHubOptions.Value.EnterpriseDomain,
                    renderAnalytics);
            }

            if (context.Request.IsHttps)
            {
                context.Response.Headers["Expect-CT"] = "max-age=1800";
            }

            context.Response.Headers["Feature-Policy"] = "accelerometer 'none'; camera 'none'; geolocation 'none'; gyroscope 'none'; magnetometer 'none'; microphone 'none'; payment 'none'; usb 'none'";
            context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            context.Response.Headers["Referrer-Policy"] = "no-referrer-when-downgrade";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Download-Options"] = "noopen";

            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
            {
                context.Response.Headers.Add("X-Frame-Options", "DENY");
            }

            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string ContentSecurityPolicy(
        string nonce,
        string gitHubEnterpriseDomain,
        bool renderAnalytics)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            ContentSecurityPolicyTemplate,
            nonce,
            ParseGitHubHost(gitHubEnterpriseDomain),
            renderAnalytics ? "www.googletagmanager.com" : string.Empty,
            renderAnalytics ? "region1.google-analytics.com www.google-analytics.com" : string.Empty);
    }

    private static string ParseGitHubHost(string gitHubEnterpriseDomain)
    {
        if (Uri.TryCreate(gitHubEnterpriseDomain, UriKind.Absolute, out Uri? gitHubHost))
        {
            return gitHubHost.Host;
        }

        return "github.com";
    }

    private static string GenerateNonce()
    {
        byte[] data = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(data).Replace('+', '/');
    }
}
