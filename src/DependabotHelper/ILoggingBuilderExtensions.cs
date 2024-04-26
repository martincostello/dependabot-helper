// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

namespace MartinCostello.DependabotHelper;

public static class ILoggingBuilderExtensions
{
    public static ILoggingBuilder AddTelemetry(this ILoggingBuilder builder)
    {
        return builder.AddOpenTelemetry((p) =>
        {
            p.IncludeFormattedMessage = true;
            p.IncludeScopes = true;

            if (TelemetryExtensions.IsAzureMonitorConfigured())
            {
                p.AddAzureMonitorLogExporter();
            }

            if (TelemetryExtensions.IsOtlpCollectorConfigured())
            {
                p.AddOtlpExporter();
            }
        });
    }
}
