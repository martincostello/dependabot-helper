﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MartinCostello.DependabotHelper;

public sealed class CustomHttpHeadersMiddleware(RequestDelegate next)
{
    private static readonly CompositeFormat ContentSecurityPolicyTemplate = CompositeFormat.Parse(string.Join(
        ';',
        new[]
        {
            "default-src 'self'",
            "script-src 'self' 'nonce-{0}' cdnjs.cloudflare.com",
            "script-src-elem 'self' 'nonce-{0}' cdnjs.cloudflare.com {2}",
            "style-src 'self' 'nonce-{0}' cdnjs.cloudflare.com use.fontawesome.com",
            "style-src-elem 'self' 'nonce-{0}' cdnjs.cloudflare.com use.fontawesome.com",
            "img-src 'self' data: avatars.githubusercontent.com {1} {4} {5}",
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
        }));

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
            context.Response.Headers.Remove(HeaderNames.Server);
            context.Response.Headers.Remove(HeaderNames.XPoweredBy);

            if (environment.IsProduction())
            {
                context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy(
                    nonce,
                    siteOptions.Value.CdnHost,
                    gitHubOptions.Value.EnterpriseDomain,
                    renderAnalytics);
            }

            if (context.Request.IsHttps)
            {
                context.Response.Headers["Expect-CT"] = "max-age=1800";
            }

            context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            context.Response.Headers["Referrer-Policy"] = "no-referrer-when-downgrade";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["X-Download-Options"] = "noopen";

            if (!context.Response.Headers.ContainsKey(HeaderNames.XFrameOptions))
            {
                context.Response.Headers.XFrameOptions = "DENY";
            }

            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            context.Response.Headers.XXSSProtection = "1; mode=block";

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static string ContentSecurityPolicy(
        string nonce,
        string cdnHost,
        string gitHubEnterpriseDomain,
        bool renderAnalytics)
    {
        var gitHubHost = ParseGitHubHost(gitHubEnterpriseDomain);

        return string.Format(
            CultureInfo.InvariantCulture,
            ContentSecurityPolicyTemplate,
            nonce,
            gitHubHost,
            renderAnalytics ? "www.googletagmanager.com" : string.Empty,
            renderAnalytics ? "region1.google-analytics.com www.google-analytics.com" : string.Empty,
            "avatars." + gitHubHost,
            cdnHost);
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
