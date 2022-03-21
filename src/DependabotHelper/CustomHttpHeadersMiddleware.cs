// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using AspNet.Security.OAuth.GitHub;
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
            "script-src-elem 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com",
            "style-src 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com use.fontawesome.com",
            "style-src-elem 'self' 'nonce-{0}' cdn.jsdelivr.net cdnjs.cloudflare.com use.fontawesome.com",
            "img-src 'self' avatars.githubusercontent.com",
            "font-src 'self' cdnjs.cloudflare.com use.fontawesome.com",
            "connect-src 'self'",
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
        IOptions<GitHubAuthenticationOptions> gitHubOptions)
    {
        string nonce = GenerateNonce();
        context.SetCspNonce(nonce);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");

            if (environment.IsProduction())
            {
                context.Response.Headers.Add(
                    "Content-Security-Policy",
                    ContentSecurityPolicy(nonce, gitHubOptions.Value.AuthorizationEndpoint));
            }

            if (context.Request.IsHttps)
            {
                context.Response.Headers.Add("Expect-CT", "max-age=1800");
            }

            context.Response.Headers.Add("Feature-Policy", "accelerometer 'none'; camera 'none'; geolocation 'none'; gyroscope 'none'; magnetometer 'none'; microphone 'none'; payment 'none'; usb 'none'");
            context.Response.Headers.Add("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
            context.Response.Headers.Add("Referrer-Policy", "no-referrer-when-downgrade");
            context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Add("X-Download-Options", "noopen");

            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
            {
                context.Response.Headers.Add("X-Frame-Options", "DENY");
            }

            context.Response.Headers.Add("X-Request-Id", context.TraceIdentifier);
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string ContentSecurityPolicy(
        string nonce,
        string gitHubAuthorizationEndpoint)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            ContentSecurityPolicyTemplate,
            nonce,
            ParseGitHubHost(gitHubAuthorizationEndpoint));
    }

    private static string ParseGitHubHost(string gitHubAuthorizationEndpoint)
    {
        if (Uri.TryCreate(gitHubAuthorizationEndpoint, UriKind.Absolute, out Uri? gitHubHost))
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
