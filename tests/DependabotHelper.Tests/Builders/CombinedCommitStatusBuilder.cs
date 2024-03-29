// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class CombinedCommitStatusBuilder(string state) : ResponseBuilder
{
    public string State { get; set; } = state;

    public IList<CommitStatusBuilder> Statuses { get; } = [];

    public override object Build()
    {
        return new
        {
            state = State,
            statuses = Statuses.Build(),
            total_count = Statuses.Count,
        };
    }
}
