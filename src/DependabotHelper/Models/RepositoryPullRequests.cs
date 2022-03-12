// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

public sealed class RepositoryPullRequests : Repository
{
    public IList<PullRequest> All { get; set; } = new List<PullRequest>();

    public IReadOnlyList<PullRequest> Error => All.Where((p) => p.Status == ChecksStatus.Error).ToList();

    public IReadOnlyList<PullRequest> Pending => All.Where((p) => p.Status == ChecksStatus.Pending).ToList();

    public IReadOnlyList<PullRequest> Success => All.Where((p) => p.Status == ChecksStatus.Success).ToList();

    public IReadOnlyList<PullRequest> Approved => All.Where((p) => p.IsApproved).ToList();
}
