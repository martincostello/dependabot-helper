// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace MartinCostello.DependabotHelper.Models;

public sealed class RepositoryPullRequests : Repository
{
    public string DependabotHtmlUrl { get; set; } = string.Empty;

    public IList<string> MergeMethods { get; } = [];

    public IList<PullRequest> All { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<PullRequest> Error => [.. All.Where((p) => p.Status == ChecksStatus.Error)];

    [JsonIgnore]
    public IReadOnlyList<PullRequest> Pending => [.. All.Where((p) => p.Status == ChecksStatus.Pending)];

    [JsonIgnore]
    public IReadOnlyList<PullRequest> Success => [.. All.Where((p) => p.Status == ChecksStatus.Success)];

    [JsonIgnore]
    public IReadOnlyList<PullRequest> Approved => [.. All.Where((p) => p.IsApproved)];
}
