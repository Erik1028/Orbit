using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Orbit;

// The weather for the watch's weather widget (Erik's Hexpad shows the same on its keys): Open-Meteo - free, no key. One forecast call
// gives the conditions now (day or night, feels like, humidity, wind, UV), the next 36 hours and 7 days (highs, lows, sunrise, sunset).
// Fetched on a thread of its own every 15 minutes while a weather widget is on one of the watch's pages - never on a frame - always in
// °C and km/h: °F and mph are worked out where they are drawn, so the switch shows at once. The last answer is kept in
// %LOCALAPPDATA%\Orbit\weather_cache.json (a file of its own, never config.json): the next start shows it at once. A fetch that fails
// keeps the last report, marked stale, and tries again after 1, 2, 4 ... 15 minutes.
// Where: WeatherConfig.Location - a city, found with Open-Meteo's geocoding, or "lat,lon". None set: the place set in Hexpad (read only:
// %APPDATA%\AjazzDock\config.json's "weather", and its weather_cache.json for where that place is). Neither: "Set a location" - never a
// guess from the IP address (a VPN puts that in another city).

// what the sky does: the widget's pictures (WeatherGlyphs) and words
public enum WxKind : byte { Clear, Partly, Cloudy, Fog, Drizzle, Rain, Snow, Storm }

// Off: nothing asked yet; NoLocation: none set here or in Hexpad; Looking: a place, no answer yet; NotFound: the geocoding knows no such
// place; Offline: no answer and nothing kept; Ok: a report (Stale when it is an old one)
public enum WxStatus : byte { Off, NoLocation, Looking, NotFound, Offline, Ok }

// an hour of the forecast, a day of it (times on the place's own clock)
public readonly record struct WxHour(DateTime At, float Temp, int Code, bool Day, float Pop);
public readonly record struct WxDay(DateTime Date, float Hi, float Lo, int Code, float Pop, float Uv, DateTime Rise, DateTime Set);

// where a place is: found by its name (Open-Meteo's geocoding), Hexpad's, or typed as numbers
public readonly record struct WxPlace(double Lat, double Lon, string Name, string Region);

// One report as it was read. Never changed once handed out (the watch reads it every frame; the thread makes a new one).
public sealed class WeatherReport
{
    public int Version;                  // goes up with each new one
    public WxStatus Status;
    public string Query = "";            // what was looked up: the setting, else Hexpad's ("" = none)
    public bool FromHexpad;
    public string Place = "", Region = "";
    public DateTime Fetched;             // UTC: when Open-Meteo answered (MinValue: never)
    public bool Stale;                   // an old one: the last fetch failed, or it is from the disk and over half an hour old
    public bool HasData;
    // now, in °C, %, km/h, degrees (where it blows from), the UV index, mm, metres
    public float Temp, Feels, Humidity, Wind, WindDir, Uv, Precip, Visibility;
    public int Code;
    public bool Day = true;
    public int UtcOffset;                // seconds: the place's clock = UTC + this
    public WxHour[] Hours = Array.Empty<WxHour>();      // from the hour it was read on
    public WxDay[] Days = Array.Empty<WxDay>();         // from the day it was read on

    public WxKind Kind => Weather.KindOf(Code);

    // the place's own clock now
    public DateTime PlaceNow(DateTime utc) => utc.AddSeconds(UtcOffset);

    public WeatherReport Copy() => (WeatherReport)MemberwiseClone();
}

public static class Weather
{
    // ---- WMO weather codes (what Open-Meteo answers) ----

    public static WxKind KindOf(int code) => code switch
    {
        0 => WxKind.Clear,
        1 or 2 => WxKind.Partly,
        3 => WxKind.Cloudy,
        45 or 48 => WxKind.Fog,
        >= 51 and <= 57 => WxKind.Drizzle,
        (>= 61 and <= 67) or (>= 80 and <= 82) => WxKind.Rain,
        (>= 71 and <= 77) or 85 or 86 => WxKind.Snow,
        >= 95 and <= 99 => WxKind.Storm,
        _ => WxKind.Cloudy,
    };

    // the conditions in words (the painter puts them in the language picked), a shorter one where the whole does not fit
    public static string Words(int code) => code switch
    {
        0 => "Clear",
        1 => "Mostly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Freezing fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Freezing drizzle",
        61 => "Light rain",
        63 => "Rain",
        65 => "Heavy rain",
        66 or 67 => "Freezing rain",
        71 => "Light snow",
        73 => "Snow",
        75 => "Heavy snow",
        77 => "Snow grains",
        80 or 81 => "Showers",
        82 => "Heavy showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm, hail",
        _ => "Cloudy",
    };

