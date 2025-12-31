anmeusing System.Net.Http.Json;
using System.Text.Json;

namespace MAXTV.Services;

public class XtreamService
{
    private readonly HttpClient _httpClient;
    
    // Hardcoded credentials as requested
    private const string _username = "username";
    private const string _password = "password";
    private const string _serverUrl = "http://your server"; // Needs to be a valid URL
    
    private string _baseUrl;

    public XtreamService()
    {
        _httpClient = new HttpClient();
        // Construct the base API URL: http://server:port/player_api.php?username=...&password=...
        // Note: Real implementations should handle DNS resolution and ports more robustly.
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
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl);
            if (response.IsSuccessStatusCode)
            {
                var loginData = await response.Content.ReadFromJsonAsync<XtreamLoginResponse>();
                return loginData?.UserInfo?.Status == "Active";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Auth Error: {ex.Message}");
        }
        return false;
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
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
        }
        return default;
    }
}
