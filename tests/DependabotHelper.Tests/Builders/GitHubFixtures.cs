// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DependabotHelper.Builders;

public static class GitHubFixtures
{
    public const string AuthorizationHeader = "Token gho_secret-access-token";

    public const int CurrentUserId = 1;

    public const string CurrentUserLogin = "john-smith";

    public const string DependabotBotName = "app/dependabot";

    public const string GitHubActionsBotName = "app/github-actions";

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

    public static CheckRunsResponseBuilder CreateCheckRuns(params CheckRunBuilder[] checkRuns)
    {
        var builder = new CheckRunsResponseBuilder();

        foreach (var item in checkRuns)
        {
            builder.CheckRuns.Add(item);
        }

        return builder;
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

    public static CheckSuitesResponseBuilder CreateCheckSuites(params CheckSuiteBuilder[] checkSuites)
    {
        var builder = new CheckSuitesResponseBuilder();

        foreach (var item in checkSuites)
        {
            builder.CheckSuites.Add(item);
        }

        return builder;
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

    public static PullRequestReviewBuilder CreateReview(
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

        return builder;
    }

    public static CommitStatusBuilder CreateStatus(string state) => new(state);

    public static CombinedCommitStatusBuilder CreateStatuses(
        string state,
        params CommitStatusBuilder[] statuses)
    {
        var builder = new CombinedCommitStatusBuilder(state);

        foreach (var item in statuses)
        {
            builder.Statuses.Add(item);
        }

        return builder;
    }

    public static UserBuilder CreateUser(
        string? login = null,
        int? id = null,
        string? userType = null)
    {
        UserBuilder builder = login is null ? new() : new(login);

        if (id is { } identifier)
        {
            builder.Id = identifier;
        }

        if (userType is not null)
        {
            builder.UserType = userType;
        }

        return builder;
    }
}
