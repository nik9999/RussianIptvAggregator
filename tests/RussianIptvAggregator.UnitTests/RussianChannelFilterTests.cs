using FluentAssertions;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Application.Services;
using Xunit;

namespace RussianIptvAggregator.UnitTests;

public class RussianChannelFilterTests
{
    [Fact]
    public void Filter_WithRussianLanguageAttribute_ReturnsChannel()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Первый канал", TvgLanguage = "rus", StreamUrl = "http://example.com/1" },
            new() { Name = "BBC News", TvgLanguage = "eng", StreamUrl = "http://example.com/2" },
            new() { Name = "Россия 1", TvgLanguage = "ru", StreamUrl = "http://example.com/3" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Первый канал");
        result.Should().Contain(c => c.Name == "Россия 1");
        result.Should().NotContain(c => c.Name == "BBC News");
    }

    [Fact]
    public void Filter_WithNoRussianIndicators_ReturnsEmpty()
    {
        // Arrange — no tvg-country, no tvg-language ru/rus, no Cyrillic in group-title or name
        var channels = new List<Channel>
        {
            new() { Name = "CNN", TvgLanguage = "eng", TvgCountry = null, GroupTitle = "News", StreamUrl = "http://example.com/1" },
            new() { Name = "Al Jazeera", TvgLanguage = "ara", TvgCountry = null, GroupTitle = "News", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_WithNoRussianChannels_ReturnsEmpty()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "CNN", TvgLanguage = "eng", StreamUrl = "http://example.com/1" },
            new() { Name = "Al Jazeera", TvgLanguage = "ara", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_WithNullInput_ReturnsEmpty()
    {
        // Act
        var result = RussianChannelFilter.Filter((IEnumerable<Channel>)null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_WithEmptyList_ReturnsEmpty()
    {
        // Act
        var result = RussianChannelFilter.Filter([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Filter_CaseInsensitiveLanguageMatch()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Test", TvgLanguage = "RUS", StreamUrl = "http://example.com/1" },
            new() { Name = "Test2", TvgLanguage = "RU", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_WithTvgCountryRU_ReturnsChannel()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Salyam", TvgCountry = "RU", StreamUrl = "http://example.com/1" },
            new() { Name = "Foreign", TvgCountry = "US", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Salyam");
    }

    [Fact]
    public void Filter_WithTvgIdRuDomain_ReturnsChannel()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Salyam", TvgId = "Salyam.ru@SD", StreamUrl = "http://example.com/1" },
            new() { Name = "Foreign", TvgId = "Foreign.tv@SD", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Salyam");
    }

    [Fact]
    public void Filter_WithGroupTitleCyrillic_ReturnsChannel()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Some Channel", TvgLanguage = "eng", GroupTitle = "Россия", StreamUrl = "http://example.com/1" },
            new() { Name = "Other", TvgLanguage = "eng", GroupTitle = "World", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Some Channel");
    }

    [Fact]
    public void Filter_WithNameCyrillic_ReturnsChannel()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Матч ТВ", TvgLanguage = "eng", StreamUrl = "http://example.com/1" },
            new() { Name = "Eurosport", TvgLanguage = "eng", StreamUrl = "http://example.com/2" },
        };

        // Act
        var result = RussianChannelFilter.Filter(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Матч ТВ");
    }

    [Fact]
    public void Channel_IsRussian_Props_PriorityOrder()
    {
        // Priority 1: tvg-country="RU"
        var ch1 = new Channel { Name = "Test", TvgCountry = "RU" };
        ch1.IsRussian.Should().BeTrue("tvg-country=RU");

        // Priority 2: tvg-language="ru"/"rus"
        var ch2 = new Channel { Name = "Test", TvgLanguage = "ru" };
        ch2.IsRussian.Should().BeTrue("tvg-language=ru");

        // Priority 3: tvg-id contains .ru
        var ch3 = new Channel { Name = "Test", TvgId = "Salyam.ru@SD" };
        ch3.IsRussian.Should().BeTrue("tvg-id contains .ru");

        // Priority 3b: tvg-id contains .рф
        var ch3b = new Channel { Name = "Test", TvgId = "Канал.рф@HD" };
        ch3b.IsRussian.Should().BeTrue("tvg-id contains .рф");

        // Priority 4: group-title contains Cyrillic
        var ch4 = new Channel { Name = "Test", GroupTitle = "Россия" };
        ch4.IsRussian.Should().BeTrue("group-title has Cyrillic");

        // Priority 5: name contains Cyrillic
        var ch5 = new Channel { Name = "Матч" };
        ch5.IsRussian.Should().BeTrue("name has Cyrillic");

        // Not Russian
        var ch6 = new Channel { Name = "CNN", TvgLanguage = "eng", GroupTitle = "World" };
        ch6.IsRussian.Should().BeFalse("no Russian indicators");

        var ch7 = new Channel { Name = "Test", TvgLanguage = null, GroupTitle = null };
        ch7.IsRussian.Should().BeFalse("all null");
    }
}
