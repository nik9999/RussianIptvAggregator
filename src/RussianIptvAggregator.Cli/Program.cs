using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RussianIptvAggregator.Application.Abstractions;
using RussianIptvAggregator.Application.Services;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Playlists;
using RussianIptvAggregator.Infrastructure.Http;
using RussianIptvAggregator.Infrastructure.Parsing;
using RussianIptvAggregator.Infrastructure.Validation;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;

namespace RussianIptvAggregator.Cli;

internal static class Program
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configuration — resolve path relative to this assembly
        var assemblyPath = typeof(Program).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyPath)!;
        var appsettingsPath = Path.Combine(assemblyDir, "appsettings.json");
        builder.Configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: false);

        // Logging — new API without deprecated properties
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Register services
        var healthCheckConfig = builder.Configuration.GetSection("HealthCheck");
        var inputConfig = builder.Configuration.GetSection("Input");
        var outputConfig = builder.Configuration.GetSection("Output");

        var maxDegreeOfParallelism = healthCheckConfig.GetValue<int>("MaxDegreeOfParallelism", 5);
        var segmentsToCheck = healthCheckConfig.GetValue<int>("SegmentsToCheck", 3);
        var manifestTimeout = TimeSpan.FromSeconds(healthCheckConfig.GetValue<int>("ManifestTimeoutSeconds", 5));
        var segmentTimeout = TimeSpan.FromSeconds(healthCheckConfig.GetValue<int>("SegmentTimeoutSeconds", 3));
        var playlistsFile = inputConfig.GetValue<string>("PlaylistsFile") ?? "config/playlists.json";
        var outputFile = outputConfig.GetValue<string>("PlaylistFile") ?? "output/russian-iptv.m3u";
        var sortByGroup = outputConfig.GetValue<bool>("SortByGroup", true);

        // Load playlist sources config — resolve relative to solution root (parent of src/)
        var configPath = Path.IsPathRooted(playlistsFile)
            ? playlistsFile
            : Path.Combine(assemblyDir, "..", "..", "..", "..", "..", playlistsFile);
        configPath = Path.GetFullPath(configPath);
        if (!File.Exists(configPath))
        {
            await Console.Error.WriteLineAsync($"Error: Playlists config file not found: {configPath}");
            return 1;
        }

        var configJson = await File.ReadAllTextAsync(configPath, cancellationToken: default).ConfigureAwait(false);
        var playlistSources = System.Text.Json.JsonSerializer.Deserialize<PlaylistSourcesConfig>(configJson, JsonOptions)
            ?? new PlaylistSourcesConfig();

        // Register HttpClient with SSL bypass for development
        var requestTimeout = TimeSpan.FromSeconds(5);
        builder.Services.AddHttpClient<ResilientStreamFetcher>(client =>
        {
            client.Timeout = requestTimeout + TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("Accept", "*/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        });
        builder.Services.AddSingleton<IStreamFetcher>(sp =>
            new ResilientStreamFetcher(
                sp.GetRequiredService<HttpClient>(),
                retryCount: 0,
                requestTimeout: requestTimeout));

        // Register domain services
        builder.Services.AddSingleton<IPlaylistParser, M3UParser>();

        // Register validators
        builder.Services.AddSingleton<MpegTsPacketValidator>();
        builder.Services.AddSingleton<MpegTsStreamValidator>(sp =>
            new MpegTsStreamValidator(
                sp.GetRequiredService<IStreamFetcher>(),
                sp.GetRequiredService<MpegTsPacketValidator>()));
        builder.Services.AddSingleton<RtmpValidator>(sp =>
            new RtmpValidator(TimeSpan.FromSeconds(5)));
        builder.Services.AddSingleton<IStreamValidatorFactory, StreamValidatorFactory>(sp =>
            new StreamValidatorFactory(
                sp.GetRequiredService<IStreamFetcher>(),
                sp.GetRequiredService<MpegTsStreamValidator>(),
                sp.GetRequiredService<RtmpValidator>(),
                sp.GetRequiredService<MpegTsPacketValidator>(),
                manifestTimeout,
                segmentTimeout));

        // Register application services
        builder.Services.AddSingleton(playlistSources);
        builder.Services.AddSingleton<IPlaylistAggregatorService>(sp =>
            new PlaylistAggregatorService(
                sp.GetRequiredService<ILogger<PlaylistAggregatorService>>(),
                sp.GetRequiredService<IStreamFetcher>(),
                sp.GetRequiredService<IPlaylistParser>(),
                sp.GetRequiredService<IStreamValidatorFactory>(),
                playlistSources,
                maxDegreeOfParallelism,
                segmentsToCheck));

        var host = builder.Build();
        var aggregator = host.Services.GetRequiredService<IPlaylistAggregatorService>();

        await Console.Out.WriteLineAsync("========================================");
        await Console.Out.WriteLineAsync("  Russian IPTV Aggregator v1.0");
        await Console.Out.WriteLineAsync("========================================");
        await Console.Out.WriteLineAsync();

        try
        {
            using var cts = new CancellationTokenSource();
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped.Register(cts.Cancel);
            var result = await aggregator.AggregateAsync(cts.Token).ConfigureAwait(false);

            // Export M3U file
            var fullOutputPath = Path.GetFullPath(outputFile);
            M3UExporter.ExportToFile(
                result.AllChannels.Where(c => c.HealthResult?.IsAlive == true).ToList(),
                fullOutputPath,
                sortByGroup);

            result.OutputFilePath = fullOutputPath;

            // Print statistics
            PrintStatistics(result);

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync($"Output file: {fullOutputPath}");

            return 0;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("\nOperation cancelled.");
            return 130;
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync($"\nIO error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            await Console.Error.WriteLineAsync($"\nError: {ex.Message}");
            return 1;
        }
    }

    private static void PrintStatistics(AggregationResult result)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  Statistics");
        Console.WriteLine("========================================");
        Console.WriteLine($"Playlists processed:        {result.PlaylistsProcessed}");
        Console.WriteLine($"Total channels found:        {result.TotalChannelsFound}");
        Console.WriteLine($"Russian channels filtered:   {result.RussianChannelsFiltered}");
        Console.WriteLine($"After deduplication:         {result.ChannelsAfterDeduplication}");
        Console.WriteLine($"Streams checked:             {result.StreamsChecked}");
        Console.WriteLine();
        Console.WriteLine($"Alive (in playlist):         {result.AliveCount}");
        Console.WriteLine($"Degraded (excluded):         {result.DegradedCount}");
        Console.WriteLine($"Dead (excluded):             {result.DeadCount}");
        Console.WriteLine($"Invalid (excluded):          {result.InvalidCount}");
        Console.WriteLine("========================================");
    }
}
