// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Models;

public class Repository
{
    public long Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string HtmlUrl { get; set; } = default!;

    public bool IsFork { get; set; }

    public bool IsPrivate { get; set; }
}
