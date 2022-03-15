// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class IssueBuilder : ResponseBuilder
{
    public IssueBuilder(RepositoryBuilder repository)
    {
        Repository = repository;
    }

    public int Number { get; set; } = RandomNumber();

    public PullRequestBuilder? PullRequest { get; set; }

    public RepositoryBuilder Repository { get; set; }

    public string Title { get; set; } = RandomString();

    public override object Build()
    {
        return new
        {
            html_url = $"https://github.com/{Repository.Owner.Login}/{Title}/issues/{Number}",
            number = Number,
            pull_request = PullRequest?.Build(),
            title = Title,
        };
    }
}
