// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

public sealed class UserCredentialStore(IHttpContextAccessor accessor) : ICredentialStore, Octokit.GraphQL.ICredentialStore
{
    public async Task<Credentials> GetCredentials()
    {
        string token = await accessor.HttpContext!.GetAccessTokenAsync();
        return new Credentials(token);
    }

    async Task<string> Octokit.GraphQL.ICredentialStore.GetCredentials(CancellationToken cancellationToken)
    {
        var credentials = await GetCredentials();
        return credentials.GetToken();
    }
}
