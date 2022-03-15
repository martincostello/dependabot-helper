// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class PullRequestBuilder : ResponseBuilder
{
    public PullRequestBuilder(RepositoryBuilder repository)
    {
        Repository = repository;
    }

    public bool IsDraft { get; set; }

    public bool IsMergeable { get; set; }

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; }

    public string Sha { get; set; } = RandomString();

    public string Title { get; set; } = RandomString();

    public IssueBuilder CreateIssue() => new(Repository)
    {
        Number = Number,
        PullRequest = this,
        Title = Title,
    };

    public override object Build()
    {
        return new
        {
            html_url = $"https://github.com/{Repository.Owner.Login}/{Repository.Name}/pull/{Number}",
            number = Number,
            draft = IsDraft,
            mergeable = IsMergeable,
            title = Title,
            head = new
            {
                sha = Sha,
            },
        };
    }
}
