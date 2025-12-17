using System.Text.Json.Serialization;

namespace MAXTV.Services;

public class XtreamServerInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("port")]
    public string? Port { get; set; }

    [JsonPropertyName("https_port")]
    public string? HttpsPort { get; set; }

    [JsonPropertyName("server_protocol")]
    public string? ServerProtocol { get; set; }
}

public class XtreamUserInfo
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class XtreamLoginResponse
{
    [JsonPropertyName("user_info")]
    public XtreamUserInfo? UserInfo { get; set; }

    [JsonPropertyName("server_info")]
    public XtreamServerInfo? ServerInfo { get; set; }
}

public class XtreamCategory
{
    [JsonPropertyName("category_id")]
    public string? CategoryId { get; set; }

    [JsonPropertyName("category_name")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("parent_id")]
    public int ParentId { get; set; }
}

public class XtreamStream
{
    [JsonPropertyName("num")]
    public object? Num { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("stream_type")]
    public string? StreamType { get; set; }

    [JsonPropertyName("stream_id")]
    public int StreamId { get; set; }

    [JsonPropertyName("stream_icon")]
    public string? StreamIcon { get; set; }

    [JsonPropertyName("rating")]
    public object? Rating { get; set; }
    
    [JsonPropertyName("added")]
    public string? Added { get; set; }
    
    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; set; }
}

public class XtreamSeries
{
    [JsonPropertyName("num")]
    public object? Num { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("series_id")]
    public int SeriesId { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("plot")]
    public string? Plot { get; set; }

    [JsonPropertyName("cast")]
    public string? Cast { get; set; }

    [JsonPropertyName("director")]
    public string? Director { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }
    
    [JsonPropertyName("rating")]
    public string? Rating { get; set; }
}

public class XtreamEpisode
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("episode_num")]
    public object? EpisodeNum { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("container_extension")]
    public string? ContainerExtension { get; set; }

    [JsonPropertyName("info")]
    public object? Info { get; set; }
    
    [JsonPropertyName("custom_sid")]
    public object? CustomSid { get; set; }
    
    [JsonPropertyName("added")]
    public string? Added { get; set; }
    
    [JsonPropertyName("season")]
    public int Season { get; set; }
    
    [JsonPropertyName("direct_source")]
    public string? DirectSource { get; set; }
}

public class XtreamSeriesDetails
{
    [JsonPropertyName("episodes")]
    public Dictionary<string, List<XtreamEpisode>>? Episodes { get; set; }
}
