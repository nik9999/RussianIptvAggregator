namespace RussianIptvAggregator.Domain.Playlists;

/// <summary>
/// Represents a source playlist URL configuration.
/// </summary>
public class PlaylistSource
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Configuration container for playlist sources.
/// </summary>
public class PlaylistSourcesConfig
{
    public List<PlaylistSource> Playlists { get; set; } = [];
}
