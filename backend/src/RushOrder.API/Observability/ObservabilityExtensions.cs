using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.ApplicationInsights.Extensibility;
using RushOrder.API.Observability;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace RushOrder.API;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Wire up OpenTelemetry + Azure Monitor + Business Metrics.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Business Metrics (custom counters, histograms, gauges via System.Diagnostics.Metrics)
        services.AddSingleton<IBusinessMetrics, BusinessMetrics>();

        var aiConnStr = configuration["ApplicationInsights:ConnectionString"];

        if (string.IsNullOrWhiteSpace(aiConnStr))
            return services; // Dev: no AI connection string — logs go to console only

        // OpenTelemetry SDK → Azure Monitor exporter.
        // UseAzureMonitor bundles:
        //   - ASP.NET Core request instrumentation (HTTP server traces)
        //   - HttpClient instrumentation (outgoing HTTP, incl. Stripe calls)
        //   - SqlClient instrumentation (PostgreSQL queries via EF Core)
        //   - Azure Monitor OTLP exporter (traces + metrics)
        //   - Redis calls captured as generic dependency spans by the Azure Monitor agent
        services.AddOpenTelemetry()
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = aiConnStr;
                // 10% sampling in prod to control ingestion costs; 100% in staging
                options.SamplingRatio = environment.IsProduction() ? 0.1f : 1.0f;
            })
            .WithMetrics(metrics =>
                metrics.AddMeter(BusinessMetrics.MeterName));

        return services;
    }

    /// <summary>
    /// Configure the Serilog → Application Insights sink (Warning+ in prod, Info+ in staging).
    /// </summary>
    public static LoggerConfiguration AddApplicationInsightsSink(
        this LoggerConfiguration cfg,
        string aiConnectionString,
        IHostEnvironment environment)
    {
        var minLevel = environment.IsProduction()
            ? LogEventLevel.Warning
            : LogEventLevel.Information;

        var telemetryConfig = new TelemetryConfiguration { ConnectionString = aiConnectionString };

        return cfg.WriteTo.ApplicationInsights(
            telemetryConfiguration: telemetryConfig,
            telemetryConverter: new TraceTelemetryConverter(),
            restrictedToMinimumLevel: minLevel);
    }
}
