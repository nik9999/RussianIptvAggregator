using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;

namespace RussianIptvAggregator.Infrastructure.Validation;

/// <summary>
/// Deep HLS stream validator that downloads manifest, resolves variants, and checks TS segments.
/// </summary>
public static class DeepHlsValidator
{
    private static readonly MpegTsPacketValidator TsValidator = new();

    public static async Task<StreamHealthResult> ValidateAsync(
        IStreamFetcher fetcher,
        Channel channel,
        TimeSpan? manifestTimeout = null,
        TimeSpan? segmentTimeout = null,
        CancellationToken cancellationToken = default)
    {
        manifestTimeout ??= TimeSpan.FromSeconds(10);
        segmentTimeout ??= TimeSpan.FromSeconds(5);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var manifestUrl = channel.StreamUrl;
            var manifestContent = await fetcher.DownloadTextAsync(manifestUrl, cancellationToken);
            var manifestResponseTime = stopwatch.Elapsed;

            // Determine if this is a master or media playlist
            var isMaster = HlsPlaylistParser.IsMasterPlaylist(manifestContent);

            if (isMaster)
            {
                // Master playlist: select first variant and validate its media playlist
                var variants = HlsPlaylistParser.ParseMasterPlaylist(manifestContent, manifestUrl);
                if (variants.Count == 0)
                    return StreamHealthResult.Failure("HLS", "No variants found in master playlist", null, manifestResponseTime);

                var firstVariant = variants[0];
                var mediaContent = await fetcher.DownloadTextAsync(firstVariant.MediaUrl, cancellationToken);

                var segments = HlsPlaylistParser.ParseMediaPlaylist(mediaContent, firstVariant.MediaUrl);
                if (segments.Count == 0)
                    return StreamHealthResult.Failure("HLS", "No media segments in variant playlist", null, manifestResponseTime);

                var lastSegments = segments.Skip(Math.Max(0, segments.Count - 3)).Take(3).ToList();
                var result = await CheckTsSegmentsAsync(fetcher, lastSegments, cancellationToken);
                result.Protocol = "HLS";
                result.ResponseTime = manifestResponseTime + result.ResponseTime;
                return result;
            }
            else
            {
                // Media playlist directly
                var segments = HlsPlaylistParser.ParseMediaPlaylist(manifestContent, manifestUrl);
                if (segments.Count == 0)
                    return StreamHealthResult.Failure("HLS", "No media segments found", null, manifestResponseTime);

                var lastSegments = segments.Skip(Math.Max(0, segments.Count - 3)).Take(3).ToList();
                var result = await CheckTsSegmentsAsync(fetcher, lastSegments, cancellationToken);
                result.Protocol = "HLS";
                result.ResponseTime = manifestResponseTime + result.ResponseTime;
                return result;
            }
        }
        catch (Exception ex)
        {
            return StreamHealthResult.Failure("HLS", "Failed to validate HLS stream", ex.Message, stopwatch.Elapsed);
        }
    }

    private static async Task<StreamHealthResult> CheckTsSegmentsAsync(
        IStreamFetcher fetcher,
        List<string> segmentUrls,
        CancellationToken cancellationToken)
    {
        var totalChecked = 0;
        var totalSucceeded = 0;
        var totalFailed = 0;
        var totalBytes = 0L;
        var overallSw = System.Diagnostics.Stopwatch.StartNew();

        var segmentsToCheck = segmentUrls.Take(3).ToList();
        foreach (var segUrl in segmentsToCheck)
        {
            try
            {
                var data = await fetcher.DownloadSegmentAsync(segUrl, 376, cancellationToken);
                totalBytes += data.Length;
                totalChecked++;

                if (TsValidator.ValidateSyncBytes(data))
                    totalSucceeded++;
                else
                    totalFailed++;
            }
            catch
            {
                totalChecked++;
                totalFailed++;
            }
        }

        return new StreamHealthResult
        {
            IsAlive = totalFailed == 0 && totalChecked > 0,
            HttpStatusCode = 200,
            BytesReceived = totalBytes,
            SegmentsChecked = totalChecked,
            SegmentsSucceeded = totalSucceeded,
            SegmentsFailed = totalFailed,
            ManifestValid = true,
            HasMediaSegments = totalChecked > 0,
            IsLivePlaylist = true,
            ResponseTime = overallSw.Elapsed,
            CheckedAt = DateTime.UtcNow,
        };
    }
}
