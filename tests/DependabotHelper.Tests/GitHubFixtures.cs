// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace MartinCostello.DependabotHelper;

public static class GitHubFixtures
{
    public static object CreateCheckSuite(string status, string? conclusion = null, string? appName = "GitHub Actions")
    {
        return new
        {
            conclusion,
            status,
            app = new
            {
                name = appName,
            },
        };
    }

    public static object CreateCheckSuites(object[]? checkSuites = null)
    {
        return new
        {
            check_suites = checkSuites ?? Array.Empty<object>(),
            total_count = checkSuites?.Length ?? 0,
        };
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
        object? pullRequest = null,
        string? title = null)
    {
        return new
        {
            html_url = $"https://github.com/{owner}/{name}/issues/{number}",
            number,
            pull_request = pullRequest,
            title,
        };
    }

    public static object CreatePullRequest(
        string owner,
        string name,
        int number,
        bool isDraft = false,
        bool? isMergeable = null,
        string? commitSha = null,
        string? title = null)
    {
        return new
        {
            html_url = $"https://github.com/{owner}/{name}/pull/{number}",
            number,
            draft = isDraft,
            mergeable = isMergeable,
            title,
            head = new
            {
                sha = commitSha ?? RandomString(),
            },
        };
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
        return new
        {
            allow_merge_commit = allowMergeCommit,
            allow_rebase_merge = allowRebaseMerge,
            fork = isFork,
            full_name = $"{owner}/{name}",
            html_url = $"https://github.com/{owner}/{name}",
            id = id ?? RandomNumber(),
            name,
            @private = isPrivate,
            visibility,
        };
    }

    public static object CreateReview(
        string login,
        string state,
        string authorAssociation = "COLLABORATOR",
        DateTimeOffset? submittedAt = null)
    {
        return new
        {
            author_association = authorAssociation,
            state,
            submitted_at = (submittedAt ?? DateTimeOffset.UtcNow).ToString("o", CultureInfo.InvariantCulture),
            user = new
            {
                login,
            },
        };
    }

    public static object CreateStatus(string state)
    {
        return new
        {
            state,
        };
    }

    public static object CreateStatuses(
        string state,
        object[]? statuses = null)
    {
        return new
        {
            state,
            statuses = statuses ?? Array.Empty<object>(),
            total_count = statuses?.Length ?? 0,
        };
    }

    public static object CreateUser(
        string login,
        string userType = "user",
        int? id = null)
    {
        id ??= RandomNumber();

        return new
        {
            avatar_url = $"https://avatars.githubusercontent.com/u/{id}?v=4",
            id,
            login,
            type = userType,
        };
    }

    public static int RandomNumber() => RandomNumberGenerator.GetInt32(int.MaxValue);

    public static string RandomString() => Guid.NewGuid().ToString();
}
