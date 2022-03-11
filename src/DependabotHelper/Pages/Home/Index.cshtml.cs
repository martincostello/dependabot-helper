// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using Humanizer;
using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MartinCostello.DependabotHelper.Pages;

public class IndexModel : PageModel
{
    private readonly GitHubService _service;

    public IndexModel(GitHubService service)
    {
        _service = service;
    }

    public IList<OwnerViewModel> Owners { get; set; } = Array.Empty<OwnerViewModel>();

    public int? RateLimitRemaining { get; set; }

    public int? RateLimitTotal { get; set; }

    public string? RateLimitResets { get; set; }

    public async Task OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            Owners = await _service.GetRepositoriesAsync();

            if (_service.GetRateLimit() is { } rateLimit)
            {
                RateLimitTotal = rateLimit.Limit;
                RateLimitRemaining = rateLimit.Remaining;
                RateLimitResets = rateLimit.Reset?.Humanize();
            }
        }
    }

    public async Task<IActionResult> OnPost()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            string owner = Request.Form["Owner"];
            string name = Request.Form["Repository"];

            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            await _service.MergePullRequestsAsync(owner, name);
        }

        return RedirectToPage("./Index");
    }
}
