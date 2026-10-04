namespace RussianIptvAggregator.Domain.Streams;

/// <summary>
/// Represents the detected stream protocol.
/// </summary>
public enum StreamProtocol
{
    Unknown,
    Hls,
    Dash,
    MpegTs,
    Rtmp
}

/// <summary>
/// Health check result for a stream.
/// </summary>
public record StreamHealthResult
{
    public bool IsAlive { get; set; }
    public string? Protocol { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ContentType { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public long BytesReceived { get; set; }
    public int SegmentsChecked { get; set; }
    public int SegmentsSucceeded { get; set; }
    public int SegmentsFailed { get; set; }
    public bool ManifestValid { get; set; }
    public bool HasMediaSegments { get; set; }
    public bool IsLivePlaylist { get; set; }
    public string? FailureReason { get; set; }
    public string? Error { get; set; }
    public DateTime CheckedAt { get; set; }

    /// <summary>
    /// True when some segments passed but not all.
    /// </summary>
    public bool IsDegraded =>
        SegmentsChecked > 0
        && SegmentsSucceeded > 0
        && SegmentsSucceeded < SegmentsChecked;

    /// <summary>
    /// Creates a successful health check result.
    /// </summary>
    public static StreamHealthResult Success(
        string protocol,
        int? httpStatusCode = null,
        string? contentType = null,
        TimeSpan? responseTime = null,
        long bytesReceived = 0,
        int segmentsChecked = 0,
        int segmentsSucceeded = 0,
        bool manifestValid = true,
        bool hasMediaSegments = true,
        bool isLivePlaylist = true)
    {
        return new StreamHealthResult
        {
            IsAlive = true,
            Protocol = protocol,
            HttpStatusCode = httpStatusCode,
            ContentType = contentType,
            ResponseTime = responseTime ?? TimeSpan.Zero,
            BytesReceived = bytesReceived,
            SegmentsChecked = segmentsChecked,
            SegmentsSucceeded = segmentsSucceeded,
            SegmentsFailed = 0,
            ManifestValid = manifestValid,
            HasMediaSegments = hasMediaSegments,
            IsLivePlaylist = isLivePlaylist,
            CheckedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates a failed health check result.
    /// </summary>
    public static StreamHealthResult Failure(
        string? protocol = null,
        string? failureReason = null,
        string? error = null,
        TimeSpan? responseTime = null)
    {
        return new StreamHealthResult
        {
            IsAlive = false,
            Protocol = protocol,
            FailureReason = failureReason,
            Error = error,
            ResponseTime = responseTime ?? TimeSpan.Zero,
            CheckedAt = DateTime.UtcNow,
        };
    }
}
