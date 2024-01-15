// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace MartinCostello.DependabotHelper;

public sealed class UserCredentialStore(IHttpContextAccessor accessor) : IAuthenticationProvider, Octokit.GraphQL.ICredentialStore
{
    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (!request.Headers.ContainsKey("Authorization"))
        {
            string token = await GetCredentials(cancellationToken);
            request.Headers.Add("Authorization", $"Bearer {token}");
        }
    }

    public async Task<string> GetCredentials(CancellationToken cancellationToken)
        => await accessor.HttpContext!.GetAccessTokenAsync();
}
