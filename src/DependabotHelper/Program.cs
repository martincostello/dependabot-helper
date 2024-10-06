// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DependabotHelper;

var builder = WebApplication.CreateBuilder(args);

builder.AddDependabotHelper();

var app = builder.Build();

app.UseDependabotHelper();

app.Run();

// Expose the Program class for use with WebApplicationFactory<T>
namespace MartinCostello.DependabotHelper
{
    public partial class Program;
}
