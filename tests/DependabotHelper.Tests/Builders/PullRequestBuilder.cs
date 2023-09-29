// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public sealed class PullRequestBuilder(RepositoryBuilder repository) : ResponseBuilder
{
    public string BaseRef { get; set; } = RandomString();

    public string BaseSha { get; set; } = RandomString();

    public string HeadRef { get; set; } = RandomString();

    public string HeadSha { get; set; } = RandomString();

    public bool IsDraft { get; set; }

    public bool? IsMergeable { get; set; }

    public string NodeId { get; set; } = RandomString();

    public string MergeableState { get; set; } = "clean";

    public int Number { get; set; } = RandomNumber();

    public RepositoryBuilder Repository { get; set; } = repository;

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
            mergeable_state = MergeableState,
            node_id = NodeId,
            title = Title,
            @base = new
            {
                @ref = BaseRef,
                sha = BaseSha,
            },
            head = new
            {
                @ref = HeadRef,
                sha = HeadSha,
            },
        };
    }
}
