// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class CheckSuiteBuilder : ResponseBuilder
{
    public CheckSuiteBuilder(string status, string? conclusion)
    {
        Status = status;
        Conclusion = conclusion;
    }

    public string ApplicationName { get; set; } = "GitHub Actions";

    public string? Conclusion { get; set; }

    public string Status { get; set; }

    public override object Build()
    {
        return new
        {
            id = Id,
            conclusion = Conclusion,
            status = Status,
            app = new
            {
                name = ApplicationName,
            },
        };
    }
}
