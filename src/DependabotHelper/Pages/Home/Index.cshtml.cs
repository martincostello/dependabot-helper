// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace MartinCostello.DependabotHelper.Pages;

[Authorize]
public sealed class IndexModel : PageModel
{
    public TimeSpan? RefreshPeriod { get; set; }

    public async Task OnGet(
        [FromServices] GitHubService service,
        [FromServices] IOptionsSnapshot<DependabotOptions> options)
    {
        RefreshPeriod = options.Value?.RefreshPeriod;

        try
        {
            await service.VerifyCredentialsAsync();
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
