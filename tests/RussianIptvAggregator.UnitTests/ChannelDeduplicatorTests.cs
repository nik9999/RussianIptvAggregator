using FluentAssertions;
using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Application.Services;
using Xunit;

namespace RussianIptvAggregator.UnitTests;

public class ChannelDeduplicatorTests
{
    [Fact]
    public void Deduplicate_RemovesDuplicateUrls()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Channel A", StreamUrl = "http://example.com/stream1" },
            new() { Name = "Channel B", StreamUrl = "http://example.com/stream2" },
            new() { Name = "Channel C (dup)", StreamUrl = "http://example.com/stream1" },
            new() { Name = "Channel D", StreamUrl = "http://example.com/stream3" },
            new() { Name = "Channel E (dup2)", StreamUrl = "http://example.com/stream2" },
        };

        // Act
        var result = ChannelDeduplicator.Deduplicate(channels);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Channel A");
        result[1].Name.Should().Be("Channel B");
        result[2].Name.Should().Be("Channel D");
    }

    [Fact]
    public void Deduplicate_KeepsFirstOccurrence()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "First", StreamUrl = "http://example.com/stream" },
            new() { Name = "Second", StreamUrl = "http://example.com/stream" },
        };

        // Act
        var result = ChannelDeduplicator.Deduplicate(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("First");
    }

    [Fact]
    public void Deduplicate_CaseInsensitiveUrlComparison()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Channel A", StreamUrl = "http://example.com/Stream" },
            new() { Name = "Channel B", StreamUrl = "http://example.com/stream" },
        };

        // Act
        var result = ChannelDeduplicator.Deduplicate(channels);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Deduplicate_SkipsEmptyUrls()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "Channel A", StreamUrl = "http://example.com/stream1" },
            new() { Name = "Channel B", StreamUrl = "" },
            new() { Name = "Channel C", StreamUrl = null! },
            new() { Name = "Channel D", StreamUrl = "http://example.com/stream2" },
        };

        // Act
        var result = ChannelDeduplicator.Deduplicate(channels);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Channel A");
        result.Should().Contain(c => c.Name == "Channel D");
    }

    [Fact]
    public void Deduplicate_NullInput_ReturnsEmpty()
    {
        // Act
        var result = ChannelDeduplicator.Deduplicate(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = ChannelDeduplicator.Deduplicate([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_AllDuplicates_ReturnsSingle()
    {
        // Arrange
        var channels = new List<Channel>
        {
            new() { Name = "A", StreamUrl = "http://example.com/s" },
            new() { Name = "B", StreamUrl = "http://example.com/s" },
            new() { Name = "C", StreamUrl = "http://example.com/s" },
        };

        // Act
        var result = ChannelDeduplicator.Deduplicate(channels);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("A");
    }
}
