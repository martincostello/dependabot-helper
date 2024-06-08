// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

public static class HttpContextExtensions
{
    private const string CspNonceKey = "x-csp-nonce";

    public static string? Content(this HttpContext context, string? contentPath, bool appendVersion = true)
    {
        string? result = string.Empty;

        if (!string.IsNullOrEmpty(contentPath))
        {
            if (contentPath[0] == '~')
            {
                var segment = new PathString(contentPath[1..]);
                var applicationPath = context.Request.PathBase;

                var path = applicationPath.Add(segment);
                result = path.Value;
            }
            else
            {
                result = contentPath;
            }
        }

        if (appendVersion)
        {
            result += $"?v={GitMetadata.Commit}";
        }

        return result;
    }

    public static string? RouteUrl(this HttpContext context, string? path)
        => Content(context, path, appendVersion: false);

    public static string? GetCspNonce(this HttpContext context)
    {
        string? nonce = null;

        if (context.Items.TryGetValue(CspNonceKey, out object? value))
        {
            nonce = value as string;
        }

        return nonce;
    }

    public static void SetCspNonce(this HttpContext context, string? value)
    {
        context.Items[CspNonceKey] = value;
    }
}
