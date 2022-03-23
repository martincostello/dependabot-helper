// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Primitives;
using Terrajobst.GitHubEvents;

namespace MartinCostello.DependabotHelper;

public class GitHubEventProcessorTests
{
    public GitHubEventProcessorTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    private ITestOutputHelper OutputHelper { get; }

    [Fact]
    public void Process_Does_Not_Throw()
    {
        // Arrange
        var logger = OutputHelper.ToLogger<GitHubEventProcessor>();
        var target = new GitHubEventProcessor(logger);

        var message = new GitHubEvent(
            "User-Agent",
            "delivery",
            "event",
            "hook-id",
            "hook-installation-target-id",
            "hook-installation-target-type",
            new Dictionary<string, StringValues>(),
            new(),
            "{}");

        // Assert
        target.Process(message);
    }
}
