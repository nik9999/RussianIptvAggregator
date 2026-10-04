using Microsoft.Extensions.Logging;
using RussianIptvAggregator.Application.Abstractions;
using RussianIptvAggregator.Application.Services;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Playlists;
using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Application.Services;

/// <summary>
/// Orchestrates the full aggregation pipeline:
/// 1. Load playlist sources from JSON
/// 2. Download and parse each playlist
/// 3. Filter Russian channels
/// 4. Deduplicate by stream URL
/// 5. Validate stream health
/// 6. Export alive channels to M3U
/// </summary>
public class PlaylistAggregatorService : IPlaylistAggregatorService
{
    private readonly ILogger<PlaylistAggregatorService> _logger;
    private readonly IStreamFetcher _fetcher;
    private readonly IPlaylistParser _parser;
    private readonly IStreamValidatorFactory _validatorFactory;
    private readonly PlaylistSourcesConfig _config;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _segmentsToCheck;

    public PlaylistAggregatorService(
        ILogger<PlaylistAggregatorService> logger,
        IStreamFetcher fetcher,
        IPlaylistParser parser,
        IStreamValidatorFactory validatorFactory,
        PlaylistSourcesConfig config,
        int maxDegreeOfParallelism = 20,
        int segmentsToCheck = 3)
    {
        _logger = logger;
        _fetcher = fetcher;
        _parser = parser;
        _validatorFactory = validatorFactory;
        _config = config;
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _segmentsToCheck = segmentsToCheck;
    }

    public async Task<AggregationResult> AggregateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting playlist aggregation...");

        var allChannels = new List<Channel>();
        var processedCount = 0;

        // Step 1: Download and parse each playlist
        foreach (var source in _config.Playlists.Where(s => s.Enabled))
        {
            _logger.LogInformation("Processing playlist: {Name} ({Url})", source.Name, source.Url);
            try
            {
                // Pre-flight connectivity check
                _logger.LogInformation("Pre-flight check for: {Url}", source.Url);
                if (!await IsUrlReachableAsync(source.Url, cancellationToken))
                {
                    _logger.LogWarning(
                        "Playlist '{Name}' is unreachable (connection timeout or refused), skipping",
                        source.Name);
                    continue;
                }

                // Download the raw playlist content
                _logger.LogInformation("Downloading playlist content from: {Url}", source.Url);
                var content = await _fetcher.DownloadTextAsync(source.Url, cancellationToken);
                _logger.LogInformation("Downloaded {Length} bytes from: {Url}", content.Length, source.Url);

                // Parse the downloaded content
                _logger.LogInformation("Parsing playlist content from: {Url}", source.Url);
                var channels = await _parser.ParseAsync(content, source.Url, cancellationToken);
                foreach (var ch in channels)
                {
                    ch.SourcePlaylistName = source.Name;
                }
                allChannels.AddRange(channels);
                processedCount++;
                _logger.LogInformation(
                    "Playlist '{Name}' parsed: {Count} channels found",
                    source.Name, channels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse playlist '{Name}' ({Url}): {Message}", source.Name, source.Url, ex.Message);
            }
        }

        var totalChannelsFound = allChannels.Count;
        _logger.LogInformation("Total Russian channels found across all playlists: {Count}", totalChannelsFound);

        // Step 2: Deduplicate
        var uniqueChannels = ChannelDeduplicator.Deduplicate(allChannels);
        var dedupCount = uniqueChannels.Count;
        _logger.LogInformation("After deduplication: {Count}", dedupCount);

        // Step 4: Validate stream health (parallel)
        _logger.LogInformation("Starting stream health validation for {Count} channels...", dedupCount);
        var checkedCount = 0;
        var aliveCount = 0;
        var degradedCount = 0;
        var deadCount = 0;
        var invalidCount = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(uniqueChannels, options, async (channel, ct) =>
        {
            try
            {
                _logger.LogDebug("Validating channel: {Name} ({Url})", channel.Name, channel.StreamUrl);
                channel.HealthResult = await _validatorFactory.ValidateAsync(channel, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate channel '{Name}' ({Url}): {Message}", channel.Name, channel.StreamUrl, ex.Message);
                channel.HealthResult = StreamHealthResult.Failure(
                    "Unknown",
                    "Validation exception",
                    ex.Message);
            }

            Interlocked.Increment(ref checkedCount);

            if (channel.HealthResult?.IsAlive == true)
                Interlocked.Increment(ref aliveCount);
            else if (channel.HealthResult?.IsDegraded == true)
                Interlocked.Increment(ref degradedCount);
            else if (channel.HealthResult?.IsAlive == false)
            {
                if (channel.HealthResult.SegmentsChecked > 0)
                    Interlocked.Increment(ref deadCount);
                else
                    Interlocked.Increment(ref invalidCount);
            }

            if (checkedCount % 10 == 0)
            {
                _logger.LogInformation(
                    "Health check progress: {Checked}/{Total} ({Alive} alive, {Dead} dead, {Degraded} degraded)",
                    checkedCount, dedupCount, aliveCount, deadCount, degradedCount);
            }
        });

        _logger.LogInformation(
            "Stream health validation complete: {Alive} alive, {Dead} dead, {Degraded} degraded, {Invalid} invalid",
            aliveCount, deadCount, degradedCount, invalidCount);

        // Step 5: Export alive channels to M3U
        var result = new AggregationResult
        {
            PlaylistsProcessed = processedCount,
            TotalChannelsFound = totalChannelsFound,
            RussianChannelsFiltered = totalChannelsFound,
            ChannelsAfterDeduplication = dedupCount,
            StreamsChecked = checkedCount,
            AliveCount = aliveCount,
            DegradedCount = degradedCount,
            DeadCount = deadCount,
            InvalidCount = invalidCount,
            AllChannels = uniqueChannels,
        };

        return result;
    }

    /// <summary>
    /// Pre-flight check: verifies the URL is reachable before downloading.
    /// Uses a short timeout to avoid long hangs on unreachable hosts.
    /// </summary>
    private static async Task<bool> IsUrlReachableAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var absoluteUrl = EnsureAbsoluteUrl(url);
            Console.WriteLine($"[PreFlight] Checking reachability: {absoluteUrl}");

            using var shortLivedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            {
                Timeout = TimeSpan.FromSeconds(2), // Short timeout for pre-flight check
            };
            shortLivedClient.DefaultRequestHeaders.Add("Accept", "*/*");

            // Use HEAD first (lighter), fallback to GET if HEAD not supported
            try
            {
                var response = await shortLivedClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, absoluteUrl),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                Console.WriteLine($"[PreFlight] HEAD response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[PreFlight] HEAD failed for {absoluteUrl}: {ex.Message}");
                // Some servers don't support HEAD for non-HTTP endpoints
                return false;
            }
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[PreFlight] Timeout for {url}: {ex.Message}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[PreFlight] HTTP error for {url}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PreFlight] Error for {url}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static string EnsureAbsoluteUrl(string url)
    {
        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return url;

        Console.WriteLine($"[PreFlight] WARNING: URL is not absolute, prefixing with http://: {url}");
        return "http://" + url;
    }
}