    public static string ShortWords(int code) => KindOf(code) switch
    {
        WxKind.Clear => "Clear", WxKind.Partly => "Cloudy", WxKind.Cloudy => "Cloudy", WxKind.Fog => "Fog", WxKind.Drizzle => "Drizzle",
        WxKind.Rain => "Rain", WxKind.Snow => "Snow", _ => "Storm",
    };

    // ---- units: always fetched metric ----

    public static float Temp(float c, bool f) => f ? c * 9 / 5 + 32 : c;
    public static float Wind(float kmh, bool f) => f ? kmh / 1.609344f : kmh;
    public static int Round(float v) => float.IsNaN(v) ? 0 : (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    // ---- where ----

    // "47.5,19.04" (a comma between, dots in the numbers; spaces allowed)
    public static bool TryCoords(string s, out double lat, out double lon)
    {
        lat = lon = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var parts = s.Split(',');
        return parts.Length == 2 &&
               double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
               double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lon) &&
               lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;
    }

    // a place typed as numbers is called by them
    public static string CoordName(double lat, double lon) => string.Format(CultureInfo.InvariantCulture, "{0:0.00}, {1:0.00}", lat, lon);

    public static bool Coordish(string s) => TryCoords(s, out _, out _);

    public static string GeocodeUrl(string name, string lang) =>
        "https://geocoding-api.open-meteo.com/v1/search?count=1&format=json&language=" + Uri.EscapeDataString(lang ?? "en") + "&name=" +
        Uri.EscapeDataString(name);

    public static string ForecastUrl(WxPlace p) => string.Format(CultureInfo.InvariantCulture,
        "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}" +
        "&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m,uv_index,visibility,is_day" +
        "&hourly=temperature_2m,weather_code,is_day,precipitation_probability" +
        "&daily=weather_code,temperature_2m_max,temperature_2m_min,uv_index_max,precipitation_probability_max,sunrise,sunset" +
        "&timezone=auto&forecast_days=7&forecast_hours=36", p.Lat, p.Lon);

