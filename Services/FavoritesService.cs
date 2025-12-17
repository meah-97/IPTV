using System.Text.Json;

namespace MAXTV.Services;

public class FavoritesService
{
    private const string KeyLive = "fav_live";
    private const string KeyMovies = "fav_movies";
    private const string KeySeries = "fav_series";

    public List<string> GetFavorites(string type)
    {
        string key = GetKey(type);
        string json = Preferences.Get(key, "[]");
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    public void ToggleFavorite(string type, string id)
    {
        var list = GetFavorites(type);
        if (list.Contains(id))
        {
            list.Remove(id);
        }
        else
        {
            list.Add(id);
        }
        Save(type, list);
    }

    public bool IsFavorite(string type, string id)
    {
        var list = GetFavorites(type);
        return list.Contains(id);
    }

    private void Save(string type, List<string> list)
    {
        string key = GetKey(type);
        string json = JsonSerializer.Serialize(list);
        Preferences.Set(key, json);
    }

    private string GetKey(string type)
    {
        return type switch
        {
            "live" => KeyLive,
            "movie" => KeyMovies,
            "series" => KeySeries,
            _ => throw new ArgumentException("Invalid type")
        };
    }
}
