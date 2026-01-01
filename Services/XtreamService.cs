using System.Net.Http.Json;
using System.Text.Json;

namespace MAXTV.Services;

public class XtreamService
{
    private readonly HttpClient _httpClient;
    
    // Hardcoded credentials as requested
    private const string _username = "14167206030";
    private const string _password = "pluto6030";
    private const string _serverUrl = "http://ky-tv.cc:25461"; // Needs to be a valid URL
    
    private string _baseUrl;

    public XtreamService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Prevent infinite hanging
        _baseUrl = $"{_serverUrl}/player_api.php?username={_username}&password={_password}";
    }

    // Live TV URL: http://server:port/username/password/streamId.ts
    public string GetStreamUrl(int streamId, string extension = "ts")
    {
        return $"{_serverUrl}/{_username}/{_password}/{streamId}.{extension}";
    }

    // VOD (Movie) URL: http://server:port/movie/username/password/streamId.ext
    public string GetVodStreamUrl(int streamId, string extension = "mp4")
    {
        return $"{_serverUrl}/movie/{_username}/{_password}/{streamId}.{extension}";
    }

    // Series Episode URL: http://server:port/series/username/password/streamId.ext
    public string GetSeriesStreamUrl(int streamId, string extension = "mp4")
    {
        return $"{_serverUrl}/series/{_username}/{_password}/{streamId}.{extension}";
    }

    public async Task<bool> AuthenticateAsync()
    {
        return await GetAsync<XtreamLoginResponse>("")?.ContinueWith(t => t.Result?.UserInfo?.Status == "Active") ?? Task.FromResult(false);
    }

    public async Task<List<XtreamCategory>> GetLiveCategoriesAsync()
    {
        return await GetAsync<List<XtreamCategory>>("&action=get_live_categories");
    }

    public async Task<List<XtreamStream>> GetLiveStreamsAsync(string categoryId)
    {
        return await GetAsync<List<XtreamStream>>($"&action=get_live_streams&category_id={categoryId}");
    }

    public async Task<List<XtreamCategory>> GetVodCategoriesAsync()
    {
        return await GetAsync<List<XtreamCategory>>("&action=get_vod_categories");
    }

    public async Task<List<XtreamStream>> GetVodStreamsAsync(string categoryId)
    {
        return await GetAsync<List<XtreamStream>>($"&action=get_vod_streams&category_id={categoryId}");
    }

    public async Task<List<XtreamCategory>> GetSeriesCategoriesAsync()
    {
        return await GetAsync<List<XtreamCategory>>("&action=get_series_categories");
    }

    public async Task<List<XtreamSeries>> GetSeriesAsync(string categoryId)
    {
        return await GetAsync<List<XtreamSeries>>($"&action=get_series&category_id={categoryId}");
    }

    public async Task<XtreamSeriesDetails> GetSeriesInfoAsync(int seriesId)
    {
        return await GetAsync<XtreamSeriesDetails>($"&action=get_series_info&series_id={seriesId}");
    }

    private async Task<T> GetAsync<T>(string actionParams)
    {
        try
        {
            var url = $"{_baseUrl}{actionParams}";
            System.Diagnostics.Debug.WriteLine($"[XtreamService] Requesting: {url}");

            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Debug log (truncated)
                if (content.Length > 200)
                    System.Diagnostics.Debug.WriteLine($"[XtreamService] Response (first 200): {content.Substring(0, 200)}...");
                else
                    System.Diagnostics.Debug.WriteLine($"[XtreamService] Response: {content}");

                try
                {
                    return JsonSerializer.Deserialize<T>(content);
                }
                catch (JsonException jex)
                {
                    System.Diagnostics.Debug.WriteLine($"[XtreamService] JSON Error: {jex.Message}");
                    // Sometimes API returns [] instead of {} on empty/error
                    return default;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[XtreamService] HTTP Error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[XtreamService] General Error: {ex.Message}");
        }
        return default;
    }
}
