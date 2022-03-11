// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

public sealed class RepositoryViewModel
{
    public long Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string HtmlUrl { get; set; } = default!;

    public IList<PullRequestViewModel> All { get; set; } = new List<PullRequestViewModel>();

    public IReadOnlyList<PullRequestViewModel> Error => All.Where((p) => p.Status == ChecksStatus.Error).ToList();

    public IReadOnlyList<PullRequestViewModel> Pending => All.Where((p) => p.Status == ChecksStatus.Pending).ToList();

    public IReadOnlyList<PullRequestViewModel> Success => All.Where((p) => p.Status == ChecksStatus.Success).ToList();

    public IReadOnlyList<PullRequestViewModel> Approved => All.Where((p) => p.IsApproved).ToList();
}
