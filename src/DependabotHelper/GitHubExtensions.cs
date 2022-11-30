// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Internal;

using IGraphQLConnection = Octokit.GraphQL.IConnection;
using IGraphQLCredentialStore = Octokit.GraphQL.ICredentialStore;

namespace MartinCostello.DependabotHelper;

public static class GitHubExtensions
{
    private static readonly ProductHeaderValue UserAgent = CreateUserAgent();

    public static IServiceCollection AddGitHubClient(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddSingleton<UserCredentialStore>();
        services.AddSingleton<ICredentialStore>((p) => p.GetRequiredService<UserCredentialStore>());
        services.AddSingleton<IGraphQLCredentialStore>((p) => p.GetRequiredService<UserCredentialStore>());

        services.AddSingleton<IJsonSerializer, SimpleJsonSerializer>();

        services.AddScoped<GitHubService>();
        services.AddScoped<IHttpClient>((provider) =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
            return new HttpClientAdapter(httpClientFactory.CreateHandler);
        });

        services.AddScoped<IConnection>((provider) =>
        {
            var baseAddress = GetGitHubApiUri(provider);
            var credentialStore = provider.GetRequiredService<ICredentialStore>();
            var httpClient = provider.GetRequiredService<IHttpClient>();
            var serializer = provider.GetRequiredService<IJsonSerializer>();

            if (baseAddress != GitHubClient.GitHubApiUrl)
            {
                baseAddress = new Uri(baseAddress, "/api/v3/");
            }

            return new Connection(UserAgent, baseAddress, credentialStore, httpClient, serializer);
        });

        services.AddScoped<IGraphQLConnection>((provider) =>
        {
            var productInformation = new Octokit.GraphQL.ProductHeaderValue(UserAgent.Name, UserAgent.Version);

            var baseAddress = GetGitHubGraphQLUri(provider);
            var credentialStore = provider.GetRequiredService<IGraphQLCredentialStore>();
            var httpClient = provider.GetRequiredService<HttpClient>();

            return new Octokit.GraphQL.Connection(productInformation, baseAddress, credentialStore, httpClient);
        });

        services.AddScoped<IGitHubClient>((provider) =>
        {
            var connection = provider.GetRequiredService<IConnection>();
            return new GitHubClient(connection);
        });

        return services;
    }

    private static ProductHeaderValue CreateUserAgent()
    {
        string productVersion = typeof(GitHubExtensions).Assembly.GetName().Version!.ToString(3);
        return new ProductHeaderValue("DependabotHelper", productVersion);
    }

    private static Uri GetGitHubApiUri(IServiceProvider provider)
    {
        var baseAddress = GitHubClient.GitHubApiUrl;
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
        var baseAddress = Octokit.GraphQL.Connection.GithubApiUri;
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
