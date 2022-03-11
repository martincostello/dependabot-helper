// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

public sealed class DependabotOptions
{
    public IList<string> Labels { get; set; } = new List<string>();

    public IList<string> Users { get; set; } = new List<string>();
}
