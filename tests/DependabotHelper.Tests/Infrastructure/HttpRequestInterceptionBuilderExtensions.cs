// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;
using MartinCostello.DependabotHelper.Builders;

namespace MartinCostello.DependabotHelper.Infrastructure;

public static class HttpRequestInterceptionBuilderExtensions
{
    public static HttpRequestInterceptionBuilder WithJsonContent<T>(this HttpRequestInterceptionBuilder builder, T response)
        where T : ResponseBuilder
    {
        return builder.WithSystemTextJsonContent(response.Build());
    }

    public static HttpRequestInterceptionBuilder WithJsonContent<T>(this HttpRequestInterceptionBuilder builder, IEnumerable<T> response)
        where T : ResponseBuilder
    {
        return builder.WithSystemTextJsonContent(response.Build());
    }
}
