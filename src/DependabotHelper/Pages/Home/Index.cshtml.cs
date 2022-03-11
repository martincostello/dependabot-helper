// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable SA1649

using Humanizer;
using MartinCostello.DependabotHelper.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.DependabotHelper.Pages;

public class IndexModel : PageModel
{
    private readonly IMemoryCache _cache;
    private readonly IGitHubClient _client;
    private readonly ILogger _logger;
    private readonly DependabotOptions _options;

    public IndexModel(
        IGitHubClient client,
        IMemoryCache cache,
        IOptionsSnapshot<DependabotOptions> options,
        ILogger<IndexModel> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public IList<OwnerViewModel> Owners { get; set; } = new List<OwnerViewModel>();

    public int? RateLimitRemaining { get; set; }

    public int? RateLimitTotal { get; set; }

    public string? RateLimitResets { get; set; }

    public async Task OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var labels = _options.Labels;

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

            Owners.Add(ownerModel);
        }

        var appInfo = _client.GetLastApiInfo();

        if (appInfo.RateLimit is { } rateLimit)
        {
            RateLimitTotal = rateLimit.Limit;
            RateLimitRemaining = rateLimit.Remaining;
            RateLimitResets = rateLimit.Reset.Humanize();
        }
    }

    public async Task<IActionResult> OnPost()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            string owner = Request.Form["Owner"];
            string name = Request.Form["Repository"];

            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
            {
                return BadRequest();
            }

            var mergeRequest = new MergePullRequest()
            {
                MergeMethod = PullRequestMergeMethod.Merge,
            };

            var repository = await GetRepoAsync(owner, name);

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

        return RedirectToPage("./Index");
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
            repos.Add(await GetRepoAsync(owner, repository));
        }

        return repos.OrderBy((p) => p.Name).ToList();
    }

    private async Task<Repository> GetRepoAsync(string owner, string name)
    {
        return await _cache.GetOrCreateAsync($"repo:{owner}/{name}", async (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await _client.Repository.Get(owner, name);
        });
    }
}
