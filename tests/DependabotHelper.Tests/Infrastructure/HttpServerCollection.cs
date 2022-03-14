﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Infrastructure;

[CollectionDefinition(Name)]
public sealed class HttpServerCollection : ICollectionFixture<HttpServerFixture>
{
    public const string Name = "DependabotHelper HTTP server collection";
}
