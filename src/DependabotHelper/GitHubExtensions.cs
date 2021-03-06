// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Internal;

namespace MartinCostello.DependabotHelper;

public static class GitHubExtensions
{
    private static readonly ProductHeaderValue UserAgent = CreateUserAgent();

    public static IServiceCollection AddGitHubClient(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddSingleton<ICredentialStore, UserCredentialStore>();
        services.AddSingleton<IJsonSerializer, SimpleJsonSerializer>();

        services.AddScoped<GitHubService>();
        services.AddScoped<IHttpClient>((provider) =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
            return new HttpClientAdapter(httpClientFactory.CreateHandler);
        });

        services.AddScoped<IConnection>((provider) =>
        {
            var baseAddress = GetGitHubUri(provider);
            var credentialStore = provider.GetRequiredService<ICredentialStore>();
            var httpClient = provider.GetRequiredService<IHttpClient>();
            var serializer = provider.GetRequiredService<IJsonSerializer>();

            return new Connection(UserAgent, baseAddress, credentialStore, httpClient, serializer);
        });

        services.AddScoped<IGitHubClient>((provider) =>
        {
            var baseAddress = GetGitHubUri(provider);

            if (baseAddress == GitHubClient.GitHubApiUrl)
            {
                var connection = provider.GetRequiredService<IConnection>();
                return new GitHubClient(connection);
            }
            else
            {
                // Using IConnection does not seem to work correctly with
                // GitHub Enterprise as the requests still get sent to github.com.
                var credentialStore = provider.GetRequiredService<ICredentialStore>();
                return new GitHubClient(UserAgent, credentialStore, baseAddress);
            }
        });

        return services;
    }

    private static ProductHeaderValue CreateUserAgent()
    {
        string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);
        return new ProductHeaderValue("DependabotHelper", productVersion);
    }

    private static Uri GetGitHubUri(IServiceProvider provider)
    {
        var baseAddress = GitHubClient.GitHubApiUrl;

        var configuration = provider.GetRequiredService<IConfiguration>();

        if (configuration["GitHub:EnterpriseDomain"] is string enterpriseUri &&
            !string.IsNullOrWhiteSpace(enterpriseUri))
        {
            baseAddress = new(enterpriseUri, UriKind.Absolute);
        }

        return baseAddress;
    }
}
