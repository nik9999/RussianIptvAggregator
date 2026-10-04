using RussianIptvAggregator.Domain.Channels;

namespace RussianIptvAggregator.Domain.Interfaces;

/// <summary>
/// Fetches remote content (playlists, streams) over HTTP.
/// </summary>
public interface IStreamFetcher
{
    /// <summary>
    /// Downloads text content from the specified URL.
    /// Uses streaming (ResponseHeadersRead) to minimize memory usage.
    /// </summary>
    Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a binary segment from the specified URL.
    /// Returns the first <paramref name="maxBytes"/> bytes.
    /// </summary>
    Task<byte[]> DownloadSegmentAsync(string url, int maxBytes, CancellationToken cancellationToken = default);
}
