namespace RussianIptvAggregator.Infrastructure.Validation.Parsing;

/// <summary>
/// Validates MPEG-TS packet sync bytes.
/// MPEG-TS packets start with a fixed sync byte 0x47 at every 188 bytes.
/// </summary>
public class MpegTsPacketValidator
{
    private const byte TsSyncByte = 0x47;
    private const int TsPacketSize = 188;

    /// <summary>
    /// Validates that the data contains valid MPEG-TS sync bytes at positions 0 and 188.
    /// Returns true if both positions contain the sync byte 0x47.
    /// </summary>
    public bool ValidateSyncBytes(byte[] data)
    {
        if (data == null || data.Length < TsPacketSize * 2)
            return false;

        return data[0] == TsSyncByte && data[TsPacketSize] == TsSyncByte;
    }

    /// <summary>
    /// Counts the number of valid MPEG-TS packets in the data.
    /// </summary>
    public int CountValidPackets(byte[] data)
    {
        if (data == null || data.Length < TsPacketSize)
            return 0;

        var count = 0;
        for (var i = 0; i + TsPacketSize <= data.Length; i += TsPacketSize)
        {
            if (data[i] == TsSyncByte)
                count++;
        }

        return count;
    }
}
