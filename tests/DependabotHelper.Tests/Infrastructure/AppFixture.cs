// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using AspNet.Security.OAuth.GitHub;
using JustEat.HttpClientInterception;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Terrajobst.GitHubEvents;

namespace MartinCostello.DependabotHelper.Infrastructure;

public class AppFixture : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    private readonly Dictionary<string, string> _configOverrides = new(StringComparer.OrdinalIgnoreCase);

    public AppFixture()
    {
        ClientOptions.AllowAutoRedirect = false;
        ClientOptions.BaseAddress = new Uri("https://localhost");
        Interceptor = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();
    }

    public HttpClientInterceptorOptions Interceptor { get; }

    public ITestOutputHelper? OutputHelper { get; set; }

    public void ClearOutputHelper()
        => OutputHelper = null;

    public void SetOutputHelper(ITestOutputHelper value)
        => OutputHelper = value;

    public void ClearConfigurationOverrides(bool reload = true)
    {
        _configOverrides.Clear();

        if (reload)
        {
            ReloadConfiguration();
        }
    }

    public void OverrideConfiguration(string key, string value, bool reload = true)
    {
        _configOverrides[key] = value;

        if (reload)
        {
            ReloadConfiguration();
        }
    }

    public async Task<AntiforgeryTokens> GetAntiforgeryTokensAsync(
        Func<HttpClient>? httpClientFactory = null,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory?.Invoke() ?? CreateClient();

        var tokens = await httpClient.GetFromJsonAsync<AntiforgeryTokens>(
            AntiforgeryTokenController.GetTokensUri,
            cancellationToken);

        return tokens!;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((configBuilder) =>
        {
            var config = new[]
            {
                KeyValuePair.Create("Dependabot:IncludeForks", bool.TrueString),
                KeyValuePair.Create("Dependabot:IncludePrivate", bool.TrueString),
                KeyValuePair.Create("Dependabot:MergeRetryWaits:0", "00:00:00.100"),
                KeyValuePair.Create("Dependabot:MergeRetryWaits:1", "00:00:00.200"),
                KeyValuePair.Create("GitHub:ClientId", "github-id"),
                KeyValuePair.Create("GitHub:ClientSecret", "github-secret"),
                KeyValuePair.Create("GitHub:EnterpriseDomain", string.Empty),
                KeyValuePair.Create("GitHub:WebhookSecret", "github-secret"),
                KeyValuePair.Create("Site:Domain", "dependabot.local"),
            };

            configBuilder.AddInMemoryCollection(config);
            configBuilder.Add(new AppFixtureConfigurationSource(this));
        });

        builder.ConfigureAntiforgeryTokenResource();

        builder.ConfigureLogging(
            (loggingBuilder) => loggingBuilder.ClearProviders().AddXUnit(this).SetMinimumLevel(LogLevel.Trace));

        builder.UseEnvironment(Environments.Production);

        builder.UseSolutionRelativeContentRoot(Path.Combine("src", "DependabotHelper"));

        builder.ConfigureServices((services) =>
        {
            services.AddHttpClient();

            services.AddSingleton<IHttpMessageHandlerBuilderFilter, HttpRequestInterceptionFilter>(
                (_) => new HttpRequestInterceptionFilter(Interceptor));

            services.AddSingleton<IPostConfigureOptions<GitHubAuthenticationOptions>, RemoteAuthorizationEventsFilter>();
            services.AddScoped<LoopbackOAuthEvents>();

            services.AddSingleton<MockGitHubEventProcessor>();
            services.AddSingleton<IGitHubEventProcessor>((p) => p.GetRequiredService<MockGitHubEventProcessor>());
        });

        Interceptor.RegisterBundle(Path.Combine("Bundles", "oauth-http-bundle.json"));
    }

    private void ReloadConfiguration()
    {
        var config = Services.GetRequiredService<IConfiguration>();

        if (config is IConfigurationRoot root)
        {
            root.Reload();
        }
    }

    private sealed class AppFixtureConfigurationProvider : ConfigurationProvider
    {
        internal AppFixtureConfigurationProvider(AppFixture fixture)
        {
            Data = fixture._configOverrides;
        }
    }

    private sealed class AppFixtureConfigurationSource : IConfigurationSource
    {
        internal AppFixtureConfigurationSource(AppFixture fixture)
        {
            Fixture = fixture;
        }

        internal AppFixture Fixture { get; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
            => new AppFixtureConfigurationProvider(Fixture);
    }
}
