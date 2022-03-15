// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace MartinCostello.DependabotHelper.Builders;

public static class GitHubFixtures
{
    public static CheckRunBuilder CreateCheckRun(
        string status,
        string? conclusion = null,
        string? applicationName = null)
    {
        var builder = new CheckRunBuilder(status, conclusion);

        if (applicationName is not null)
        {
            builder.ApplicationName = applicationName;
        }

        return builder;
    }

    public static object CreateCheckRuns(params CheckRunBuilder[] checkRuns)
    {
        var builder = new CheckRunsResponseBuilder();

        foreach (var item in checkRuns)
        {
            builder.CheckRuns.Add(item);
        }

        return builder.Build();
    }

    public static CheckSuiteBuilder CreateCheckSuite(
        string status,
        string? conclusion = null,
        int? id = null,
        string? applicationName = null)
    {
        var builder = new CheckSuiteBuilder(status, conclusion);

        if (id is { } identifier)
        {
            builder.Id = identifier;
        }

        if (applicationName is not null)
        {
            builder.ApplicationName = applicationName;
        }

        return builder;
    }

    public static object CreateCheckSuites(params CheckSuiteBuilder[] checkSuites)
    {
        var builder = new CheckSuitesResponseBuilder();

        foreach (var item in checkSuites)
        {
            builder.CheckSuites.Add(item);
        }

        return builder.Build();
    }

    public static byte[] CreateDependabotYaml()
    {
        const string Yaml = @"
version: 2
updates:
- package-ecosystem: nuget
  directory: '/'
  schedule:
    interval: daily
    time: '05:30'
    timezone: Europe/London
";

        return System.Text.Encoding.UTF8.GetBytes(Yaml);
    }

    public static object CreateIssue(
        string owner,
        string name,
        int number,
        PullRequestBuilder? pullRequest = null,
        string? title = null)
    {
        var user = new UserBuilder(owner);
        var repository = new RepositoryBuilder(user, name);

        var builder = new IssueBuilder(repository)
        {
            Number = number,
            PullRequest = pullRequest,
        };

        if (title is not null)
        {
            builder.Title = title;
        }

        return builder.Build();
    }

    public static PullRequestBuilder CreatePullRequest(
        string owner,
        string name,
        int number,
        bool isDraft = false,
        bool? isMergeable = null,
        string? commitSha = null,
        string? title = null)
    {
        var user = new UserBuilder(owner);
        var repository = new RepositoryBuilder(user, name);

        var builder = new PullRequestBuilder(repository)
        {
            IsDraft = isDraft,
            Number = number,
        };

        if (isMergeable is { } mergeable)
        {
            builder.IsMergeable = mergeable;
        }

        if (commitSha is not null)
        {
            builder.Sha = commitSha;
        }

        if (title is not null)
        {
            builder.Title = title;
        }

        return builder;
    }

    public static object CreateRepository(
        string owner,
        string name,
        int? id = null,
        bool isFork = false,
        bool isPrivate = false,
        string? visibility = null,
        bool allowMergeCommit = true,
        bool allowRebaseMerge = true)
    {
        var user = new UserBuilder(owner);

        var builder = new RepositoryBuilder(user, name)
        {
            AllowMergeCommit = allowMergeCommit,
            AllowRebaseMerge = allowRebaseMerge,
            IsFork = isFork,
            IsPrivate = isPrivate,
            Visibility = visibility,
        };

        if (id is { } identifier)
        {
            builder.Id = identifier;
        }

        return builder.Build();
    }

    public static object CreateReview(
        string login,
        string state,
        string? authorAssociation = null,
        DateTimeOffset? submittedAt = null)
    {
        var user = new UserBuilder(login);
        var builder = new PullRequestReviewBuilder(user, state);

        if (authorAssociation is not null)
        {
            builder.AuthorAssociation = authorAssociation;
        }

        if (submittedAt is { } timestamp)
        {
            builder.SubmittedAt = timestamp;
        }

        return builder.Build();
    }

    public static CommitStatusBuilder CreateStatus(string state) => new(state);

    public static object CreateStatuses(
        string state,
        params CommitStatusBuilder[] statuses)
    {
        var builder = new CombinedCommitStatusBuilder(state);

        foreach (var item in statuses)
        {
            builder.Statuses.Add(item);
        }

        return builder.Build();
    }

    public static object CreateUser(
        string login,
        string? userType = null,
        int? id = null)
    {
        var builder = new UserBuilder(login);

        if (id is { } identifier)
        {
            builder.Id = identifier;
        }

        if (userType is not null)
        {
            builder.UserType = userType;
        }

        return builder.Build();
    }

    public static int RandomNumber() => RandomNumberGenerator.GetInt32(int.MaxValue);

    public static string RandomString() => Guid.NewGuid().ToString();
}
