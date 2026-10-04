using System.Text.RegularExpressions;

namespace RussianIptvAggregator.Infrastructure.Validation.Parsing;

/// <summary>
/// Parses HLS master and media playlists.
/// </summary>
public static class HlsPlaylistParser
{
    private const string MasterPlaylistIndicator = "#EXT-X-STREAM-INF";
    private const string MediaSegmentIndicator = "#EXTINF:";

    /// <summary>
    /// Determines whether the content is a master playlist (contains #EXT-X-STREAM-INF).
    /// </summary>
    public static bool IsMasterPlaylist(string content)
    {
        return content.Contains(MasterPlaylistIndicator);
    }

    /// <summary>
    /// Parses a master playlist and returns variant stream URLs.
    /// </summary>
    public static List<HlsVariant> ParseMasterPlaylist(string content)
    {
        return ParseMasterPlaylist(content, null);
    }

    /// <summary>
    /// Parses a master playlist and resolves variant URLs against the given base URL.
    /// </summary>
    public static List<HlsVariant> ParseMasterPlaylist(string content, string? baseUrl)
    {
        var variants = new List<HlsVariant>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var baseUri = baseUrl != null ? EnsureAbsoluteUri(baseUrl) : null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith(MasterPlaylistIndicator))
                continue;

            // The media URL is on the next non-empty line
            string? mediaUrl = null;
            for (var j = i + 1; j < lines.Length; j++)
            {
                var nextLine = lines[j].Trim();
                if (!string.IsNullOrWhiteSpace(nextLine) && !nextLine.StartsWith("#"))
                {
                    mediaUrl = nextLine;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(mediaUrl))
            {
                // Resolve relative URLs against the master playlist base URL
                var resolvedUrl = baseUri != null ? ResolveUrl(mediaUrl, baseUri).ToString() : mediaUrl;

                variants.Add(new HlsVariant
                {
                    MediaUrl = resolvedUrl,
                    Bandwidth = ExtractBandwidth(line),
                });
            }
        }

        return variants;
    }

    /// <summary>
    /// Parses a media playlist and returns segment URLs.
    /// </summary>
    public static List<string> ParseMediaPlaylist(string content, string baseUrl)
    {
        var segments = new List<string>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var baseUri = EnsureAbsoluteUri(baseUrl);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith(MediaSegmentIndicator))
                continue;

            // The segment URL is on the next line
            if (i + 1 >= lines.Length) break;
            i++;

            var segUrl = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(segUrl) || segUrl.StartsWith("#"))
                continue;

            segments.Add(ResolveUrl(segUrl, baseUri).ToString());
        }

        return segments;
    }

    private static int ExtractBandwidth(string line)
    {
        var match = Regex.Match(line, @"BANDWIDTH\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }

    private static Uri ResolveUrl(string url, Uri baseUri)
    {
        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return new Uri(url);

        return new Uri(baseUri, url);
    }

    private static Uri EnsureAbsoluteUri(string baseUrl)
    {
        if (Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute))
            return new Uri(baseUrl);

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        return new Uri(baseUrl);
    }
}

/// <summary>
/// Represents a variant stream from an HLS master playlist.
/// </summary>
public class HlsVariant
{
    public string MediaUrl { get; init; } = string.Empty;
    public int Bandwidth { get; init; }
}
