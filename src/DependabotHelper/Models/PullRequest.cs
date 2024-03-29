﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.DependabotHelper.Models;

public sealed class PullRequest
{
    public string HtmlUrl { get; set; } = default!;

    public bool CanApprove { get; set; }

    public bool HasConflicts { get; set; }

    public bool IsApproved { get; set; }

    public string NodeId { get; set; } = default!;

    public int Number { get; set; }

    public string RepositoryOwner { get; set; } = default!;

    public string RepositoryName { get; set; } = default!;

    [JsonConverter(typeof(JsonStringEnumConverter<ChecksStatus>))]
    public ChecksStatus Status { get; set; }

    public string Title { get; set; } = default!;
}
