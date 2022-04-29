// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

public sealed class SiteOptions
{
    public string AnalyticsId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Twitter { get; set; } = string.Empty;
}
