using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Domain.Interfaces;

/// <summary>
/// Validates the health of a stream identified by a channel.
/// </summary>
public interface IStreamValidator
{
    /// <summary>
    /// Checks whether the stream is alive and healthy.
    /// </summary>
    Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default);
}
