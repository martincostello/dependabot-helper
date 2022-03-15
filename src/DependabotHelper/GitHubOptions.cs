﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper;

public sealed class GitHubOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string EnterpriseDomain { get; set; } = string.Empty;

    public IList<string> Scopes { get; set; } = new List<string>();
}
