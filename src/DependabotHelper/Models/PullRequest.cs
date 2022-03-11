// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

public sealed class PullRequest
{
    public string HtmlUrl { get; set; } = default!;

    public bool IsApproved { get; set; }

    public int Number { get; set; }

    public string RepositoryOwner { get; set; } = default!;

    public string RepositoryName { get; set; } = default!;

    public ChecksStatus Status { get; set; }
}
