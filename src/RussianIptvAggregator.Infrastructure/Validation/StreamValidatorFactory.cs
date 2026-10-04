using RussianIptvAggregator.Application.Abstractions;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;

namespace RussianIptvAggregator.Infrastructure.Validation;

/// <summary>
/// Factory that creates the appropriate IStreamValidator based on the channel's stream URL protocol.
/// </summary>
public class StreamValidatorFactory : IStreamValidatorFactory
{
    private readonly IStreamFetcher _fetcher;
    private readonly MpegTsStreamValidator _mpegTsValidator;
    private readonly RtmpValidator _rtmpValidator;
    private readonly MpegTsPacketValidator _tsValidator;
    private readonly TimeSpan _manifestTimeout;
    private readonly TimeSpan _segmentTimeout;

    public StreamValidatorFactory(
        IStreamFetcher fetcher,
        MpegTsStreamValidator mpegTsValidator,
        RtmpValidator rtmpValidator,
        MpegTsPacketValidator tsValidator,
        TimeSpan? manifestTimeout = null,
        TimeSpan? segmentTimeout = null)
    {
        _fetcher = fetcher;
        _mpegTsValidator = mpegTsValidator;
        _rtmpValidator = rtmpValidator;
        _tsValidator = tsValidator;
        _manifestTimeout = manifestTimeout ?? TimeSpan.FromSeconds(10);
        _segmentTimeout = segmentTimeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Returns the appropriate validator for the given channel based on its stream URL.
    /// </summary>
    public IStreamValidator GetValidator(Channel channel)
    {
        var url = channel.StreamUrl.ToUpperInvariant();

        return url switch
        {
            var u when u.Contains(".MPD") || u.Contains("DASH") => new DashValidator(_fetcher, _tsValidator, _manifestTimeout, _segmentTimeout),
            var u when u.Contains(".TS") || u.Contains("MPEGTS") => _mpegTsValidator,
            var u when u.StartsWith("RTMP://") => _rtmpValidator,
            _ => new DelegatingValidator(ValidateHlsAsync),
        };
    }

    private async Task<StreamHealthResult> ValidateHlsAsync(Channel ch, CancellationToken ct)
        => await DeepHlsValidator.ValidateAsync(_fetcher, ch, _manifestTimeout, _segmentTimeout, ct);

    /// <summary>
    /// Validates a channel using the protocol-appropriate validator.
    /// For unknown protocols, attempts HLS first, then falls back to MPEG-TS.
    /// </summary>
    public async Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default)
    {
        var url = channel.StreamUrl.ToUpperInvariant();
        var isUnknownProtocol = !url.Contains(".M3U8")
                                && !url.Contains("M3U8")
                                && !url.Contains(".MPD")
                                && !url.Contains("DASH")
                                && !url.Contains(".TS")
                                && !url.Contains("MPEGTS")
                                && !url.StartsWith("RTMP://");

        if (isUnknownProtocol)
        {
            // Try HLS first
            var hlsResult = await DeepHlsValidator.ValidateAsync(_fetcher, channel, _manifestTimeout, _segmentTimeout, cancellationToken);
            if (hlsResult.IsAlive)
                return hlsResult;

            // Fallback to MPEG-TS
            var mpegTsResult = await _mpegTsValidator.ValidateAsync(channel, cancellationToken);
            return mpegTsResult;
        }

        var validator = GetValidator(channel);
        return await validator.ValidateAsync(channel, cancellationToken);
    }
}

/// <summary>
/// Wraps an async delegate as IStreamValidator.
/// </summary>
internal class DelegatingValidator : IStreamValidator
{
    private readonly Func<Channel, CancellationToken, Task<StreamHealthResult>> _validate;

    public DelegatingValidator(Func<Channel, CancellationToken, Task<StreamHealthResult>> validate)
    {
        _validate = validate;
    }

    public Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default)
        => _validate(channel, cancellationToken);
}
