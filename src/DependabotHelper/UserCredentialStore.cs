// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

public sealed class UserCredentialStore : ICredentialStore
{
    private readonly IHttpContextAccessor _accessor;

    public UserCredentialStore(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public async Task<Credentials> GetCredentials()
    {
        string token = await _accessor.HttpContext!.GetAccessTokenAsync();
        return new Credentials(token);
    }
}