    // the geocoding's first answer (null: it knows no such place)
    public static WxPlace? ParsePlace(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var res) || res.ValueKind != JsonValueKind.Array || res.GetArrayLength() == 0) return null;
        var r = res[0];
        if (!r.TryGetProperty("latitude", out var la) || !r.TryGetProperty("longitude", out var lo)) return null;
        string name = Str(r, "name"), region = Str(r, "country");
        if (region.Length == 0) region = Str(r, "admin1");
        return new WxPlace(la.GetDouble(), lo.GetDouble(), name, region);
    }

    // ---- the forecast ----

    // Open-Meteo's answer as a report (throws when it is not one: no temperature now)
    public static WeatherReport Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var r = new WeatherReport { Status = WxStatus.Ok, HasData = true };
        if (root.TryGetProperty("utc_offset_seconds", out var off) && off.TryGetInt32(out int o)) r.UtcOffset = o;
        if (!root.TryGetProperty("current", out var cur)) throw new FormatException("no current weather");
        r.Temp = Num(cur, "temperature_2m");
        if (float.IsNaN(r.Temp)) throw new FormatException("no temperature");
        r.Feels = Num(cur, "apparent_temperature");
        if (float.IsNaN(r.Feels)) r.Feels = r.Temp;
        r.Humidity = Num(cur, "relative_humidity_2m");
        r.Wind = Num(cur, "wind_speed_10m");
        r.WindDir = Num(cur, "wind_direction_10m");
        r.Uv = Num(cur, "uv_index");
        r.Precip = Num(cur, "precipitation");
        r.Visibility = Num(cur, "visibility");
        float code = Num(cur, "weather_code");
        r.Code = float.IsNaN(code) ? 3 : (int)code;
        float day = Num(cur, "is_day");
        r.Day = float.IsNaN(day) || day >= 0.5f;
        if (root.TryGetProperty("hourly", out var h) && h.TryGetProperty("time", out var ht) && ht.ValueKind == JsonValueKind.Array)
        {
            int n = ht.GetArrayLength();
            var hours = new WxHour[n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (!Time(ht[i], out var at)) continue;
                float hc = At(h, "weather_code", i), hd = At(h, "is_day", i);
                hours[k++] = new WxHour(at, At(h, "temperature_2m", i), float.IsNaN(hc) ? 3 : (int)hc, float.IsNaN(hd) || hd >= 0.5f,
                    At(h, "precipitation_probability", i));
            }
            Array.Resize(ref hours, k);
            r.Hours = hours;
        }
        if (root.TryGetProperty("daily", out var dl) && dl.TryGetProperty("time", out var dt) && dt.ValueKind == JsonValueKind.Array)
        {
            int n = dt.GetArrayLength();
            var days = new WxDay[n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                if (!Time(dt[i], out var date)) continue;
                float dc = At(dl, "weather_code", i);
                Time(Item(dl, "sunrise", i), out var rise);
                Time(Item(dl, "sunset", i), out var set);
                days[k++] = new WxDay(date.Date, At(dl, "temperature_2m_max", i), At(dl, "temperature_2m_min", i), float.IsNaN(dc) ? 3 : (int)dc,
                    At(dl, "precipitation_probability_max", i), At(dl, "uv_index_max", i), rise, set);
            }
            Array.Resize(ref days, k);
            r.Days = days;
        }
        return r;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static float Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : float.NaN;

    static JsonElement Item(JsonElement block, string name, int i) =>
        block.TryGetProperty(name, out var a) && a.ValueKind == JsonValueKind.Array && i < a.GetArrayLength() ? a[i] : default;

    static float At(JsonElement block, string name, int i)
    {
        var v = Item(block, name, i);
        return v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : float.NaN;
    }

    static readonly string[] TimeForms = { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd" };

    // "2026-10-17T16:00" / "2026-10-17": the place's own clock
    static bool Time(JsonElement v, out DateTime t)
    {
        t = DateTime.MinValue;
        return v.ValueKind == JsonValueKind.String &&
               DateTime.TryParseExact(v.GetString(), TimeForms, CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
    }

    // ---- the cache: the last answer and where it was for, as Open-Meteo wrote it ----

    public static void SaveCache(string path, string query, WxPlace p, DateTime fetchedUtc, string forecast)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("version", 1);
            w.WriteString("query", query);
            w.WriteString("place", p.Name ?? "");
            w.WriteString("region", p.Region ?? "");
            w.WriteNumber("lat", p.Lat);
            w.WriteNumber("lon", p.Lon);
            w.WriteString("fetched", DateTime.SpecifyKind(fetchedUtc, DateTimeKind.Utc));
            w.WritePropertyName("forecast");
            w.WriteRawValue(forecast);
            w.WriteEndObject();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, ms.ToArray());
        File.Move(tmp, path, true);            // a whole file or the old one: never half of one
    }

    // the kept answer, when it is for this query (anything wrong with the file: none)
    public static (WxPlace place, DateTime fetched, string forecast)? LoadCache(string path, string query)
    {
        try
        {
            if (string.IsNullOrEmpty(query) || !File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (!string.Equals(Str(root, "query").Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
            if (!root.TryGetProperty("forecast", out var f) || f.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("fetched", out var fe) || !fe.TryGetDateTime(out var fetched)) return null;
            double lat = root.TryGetProperty("lat", out var la) && la.ValueKind == JsonValueKind.Number ? la.GetDouble() : double.NaN;
            double lon = root.TryGetProperty("lon", out var lo) && lo.ValueKind == JsonValueKind.Number ? lo.GetDouble() : double.NaN;
            if (double.IsNaN(lat) || double.IsNaN(lon)) return null;
            return (new WxPlace(lat, lon, Str(root, "place"), Str(root, "region")), fetched.ToUniversalTime(), f.GetRawText());
        }
        catch (Exception e)
        {
            Log.Info("weather: the cache is not readable (" + e.Message + ")");
            return null;
        }
    }

    // ---- Hexpad's place (read only - never written) ----

    public static readonly string HexpadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AjazzDock");

    // Its config's "weather" (a city or "lat,lon"; empty there = Hexpad goes by the IP address: not taken), and where its own cache found
    // that place (so it is not looked up again); (null, null): none
    public static (string query, WxPlace? place) Hexpad(string dir)
    {
        try
        {
            string cfg = Path.Combine(dir, "config.json");
            if (!File.Exists(cfg)) return (null, null);
            string q;
            using (var doc = JsonDocument.Parse(ReadShared(cfg)))
                q = doc.RootElement.ValueKind == JsonValueKind.Object ? Str(doc.RootElement, "weather").Trim() : "";
            if (q.Length == 0) return (null, null);
            WxPlace? p = null;
            string cache = Path.Combine(dir, "weather_cache.json");
            if (File.Exists(cache))
            {
                using var doc = JsonDocument.Parse(ReadShared(cache));
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && Str(root, "place").Trim() == q && root.TryGetProperty("loc", out var loc) &&
                    loc.ValueKind == JsonValueKind.Array && loc.GetArrayLength() >= 2 && loc[0].ValueKind == JsonValueKind.Number &&
                    loc[1].ValueKind == JsonValueKind.Number)
                {
                    string name = loc.GetArrayLength() > 2 && loc[2].ValueKind == JsonValueKind.String ? loc[2].GetString() : "";
                    if (string.IsNullOrEmpty(name) || Coordish(name)) name = q;
                    p = new WxPlace(loc[0].GetDouble(), loc[1].GetDouble(), name, "");
                }
            }
            return (q, p);
        }
        catch (Exception e)
        {
            Log.Info("weather: Hexpad's config is not readable (" + e.Message + ")");
            return (null, null);
        }
    }

    // read without locking Hexpad out of its own file (it may be writing it)
    static string ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8, true);
        return sr.ReadToEnd();
    }
}

