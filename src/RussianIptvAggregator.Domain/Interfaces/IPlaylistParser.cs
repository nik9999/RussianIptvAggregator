using RussianIptvAggregator.Domain.Channels;

namespace RussianIptvAggregator.Domain.Interfaces;

/// <summary>
/// Parses M3U/M3U8 playlist content into a list of channels.
/// </summary>
public interface IPlaylistParser
{
    /// <summary>
    /// Parses M3U content and returns a list of channels.
    /// Relative URLs are resolved against the provided base URL.
    /// </summary>
    Task<List<Channel>> ParseAsync(string content, string baseUrl, CancellationToken cancellationToken = default);
}
