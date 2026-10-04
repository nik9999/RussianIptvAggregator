using RussianIptvAggregator.Domain.Channels;
using RussianIptvAggregator.Domain.Interfaces;
using RussianIptvAggregator.Domain.Streams;

namespace RussianIptvAggregator.Application.Services;

/// <summary>
/// Interface for the main aggregation service.
/// </summary>
public interface IPlaylistAggregatorService
{
    /// <summary>
    /// Runs the full aggregation pipeline.
    /// </summary>
    Task<AggregationResult> AggregateAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of the aggregation pipeline.
/// </summary>
public class AggregationResult
{
    public int PlaylistsProcessed { get; init; }
    public int TotalChannelsFound { get; init; }
    public int RussianChannelsFiltered { get; init; }
    public int ChannelsAfterDeduplication { get; init; }
    public int StreamsChecked { get; init; }
    public int AliveCount { get; init; }
    public int DegradedCount { get; init; }
    public int DeadCount { get; init; }
    public int InvalidCount { get; init; }
    public string OutputFilePath { get; set; } = string.Empty;
    public List<Channel> AllChannels { get; init; } = [];
}