// The thread that keeps the report: the app sets the place (SetLocation) and says while a widget wants it (Active); the watch reads
// Report. What it reads from and writes to can be pointed elsewhere (the tools: a copy of nothing real, made-up answers).
public sealed class WeatherService : IDisposable
{
    public string CachePath = Path.Combine(Log.Dir, "weather_cache.json");
    public string HexpadDir = Weather.HexpadDir;
    public Func<string, string> Get;             // a URL's answer (null: the network); --snapshot gives made-up ones
    public double Poll = 900, Retry = 60, StaleAfter = 1800;     // seconds: the next fetch, the first retry, a kept report's age

    readonly AutoResetEvent _wake = new(false);
    Thread _thread;
    volatile bool _stop, _active;
    volatile string _setting = "";
    volatile WeatherReport _pub = new() { Status = WxStatus.Off };
    int _version;
    HttpClient _http;

    // the newest report (never null)
    public WeatherReport Report => _pub;

    // a weather widget is on a page: fetch (else only the place is worked out, and a kept report read, for the settings)
    public bool Active
    {
        get => _active;
        set
        {
            if (value == _active) return;
            _active = value;
            Start();
            _wake.Set();
        }
    }

    // WeatherConfig.Location (asked every frame: only a change wakes the thread)
    public void SetLocation(string s)
    {
        s = s?.Trim() ?? "";
        if (s == _setting && _thread != null) return;
        _setting = s;
        Start();
        _wake.Set();
    }

