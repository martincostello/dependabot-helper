// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DependabotHelper.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;
using Polly;

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

    public (int? Remaining, int? Limit, DateTimeOffset? Reset) GetRateLimit()
    {
        var appInfo = _client.GetLastApiInfo();

        if (appInfo.RateLimit is { } rateLimit)
        {
            _logger.LogInformation(
                "GitHub API rate limit {Remaining}/{Limit}. Rate limit resets at {Reset:u}.",
                rateLimit.Remaining,
                rateLimit.Limit,
                rateLimit.Reset);

            return (rateLimit.Remaining, rateLimit.Limit, rateLimit.Reset);
        }

        return default;
    }

    public async Task<IList<OwnerViewModel>> GetRepositoriesAsync()
    {
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
                    owner,
                    repository.Name,
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
            owner,
            name,
            fetchStatuses: false);

        if (mergeCandidates.Count > 0)
        {
            var policy = CreateMergePolicy();

            foreach (var pr in mergeCandidates)
            {
                try
                {
                    await policy.ExecuteAsync(
                        () => _client.PullRequest.Merge(owner, name, pr.Number, mergeRequest));
                }
                catch (ApiException ex)
                {
                    _logger.LogError(
                        ex,
                        "Could not merge pull request {Owner}/{Repository}#{Number}.",
                        pr.RepositoryOwner,
                        pr.RepositoryName,
                        pr.Number);
                }
            }
        }
    }

    private static AsyncPolicy CreateMergePolicy()
    {
        return Policy
            .Handle<PullRequestNotMergeableException>()
            .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });
    }

    private async Task<IList<PullRequestViewModel>> GetPullRequestsAsync(
        string owner,
        string name,
        bool fetchStatuses)
    {
        var result = new List<PullRequestViewModel>();

        foreach (string user in _options.Users)
        {
            var request = new RepositoryIssueRequest()
            {
                Creator = user,
                Filter = IssueFilter.Created,
                State = ItemStateFilter.Open,
            };

            foreach (string label in _options.Labels)
            {
                request.Labels.Add(label);
            }

            _logger.LogInformation(
                "Finding open issues created by {User} in repository {Owner}/{Name}.",
                user,
                owner,
                name);

            var issues = await _client.Issue.GetAllForRepository(owner, name, request);

            var openPullRequests = issues
                .Where((p) => p.PullRequest is not null)
                .ToList();

            _logger.LogInformation(
                "Found {Count} open pull requests created by {User} in repository {Owner}/{Name}.",
                openPullRequests.Count,
                user,
                owner,
                name);

            foreach (var issue in openPullRequests)
            {
                _logger.LogInformation(
                    "Fetching pull request {Number} from repository {Owner}/{Name}.",
                    issue.Number,
                    owner,
                    name);

                var pr = await _client.PullRequest.Get(owner, name, issue.Number);

                if (pr.Draft)
                {
                    _logger.LogInformation(
                        "Ignoring pull request {Number} in repository {Owner}/{Name} because it is in draft.",
                        issue.Number,
                        owner,
                        name);

                    continue;
                }

                if (!fetchStatuses && pr.Mergeable == false)
                {
                    _logger.LogInformation(
                        "Ignoring pull request {Number} in repository {Owner}/{Name} because it cannot be merged.",
                        issue.Number,
                        owner,
                        name);

                    continue;
                }

                bool isApproved = false;
                var status = ChecksStatus.Pending;

                if (fetchStatuses)
                {
                    _logger.LogInformation(
                        "Fetching approvals and statuses for pull request {Number} in repository {Owner}/{Name}.",
                        issue.Number,
                        owner,
                        name);

                    isApproved = await IsApprovedAsync(owner, name, issue.Number);
                    status = await GetChecksStatusAsync(owner, name, pr.Head.Sha);

                    _logger.LogInformation(
                        "Fetched approvals and statuses for pull request {Number} in repository {Owner}/{Name}. Approved: {Approved}; Status: {Status}.",
                        issue.Number,
                        owner,
                        name,
                        isApproved,
                        status);
                }

                result.Add(new()
                {
                    HtmlUrl = issue.HtmlUrl,
                    Number = issue.Number,
                    IsApproved = isApproved,
                    RepositoryName = name,
                    RepositoryOwner = owner,
                    Status = status,
                });
            }
        }

        return result;
    }

    private async Task<bool> IsApprovedAsync(string owner, string name, int number)
    {
        _logger.LogDebug(
            "Fetching approvals for pull request {Number} in repository {Owner}/{Name}.",
            number,
            owner,
            name);

        var approved = await _client.PullRequest.Review.GetAll(owner, name, number);

        _logger.LogDebug(
            "Found {Count} approvals for pull request {Number} in repository {Owner}/{Name}.",
            approved.Count,
            number,
            owner,
            name);

        return approved.Any() && approved.All((p) => p.State != PullRequestReviewState.ChangesRequested);
    }

    private async Task<ChecksStatus> GetChecksStatusAsync(string owner, string name, string commitSha)
    {
        ChecksStatus? status = null;

        _logger.LogDebug(
            "Fetching combined status for commit {Reference} in repository {Owner}/{Name}.",
            commitSha,
            owner,
            name);

        var combinedCommitStatus = await _client.Repository.Status.GetCombined(owner, name, commitSha);

        _logger.LogDebug(
            "Found {Count} statuses for commit {Reference} in repository {Owner}/{Name}.",
            combinedCommitStatus.TotalCount,
            commitSha,
            owner,
            name);

        if (combinedCommitStatus.TotalCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                foreach (var commitStatus in combinedCommitStatus.Statuses)
                {
                    _logger.LogTrace(
                        "Commit status: Context: {Context}; State: {State}",
                        commitStatus.Context,
                        commitStatus.State);
                }
            }

            status = combinedCommitStatus.State.Value switch
            {
                CommitState.Error or CommitState.Failure => ChecksStatus.Error,
                CommitState.Success => ChecksStatus.Success,
                _ => ChecksStatus.Pending,
            };
        }

        _logger.LogDebug(
            "Fetching check suites for commit {Reference} in repository {Owner}/{Name}.",
            commitSha,
            owner,
            name);

        var checkSuitesResponse = await _client.Check.Suite.GetAllForReference(owner, name, commitSha);

        _logger.LogDebug(
            "Found {Count} check suites for commit {Reference} in repository {Owner}/{Name}.",
            checkSuitesResponse.TotalCount,
            commitSha,
            owner,
            name);

        // No need to query the check suites if we already know the status is failed
        if (checkSuitesResponse.TotalCount > 0 && status != ChecksStatus.Error)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                foreach (var checkSuite in checkSuitesResponse.CheckSuites)
                {
                    _logger.LogTrace(
                        "Check suite {Name}: Status: {Status}; Conclusion: {Conclusion}",
                        checkSuite.App.Name,
                        checkSuite.Status,
                        checkSuite.Conclusion);
                }
            }

            static bool IsError(CheckSuite suite)
                => suite.Conclusion == CheckConclusion.ActionRequired ||
                   suite.Conclusion == CheckConclusion.Cancelled ||
                   suite.Conclusion == CheckConclusion.Failure ||
                   suite.Conclusion == CheckConclusion.TimedOut;

            // If a check has not run at all consider it successful as it
            // might not be required to run at all (e.g. an old installation)
            // as it would otherwise block the Pull Request from being successful.
            static bool IsSuccess(CheckSuite suite)
                => suite.Conclusion is null ||
                   suite.Conclusion == CheckConclusion.Success ||
                   suite.Conclusion == CheckConclusion.Neutral;

            if (checkSuitesResponse.CheckSuites.All(IsSuccess))
            {
                // Success can only be reported if there are no existing
                // commit statuses or there are no pending commit statuses.
                status = status switch
                {
                    ChecksStatus.Error => ChecksStatus.Error,
                    ChecksStatus.Pending => ChecksStatus.Pending,
                    _ => ChecksStatus.Success,
                };
            }
            else if (checkSuitesResponse.CheckSuites.Any(IsError))
            {
                status = ChecksStatus.Error;
            }
        }

        return status ?? ChecksStatus.Pending;
    }

    private async Task<IReadOnlyList<Repository>> GetRepositoriesAsync(
        string owner,
        ICollection<string> repositories)
    {
        var repos = new List<Repository>();

        _logger.LogInformation("Fetching {Count} repositories for owner {Owner}.", repositories.Count, owner);

        foreach (string repository in repositories)
        {
            repos.Add(await GetRepositoryAsync(owner, repository));
        }

        _logger.LogInformation("Fetched {Count} repositories for owner {Owner}.", repositories.Count, owner);

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
