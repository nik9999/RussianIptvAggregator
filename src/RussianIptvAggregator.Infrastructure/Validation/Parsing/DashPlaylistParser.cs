using System.Text.RegularExpressions;

namespace RussianIptvAggregator.Infrastructure.Validation.Parsing;

/// <summary>
/// Parses DASH MPD manifests to extract media segment URLs.
/// </summary>
public static class DashPlaylistParser
{
    private static readonly Regex SegmentUrlPattern = new(
        @"<BaseURL[^>]*>(.*?)</BaseURL>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts segment URLs from a DASH MPD manifest content.
    /// </summary>
    public static List<string> ExtractSegmentUrls(string mpdContent, string baseUrl)
    {
        var segments = new List<string>();
        var baseUri = EnsureAbsoluteUri(baseUrl);

        // Find all BaseURL elements
        var baseUrlMatches = SegmentUrlPattern.Matches(mpdContent);
        foreach (Match match in baseUrlMatches)
        {
            var url = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                segments.Add(ResolveUrl(url, baseUri).ToString());
            }
        }

        // Also look for Template patterns with $Number$ or $Time$
        var templatePattern = new Regex(
            @"<SegmentTemplate\s+media=\""(.*?)\""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var templateMatches = templatePattern.Matches(mpdContent);
        foreach (Match match in templateMatches)
        {
            var template = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(template) && template.Contains('$'))
            {
                // Generate placeholder URLs for the template
                for (var i = 1; i <= 3; i++)
                {
                    var resolved = template.Replace("$Number$", i.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                           .Replace("$Time$", (i * 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    segments.Add(ResolveUrl(resolved, baseUri).ToString());
                }
            }
        }

        return segments;
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