    void Start()
    {
        if (_thread != null || _stop) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "weather" };
        _thread.Start();
    }

    // ---- the thread ----
    string _query;                   // the place looked up now ("" none, null: not worked out yet)
    bool _fromHexpad;
    WxPlace? _place, _hexPlace;
    string _hexQuery, _forecast;     // Hexpad's place; the last answer as Open-Meteo wrote it
    DateTime _fetched = DateTime.MinValue, _due = DateTime.MinValue, _nextHexpad = DateTime.MinValue;
    double _backoff;
    bool _failed, _cacheRead;
    WxStatus _said = WxStatus.Off;

    void Run()
    {
        while (!_stop)
        {
            try { Step(); }
            catch (Exception e) { Log.Warn("weather: " + e.Message); _due = DateTime.UtcNow.AddSeconds(Retry); }
            var now = DateTime.UtcNow;
            double wait = _active && _query?.Length > 0 ? Math.Clamp((_due - now).TotalSeconds, 0.05, 60) : 60;
            _wake.WaitOne(TimeSpan.FromSeconds(wait));
        }
    }

    void Step()
    {
        var now = DateTime.UtcNow;
        string setting = _setting;
        string q = setting;
        bool hex = false;
        if (q.Length == 0)
        {
            // Hexpad's place, read again every few minutes (Erik may change it there)
            if (now >= _nextHexpad || _query == null)
            {
                (_hexQuery, _hexPlace) = Weather.Hexpad(HexpadDir);
                _nextHexpad = now.AddMinutes(5);
            }
            q = _hexQuery ?? "";
            hex = q.Length > 0;
        }
        if (_query == null || hex != _fromHexpad || !string.Equals(q, _query, StringComparison.OrdinalIgnoreCase))
        {
            // another place: everything about the last one goes (its kept answer is read again only if it is this place's)
            _query = q;
            _fromHexpad = hex;
            _place = hex ? _hexPlace : null;
            _forecast = null;
            _fetched = DateTime.MinValue;
            _due = now;
            _backoff = Retry;
            _failed = false;
            _cacheRead = false;
            Publish(q.Length == 0 ? WxStatus.NoLocation : WxStatus.Looking);
            if (q.Length > 0) Log.Info(hex ? "weather: the place set in Hexpad" : "weather: the place set in OrbitOverlay");
            else Log.Info("weather: no place set (here or in Hexpad)");
        }
        if (_query.Length == 0) return;
        if (!_cacheRead)
        {
            _cacheRead = true;
            if (Weather.LoadCache(CachePath, _query) is { } c)
            {
                _place = c.place;
                _forecast = c.forecast;
                _fetched = c.fetched;
                _due = _fetched.AddSeconds(Poll);
                PublishData((now - _fetched).TotalSeconds > StaleAfter);
            }
        }
        if (!_active || now < _due) return;
        Fetch();
    }

    void Fetch()
    {
        try
        {
            if (_place == null)
            {
                if (Weather.TryCoords(_query, out double lat, out double lon)) _place = new WxPlace(lat, lon, Weather.CoordName(lat, lon), "");
                else _place = Weather.ParsePlace(Text(Weather.GeocodeUrl(_query, Lang.Code ?? "en")));
                if (_place == null)
                {
                    // no such place: it waits for another setting (or an hour, the place may have been mistyped in Hexpad and fixed there)
                    Publish(WxStatus.NotFound);
                    _due = DateTime.UtcNow.AddHours(1);
                    return;
                }
            }
            string json = Text(Weather.ForecastUrl(_place.Value));
            Weather.Parse(json);                      // (throws when it is not a forecast: nothing kept then)
            _forecast = json;
            _fetched = DateTime.UtcNow;
            _due = _fetched.AddSeconds(Poll);
            _backoff = Retry;
            if (_failed) Log.Info("weather: answering again");
            _failed = false;
            PublishData(false);
            try { Weather.SaveCache(CachePath, _query, _place.Value, _fetched, json); }
            catch (Exception e) { Log.Warn("weather: the cache not written (" + e.Message + ")"); }
        }
        catch (Exception e)
        {
            if (!_failed) Log.Info($"weather: no answer ({e.Message}) - the last report stays, marked stale; again in {_backoff:0} s");
            _failed = true;
            _due = DateTime.UtcNow.AddSeconds(_backoff);
            _backoff = Math.Min(_backoff * 2, Poll);
            if (_forecast != null) PublishData(true);
            else Publish(WxStatus.Offline);
        }
    }

    string Text(string url)
    {
        if (Get != null) return Get(url);
        if (_http == null)
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"OrbitOverlay/{Updater.CurrentText}");
        }
        return _http.GetStringAsync(url).GetAwaiter().GetResult();
    }

    // a report without weather in it
    void Publish(WxStatus s)
    {
        var r = new WeatherReport
        {
            Version = Interlocked.Increment(ref _version), Status = s, Query = _query ?? "", FromHexpad = _fromHexpad,
            Place = _place?.Name ?? "", Region = _place?.Region ?? "",
        };
        _pub = r;
        Said(s);
    }

    // the kept answer as a report
    void PublishData(bool stale)
    {
        WeatherReport r;
        try { r = Weather.Parse(_forecast); }
        catch (Exception e)
        {
            Log.Warn("weather: the kept answer is not a forecast (" + e.Message + ")");
            _forecast = null;
            Publish(WxStatus.Offline);
            return;
        }
        r.Version = Interlocked.Increment(ref _version);
        r.Query = _query ?? "";
        r.FromHexpad = _fromHexpad;
        r.Place = _place?.Name ?? "";
        r.Region = _place?.Region ?? "";
        r.Fetched = _fetched;
        r.Stale = stale;
        _pub = r;
        Said(WxStatus.Ok);
    }

    // the log says what changed (never where: the place stays out of it)
    void Said(WxStatus s)
    {
        if (s == _said) return;
        _said = s;
        if (s is WxStatus.NotFound or WxStatus.Offline) Log.Info($"weather: {s}");
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        _http?.Dispose();
    }
}
