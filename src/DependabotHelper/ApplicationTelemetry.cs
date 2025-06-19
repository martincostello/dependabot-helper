// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace MartinCostello.DependabotHelper;

public static class ApplicationTelemetry
{
    public static readonly string ServiceName = "DependabotHelper";
    public static readonly string ServiceVersion = GitMetadata.Version.Split('+')[0];
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    public static ResourceBuilder ResourceBuilder { get; } = ResourceBuilder.CreateDefault()
        .AddService(ServiceName, serviceVersion: ServiceVersion)
        .AddAzureAppServiceDetector()
        .AddContainerDetector()
        .AddHostDetector()
        .AddOperatingSystemDetector()
        .AddProcessRuntimeDetector();

    internal static bool IsOtlpCollectorConfigured()
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));
}
