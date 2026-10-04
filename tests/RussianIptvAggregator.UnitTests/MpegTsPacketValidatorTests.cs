using FluentAssertions;
using RussianIptvAggregator.Infrastructure.Validation;
using RussianIptvAggregator.Infrastructure.Validation.Parsing;
using Xunit;

namespace RussianIptvAggregator.UnitTests;

public class MpegTsPacketValidatorTests
{
    private readonly MpegTsPacketValidator _validator;

    public MpegTsPacketValidatorTests()
    {
        _validator = new MpegTsPacketValidator();
    }

    [Fact]
    public void ValidateSyncBytes_WithValidTsData_ReturnsTrue()
    {
        // Arrange: Create data with sync byte 0x47 at positions 0 and 188
        var data = new byte[376];
        data[0] = 0x47;
        data[188] = 0x47;
        // Fill rest with random data (skip positions 0 and 188)
        new Random(42).NextBytes(data.AsSpan(1, 187));
        new Random(43).NextBytes(data.AsSpan(189));

        // Act
        var result = _validator.ValidateSyncBytes(data);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateSyncBytes_WithMissingFirstSyncByte_ReturnsFalse()
    {
        // Arrange
        var data = new byte[376];
        data[0] = 0x00; // Wrong sync byte
        data[188] = 0x47;

        // Act
        var result = _validator.ValidateSyncBytes(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSyncBytes_WithMissingSecondSyncByte_ReturnsFalse()
    {
        // Arrange
        var data = new byte[376];
        data[0] = 0x47;
        data[188] = 0x00; // Wrong sync byte

        // Act
        var result = _validator.ValidateSyncBytes(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSyncBytes_WithNullData_ReturnsFalse()
    {
        // Act
        var result = _validator.ValidateSyncBytes(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateSyncBytes_WithTooShortData_ReturnsFalse()
    {
        // Arrange
        var data = new byte[100];
        data[0] = 0x47;

        // Act
        var result = _validator.ValidateSyncBytes(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CountValidPackets_WithValidTsData_ReturnsCorrectCount()
    {
        // Arrange: Create 5 valid TS packets
        var data = new byte[188 * 5];
        for (var i = 0; i < 5; i++)
        {
            data[i * 188] = 0x47;
        }

        // Act
        var result = _validator.CountValidPackets(data);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void CountValidPackets_WithSomeInvalidPackets_ReturnsPartialCount()
    {
        // Arrange: 5 packets, but 2nd and 4th have wrong sync byte
        var data = new byte[188 * 5];
        data[0] = 0x47;
        data[188 * 1] = 0x00; // Invalid
        data[188 * 2] = 0x47;
        data[188 * 3] = 0x00; // Invalid
        data[188 * 4] = 0x47;

        // Act
        var result = _validator.CountValidPackets(data);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void CountValidPackets_WithNullData_ReturnsZero()
    {
        // Act
        var result = _validator.CountValidPackets(null!);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountValidPackets_WithEmptyData_ReturnsZero()
    {
        // Act
        var result = _validator.CountValidPackets([]);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CountValidPackets_WithLessThanOnePacket_ReturnsZero()
    {
        // Arrange
        var data = new byte[100];
        data[0] = 0x47;

        // Act
        var result = _validator.CountValidPackets(data);

        // Assert
        result.Should().Be(0);
    }
}
