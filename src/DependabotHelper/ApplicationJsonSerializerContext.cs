﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MartinCostello.DependabotHelper.Models;

namespace MartinCostello.DependabotHelper;

[ExcludeFromCodeCoverage]
[JsonSerializable(typeof(IList<Repository>))]
[JsonSerializable(typeof(MergePullRequestsResponse))]
[JsonSerializable(typeof(RepositoryPullRequests))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
internal sealed partial class ApplicationJsonSerializerContext : JsonSerializerContext
{
}
