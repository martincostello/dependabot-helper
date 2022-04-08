// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class BranchProtectionSettingsBuilder : ResponseBuilder
{
    public int RequiredApprovingReviewCount { get; set; }

    public IList<string>? RequiredStatusCheckContexts { get; set; }

    public override object Build()
    {
        return new
        {
            required_pull_request_reviews = new
            {
                required_approving_review_count = RequiredApprovingReviewCount,
            },
            required_status_checks = new
            {
                contexts = RequiredStatusCheckContexts,
            },
        };
    }
}
