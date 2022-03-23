// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Terrajobst.GitHubEvents;

namespace MartinCostello.DependabotHelper;

public sealed class GitHubEventProcessor : IGitHubEventProcessor
{
    private readonly ILogger<GitHubEventProcessor> _logger;

    public GitHubEventProcessor(ILogger<GitHubEventProcessor> logger)
    {
        _logger = logger;
    }

    public void Process(GitHubEvent message)
    {
        _logger.LogInformation("Received webhook with ID {HookId}.", message.HookId);
    }
}
