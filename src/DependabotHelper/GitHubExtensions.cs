// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Internal;

namespace MartinCostello.DependabotHelper;

public static class GitHubExtensions
{
    public static IServiceCollection AddGitHubClient(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddSingleton<ICredentialStore, UserCredentialStore>();
        services.AddSingleton<IJsonSerializer, SimpleJsonSerializer>();

        services.AddScoped<GitHubRateLimitsAccessor>();
        services.AddScoped<GitHubService>();

        services.AddScoped<IHttpClient>((provider) =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
            return new HttpClientAdapter(httpClientFactory.CreateHandler);
        });

        services.AddScoped<IConnection>((provider) =>
        {
            string productName = "DependabotHelper";
            string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);

            var productInformation = new ProductHeaderValue(productName, productVersion);
            var baseAddress = GitHubClient.GitHubApiUrl;

            var configuration = provider.GetRequiredService<IConfiguration>();

            if (configuration["GitHub:EnterpriseDomain"] is string enterpriseUri &&
                !string.IsNullOrWhiteSpace(enterpriseUri))
            {
                baseAddress = new(enterpriseUri, UriKind.Absolute);
            }

            var credentialStore = provider.GetRequiredService<ICredentialStore>();
            var httpClient = provider.GetRequiredService<IHttpClient>();
            var serializer = provider.GetRequiredService<IJsonSerializer>();

            return new Connection(productInformation, baseAddress, credentialStore, httpClient, serializer);
        });

        services.AddScoped<IGitHubClient>((provider) =>
        {
            var connection = provider.GetRequiredService<IConnection>();
            return new GitHubClient(connection);
        });

        return services;
    }
}
