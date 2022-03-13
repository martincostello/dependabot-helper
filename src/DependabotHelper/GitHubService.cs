// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using Humanizer;
using MartinCostello.DependabotHelper.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;
using Polly;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly IGitHubClient _client;
    private readonly ILogger _logger;
    private readonly DependabotOptions _options;
    private readonly GitHubRateLimitsAccessor _rateLimitsAccessor;

    public GitHubService(
        IGitHubClient client,
        IMemoryCache cache,
        GitHubRateLimitsAccessor rateLimitsAccessor,
        IOptionsSnapshot<DependabotOptions> options,
        ILogger<GitHubService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _rateLimitsAccessor = rateLimitsAccessor;
    }

    public static string ApplyMaximumAvatarSize(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            url += "&size=32";
        }

        return url;
    }

    public async Task ApprovePullRequestAsync(string owner, string name, int number)
    {
        _logger.LogInformation(
            "Approving pull request {Owner}/{Repository}#{Number}.",
            owner,
            name,
            number);

        var review = new PullRequestReviewCreate()
        {
            Event = PullRequestReviewEvent.Approve,
        };

        await _client.PullRequest.Review.Create(owner, name, number, review);

        _logger.LogInformation(
            "Pull request {Owner}/{Repository}#{Number} approved.",
            owner,
            name,
            number);
    }

    public async Task<IReadOnlyList<Owner>> GetOwnersAsync(ClaimsPrincipal user)
    {
        string id = user.GetUserId();
        string login = user.GetUserLogin();

        _logger.LogInformation(
            "Fetching organizations for user {Login}.",
            login);

        var organizations = await CacheGetOrCreateAsync(user, $"orgs:{id}", async () =>
        {
            return await _client.Organization.GetAllForCurrent();
        });

        _logger.LogInformation(
            "Found {Count} organizations user {Login} has access to.",
            organizations.Count,
            login);

        var owners = new List<Owner>(organizations.Count + 1);

        foreach (var organization in organizations.OrderBy((p) => p.Login, StringComparer.OrdinalIgnoreCase))
        {
            owners.Add(new()
            {
                AvatarUrl = ApplyMaximumAvatarSize(organization.AvatarUrl),
                Name = organization.Login,
            });
        }

        // Always list the user themselves first
        owners.Insert(0, new()
        {
            AvatarUrl = user.GetAvatarUrl(),
            Name = login,
        });

        return owners;
    }

    public async Task<RateLimits> GetRateLimitsAsync()
    {
        var rateLimit = _client.GetLastApiInfo()?.RateLimit;

        if (rateLimit is null)
        {
            try
            {
                // Force an API request to get the rate limits
                var response = await _client.Miscellaneous.GetRateLimits();
                rateLimit = response.Rate;
            }
            catch (ApiException)
            {
                // Ignore
            }
        }

        var result = new RateLimits();

        if (rateLimit is not null)
        {
            _logger.LogInformation(
                "GitHub API rate limit {Remaining}/{Limit}. Rate limit resets at {Reset:u}.",
                rateLimit.Remaining,
                rateLimit.Limit,
                rateLimit.Reset);

            result.Limit = rateLimit.Limit;
            result.Remaining = rateLimit.Remaining;
            result.Resets = rateLimit.Reset;
            result.ResetsText = result.Resets.Humanize();
        }

        _rateLimitsAccessor.Current = result;

        return result;
    }

    public async Task<RepositoryPullRequests> GetPullRequestsAsync(ClaimsPrincipal user, string owner, string name)
    {
        var repository = await GetRepositoryAsync(user, owner, name);

        var result = new RepositoryPullRequests()
        {
            HtmlUrl = repository.HtmlUrl + "/pulls",
            Id = repository.Id,
            IsFork = repository.Fork,
            IsPrivate = repository.Private || repository.Visibility != RepositoryVisibility.Public,
            Name = repository.Name,
        };

        if (await IsDependabotEnabledAsync(user, owner, name))
        {
            result.DependabotHtmlUrl = repository.HtmlUrl + "/network/updates";
        }

        result.All = await GetPullRequestsAsync(
            owner,
            repository.Name,
            fetchStatuses: true);

        return result;
    }

    public async Task<IList<Models.Repository>> GetRepositoriesAsync(ClaimsPrincipal user, string owner)
    {
        _logger.LogInformation("Fetching repositories for owner {Owner}.", owner);

        var repositories = await CacheGetOrCreateAsync(user, $"repos:{owner}", async () =>
        {
            var ownerUser = await GetUserAsync(user, owner);

            IReadOnlyList<Octokit.Repository> repos;

            if (ownerUser.Type == AccountType.Organization)
            {
                repos = await _client.Repository.GetAllForOrg(owner);
            }
            else
            {
                var current = await _client.User.Current();

                if (current.Login == ownerUser.Login)
                {
                    repos = await _client.Repository.GetAllForCurrent();
                }
                else
                {
                    repos = await _client.Repository.GetAllForUser(owner);
                }
            }

            return repos;
        });

        _logger.LogInformation("Fetched {Count} repositories for owner {Owner}.", repositories.Count, owner);

        return repositories
            .Where((p) => !p.Archived)
            .Where((p) => _options.IncludeForks || !p.Fork)
            .Select((p) =>
            {
                return new Models.Repository()
                {
                    HtmlUrl = p.HtmlUrl,
                    Id = p.Id,
                    IsFork = p.Fork,
                    IsPrivate = p.Private || p.Visibility != RepositoryVisibility.Public,
                    Name = p.Name,
                };
            })
            .OrderBy((p) => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task MergePullRequestsAsync(ClaimsPrincipal user, string owner, string name)
    {
        var mergeRequest = new MergePullRequest()
        {
            MergeMethod = PullRequestMergeMethod.Merge,
        };

        var repository = await GetRepositoryAsync(user, owner, name);

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
                    _logger.LogInformation(
                        "Merging pull request {Owner}/{Repository}#{Number}.",
                        owner,
                        name,
                        pr.Number);

                    await policy.ExecuteAsync(
                        () => _client.PullRequest.Merge(owner, name, pr.Number, mergeRequest));

                    _logger.LogInformation(
                        "Pull request {Owner}/{Repository}#{Number} merged.",
                        owner,
                        name,
                        pr.Number);
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

    public async Task VerifyCredentialsAsync()
    {
        _ = await _client.User.Current();
    }

    private static AsyncPolicy CreateMergePolicy()
    {
        return Policy
            .Handle<PullRequestNotMergeableException>()
            .WaitAndRetryAsync(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });
    }

    private async Task<IList<Models.PullRequest>> GetPullRequestsAsync(
        string owner,
        string name,
        bool fetchStatuses)
    {
        var result = new List<Models.PullRequest>();

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
                    Title = issue.Title,
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

        if (approved.Count < 1)
        {
            return false;
        }

        // Only use the most recent review for each approver
        var reviewsPerUsers = approved
            .OrderByDescending((p) => p.SubmittedAt)
            .DistinctBy((p) => p.User.Login)
            .ToList();

        if (reviewsPerUsers.Any((p) => p.State == PullRequestReviewState.ChangesRequested))
        {
            return false;
        }

        return reviewsPerUsers.Any((p) => p.State == PullRequestReviewState.Approved);
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

            static bool IsPending(CheckSuite suite)
                => suite.Conclusion is null && suite.Status == CheckStatus.InProgress;

            // If a check has not run at all consider it successful as it
            // might not be required to run at all (e.g. an old installation)
            // as it would otherwise block the Pull Request from being successful.
            static bool IsSuccess(CheckSuite suite)
                => (suite.Conclusion is null && suite.Status != CheckStatus.InProgress) ||
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
            else if (checkSuitesResponse.CheckSuites.Any(IsPending))
            {
                status = ChecksStatus.Pending;
            }
        }

        return status ?? ChecksStatus.Pending;
    }

    private async Task<Octokit.Repository> GetRepositoryAsync(ClaimsPrincipal user, string owner, string name)
    {
        return await CacheGetOrCreateAsync(user, $"repo:{owner}/{name}", async () =>
        {
            return await _client.Repository.Get(owner, name);
        });
    }

    private async Task<User> GetUserAsync(ClaimsPrincipal user, string login)
    {
        return await CacheGetOrCreateAsync(user, $"user:{login}", async () =>
        {
            return await _client.User.Get(login);
        });
    }

    private async Task<bool> IsDependabotEnabledAsync(ClaimsPrincipal user, string owner, string name)
    {
        return await CacheGetOrCreateAsync(user, $"repo:{owner}/{name}/dependabot", async () =>
        {
            try
            {
                _ = await _client.Repository.Content.GetRawContent(owner, name, ".github/dependabot.yml");
                return true;
            }
            catch (NotFoundException)
            {
                return false;
            }
        });
    }

    private async Task<T> CacheGetOrCreateAsync<T>(ClaimsPrincipal user, string key, Func<Task<T>> factory)
    {
        if (_options.DisableCaching)
        {
            return await factory();
        }

        string prefix = user.GetUserId();
        return await _cache.GetOrCreateAsync($"{prefix}:{key}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;
            return await factory();
        });
    }
}
