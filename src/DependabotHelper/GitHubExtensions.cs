// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using GitHub;
using GitHub.Client;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Octokit.GraphQL;

namespace MartinCostello.DependabotHelper;

public static class GitHubExtensions
{
    private static readonly Uri GitHubApiUrl = new("https://api.github.com", UriKind.Absolute);
    private static readonly ProductHeaderValue UserAgent = CreateUserAgent();

    public static IServiceCollection AddGitHubClient(this IServiceCollection services)
    {
        services.AddHttpClient()
                .AddHttpClient(Options.DefaultName)
                .AddStandardResilienceHandler();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddSingleton<UserCredentialStore>();
        services.AddSingleton<IAuthenticationProvider>((p) => p.GetRequiredService<UserCredentialStore>());
        services.AddSingleton<ICredentialStore>((p) => p.GetRequiredService<UserCredentialStore>());

        services.AddScoped<GitHubService>();

        services.AddScoped((provider) =>
        {
            var authenticationProvider = provider.GetRequiredService<IAuthenticationProvider>();
            var httpClient = provider.GetRequiredService<HttpClient>();
            var requestAdapter = RequestAdapter.Create(authenticationProvider, httpClient);

            var baseAddress = GetGitHubApiUri(provider);

            if (baseAddress != GitHubApiUrl)
            {
                baseAddress = new Uri(baseAddress, "/api/v3/");
            }

            requestAdapter.BaseUrl = baseAddress.ToString();

            return new GitHubClient(requestAdapter);
        });

        services.AddScoped<IConnection>((provider) =>
        {
            var baseAddress = GetGitHubGraphQLUri(provider);
            var credentialStore = provider.GetRequiredService<ICredentialStore>();
            var httpClient = provider.GetRequiredService<HttpClient>();

            return new Connection(UserAgent, baseAddress, credentialStore, httpClient);
        });

        return services;
    }

    private static ProductHeaderValue CreateUserAgent()
    {
        string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);
        return new("DependabotHelper", productVersion);
    }

    private static Uri GetGitHubApiUri(IServiceProvider provider)
    {
        var baseAddress = GitHubApiUrl;
        var configuration = provider.GetRequiredService<IConfiguration>();

        if (configuration["GitHub:EnterpriseDomain"] is string enterpriseDomain &&
            !string.IsNullOrWhiteSpace(enterpriseDomain))
        {
            baseAddress = new(enterpriseDomain, UriKind.Absolute);
        }

        return baseAddress;
    }

    private static Uri GetGitHubGraphQLUri(IServiceProvider provider)
    {
        var baseAddress = Connection.GithubApiUri;
        var configuration = provider.GetRequiredService<IConfiguration>();

        if (configuration["GitHub:EnterpriseDomain"] is string enterpriseDomain &&
            !string.IsNullOrWhiteSpace(enterpriseDomain))
        {
            var enterpriseUri = new Uri(enterpriseDomain, UriKind.Absolute);
            baseAddress = new(enterpriseUri, "api" + baseAddress.AbsolutePath);
        }

        return baseAddress;
    }
}
