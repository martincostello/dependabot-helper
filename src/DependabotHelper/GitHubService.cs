// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DependabotHelper.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubService
{
    private readonly IMemoryCache _cache;
    private readonly IGitHubClient _client;
    private readonly ILogger _logger;
    private readonly DependabotOptions _options;

    public GitHubService(
        IGitHubClient client,
        IMemoryCache cache,
        IOptionsSnapshot<DependabotOptions> options,
        ILogger<GitHubService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public (int? Remaining, int? Limit, DateTimeOffset? Reset) GetRateLimits()
    {
        var appInfo = _client.GetLastApiInfo();

        if (appInfo.RateLimit is { } rateLimit)
        {
            return (rateLimit.Remaining, rateLimit.Limit, rateLimit.Reset);
        }

        return default;
    }

    public async Task<IList<OwnerViewModel>> GetRepositoriesAsync()
    {
        var labels = _options.Labels;

        var result = new List<OwnerViewModel>();

        foreach (var (owner, names) in _options.Repositories)
        {
            var repositories = await GetRepositoriesAsync(owner, names);

            if (repositories.Count < 1)
            {
                continue;
            }

            var ownerModel = new OwnerViewModel()
            {
                Name = repositories[0].Owner.Login,
            };

            foreach (var repository in repositories)
            {
                var repoModel = new RepositoryViewModel()
                {
                    Id = repository.Id,
                    HtmlUrl = repository.HtmlUrl + "/pulls",
                    Name = repository.Name,
                };

                repoModel.All = await GetPullRequestsAsync(
                    (owner, repository.Name),
                    labels,
                    _options.Users,
                    fetchStatuses: true);

                ownerModel.Repositories.Add(repoModel);
            }

            result.Add(ownerModel);
        }

        return result;
    }

    public async Task MergePullRequestsAsync(string owner, string name)
    {
        var mergeRequest = new MergePullRequest()
        {
            MergeMethod = PullRequestMergeMethod.Merge,
        };

        var repository = await GetRepositoryAsync(owner, name);

        if (repository.AllowMergeCommit == false)
        {
            mergeRequest.MergeMethod = repository.AllowRebaseMerge switch
            {
                true => PullRequestMergeMethod.Rebase,
                _ => PullRequestMergeMethod.Squash,
            };
        }

        var mergeCandidates = await GetPullRequestsAsync(
            (owner, name),
            _options.Labels,
            _options.Users,
            fetchStatuses: false);

        foreach (var pr in mergeCandidates)
        {
            try
            {
                await _client.PullRequest.Merge(owner, name, pr.Number, mergeRequest);
            }
            catch (ApiException ex)
            {
                _logger.LogError(
                    ex,
                    "Could not merge pull request {Owner}/{Repository}#{Number}",
                    pr.RepositoryOwner,
                    pr.RepositoryName,
                    pr.Number);
            }
        }
    }

    private async Task<IList<PullRequestViewModel>> GetPullRequestsAsync(
        (string Owner, string Name) repo,
        IEnumerable<string> labels,
        IEnumerable<string> users,
        bool fetchStatuses)
    {
        var result = new List<PullRequestViewModel>();

        foreach (string user in users)
        {
            var issuesRequest = new RepositoryIssueRequest()
            {
                Creator = user,
                Filter = IssueFilter.All,
                State = ItemStateFilter.Open,
            };

            foreach (string label in labels)
            {
                issuesRequest.Labels.Add(label);
            }

            var issues = await _client.Issue.GetAllForRepository(repo.Owner, repo.Name, issuesRequest);

            var openPullRequests = issues.Where((p) => p.PullRequest is not null);

            foreach (var issue in openPullRequests)
            {
                var pr = await _client.PullRequest.Get(repo.Owner, repo.Name, issue.Number);

                if (pr.Draft || pr.Mergeable == false)
                {
                    continue;
                }

                bool isApproved = false;
                var status = ChecksStatus.Pending;

                if (fetchStatuses)
                {
                    var approved = await _client.PullRequest.Review.GetAll(repo.Owner, repo.Name, issue.Number);
                    isApproved = approved.Any() && approved.All((p) => p.State != PullRequestReviewState.ChangesRequested);
                    var commitStatus = await _client.Repository.Status.GetCombined(repo.Owner, repo.Name, pr.Head.Sha);

                    if (commitStatus.TotalCount > 0)
                    {
                        status = commitStatus.State.Value switch
                        {
                            CommitState.Error or CommitState.Failure => ChecksStatus.Error,
                            CommitState.Success => ChecksStatus.Success,
                            _ => ChecksStatus.Pending,
                        };
                    }
                    else
                    {
                        var checks = await _client.Check.Suite.GetAllForReference(repo.Owner, repo.Name, pr.Head.Sha);

                        if (checks.TotalCount > 0)
                        {
                            static bool Success(CheckSuite suite)
                                => suite.Conclusion is null ||
                                   suite.Conclusion == CheckConclusion.Success ||
                                   suite.Conclusion == CheckConclusion.Neutral;

                            static bool Error(CheckSuite suite)
                                => suite.Conclusion == CheckConclusion.ActionRequired ||
                                   suite.Conclusion == CheckConclusion.Cancelled ||
                                   suite.Conclusion == CheckConclusion.Failure ||
                                   suite.Conclusion == CheckConclusion.TimedOut;

                            if (checks.CheckSuites.All(Success))
                            {
                                status = ChecksStatus.Success;
                            }
                            else if (checks.CheckSuites.Any(Error))
                            {
                                status = ChecksStatus.Error;
                            }
                        }
                    }
                }

                result.Add(new()
                {
                    HtmlUrl = issue.HtmlUrl,
                    Number = issue.Number,
                    IsApproved = isApproved,
                    RepositoryName = repo.Name,
                    RepositoryOwner = repo.Owner,
                    Status = status,
                });
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<Repository>> GetRepositoriesAsync(
        string owner,
        IEnumerable<string> repositories)
    {
        var repos = new List<Repository>();

        foreach (string repository in repositories)
        {
            repos.Add(await GetRepositoryAsync(owner, repository));
        }

        return repos.OrderBy((p) => p.Name).ToList();
    }

    private async Task<Repository> GetRepositoryAsync(string owner, string name)
    {
        return await _cache.GetOrCreateAsync($"repo:{owner}/{name}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _client.Repository.Get(owner, name);
        });
    }
}
