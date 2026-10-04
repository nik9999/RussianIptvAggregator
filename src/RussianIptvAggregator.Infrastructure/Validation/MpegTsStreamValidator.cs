using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;
using RussianIptvAggregator.Infrastructure.Http;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;

namespace RussianIptvAggregator.Infrastructure.Validation;

/// <summary>
/// MPEG-TS stream validator that checks the sync byte (0x47) in the first bytes.
/// </summary>
public class MpegTsStreamValidator : IStreamValidator
{
    private readonly IStreamFetcher _fetcher;
    private readonly MpegTsPacketValidator _tsValidator;

    public MpegTsStreamValidator(
        IStreamFetcher fetcher,
        MpegTsPacketValidator? tsValidator = null)
    {
        _fetcher = fetcher;
        _tsValidator = tsValidator ?? new MpegTsPacketValidator();
    }

    public async Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var data = await _fetcher.DownloadSegmentAsync(channel.StreamUrl, 376, cancellationToken);
            var isValid = _tsValidator.ValidateSyncBytes(data);

            return new StreamHealthResult
            {
                IsAlive = isValid,
                Protocol = "MPEG-TS",
                HttpStatusCode = 200,
                ContentType = "video/mp2t",
                ResponseTime = stopwatch.Elapsed,
                BytesReceived = data.Length,
                SegmentsChecked = 1,
                SegmentsSucceeded = isValid ? 1 : 0,
                SegmentsFailed = isValid ? 0 : 1,
                ManifestValid = true,
                HasMediaSegments = true,
                CheckedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return StreamHealthResult.Failure("MPEG-TS", "Failed to download MPEG-TS segment", ex.Message, stopwatch.Elapsed);
        }
    }
}
