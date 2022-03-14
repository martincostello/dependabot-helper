// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace MartinCostello.DependabotHelper;

public static class GitHubFixtures
{
    public static object CreateIssue(
        int number,
        object? pullRequest = null,
        bool isDraft = false)
    {
        return new
        {
            number,
            draft = isDraft,
            pull_request = pullRequest,
        };
    }

    public static object CreatePullRequest(
        int number,
        bool isDraft = false)
    {
        return new
        {
            number,
            draft = isDraft,
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

    public static object CreateUser(
        string login,
        string userType = "user")
    {
        return new
        {
            login,
            type = userType,
        };
    }

    public static int RandomNumber() => RandomNumberGenerator.GetInt32(int.MaxValue);

    public static string RandomString() => Guid.NewGuid().ToString();
}
