using RussianIptvAggregator.Domain.Channels;

namespace RussianIptvAggregator.Application.Services;

/// <summary>
/// Exports a list of channels to an M3U playlist file.
/// Only includes channels with IsAlive == true in their HealthResult.
/// Channels are sorted by group-title, then by name.
/// </summary>
public static class M3UExporter
{
    /// <summary>
    /// Exports alive channels to an M3U file with UTF-8 BOM encoding.
    /// </summary>
    public static void ExportToFile(
        List<Channel> channels,
        string outputPath,
        bool sortByGroup = true)
    {
        // Filter only alive channels
        var aliveChannels = channels
            .Where(c => c.HealthResult?.IsAlive == true)
            .ToList();

        // Sort
        if (sortByGroup)
        {
            aliveChannels.Sort((a, b) =>
            {
                var groupA = a.GroupTitle ?? string.Empty;
                var groupB = b.GroupTitle ?? string.Empty;
                var groupCompare = string.Compare(groupA, groupB, StringComparison.Ordinal);
                if (groupCompare != 0) return groupCompare;
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
        }
        else
        {
            aliveChannels.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Write M3U with BOM
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(bom, 0, bom.Length);

        using var sw = new StreamWriter(fs, new System.Text.UTF8Encoding(false));

        sw.WriteLine("#EXTM3U");

        foreach (var channel in aliveChannels)
        {
            sw.WriteLine(CreateExtInfLine(channel));
            sw.WriteLine(channel.StreamUrl);
        }

        sw.Flush();
    }

    /// <summary>
    /// Creates the #EXTINF line for a channel.
    /// </summary>
    private static string CreateExtInfLine(Channel channel)
    {
        var tvgId = string.IsNullOrEmpty(channel.TvgId) ? string.Empty : $"tvg-id=\"{EscapeAttribute(channel.TvgId)}\"";
        var tvgName = string.IsNullOrEmpty(channel.TvgName) ? string.Empty : $"tvg-name=\"{EscapeAttribute(channel.TvgName)}\"";
        var tvgLogo = string.IsNullOrEmpty(channel.TvgLogo) ? string.Empty : $"tvg-logo=\"{EscapeAttribute(channel.TvgLogo)}\"";
        var tvgLang = string.IsNullOrEmpty(channel.TvgLanguage) ? "rus" : $"tvg-language=\"{EscapeAttribute(channel.TvgLanguage)}\"";
        var groupTitle = string.IsNullOrEmpty(channel.GroupTitle) ? string.Empty : $"group-title=\"{EscapeAttribute(channel.GroupTitle)}\"";

        var attributes = new[] { tvgId, tvgName, tvgLogo, tvgLang, groupTitle }
            .Where(a => !string.IsNullOrEmpty(a))
            .Aggregate((a, b) => $"{a} {b}");

        return $"#EXTINF:-1 {attributes},{channel.Name}";
    }

    private static string EscapeAttribute(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
