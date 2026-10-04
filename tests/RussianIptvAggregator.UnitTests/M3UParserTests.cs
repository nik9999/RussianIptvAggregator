using FluentAssertions;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Infrastructure.Parsing;
using Xunit;

namespace RussianIptvAggregator.UnitTests;

public class M3UParserTests
{
    private readonly M3UParser _parser;

    public M3UParserTests()
    {
        _parser = new M3UParser();
    }

    [Fact]
    public async Task ParseAsync_WithValidM3U_ReturnsChannels()
    {
        var m3uContent = BuildM3U(
            ("Channel1", "Первый канал", "https://example.com/logo1.png", "rus", "Россия", "Первый канал", "http://example.com/stream1.m3u8"),
            ("Channel2", "Россия 1", "https://example.com/logo2.png", "rus", "Россия", "Россия 1", "http://example.com/stream2.m3u8")
        );

        var channels = await _parser.ParseAsync(m3uContent, "https://example.com/playlist.m3u");

        channels.Should().HaveCount(2);
        channels[0].Name.Should().Be("Первый канал");
        channels[0].TvgId.Should().Be("Channel1");
        channels[0].TvgName.Should().Be("Первый канал");
        channels[0].TvgLogo.Should().Be("https://example.com/logo1.png");
        channels[0].TvgLanguage.Should().Be("rus");
        channels[0].GroupTitle.Should().Be("Россия");
        channels[0].StreamUrl.Should().Be("http://example.com/stream1.m3u8");

        channels[1].Name.Should().Be("Россия 1");
        channels[1].StreamUrl.Should().Be("http://example.com/stream2.m3u8");
    }

    [Fact]
    public async Task ParseAsync_WithRelativeUrl_ResolvesToAbsolute()
    {
        var m3uContent = BuildM3U(
            (null, "Test Channel", null, "rus", "Test", "Test Channel", "streams/test.m3u8")
        );

        var channels = await _parser.ParseAsync(m3uContent, "https://example.com/playlists/playlist.m3u");

        channels.Should().HaveCount(1);
        channels[0].StreamUrl.Should().Be("https://example.com/playlists/streams/test.m3u8");
    }

    [Fact]
    public async Task ParseAsync_WithAbsoluteUrl_KeepsUrlAsIs()
    {
        var m3uContent = BuildM3U(
            (null, "Test Channel", null, "rus", "Test", "Test Channel", "https://cdn.example.com/live/stream.m3u8")
        );

        var channels = await _parser.ParseAsync(m3uContent, "https://example.com/playlist.m3u");

        channels.Should().HaveCount(1);
        channels[0].StreamUrl.Should().Be("https://cdn.example.com/live/stream.m3u8");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyContent_ReturnsEmptyList()
    {
        var channels = await _parser.ParseAsync("", "https://example.com/playlist.m3u");
        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithInvalidM3U_ThrowsException()
    {
        Exception? caughtException = null;
        try
        {
            await _parser.ParseAsync("#SOME_INVALID_TAG", "https://example.com/playlist.m3u");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        caughtException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ParseAsync_WithChannelNameAfterComma_ExtractsName()
    {
        var m3uContent = BuildM3U(
            (null, "ChannelName", null, "rus", "News", "Channel Name", "http://example.com/stream.m3u8")
        );

        var channels = await _parser.ParseAsync(m3uContent, "https://example.com/playlist.m3u");

        channels.Should().HaveCount(1);
        channels[0].Name.Should().Be("Channel Name");
    }

    [Fact]
    public async Task ParseAsync_WithNoComma_UsesEntireLineAsName()
    {
        var m3uContent = BuildM3U(
            (null, null, null, "rus", "Test", "", "http://example.com/stream.m3u8")
        );

        var channels = await _parser.ParseAsync(m3uContent, "https://example.com/playlist.m3u");

        channels.Should().HaveCount(1);
        channels[0].Name.Should().BeEmpty();
    }

    private static string BuildM3U(params (string? tvgId, string? tvgName, string? tvgLogo, string? tvgLang, string? groupTitle, string name, string url)[] entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#EXTM3U");
        foreach (var (tvgId, tvgName, tvgLogo, tvgLang, groupTitle, name, url) in entries)
        {
            sb.Append("#EXTINF:-1");
            if (!string.IsNullOrEmpty(tvgId)) sb.Append($" tvg-id=\"{tvgId}\"");
            if (!string.IsNullOrEmpty(tvgName)) sb.Append($" tvg-name=\"{tvgName}\"");
            if (!string.IsNullOrEmpty(tvgLogo)) sb.Append($" tvg-logo=\"{tvgLogo}\"");
            if (!string.IsNullOrEmpty(tvgLang)) sb.Append($" tvg-language=\"{tvgLang}\"");
            if (!string.IsNullOrEmpty(groupTitle)) sb.Append($" group-title=\"{groupTitle}\"");
            sb.Append($",{name}");
            sb.AppendLine();
            sb.AppendLine(url);
        }
        return sb.ToString();
    }
}
