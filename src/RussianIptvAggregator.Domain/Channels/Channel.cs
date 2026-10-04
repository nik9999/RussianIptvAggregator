using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Domain.Channels;

/// <summary>
/// Represents a single TV channel parsed from an M3U playlist.
/// </summary>
public class Channel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? TvgId { get; set; }
    public string? TvgName { get; set; }
    public string? TvgLogo { get; set; }
    public string? TvgLanguage { get; set; }
    public string? TvgCountry { get; set; }
    public string? GroupTitle { get; set; }
    public StreamHealthResult? HealthResult { get; set; }
    public string? SourcePlaylistName { get; set; }

    /// <summary>
    /// True if the channel is Russian: tvg-country="RU", tvg-language="ru"/"rus",
    /// tvg-id contains .ru/.рф, group-title contains Cyrillic, or name contains Cyrillic.
    /// </summary>
    public bool IsRussian
    {
        get
        {
            // Priority 1: tvg-country="RU"
            if (!string.IsNullOrWhiteSpace(TvgCountry))
            {
                var country = TvgCountry.Trim().ToUpperInvariant();
                if (country == "RU" || country == "RUS")
                    return true;
            }

            // Priority 2: tvg-language="ru"/"rus"
            if (!string.IsNullOrWhiteSpace(TvgLanguage))
            {
                var lang = TvgLanguage.Trim().ToLowerInvariant();
                if (lang == "rus" || lang == "ru")
                    return true;
            }

            // Priority 3: tvg-id contains .ru or .рф
            if (!string.IsNullOrWhiteSpace(TvgId))
            {
                var id = TvgId.ToLowerInvariant();
                if (id.Contains(".ru") || id.Contains(".рф"))
                    return true;
            }

            // Priority 4: group-title contains Cyrillic
            if (!string.IsNullOrWhiteSpace(GroupTitle))
            {
                foreach (var c in GroupTitle)
                {
                    if (c >= '\u0410' && c <= '\u044F') return true; // А-я
                    if (c == '\u0401' || c == '\u0451') return true; // Ё, ё
                }
            }

            // Priority 5: channel name contains Cyrillic
            if (!string.IsNullOrWhiteSpace(Name))
            {
                foreach (var c in Name)
                {
                    if (c >= '\u0410' && c <= '\u044F') return true; // А-я
                    if (c == '\u0401' || c == '\u0451') return true; // Ё, ё
                }
            }

            return false;
        }
    }

    public override string ToString() => Name;
}
