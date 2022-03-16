// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MartinCostello.DependabotHelper.Pages;

[Authorize]
public sealed class ConfigureModel : PageModel
{
    public IReadOnlyList<Owner> Owners { get; set; } = Array.Empty<Owner>();

    public async Task OnGet([FromServices] GitHubService service)
    {
        try
        {
            Owners = await service.GetOwnersAsync(User);
        }
        catch (Octokit.AuthorizationException)
        {
            await HttpContext.ReauthenticateAsync();
        }
        catch (Octokit.RateLimitExceededException)
        {
            // Ignore and let the page load
        }
    }
}
