// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

public sealed class RateLimits
{
    public int? Remaining { get; set; }

    public int? Limit { get; set; }

    public DateTimeOffset? ResetsAt { get; set; }
}
