using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Application.Abstractions;

/// <summary>
/// Factory that creates the appropriate IStreamValidator based on the channel's stream URL protocol.
/// </summary>
public interface IStreamValidatorFactory
{
    /// <summary>
    /// Returns the appropriate validator for the given channel based on its stream URL.
    /// </summary>
    IStreamValidator GetValidator(Channel channel);

    /// <summary>
    /// Validates a channel using the protocol-appropriate validator.
    /// </summary>
    Task<StreamHealthResult> ValidateAsync(Channel channel, CancellationToken cancellationToken = default);
}
