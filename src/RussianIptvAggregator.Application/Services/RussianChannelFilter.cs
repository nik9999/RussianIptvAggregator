using RussianIptvAggregator.Domain.Channels;

namespace RussianIptvAggregator.Application.Services;

/// <summary>
/// Filters channels to keep only Russian-language ones.
/// </summary>
public static class RussianChannelFilter
{
    /// <summary>
    /// Filters a list of channels, keeping only those with IsRussian == true.
    /// </summary>
    public static List<Channel> Filter(IEnumerable<Channel> channels)
    {
        if (channels == null) return [];
        return channels.Where(c => c.IsRussian).ToList();
    }
}
