// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class PullRequestReviewBuilder(UserBuilder user, string state) : ResponseBuilder
{
    public string AuthorAssociation { get; set; } = "COLLABORATOR";

    public string State { get; set; } = state;

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserBuilder User { get; set; } = user;

    public override object Build()
    {
        return new
        {
            author_association = AuthorAssociation,
            state = State,
            submitted_at = SubmittedAt.ToString("o", CultureInfo.InvariantCulture),
            user = User.Build(),
        };
    }
}
