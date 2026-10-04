using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;
using RussianIptvAggregator.Infrastructure.Http;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;

namespace RussianIptvAggregator.Infrastructure.Validation;

/// <summary>
/// DASH stream validator that downloads MPD manifest and checks the first media segment.
/// </summary>
public class DashValidator : IStreamValidator
{
    private readonly IStreamFetcher _fetcher;
    private readonly MpegTsPacketValidator _tsValidator;
    private readonly TimeSpan _manifestTimeout;
    private readonly TimeSpan _segmentTimeout;

    public DashValidator(
        IStreamFetcher fetcher,
        MpegTsPacketValidator? tsValidator = null,
        TimeSpan? manifestTimeout = null,
        TimeSpan? segmentTimeout = null)
    {
        _fetcher = fetcher;
        _tsValidator = tsValidator ?? new MpegTsPacketValidator();
        _manifestTimeout = manifestTimeout ?? TimeSpan.FromSeconds(10);
        _segmentTimeout = segmentTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var mpdContent = await _fetcher.DownloadTextAsync(channel.StreamUrl, cancellationToken);
            var manifestTime = stopwatch.Elapsed;

            if (string.IsNullOrWhiteSpace(mpdContent))
                return StreamHealthResult.Failure("DASH", "Empty MPD manifest", null, manifestTime);

            var segmentUrls = DashPlaylistParser.ExtractSegmentUrls(mpdContent, channel.StreamUrl);

            if (segmentUrls.Count == 0)
                return StreamHealthResult.Failure("DASH", "No media segments found in MPD", null, manifestTime);

            // Check the first segment
            var data = await _fetcher.DownloadSegmentAsync(segmentUrls[0], 376, cancellationToken);
            var segmentTime = stopwatch.Elapsed - manifestTime;

            var isValid = _tsValidator.ValidateSyncBytes(data);

            return new StreamHealthResult
            {
                IsAlive = isValid,
                Protocol = "DASH",
                HttpStatusCode = 200,
                ContentType = "application/dash+xml",
                ResponseTime = manifestTime + segmentTime,
                BytesReceived = data.Length,
                SegmentsChecked = 1,
                SegmentsSucceeded = isValid ? 1 : 0,
                SegmentsFailed = isValid ? 0 : 1,
                ManifestValid = true,
                HasMediaSegments = true,
                IsLivePlaylist = mpdContent.Contains("<Live>", StringComparison.OrdinalIgnoreCase),
                CheckedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return StreamHealthResult.Failure("DASH", "Failed to validate DASH stream", ex.Message, stopwatch.Elapsed);
        }
    }
}
