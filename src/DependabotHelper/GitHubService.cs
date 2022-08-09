// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Claims;
using MartinCostello.DependabotHelper.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.GraphQL;
using Polly;

using IGraphQLConnection = Octokit.GraphQL.IConnection;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubService
{
    /// <summary>
    /// The cache period for data that is long-lived and/or stable.
    /// </summary>
    private static readonly TimeSpan LongCacheLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// The cache period for data that is short-lived and/or volatile.
    /// </summary>
    private static readonly TimeSpan ShortCacheLifetime = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private readonly IGitHubClient _client;
    private readonly IGraphQLConnection _connection;
    private readonly ILogger _logger;
    private readonly DependabotOptions _options;

    public GitHubService(
        IGitHubClient client,
        IGraphQLConnection connection,
        IMemoryCache cache,
        IOptionsSnapshot<DependabotOptions> options,
        ILogger<GitHubService> logger)
    {
        _client = client;
        _connection = connection;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
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

    public async Task<RepositoryPullRequests> GetPullRequestsAsync(ClaimsPrincipal user, string owner, string name)
    {
        var repository = await GetRepositoryAsync(user, owner, name);

        var result = new RepositoryPullRequests()
        {
            HtmlUrl = repository.HtmlUrl + "/pulls",
            Id = repository.Id,
            IsFork = repository.Fork,
            IsPrivate = repository.IsPrivate(),
            Name = repository.Name,
        };

        if (await IsDependabotEnabledAsync(user, owner, name))
        {
            result.DependabotHtmlUrl = repository.HtmlUrl + "/network/updates";
        }

        result.All = await GetPullRequestsAsync(
            user,
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

                if (current.Id == ownerUser.Id)
                {
                    repos = await _client.Repository.GetAllForCurrent(new RepositoryRequest()
                    {
                        Type = RepositoryType.Owner,
                    });
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
            .Where((p) => _options.IncludePrivate || !p.IsPrivate())
            .Select((p) =>
            {
                return new Models.Repository()
                {
                    HtmlUrl = p.HtmlUrl,
                    Id = p.Id,
                    IsFork = p.Fork,
                    IsPrivate = p.IsPrivate(),
                    Name = p.Name,
                };
            })
            .OrderBy((p) => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<MergePullRequestsResponse> MergePullRequestsAsync(
        ClaimsPrincipal user,
        string owner,
        string name)
    {
        var mergeCandidates = await GetPullRequestsAsync(
            user,
            owner,
            name,
            fetchStatuses: false);

        var result = new MergePullRequestsResponse();

        if (mergeCandidates.Count > 0)
        {
            var mergeMethod = PullRequestMergeMethod.Merge;

            var repository = await GetRepositoryAsync(user, owner, name);

            if (repository.AllowMergeCommit == false)
            {
                mergeMethod = repository.AllowRebaseMerge switch
                {
                    true => PullRequestMergeMethod.Rebase,
                    _ => PullRequestMergeMethod.Squash,
                };
            }

            var mergeRequest = new MergePullRequest()
            {
                MergeMethod = mergeMethod,
            };

            var policy = CreateMergePolicy();

            foreach (var pr in mergeCandidates.OrderBy((p) => p.Number))
            {
                bool enableAutoMerge = false;

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

                    result.Numbers.Add(pr.Number);
                }
                catch (PullRequestNotMergeableException ex)
                {
                    _logger.LogInformation(
                        ex,
                        "Could not merge pull request {Owner}/{Repository}#{Number} as it is not mergeable.",
                        pr.RepositoryOwner,
                        pr.RepositoryName,
                        pr.Number);

                    enableAutoMerge = true;
                }
                catch (ApiException ex)
                {
                    _logger.LogError(
                        ex,
                        "Could not merge pull request {Owner}/{Repository}#{Number} due to an error.",
                        pr.RepositoryOwner,
                        pr.RepositoryName,
                        pr.Number);
                }

                if (enableAutoMerge)
                {
                    await TryEnableAutoMergeAsync(pr, mergeMethod);
                }
            }
        }

        return result;
    }

    public async Task VerifyCredentialsAsync()
    {
        _ = await _client.User.Current();
    }

    private AsyncPolicy CreateMergePolicy()
    {
        return Policy
            .Handle<PullRequestNotMergeableException>()
            .WaitAndRetryAsync(_options.MergeRetryWaits);
    }

    private async Task TryEnableAutoMergeAsync(
        Models.PullRequest pullRequest,
        PullRequestMergeMethod mergeMethod)
    {
        var input = new Octokit.GraphQL.Model.EnablePullRequestAutoMergeInput()
        {
            MergeMethod = Enum.Parse<Octokit.GraphQL.Model.PullRequestMergeMethod>(mergeMethod.ToString()),
            PullRequestId = new(pullRequest.NodeId),
        };

        var query = new Mutation()
            .EnablePullRequestAutoMerge(input)
            .Select((p) => new { p.PullRequest.Number })
            .Compile();

        try
        {
            await _connection.Run(query);

            _logger.LogInformation(
                "Enabled auto-merge for pull request {Owner}/{Repository}#{Number}.",
                pullRequest.RepositoryOwner,
                pullRequest.RepositoryName,
                pullRequest.Number);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to enable auto-merge for pull request {Owner}/{Repository}#{Number} with node ID {NodeId}.",
                pullRequest.RepositoryOwner,
                pullRequest.RepositoryName,
                pullRequest.Number,
                pullRequest.NodeId);
        }
    }

    private async Task<IList<Models.PullRequest>> GetPullRequestsAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        bool fetchStatuses)
    {
        var result = new List<Models.PullRequest>();

        foreach (string creator in _options.Users)
        {
            // If fetching the statuses, fetch the pull requests bypassing the cache
            // otherwise when PRs are merged, the list does not reflect the just-merged
            // issues in the UI, making it look like the PRs were not merged at all.
            bool useCache = !fetchStatuses;

            var openPullRequests = await GetOpenPullRequestsAsync(user, owner, name, creator, useCache);

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

                bool canApprove = false;
                bool isApproved = false;
                var status = ChecksStatus.Pending;

                if (fetchStatuses)
                {
                    _logger.LogInformation(
                        "Fetching approvals and statuses for pull request {Number} in repository {Owner}/{Name}.",
                        issue.Number,
                        owner,
                        name);

                    (canApprove, isApproved) = await IsApprovedAsync(user, owner, name, issue.Number, pr.Base.Ref);
                    status = await GetChecksStatusAsync(user, owner, name, pr.Head.Sha, pr.Base.Ref);

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
                    CanApprove = canApprove,
                    HtmlUrl = pr.HtmlUrl,
                    IsApproved = isApproved,
                    NodeId = pr.NodeId,
                    Number = pr.Number,
                    RepositoryName = name,
                    RepositoryOwner = owner,
                    Status = status,
                    Title = issue.Title,
                });
            }
        }

        return result
            .OrderByDescending((p) => p.Number)
            .ToList();
    }

    private async Task<(bool CanApprove, bool IsApproved)> IsApprovedAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        int number,
        string targetBranch)
    {
        _logger.LogDebug(
            "Fetching approvals for pull request {Number} in repository {Owner}/{Name}.",
            number,
            owner,
            name);

        // Do not cache reviews so that the UI updates correctly on merging
        var approved = await _client.PullRequest.Review.GetAll(owner, name, number);

        _logger.LogDebug(
            "Found {Count} approvals for pull request {Number} in repository {Owner}/{Name}.",
            approved.Count,
            number,
            owner,
            name);

        if (approved.Count < 1)
        {
            return (true, false);
        }

        // Only use the most recent review for each approver.
        // Ignore reviews from people unassociated with the repository.
        var reviewsPerUsers = approved
            .OrderByDescending((p) => p.SubmittedAt)
            .DistinctBy((p) => p.User.Login)
            .Where((p) => p.AuthorAssociation.Value.CanReview() || p.User.Type == AccountType.Bot)
            .ToList();

        bool canApprove = !reviewsPerUsers.Any((p) => p.User.Login == user.GetUserLogin());

        if (reviewsPerUsers.Any((p) => p.State == PullRequestReviewState.ChangesRequested))
        {
            return (canApprove, false);
        }

        int approvedReviews = reviewsPerUsers.Count((p) => p.State == PullRequestReviewState.Approved);

        int requiredReviewsCount = await GetNumberOfRequiredReviewersAsync(user, owner, name, targetBranch);

        bool isApproved = approvedReviews >= requiredReviewsCount;

        return (canApprove, isApproved);
    }

    private async Task<int> GetNumberOfRequiredReviewersAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        string branch)
    {
        var protection = await GetBranchProtectionAsync(user, owner, name, branch);

        // For the purposes of this app, at least one reviewer is always required
        return protection?.RequiredPullRequestReviews?.RequiredApprovingReviewCount ?? 1;
    }

    private async Task<ChecksStatus> GetChecksStatusAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        string commitSha,
        string branch)
    {
        var requiredStatuses = await GetRequiredStatusChecksAsync(user, owner, name, branch);

        _logger.LogDebug(
            "Found {Count} required status checks for the {Branch} of repository {Owner}/{Name}.",
            requiredStatuses.Count,
            branch,
            owner,
            name);

        ChecksStatus? status = null;

        _logger.LogDebug(
            "Fetching combined status for commit {Reference} in repository {Owner}/{Name}.",
            commitSha,
            owner,
            name);

        var combinedCommitStatus = await CacheGetOrCreateAsync(user, $"status:{owner}:{name}:{commitSha}", ShortCacheLifetime, async () =>
        {
            return await _client.Repository.Status.GetCombined(owner, name, commitSha);
        });

        _logger.LogDebug(
            "Found {Count} statuses for commit {Reference} in repository {Owner}/{Name}.",
            combinedCommitStatus.TotalCount,
            commitSha,
            owner,
            name);

        var successfulStatuses = new HashSet<string>();

        if (combinedCommitStatus.TotalCount > 0)
        {
            foreach (var commitStatus in combinedCommitStatus.Statuses)
            {
                successfulStatuses.Add(commitStatus.Context);

                _logger.LogTrace(
                    "Commit status: Context: {Context}; State: {State}",
                    commitStatus.Context,
                    commitStatus.State);
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

        CheckSuitesResponse? checkSuitesResponse = null;

        // No need to query the check suites if we already know the status is failed
        if (status != ChecksStatus.Error)
        {
            checkSuitesResponse = await _client.Check.Suite.GetAllForReference(owner, name, commitSha);

            _logger.LogDebug(
                "Found {Count} check suites for commit {Reference} in repository {Owner}/{Name}.",
                checkSuitesResponse.TotalCount,
                commitSha,
                owner,
                name);
        }

        if (checkSuitesResponse?.TotalCount > 0 && status != ChecksStatus.Error)
        {
            // Split the check suites into their possible statuses
            var queuedSuites = checkSuitesResponse.CheckSuites.Where((p) => p.Status == CheckStatus.Queued);
            var runningSuites = checkSuitesResponse.CheckSuites.Where((p) => p.Status == CheckStatus.InProgress);
            var completedSuites = checkSuitesResponse.CheckSuites.Where((p) => p.Status == CheckStatus.Completed);

            // Running and completed suites always affect the overally status
            var candidateSuites = runningSuites.Concat(completedSuites);

            async Task<CheckRunsResponse> GetCheckRunsAsync(long checkSuiteId)
            {
                return await _client.Check.Run.GetAllForCheckSuite(owner, name, checkSuiteId, new CheckRunRequest()
                {
                    Filter = CheckRunCompletedAtFilter.Latest,
                });
            }

            if (requiredStatuses.Count > 0)
            {
                foreach (var checkSuite in candidateSuites)
                {
                    var checkRuns = await GetCheckRunsAsync(checkSuite.Id);

                    foreach (var checkRun in checkRuns.CheckRuns)
                    {
                        if (checkRun.Conclusion == CheckConclusion.Success)
                        {
                            successfulStatuses.Add(checkRun.Name);
                        }
                    }
                }
            }

            // Queued suites only count if they contain at least one check run.
            // Otherwise they're likely just some old integration no longer in use.
            foreach (var checkSuite in queuedSuites)
            {
                var checkRuns = await GetCheckRunsAsync(checkSuite.Id);

                if (checkRuns.TotalCount > 0)
                {
                    candidateSuites = candidateSuites.Append(checkSuite);
                }
            }

            var applicableSuites = candidateSuites.ToList();

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
                => suite.Conclusion == CheckConclusion.Success ||
                   suite.Conclusion == CheckConclusion.Neutral;

            if (applicableSuites.All(IsSuccess))
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
            else if (applicableSuites.Any(IsError))
            {
                status = ChecksStatus.Error;
            }
            else if (applicableSuites.Any(IsPending))
            {
                status = ChecksStatus.Pending;
            }
        }

        // The status is only really successful if all required statuses are successful.
        // Otherwise, it's still pending until any missing required statuses have run.
        if (status == ChecksStatus.Success &&
            requiredStatuses.Count > 0 &&
            !successfulStatuses.IsSupersetOf(requiredStatuses))
        {
            status = ChecksStatus.Pending;
        }

        return status ?? ChecksStatus.Pending;
    }

    private async Task<IReadOnlyList<string>> GetRequiredStatusChecksAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        string branch)
    {
        var protection = await GetBranchProtectionAsync(user, owner, name, branch);
        return protection?.RequiredStatusChecks?.Contexts ?? Array.Empty<string>();
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
        return await CacheGetOrCreateAsync(user, $"user:{login}", LongCacheLifetime, async () =>
        {
            return await _client.User.Get(login);
        });
    }

    private async Task<bool> IsDependabotEnabledAsync(ClaimsPrincipal user, string owner, string name)
    {
        return await CacheGetOrCreateAsync(user, $"repo:{owner}/{name}/dependabot", LongCacheLifetime, async () =>
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

    private async Task<BranchProtectionSettings?> GetBranchProtectionAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        string branch)
    {
        return await CacheGetOrCreateAsync(user, $"{owner}:{name}:branch-protection:{branch}", async () =>
        {
            try
            {
                return await _client.Repository.Branch.GetBranchProtection(owner, name, branch);
            }
            catch (NotFoundException)
            {
                return null;
            }
        });
    }

    private async Task<IReadOnlyList<Issue>> GetOpenPullRequestsAsync(
        ClaimsPrincipal user,
        string owner,
        string name,
        string creator,
        bool useCache = true)
    {
        var request = new RepositoryIssueRequest()
        {
            Creator = creator,
            Filter = IssueFilter.Created,
            State = ItemStateFilter.Open,
        };

        foreach (string label in _options.Labels)
        {
            request.Labels.Add(label);
        }

        var options = new ApiOptions()
        {
            PageCount = _options.PageCount,
            PageSize = _options.PageSize,
        };

        _logger.LogInformation(
            "Finding open issues created by {User} in repository {Owner}/{Name}.",
            creator,
            owner,
            name);

        var cacheLifetime = useCache ? ShortCacheLifetime : TimeSpan.Zero;

        var issues = await CacheGetOrCreateAsync(user, $"issues:{owner}:{name}:{creator}", cacheLifetime, async () =>
        {
            return await _client.Issue.GetAllForRepository(owner, name, request, options);
        });

        var openPullRequests = issues
            .Where((p) => p.PullRequest is not null)
            .ToList();

        _logger.LogInformation(
            "Found {Count} open pull requests created by {User} in repository {Owner}/{Name}.",
            openPullRequests.Count,
            creator,
            owner,
            name);

        return openPullRequests;
    }

    private async Task<T> CacheGetOrCreateAsync<T>(
        ClaimsPrincipal user,
        string key,
        TimeSpan? absoluteExpirationRelativeToNow,
        Func<Task<T>> factory)
    {
        return await CacheGetOrCreateAsync(
            user,
            key,
            factory,
            absoluteExpirationRelativeToNow);
    }

    private async Task<T> CacheGetOrCreateAsync<T>(
        ClaimsPrincipal user,
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpirationRelativeToNow = default)
    {
        absoluteExpirationRelativeToNow ??= _options.CacheLifetime;

        if (_options.DisableCaching || absoluteExpirationRelativeToNow < ShortCacheLifetime)
        {
            return await factory();
        }

        string prefix = user.GetUserId();
        return await _cache.GetOrCreateAsync($"{prefix}:{key}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
            return await factory();
        });
    }
}
