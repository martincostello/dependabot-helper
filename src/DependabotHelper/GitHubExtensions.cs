// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;

namespace MartinCostello.DependabotHelper;

public static class GitHubExtensions
{
    public static IServiceCollection AddGitHubClient(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICredentialStore, UserCredentialStore>();

        services.AddMemoryCache();

        services.AddScoped<GitHubRateLimitsAccessor>();
        services.AddScoped<GitHubService>();
        services.AddScoped<IGitHubClient>((provider) =>
        {
            string productName = "DependabotHelper";
            string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);

            var productInformation = new ProductHeaderValue(productName, productVersion);
            var baseAddress = GitHubClient.GitHubApiUrl;

            var configuration = provider.GetRequiredService<IConfiguration>();
            var credentialStore = provider.GetRequiredService<ICredentialStore>();

            if (configuration["GitHub:EnterpriseDomain"] is string enterpriseUri &&
                !string.IsNullOrWhiteSpace(enterpriseUri))
            {
                baseAddress = new(enterpriseUri, UriKind.Absolute);
            }

            return new GitHubClient(productInformation, credentialStore, baseAddress);
        });

        return services;
    }
}
