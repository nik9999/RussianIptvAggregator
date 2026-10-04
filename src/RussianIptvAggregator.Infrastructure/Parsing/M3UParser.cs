using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;

namespace RussianIptvAggregator.Infrastructure.Parsing;

/// <summary>
/// Parses M3U/M3U8 playlist content into a list of Channel objects.
/// Only includes Russian channels (tvg-country="RU" or tvg-language="ru"/"rus").
/// </summary>
public class M3UParser : IPlaylistParser
{
    private const string ExtInfTag = "#EXTINF:";
    private const string ExtM3uTag = "#EXTM3U";
    private const string AttributeSeparator = ",";
    private const string TvgIdAttr = "tvg-id=";
    private const string TvgNameAttr = "tvg-name=";
    private const string TvgLogoAttr = "tvg-logo=";
    private const string TvgLanguageAttr = "tvg-language=";
    private const string TvgCountryAttr = "tvg-country=";
    private const string GroupTitleAttr = "group-title=";

    public async Task<List<Channel>> ParseAsync(string content, string baseUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        // Strip UTF-8 BOM if present
        var trimmed = content.TrimStart('\uFEFF');

        // Find #EXTM3U header — it may be on the first non-empty line
        var firstLineIndex = FindFirstNonEmptyLine(trimmed);
        if (firstLineIndex < 0 || !trimmed.Substring(firstLineIndex).StartsWith(ExtM3uTag, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid M3U file: missing #EXTM3U header.");

        // Split only from the #EXTM3U line onwards
        var playlistContent = trimmed.Substring(firstLineIndex);
        var lines = playlistContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var channels = new List<Channel>();
        var baseUri = EnsureAbsoluteUri(baseUrl);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith(ExtInfTag, StringComparison.OrdinalIgnoreCase))
                continue;

            // The URL is on the next line
            i++;
            if (i >= lines.Length) break;

            var streamUrl = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(streamUrl) || streamUrl.StartsWith("#"))
                continue;

            var channel = ParseExtInf(line, streamUrl, baseUri);
            channel.SourcePlaylistName = baseUrl;

            // Only include Russian channels (tvg-country="RU" or tvg-language="ru"/"rus")
            if (channel.IsRussian)
                channels.Add(channel);
        }

        return channels;
    }

    private static int FindFirstNonEmptyLine(string content)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                return i;
        }
        return -1;
    }

    private static Channel ParseExtInf(string extInfLine, string streamUrl, Uri baseUri)
    {
        var channel = new Channel
        {
            StreamUrl = ResolveUrl(streamUrl, baseUri).ToString(),
        };

        // Extract attributes from the EXTINF line
        // Format: #EXTINF:-1 tvg-id="..." tvg-name="..." tvg-logo="..." tvg-language="..." group-title="...",Channel Name
        var afterExtInf = extInfLine.Substring(ExtInfTag.Length);

        channel.TvgId = ExtractAttribute(afterExtInf, TvgIdAttr);
        channel.TvgName = ExtractAttribute(afterExtInf, TvgNameAttr);
        channel.TvgLogo = ExtractAttribute(afterExtInf, TvgLogoAttr);
        channel.TvgLanguage = ExtractAttribute(afterExtInf, TvgLanguageAttr);
        channel.TvgCountry = ExtractAttribute(afterExtInf, TvgCountryAttr);
        channel.GroupTitle = ExtractAttribute(afterExtInf, GroupTitleAttr);

        // Channel name is after the FIRST comma (separates attributes from name)
        var firstCommaIndex = afterExtInf.IndexOf(AttributeSeparator, StringComparison.Ordinal);
        if (firstCommaIndex >= 0)
        {
            channel.Name = afterExtInf.Substring(firstCommaIndex + 1).Trim();
        }
        else
        {
            channel.Name = afterExtInf.Trim();
        }

        return channel;
    }

    private static string? ExtractAttribute(string text, string attributeName)
    {
        // Search for attribute name (e.g., "tvg-id=") followed by a quote
        var startIndex = text.IndexOf(attributeName, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return null;

        var valueStart = startIndex + attributeName.Length;
        if (valueStart >= text.Length) return null;

        // Skip whitespace between attribute name and value
        while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
            valueStart++;

        if (valueStart >= text.Length) return null;

        var quoteChar = text[valueStart];
        if (quoteChar != '"' && quoteChar != '\'') return null;

        var endQuoteIndex = text.IndexOf(quoteChar, valueStart + 1);
        if (endQuoteIndex < 0) return null;

        return text.Substring(valueStart + 1, endQuoteIndex - valueStart - 1);
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

        // If it's a directory URL, ensure it ends with /
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";

        return new Uri(baseUrl);
    }
}
