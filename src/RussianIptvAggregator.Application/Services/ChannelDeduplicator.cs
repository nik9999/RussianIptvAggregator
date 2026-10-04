using RussianIptvAggregator.Domain.Channels;

namespace RussianIptvAggregator.Application.Services;

/// <summary>
/// Removes duplicate channels based on exact stream URL match.
/// Keeps the first occurrence.
/// </summary>
public static class ChannelDeduplicator
{
    /// <summary>
    /// Removes duplicates by stream URL, keeping the first occurrence of each URL.
    /// </summary>
    public static List<Channel> Deduplicate(IEnumerable<Channel> channels)
    {
        if (channels == null) return [];

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Channel>();

        foreach (var channel in channels)
        {
            if (string.IsNullOrWhiteSpace(channel.StreamUrl))
                continue;

            if (seenUrls.Add(channel.StreamUrl))
            {
                result.Add(channel);
            }
        }

        return result;
    }
}
