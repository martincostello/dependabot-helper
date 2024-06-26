﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

public sealed class DependabotOptions
{
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public bool DisableCaching { get; set; }

    public bool IncludeForks { get; set; }

    public bool IncludePrivate { get; set; }

    public IList<string> Labels { get; set; } = [];

    public IList<string> MergePreferences { get; set; } = [];

    public IList<TimeSpan> MergeRetryWaits { get; set; } = [];

    public int PageCount { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public TimeSpan? RefreshPeriod { get; set; }

    public IList<string> Users { get; set; } = [];
}
