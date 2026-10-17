using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;

namespace Orbit;

// The weather widget ("weather"; the report: Sys/Weather.cs, the pictures: Gfx/WeatherGlyphs.cs). A 1x1 is the picture and the
// temperature; a 2x1 adds the conditions and the day's highest / lowest; a 2x2 the place and the next three days; a 4x1 the next hours;
// the 4 x 3 page's S, M, L (hours and days) and wide ones the same, bigger. A tap opens it: now big, feels like, humidity, wind, UV,
// sunrise and sunset, the next 12 hours as a strip, 6 days. An old report (the last fetch failed) shows with an amber dot / grey
// numbers; none at all says why ("Set a location" - never a guess from the IP address).
// Modern: Bento cards with the sky of the weather in them (the default look: warm by a clear day, deep indigo by a clear night, cool
// blue in rain, icy in snow, purple in a storm, grey under clouds - Hexpad's keys' backgrounds); a look of its own keeps its colours
// and draws the picture in its text colour. Cyber: the yellow WEATHER tag, segmented bars for wind / UV / humidity, monospace values.
// The retro styles: WristWeather95.cs (95, XP, the Mac), Skins/Aero (7's gadget), Skins/Terminal (wttr.in).
public sealed partial class WristPanel
{
    // ================= the texts =================
    // Every text the weather draws, made when the report, the units, the clock, the language, the culture, the look or the hour
    // changes - a frame drawn again makes none (cut ones are kept too: WxFit).

    sealed class WxText
    {
        public WeatherReport R;
        public bool F, H24;
        public int Lang = -1, Look = -1;
        public CultureInfo Culture;
        public long Hour = -1;
        public bool Has, Night, Stale;
        public WxKind Kind;
        public string Msg, Short, Hint, Icon;                 // no report: what to say, a 1x1's word for it, a hint, its Segoe icon
        public string Temp, TempU, Feels, FeelsLine, Hi, Lo, HiArrow, LoArrow, HiLo, HiLoSlash, HiLoFeels, Cond, CondFeels, Hum, Wind, WindNum,
            WindUnit, Uv, Rise, Set, Updated, UpdatedAt, Place, PlaceLine, Rain, CyHead, CyFeels;
        // the Terminal's lines (wttr.in's)
        public string TTempCond, THiWind, TWind, TUvHum, TCurl, TReport, TNow, TUvHumRain, TSun;
        public string Status95, WindArrow;                    // the 95 window's status line; the wind with its arrow (Cyber)
        public readonly string[] CondForms = new string[2], PlaceForms = new string[2];
        public float HumFrac, WindFrac, UvFrac, WindDir, UvValue;
        public char Arrow;                                    // where the wind blows to: ↑ ↗ → ↘ ↓ ↙ ← ↖
        public int HourN, DayN;
        public readonly string[] HourAt = new string[WxHours], HourShort = new string[WxHours], HourT = new string[WxHours];
        public readonly WxKind[] HourK = new WxKind[WxHours];
        public readonly bool[] HourNight = new bool[WxHours];
        public readonly string[] DayName = new string[WxDays], DayHi = new string[WxDays], DayLo = new string[WxDays], DayHiLo = new string[WxDays],
            DayWord = new string[WxDays];
        public readonly WxKind[] DayK = new WxKind[WxDays];
    }

    const int WxHours = 12, WxDays = 6;
    readonly WxText _wxt = new();
    readonly Dictionary<(string, float, float, int), string> _wxFits = new();
    readonly Dictionary<(string, float, float), (string, string)> _wxTwo = new();
    static readonly string[] CapWeather = { "Weather" };
    const string ArrowsTo = "↑↗→↘↓↙←↖";

    WxText Wxt(PanelData d)
    {
        var r = d.Weather;
        var t = _wxt;
        bool f = _cfg.Data.Weather?.Units == "f", h24 = _cfg.Data.Appearance.Clock24h;
        var culture = CultureInfo.CurrentCulture;
        long hour = (DateTime.UtcNow.Ticks + (r?.UtcOffset ?? 0) * TimeSpan.TicksPerSecond) / TimeSpan.TicksPerHour;     // the place's hour
        if (ReferenceEquals(r, t.R) && f == t.F && h24 == t.H24 && t.Lang == Lang.Version && ReferenceEquals(culture, t.Culture) && hour == t.Hour &&
            t.Look == Theme.Version) return t;
        (t.R, t.F, t.H24, t.Lang, t.Culture, t.Hour, t.Look) = (r, f, h24, Lang.Version, culture, hour, Theme.Version);
        WxMake(t, r, f, h24, culture);
        return t;
    }

    // the texts made again (a method of its own: the lambdas it holds made their closure on every call of Wxt, every frame)
    void WxMake(WxText t, WeatherReport r, bool f, bool h24, CultureInfo culture)
    {
        _wxFits.Clear();
        _wxTwo.Clear();
        t.Has = r != null && r.HasData;
        t.HourN = t.DayN = 0;
        t.Place = r?.Place ?? "";
        if (!t.Has)
        {
            (t.Msg, t.Short, t.Hint, t.Icon) = (r?.Status ?? WxStatus.Off) switch
            {
                WxStatus.NoLocation => ("Set a location", "No place", "Settings > Wrist > Weather", "\uE707"),
                WxStatus.NotFound => ("Place not found", "Not found", r.Query, "\uE721"),
                WxStatus.Offline => ("No connection", "Offline", "Trying again soon", "\uE7BA"),
                WxStatus.Looking => ("Loading", "Loading", r.FromHexpad ? "Hexpad's location" : r.Query, "\uE895"),
                _ => ("Weather", "Weather", "", "\uE706"),
            };
            t.Stale = false;
            return;
        }
        var now = r.PlaceNow(DateTime.UtcNow);
        t.Kind = r.Kind;
        t.Night = !r.Day;
        t.Stale = r.Stale;
        t.Temp = Deg(r.Temp, f, culture);
        t.TempU = t.Temp + (f ? "F" : "C");
        t.Feels = Deg(r.Feels, f, culture);
        t.FeelsLine = Lang.F("Feels like {0}", t.Feels);
        t.Cond = Lang.C("weather", Weather.Words(r.Code));
        t.CondForms[0] = t.Cond;
        t.CondForms[1] = Lang.C("weather", Weather.ShortWords(r.Code));
        // today on the place's clock: its highest and lowest, sunrise and sunset, the UV it gets to
        int d0 = Array.FindIndex(r.Days, x => x.Date >= now.Date);
        var today = d0 >= 0 ? r.Days[d0] : default;
        t.Hi = d0 >= 0 ? Deg(today.Hi, f, culture) : "--";
        t.Lo = d0 >= 0 ? Deg(today.Lo, f, culture) : "--";
        t.HiArrow = "↑" + t.Hi;
        t.LoArrow = "↓" + t.Lo;
        t.HiLo = t.HiArrow + " " + t.LoArrow;
        t.HiLoSlash = t.Hi + " / " + t.Lo;
        t.HiLoFeels = t.HiLo + " · " + t.FeelsLine;
        t.CondFeels = t.Cond + " · " + t.FeelsLine;
        t.Rise = d0 >= 0 && today.Rise != default ? ShortClock(today.Rise) : "--";
        t.Set = d0 >= 0 && today.Set != default ? ShortClock(today.Set) : "--";
        float hum = float.IsNaN(r.Humidity) ? -1 : r.Humidity;
        t.Hum = hum < 0 ? "--" : Pct(Weather.Round(hum));
        t.HumFrac = hum < 0 ? 0 : Math.Clamp(hum / 100, 0, 1);
        float wind = float.IsNaN(r.Wind) ? -1 : r.Wind;
        t.WindNum = wind < 0 ? "--" : Weather.Round(Weather.Wind(wind, f)).ToString(culture);
        t.WindUnit = f ? "mph" : "km/h";
        t.Wind = t.WindNum + " " + t.WindUnit;
        t.WindFrac = wind < 0 ? 0 : Math.Clamp(wind / 60, 0, 1);
        t.WindDir = float.IsNaN(r.WindDir) ? -1 : r.WindDir;
        t.Arrow = t.WindDir < 0 ? ' ' : ArrowsTo[(int)MathF.Round(((t.WindDir + 180) % 360) / 45) % 8];
        float uv = !float.IsNaN(r.Uv) ? r.Uv : d0 >= 0 && !float.IsNaN(today.Uv) ? today.Uv : -1;
        t.UvValue = uv;
        t.Uv = uv < 0 ? "--" : Weather.Round(uv).ToString(culture);
        t.UvFrac = uv < 0 ? 0 : Math.Clamp(uv / 11, 0, 1);
        t.Rain = float.IsNaN(r.Precip) ? "--" : f ? (r.Precip / 25.4f).ToString("0.00", culture) + " in" : r.Precip.ToString("0.0", culture) + " mm";
        t.UpdatedAt = r.Fetched > DateTime.MinValue ? ShortClock(r.Fetched.ToLocalTime()) : "--";
        t.Updated = r.Stale ? Lang.F("Not updated since {0}", t.UpdatedAt) : Lang.F("Updated {0}", t.UpdatedAt);
        // a place typed as numbers is called by its weather on the cards (the opened view still says where)
        if (t.Place.Length == 0) t.Place = Lang.T("Weather");
        t.PlaceLine = r.Region.Length > 0 ? t.Place + ", " + r.Region : t.Place;
        t.PlaceForms[0] = Weather.Coordish(r.Place) ? Lang.T("Weather") : t.Place;
        t.PlaceForms[1] = Lang.T("Weather");
        t.CyHead = Upper(t.PlaceLine) + "  //  " + Upper(t.Updated);
        t.CyFeels = Upper(t.FeelsLine) + "  " + t.HiLo;
        t.TTempCond = t.TempU + " " + Upper(t.Cond);
        t.THiWind = t.HiLoSlash + "  " + t.Arrow + t.WindNum;
        t.TWind = t.Arrow + " " + t.Wind;
        t.TUvHum = "UV " + t.Uv + "  " + t.Hum;
        t.TCurl = "$ curl wttr.in/" + t.Place;
        t.TReport = Lang.F("Weather report: {0}", t.PlaceLine);
        t.TNow = Weather.Round(Weather.Temp(r.Temp, f)).ToString(culture) + " (" + Weather.Round(Weather.Temp(r.Feels, f)).ToString(culture) + ") °" + (f ? "F" : "C");
        t.TUvHumRain = t.TUvHum + "  " + t.Rain;
        t.TSun = t.Rise + " - " + t.Set;
        t.Status95 = r.Stale ? t.Updated + " - " + t.PlaceLine : t.PlaceLine + " - " + t.Updated;      // an old one says so first
        t.WindArrow = t.Arrow != ' ' ? t.Wind + " " + t.Arrow : t.Wind;
        // the next hours from the one the place is in now ("Now", then 16, 17 ... or 4 PM ...)
        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
        int h0 = Array.FindIndex(r.Hours, x => x.At >= hourStart);
        for (int i = 0; h0 >= 0 && i < WxHours && h0 + i < r.Hours.Length; i++)
        {
            var h = r.Hours[h0 + i];
            t.HourAt[i] = i == 0 ? Lang.T("Now") : h24 ? h.At.Hour.ToString(culture) : ((h.At.Hour + 11) % 12 + 1).ToString(culture) + Lang.T(h.At.Hour < 12 ? " AM" : " PM");
            // the retro strips' narrow columns: 16, or 4p
            t.HourShort[i] = i == 0 ? t.HourAt[i] : h24 ? t.HourAt[i] : ((h.At.Hour + 11) % 12 + 1).ToString(culture) + (h.At.Hour < 12 ? "a" : "p");
            t.HourT[i] = Deg(h.Temp, f, culture);
            t.HourK[i] = Weather.KindOf(h.Code);
            t.HourNight[i] = !h.Day;
            t.HourN = i + 1;
        }
        for (int i = 0; d0 >= 0 && i < WxDays && d0 + i < r.Days.Length; i++)
        {
            var day = r.Days[d0 + i];
            t.DayName[i] = i == 0 ? Lang.T("Today") : Capital(day.Date.ToString("ddd", culture).TrimEnd('.'));
            t.DayHi[i] = Deg(day.Hi, f, culture);
            t.DayLo[i] = Deg(day.Lo, f, culture);
            t.DayHiLo[i] = t.DayHi[i] + "/" + t.DayLo[i];
            t.DayK[i] = Weather.KindOf(day.Code);
            t.DayWord[i] = Lang.C("weather", Weather.ShortWords(day.Code));
            t.DayN = i + 1;
        }
    }

    // "18°" in the culture's numbers ("-0" never)
    static string Deg(float c, bool f, CultureInfo culture) => float.IsNaN(c) ? "--" : Weather.Round(Weather.Temp(c, f)).ToString(culture) + "°";

    // a text cut to w, kept until the texts are made again (a cut made every frame was garbage every frame)
    string WxFit(string s, float w, float size, FontWeight wt = FontWeight.SemiBold, string fam = null)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var key = (s, w, size, (int)wt * 31 + (fam == null ? 0 : fam.GetHashCode()));
        if (_wxFits.TryGetValue(key, out var done)) return done;
        if (_wxFits.Count > 256) _wxFits.Clear();
        return _wxFits[key] = Ellipsize(s, w, size, wt, fam);
    }

    // over two lines (TwoLines, kept)
    (string, string) WxTwo(string s, float w, float size)
    {
        if (_wxTwo.TryGetValue((s, w, size), out var done)) return done;
        if (_wxTwo.Count > 64) _wxTwo.Clear();
        return _wxTwo[(s, w, size)] = TwoLines(s, w, size, FontWeight.SemiBold);
    }

    // the picture's colours: Cyber's; a look of its own: its text colour; else in colour
    WeatherGlyphs.Palette WxPal(bool own) =>
        Theme.Cyber ? WeatherGlyphs.Cyber : own ? WeatherGlyphs.Mono(Bento.Text, Bento.Sub) : WeatherGlyphs.Colour;

    void WxGlyph(WxKind k, bool night, float x, float y, float size, in WeatherGlyphs.Palette pal) =>
        WeatherGlyphs.Draw(_p, k, night, new RectangleF(x, y, size, size), pal);

    // the sky in each weather, by day and by night (mixed into the card's own colour)
    static uint Sky(WxKind k, bool night) => k switch
    {
        WxKind.Clear => night ? 0xFF3B3F9E : 0xFFE7A33E,
        WxKind.Partly => night ? 0xFF34437E : 0xFF4C86C6,
        WxKind.Cloudy => 0xFF6E7787,
        WxKind.Fog => 0xFF7F8C8D,
        WxKind.Drizzle => 0xFF4C7DA8,
        WxKind.Rain => 0xFF2F6CB0,
        WxKind.Snow => 0xFF8EB6DA,
        _ => 0xFF6B3FA8,
    };

    // ================= Modern =================

    // The card of a weather widget in the default look: the card's colours with the sky in them, strongest at the top (a card,
    // so the layout check's background has it too); no report: the plain card
    void WeatherBack(RectangleF r, PanelData d, bool cards)
    {
        var t = Wxt(d);
        ref readonly var L = ref Bento.Cur;
        uint f0 = L.Fill0, f1 = L.Fill1;
        if (t.Has)
        {
            uint sky = Sky(t.Kind, t.Night);
            f0 = Theme.Mix(f0, sky, 0.34f);
            f1 = Theme.Mix(f1, sky, 0.12f);
        }
        if (cards) { f0 = Theme.WithAlpha(f0, 0.96f); f1 = Theme.WithAlpha(f1, 0.96f); }
        _p.FillGradient(r, f0, f1, CardRadius, 90);
        if (L.Border != 0 && L.BorderW > 0)
        {
            float i = L.BorderW * 0.48f;
            _p.Stroke(RectangleF.Inflate(r, -i, -i), L.Border, L.BorderW, CardRadius - i);
        }
    }

    void WeatherOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var t = Wxt(d);
        if (!t.Has) { WeatherNone(r, w, t); return; }
        var pal = WxPal(Styled(w));
        float pad = PadX(r), x = r.X + pad, right = r.Right - pad;
        uint vc = t.Stale ? Sub : Bento.Text;
        float capRight = right;
        if (t.Stale)
        {
            // an old report: an amber dot at the top right (its numbers grey)
            StatusDot(right - 6, r.Y + CapBase - 5, StatLevel.Warn);
            capRight -= DotW;
        }
        if (w.W == 1) WxSmall(r, t, pal, vc, x, right, id);
        else if (w.H == 1 && w.W <= 3) WxMedium(r, t, pal, vc, x, right, capRight, id);
        else if (w.H == 1) WxWide(r, t, pal, vc, x, right, capRight, id);
        else WxLarge(r, t, pal, vc, x, right, capRight, id);
    }

    // 1x1: the picture at the top, the temperature big under it (a 4 x 3 one: the day's highest and lowest beside the picture)
    void WxSmall(RectangleF r, WxText t, in WeatherGlyphs.Palette pal, uint vc, float x, float right, int id)
    {
        float g = _big ? 56 : 40, iw = right - x;
        WxGlyph(t.Kind, t.Night, x - 4, r.Y + (_big ? 10 : 9), g, pal);       // (under a header look's strip)
        float vs = FitSize(t.Temp, iw, _big ? 46 : 34, FontWeight.Bold, Theme.Display);
        ValueText(x, r.Bottom - (_big ? 17 : 13), t.Temp, vs, vc, Ui.Id("wxt", id));
        if (!_big) return;
        float hx = x + g, hw = right - hx;
        TextAt(WxFit(t.HiArrow, hw, SmallText), hx, r.Y + 40, hw + 2, Sub, SmallText, FontWeight.SemiBold, TextAlignment.Trailing);
        TextAt(WxFit(t.LoArrow, hw, SmallText), hx, r.Y + 62, hw + 2, Sub, SmallText, FontWeight.SemiBold, TextAlignment.Trailing);
    }

    // 2x1 (and the 4 x 3 M): the conditions as the caption, the day's highest / lowest beside it; the picture, the temperature, feels like
    // where it fits
    void WxMedium(RectangleF r, WxText t, in WeatherGlyphs.Palette pal, uint vc, float x, float right, float capRight, int id)
    {
        CaptionRow(r, x, capRight, t.HiLo, Bento.Cap, t.CondForms);
        float g = _big ? 64 : 46;
        WxGlyph(t.Kind, t.Night, x - 4, r.Y + (_big ? 40 : 38), g, pal);      // (clear of a header look's band)
        float tx = x + g + (_big ? 6 : 2), by = r.Y + (_big ? 100 : 74);
        float vs = FitSize(t.Temp, right - tx, _big ? 56 : 40, FontWeight.Bold, Theme.Display);
        float tw = ValueText(tx, by, t.Temp, vs, vc, Ui.Id("wxt", id));
        float fx = tx + tw + 12, fw = right - fx;
        if (fw >= _p.MeasureWidth(t.FeelsLine, SmallText, FontWeight.SemiBold)) TextAt(t.FeelsLine, fx, by, fw + 2, Sub, SmallText, FontWeight.SemiBold);
        else if (fw >= _p.MeasureWidth(t.Feels, SmallText, FontWeight.SemiBold) + 30 && _big) TextAt(t.Feels, fx, by, fw + 2, Sub, SmallText, FontWeight.SemiBold);
    }

    // 4x1 (and the 4 x 3 wide one): now on the left as a 2x1, the next hours beside it
    void WxWide(RectangleF r, WxText t, in WeatherGlyphs.Palette pal, uint vc, float x, float right, float capRight, int id)
    {
        float lw = _big ? 250 : 186;
        CaptionRow(r, x, x + lw - 18, t.HiLo, Bento.Cap, t.CondForms);
        float g = _big ? 64 : 46;
        WxGlyph(t.Kind, t.Night, x - 4, r.Y + (_big ? 40 : 32), g, pal);
        float tx = x + g + (_big ? 6 : 2), by = r.Y + (_big ? 100 : 72);
        float vs = FitSize(t.Temp, x + lw - 18 - tx, _big ? 56 : 40, FontWeight.Bold, Theme.Display);
        ValueText(tx, by, t.Temp, vs, vc, Ui.Id("wxt", id));
        _p.Fill(new RectangleF(x + lw - 6, r.Y + 18, 1.5f, r.Height - 36), Theme.WithAlpha(Bento.Text, 0.12f));
        float hx = x + lw + 4;
        WxHourRow(hx, r.Y, capRight - hx, r.Bottom, t, pal, _big ? 64 : 50, _big ? 40 : 30, _big ? 20 : SmallText);
    }

    // a row of hours from 'top' to the card's bottom: the hour (its caption row), the picture, the temperature at the bottom
    void WxHourRow(float x, float top, float w, float bottom, WxText t, in WeatherGlyphs.Palette pal, float colMin, float gs, float ts)
    {
        int n = Math.Min(t.HourN, (int)(w / colMin));
        if (n <= 0) return;
        float cw = w / n;
        for (int i = 0; i < n; i++)
        {
            float cx = x + i * cw;
            TextAt(WxFit(t.HourAt[i], cw - 2, CapSize), cx, top + CapBase, cw, i == 0 ? Bento.Text : Sub, CapSize, FontWeight.SemiBold, TextAlignment.Center);
            WxGlyph(t.HourK[i], t.HourNight[i], cx + (cw - gs) / 2, top + CapBase + 6, gs, pal);
            TextAt(t.HourT[i], cx, bottom - (ts > SmallText ? 18 : 13), cw, Bento.Text, ts, FontWeight.SemiBold, TextAlignment.Center);
        }
    }

    // 2x2 (and the 4 x 3 L): the place as the caption, the day's highest / lowest beside it; the picture and the temperature, the
    // conditions under them; the next three days (an L: the hours too)
    void WxLarge(RectangleF r, WxText t, in WeatherGlyphs.Palette pal, uint vc, float x, float right, float capRight, int id)
    {
        CaptionRow(r, x, capRight, t.HiLo, Bento.Cap, t.PlaceForms);
        float g = _big ? 64 : 54, gy = r.Y + 38, iw = right - x;           // (clear of a header look's band)
        WxGlyph(t.Kind, t.Night, x - 4, gy, g, pal);
        float tx = x + g + 4, by = gy + g * 0.8f;
        float vs = FitSize(t.Temp, right - tx, _big ? 58 : 46, FontWeight.Bold, Theme.Display);
        ValueText(tx, by, t.Temp, vs, vc, Ui.Id("wxt", id));
        TextAt(WxFit(_big ? t.CondFeels : t.Cond, iw, SmallText), x, r.Y + (_big ? 120 : 108), iw + 2, Bento.Text, SmallText, FontWeight.SemiBold);
        if (_big)
        {
            WxHourRow(x, r.Y + 120, iw, r.Y + 204, t, pal, 50, 26, SmallText);
            WxDayRow(x, r.Y + 206, iw, r.Bottom - 14, t, pal, 3, 22);
        }
        else WxDayRow(x, r.Y + 116, iw, r.Bottom - 12, t, pal, 3, 24);
    }

    // n days side by side from 'top': the day's name (a caption), its picture, its highest and lowest on the baseline 'by'
    void WxDayRow(float x, float top, float w, float by, WxText t, in WeatherGlyphs.Palette pal, int n, float gs)
    {
        n = Math.Min(n, t.DayN);
        if (n <= 0) return;
        float cw = w / n;
        for (int i = 0; i < n; i++)
        {
            float cx = x + i * cw;
            string name = Upper(t.DayName[i]);
            float nw = MathF.Min(cw - 2, _p.MeasureTracked(name, CapSize, FontWeight.SemiBold, CapTrack));
            CapText(WxFit(name, cw - 2, CapSize), cx + (cw - nw) / 2, top + 19, nw + 2, Sub);
            WxGlyph(t.DayK[i], false, cx + (cw - gs) / 2, top + 24, gs, pal);
            float hw = _p.MeasureWidth(t.DayHi[i], CapSize, FontWeight.SemiBold), lw = _p.MeasureWidth(t.DayLo[i], CapSize, FontWeight.SemiBold);
            float sx = cx + (cw - hw - 5 - lw) / 2;
            TextAt(t.DayHi[i], sx, by, hw + 2, Bento.Text, CapSize, FontWeight.SemiBold);
            TextAt(t.DayLo[i], sx + hw + 5, by, lw + 2, Sub, CapSize, FontWeight.SemiBold);
        }
    }

    // no report: why, small (a 1x1: its icon and a word; wider: the caption, the icon, what to do)
    void WeatherNone(RectangleF r, WatchWidget w, WxText t)
    {
        float pad = PadX(r), x = r.X + pad, iw = r.Width - 2 * pad;
        if (w.W == 1)
        {
            _p.Icon(t.Icon, new RectangleF(x - 2, r.Y + 8, 32, 32), Sub, 24);
            var (a, b) = WxTwo(t.Short, iw, SmallText);
            TextAt(a, x, r.Bottom - (b.Length > 0 ? 34 : 14), iw + 2, Bento.Text, SmallText, FontWeight.SemiBold);
            TextAt(b, x, r.Bottom - 14, iw + 2, Bento.Text, SmallText, FontWeight.SemiBold);
            return;
        }
        Caption(r, iw, CapWeather);
        if (r.Height >= 150)
        {
            // a tall card: the icon, then what is wrong over two lines, the hint along the bottom
            _p.Icon(t.Icon, new RectangleF(x - 2, r.Y + 40, 44, 44), Sub, 34);
            var (l1, l2) = WxTwo(t.Msg, iw, 20);
            TextAt(l1, x, r.Y + 112, iw + 2, Bento.Text, 20, FontWeight.SemiBold);
            TextAt(l2, x, r.Y + 136, iw + 2, Bento.Text, 20, FontWeight.SemiBold);
            if (!string.IsNullOrEmpty(t.Hint)) TextAt(WxFit(t.Hint, iw, CapSize), x, r.Bottom - 16, iw + 2, Sub, CapSize, FontWeight.SemiBold);
            return;
        }
        float ic = 30, top = r.Y + 34, mid = top + (r.Bottom - 12 - top) / 2;
        _p.Icon(t.Icon, new RectangleF(x - 2, mid - ic / 2 - 2, ic + 4, ic + 4), Sub, ic * 0.8f);
        float tx = x + ic + 12, tw = r.Right - pad - tx;
        bool hint = !string.IsNullOrEmpty(t.Hint) && r.Height >= 90;
        TextAt(WxFit(t.Msg, tw, 20), tx, mid + (hint ? -2 : 7), tw + 2, Bento.Text, 20, FontWeight.SemiBold);
        if (hint) TextAt(WxFit(t.Hint, tw, CapSize), tx, mid + 20, tw + 2, Sub, CapSize, FontWeight.SemiBold);
    }

    // ---- opened ----

    // E: the opened card. The heading is the place (DrawDetail leaves it to this view), when it was read at its right before the X;
    // the sky tile with now big, six tiles (feels like, humidity, wind, UV, sunrise, sunset), the next 12 hours, 6 days
    void WeatherDetail(RectangleF E, PanelData d)
    {
        if (Theme.Cyber) { CyberWeatherDetail(E, d); return; }
        var t = Wxt(d);
        var pal = WeatherGlyphs.Colour;
        float hx = E.X + 24, close = E.Right - 22 - 46 - 14;
        float uw = t.Has ? MathF.Min(_p.MeasureWidth(t.Updated, SmallText, FontWeight.SemiBold), 240) : 0;
        float room = close - hx - (uw > 0 ? uw + 18 : 0);
        _p.Text(WxFit(t.Has ? t.PlaceLine : Lang.T("Weather"), room, 24), new RectangleF(hx, E.Y + 16, room, 40), Bento.Text, 24, FontWeight.SemiBold);
        if (uw > 0) TextAt(WxFit(t.Updated, uw, SmallText), close - uw - 2, E.Y + 45, uw + 4, t.Stale ? Bento.Warn : Sub, SmallText, FontWeight.SemiBold);
        var c = new RectangleF(E.X + 24, E.Y + 74, E.Width - 48, E.Height - 96);
        if (!t.Has) { WxNoneDetail(c, t); return; }
        // the sky tile: the picture, the temperature, the conditions, the day's highest / lowest and feels like
        var hero = new RectangleF(c.X, c.Y, 262, 150);
        uint sky = Sky(t.Kind, t.Night);
        _p.FillGradient(hero, Theme.Mix(Bento.Tile, sky, 0.42f), Theme.Mix(Bento.Tile, sky, 0.14f), 14f, 90);
        WxGlyph(t.Kind, t.Night, hero.X + 8, hero.Y + 8, 92, pal);
        float tx = hero.X + 108;
        ValueText(tx, hero.Y + 84, t.Temp, FitSize(t.Temp, hero.Right - 14 - tx, 64, FontWeight.Bold, Theme.Display), t.Stale ? Sub : Bento.Text, "wxdt");
        float hw = hero.Width - 28;
        TextAt(WxFit(t.Cond, hw, 20), hero.X + 14, hero.Y + 116, hw + 2, Bento.Text, 20, FontWeight.SemiBold);
        TextAt(WxFit(t.HiLoFeels, hw, SmallText), hero.X + 14, hero.Y + 139, hw + 2, Sub, SmallText, FontWeight.SemiBold);
        // the six tiles beside it
        float x0 = hero.Right + 12, tw = (c.Right - x0 - 10) / 2, th = 46;
        for (int i = 0; i < 6; i++)
        {
            var tile = new RectangleF(x0 + (i % 2) * (tw + 10), c.Y + (i / 2) * (th + 6), tw, th);
            _p.Fill(tile, Bento.Tile, 12);
            string cap = i switch { 0 => "Feels like", 1 => "Humidity", 2 => "Wind", 3 => "UV index", 4 => "Sunrise", _ => "Sunset" };
            string val = i switch { 0 => t.Feels, 1 => t.Hum, 2 => t.Wind, 3 => t.Uv, 4 => t.Rise, _ => t.Set };
            string cu = Upper(cap);
            CapText(WxFit(cu, tw - 24, CapSize), tile.X + 12, tile.Y + 18, tw - 22, Bento.Cap);
            float vw = _p.MeasureWidth(val, 20, FontWeight.Bold, Theme.Display);
            TextAt(val, tile.X + 12, tile.Y + 40, vw + 4, Bento.Text, 20, FontWeight.Bold, TextAlignment.Leading, Theme.Display);
            float mx = tile.X + 12 + vw + 12, mw = tile.Right - 12 - mx;
            if (i == 1 && mw >= 24) Meter(new RectangleF(mx, tile.Y + 33, mw, 4), t.HumFrac, Bento.Accent, "wxdh");
            else if (i == 3 && mw >= 24)
            {
                var lv = t.UvValue >= 8 ? StatLevel.Bad : t.UvValue >= 6 ? StatLevel.Warn : StatLevel.Ok;
                Meter(new RectangleF(mx, tile.Y + 33, mw, 4), t.UvFrac, Bento.Level(lv, Bento.Accent), "wxdu");
            }
            else if (i == 2 && t.WindDir >= 0 && mw >= 20) WxArrow(tile.Right - 22, tile.Y + 31, 16, t.WindDir, Sub, 2.2f);
        }
        // the next 12 hours
        var hs = new RectangleF(c.X, c.Y + 162, c.Width, 76);
        _p.Fill(hs, Bento.Tile, 14);
        float cw = hs.Width / WxHours;
        for (int i = 0; i < t.HourN; i++)
        {
            float cx = hs.X + i * cw;
            TextAt(WxFit(t.HourAt[i], cw - 4, CapSize), cx, hs.Y + 21, cw, i == 0 ? Bento.Text : Sub, CapSize, FontWeight.SemiBold, TextAlignment.Center);
            WxGlyph(t.HourK[i], t.HourNight[i], cx + (cw - 28) / 2, hs.Y + 25, 28, pal);
            TextAt(t.HourT[i], cx, hs.Y + 68, cw, Bento.Text, SmallText, FontWeight.SemiBold, TextAlignment.Center);
        }
        // 6 days
        float dy = c.Y + 248, dh = c.Bottom - dy, dw = (c.Width - 5 * 8) / WxDays;
        for (int i = 0; i < t.DayN; i++)
        {
            var tile = new RectangleF(c.X + i * (dw + 8), dy, dw, dh);
            _p.Fill(tile, Bento.Tile, 14);
            string name = Upper(t.DayName[i]);
            CapText(WxFit(name, dw - 18, CapSize), tile.X + 10, tile.Y + 20, dw - 16, i == 0 ? Bento.Text : Bento.Cap);
            WxGlyph(t.DayK[i], false, tile.X + 5, tile.Y + 30, 32, pal);
            TextAt(t.DayHi[i], tile.X, tile.Y + 50, dw - 10, Bento.Text, 20, FontWeight.Bold, TextAlignment.Trailing, Theme.Display);
            TextAt(t.DayLo[i], tile.X, tile.Y + 70, dw - 10, Sub, SmallText, FontWeight.SemiBold, TextAlignment.Trailing);
        }
    }

    // an arrow for the wind, pointing where it blows (dirFrom: where it comes from, as the forecast says; 0 = north = up)
    void WxArrow(float cx, float cy, float len, float dirFrom, uint c, float width)
    {
        float a = (dirFrom + 180) * MathF.PI / 180, dx = MathF.Sin(a), dy = -MathF.Cos(a);
        float x1 = cx + dx * len / 2, y1 = cy + dy * len / 2;
        _p.RoundLine(cx - dx * len / 2, cy - dy * len / 2, x1, y1, c, width);
        float hl = len * 0.42f, co = MathF.Cos(0.6f), si = MathF.Sin(0.6f);
        _p.RoundLine(x1, y1, x1 + hl * (-dx * co + dy * si), y1 + hl * (-dy * co - dx * si), c, width);
        _p.RoundLine(x1, y1, x1 + hl * (-dx * co - dy * si), y1 + hl * (-dy * co + dx * si), c, width);
    }

    // no report, opened: its icon, what is wrong, what to do
    void WxNoneDetail(RectangleF c, WxText t)
    {
        float my = c.Y + (c.Height - 124) / 2;
        _p.Icon(t.Icon, new RectangleF(c.X, my, c.Width, 52), Bento.Faint, 40);
        _p.Text(WxFit(t.Msg, c.Width, 22), new RectangleF(c.X, my + 58, c.Width, 34), Bento.Text, 22, FontWeight.SemiBold, TextAlignment.Center);
        if (!string.IsNullOrEmpty(t.Hint))
            _p.Text(WxFit(t.Hint, c.Width, SmallText), new RectangleF(c.X, my + 94, c.Width, 28), Sub, SmallText, FontWeight.SemiBold, TextAlignment.Center);
    }

    // ================= Cyber =================

    // The yellow WEATHER tag with the place as its note; the picture in the HUD's colours, the temperature in the condensed bold, the
    // conditions and the day's highest / lowest in monospace; a 2x2 the wind, the UV and the humidity as segments; a wide one the hours
    void CyberWeather(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var t = Wxt(d);
        float pad = PadX(r), x = r.X + pad, right = r.Right - pad, iw = right - x;
        bool wide = w.H == 1 && w.W >= 4;
        string note = !t.Has ? null : t.Stale ? "Offline" : w.W >= 2 && !wide ? t.PlaceForms[0] : null;
        CyberCaptionRow(r, x, right, note, t.Stale ? CyR : CyG, CapWeather);
        if (!t.Has) { CyWeatherNone(r, w, t); return; }
        var pal = WeatherGlyphs.Cyber;
        uint vc = t.Stale ? CyG : CyW;
        string key = Ui.Id("wxv", id);
        if (w.W == 1)
        {
            float g = _big ? 52 : 32;
            WxGlyph(t.Kind, t.Night, x - 2, r.Y + (_big ? 36 : 38), g, pal);
            float tx = x + g + 2, by = r.Y + (_big ? 82 : 70);
            CyValue(tx, by, t.Temp, CyFit(t.Temp, right - tx, _big ? 46 : 36), vc, key);
            if (_big) CyMono(WxFit(t.HiLo, iw, CyNote, FontWeight.SemiBold, CyberHud.Mono), x, r.Bottom - 14, iw + 2, CyG);
            return;
        }
        float lw = wide ? (_big ? 262 : 196) : iw;
        float gg = w.H >= 2 ? (_big ? 66 : 54) : _big ? 62 : 44, gy = r.Y + (w.H == 1 && _big ? 40 : 34);
        WxGlyph(t.Kind, t.Night, x - 2, gy, gg, pal);
        float tx2 = x + gg + 4, by2 = gy + gg * (w.H == 1 ? 0.92f : 0.82f);
        float tw = CyValue(tx2, by2, t.Temp, CyFit(t.Temp, MathF.Min(lw, iw) - gg - 70, w.H >= 2 ? (_big ? 60 : 50) : _big ? 58 : 44), vc, key);
        // beside it: the conditions, the day's highest / lowest
        float cx = tx2 + tw + 12, cw = x + lw - cx;
        if (cw >= 40)
        {
            CyMono(WxFit(Upper(t.CondForms[CyMonoW(Upper(t.Cond)) <= cw ? 0 : 1]), cw, CyNote, FontWeight.SemiBold, CyberHud.Mono), cx, by2 - 22, cw + 2, CyW);
            CyMono(WxFit(t.HiLo, cw, CyNote, FontWeight.SemiBold, CyberHud.Mono), cx, by2, cw + 2, CyG);
        }
        if (wide)
        {
            _p.Fill(new RectangleF(x + lw - 6, r.Y + 12, 1.25f, r.Height - 24), CyberHud.Line);
            float hx = x + lw + 6, hw = right - hx;
            int n = Math.Min(t.HourN, (int)(hw / (_big ? 62 : 50)));
            float colW = n > 0 ? hw / n : 0, gs = _big ? 40 : 28;
            for (int i = 0; i < n; i++)
            {
                float hc = hx + i * colW;
                CyMono(WxFit(Upper(t.HourAt[i]), colW - 4, CyNote, FontWeight.SemiBold, CyberHud.Mono), hc, r.Y + CyBase, colW, i == 0 ? CyY : CyG, CyNote,
                    FontWeight.SemiBold, TextAlignment.Center);
                WxGlyph(t.HourK[i], t.HourNight[i], hc + (colW - gs) / 2, r.Y + (_big ? 40 : 32), gs, pal);
                CyMono(t.HourT[i], hc, r.Bottom - (_big ? 16 : 12), colW, CyW, CyNote, FontWeight.Bold, TextAlignment.Center);
            }
            return;
        }
        if (w.H < 2) return;
        // the wind, the UV and the humidity as segments (a big L: the hours under them)
        float gy0 = r.Y + (_big ? 118 : 114);
        CyGaugeRow(x, gy0, iw, "Wind", t.Wind, t.WindNum, t.WindFrac, CyC);
        CyGaugeRow(x, gy0 + 25, iw, "UV", t.Uv, t.Uv, t.UvFrac, t.UvValue >= 8 ? CyR : CyY);
        CyGaugeRow(x, gy0 + 50, iw, "Humidity", t.Hum, t.Hum, t.HumFrac, CyC);
        if (!_big) return;
        int hn = Math.Min(t.HourN, (int)(iw / 50));
        float hcw = hn > 0 ? iw / hn : 0, hy = gy0 + 82;
        for (int i = 0; i < hn; i++)
        {
            float hc = x + i * hcw;
            CyMono(WxFit(Upper(t.HourAt[i]), hcw - 4, CyNote, FontWeight.SemiBold, CyberHud.Mono), hc, hy + 12, hcw, i == 0 ? CyY : CyG, CyNote,
                FontWeight.SemiBold, TextAlignment.Center);
            WxGlyph(t.HourK[i], t.HourNight[i], hc + (hcw - 24) / 2, hy + 17, 24, pal);
            CyMono(t.HourT[i], hc, r.Bottom - 14, hcw, CyW, CyNote, FontWeight.Bold, TextAlignment.Center);
        }
    }

    // a gauge row: the label (monospace, grey), the value at the right (the short one where the whole does not fit), segments under them
    void CyGaugeRow(float x, float y, float w, string label, string value, string shortValue, float frac, uint c)
    {
        string v = Upper(value);
        if (CyMonoW(v, CyNote, FontWeight.Bold) > w * 0.45f) v = Upper(shortValue);
        float vw = CyMonoW(v, CyNote, FontWeight.Bold);
        CyMono(WxFit(Upper(label), w - vw - 10, CyNote, FontWeight.SemiBold, CyberHud.Mono), x, y + 11, w - vw - 8, CyG);
        CyMono(v, x + w, y + 11, vw + 2, CyW, CyNote, FontWeight.Bold, TextAlignment.Trailing);
        CyberHud.Segments(_p, new RectangleF(x, y + 15, w, 6), frac * CyIn(0.28f, 0.3f), c, CyberHud.Track, w >= 160 ? 16 : 10, 2);
    }

    // no report: why, in monospace capitals
    void CyWeatherNone(RectangleF r, WatchWidget w, WxText t)
    {
        float pad = PadX(r), x = r.X + pad, iw = r.Width - 2 * pad;
        string msg = Upper(w.W == 1 ? t.Short : t.Msg);
        CyMono(WxFit(msg, iw, w.W == 1 ? CyNote : 18, FontWeight.Bold, CyberHud.Mono), x, r.Y + (r.Height >= 120 ? 70 : 60), iw + 2, CyG,
            w.W == 1 ? CyNote : 18, FontWeight.Bold);
        if (w.W >= 2 && !string.IsNullOrEmpty(t.Hint) && r.Height >= 90)
            CyMono(WxFit(Upper(t.Hint), iw, CyNote, FontWeight.SemiBold, CyberHud.Mono), x, r.Y + 86, iw + 2, CyberHud.Faint);
    }

    // Opened: the WEATHER tag is DrawDetail's (it types in), the place and when it was read beside it; the hero plate (the picture, the
    // temperature in yellow, the conditions in the condensed capitals, feels like); the wind, the UV and the humidity as segmented
    // gauges and the sun's times beside it; the next 12 hours on a plate; 6 days as plates
    void CyberWeatherDetail(RectangleF E, PanelData d)
    {
        var t = Wxt(d);
        CyFrame();
        float nx = E.X + 24 + CyberHud.TagWidth(_p, Upper("Weather"), 18) + 14, nr = E.Right - 22 - 46 - 14;
        if (t.Has && nr - nx > 40)
            CyMono(WxFit(t.CyHead, nr - nx, CyNote, FontWeight.SemiBold, CyberHud.Mono), nx, E.Y + 23 + (26 + CyberHud.CapH(_p, CyNote)) / 2, nr - nx,
                t.Stale ? CyR : CyC);
        var c = new RectangleF(E.X + 24, E.Y + 74, E.Width - 48, E.Height - 96);
        if (!t.Has)
        {
            var p0 = new RectangleF(c.X, c.Y + (c.Height - 120) / 2, c.Width, 120);
            CyberHud.CardShape(_p, p0, CyberHud.Card2, CyberHud.Line);
            CyberHud.Brackets(_p, p0, CyY, 12, 7, 2);
            CyMono(WxFit(Upper(t.Msg), p0.Width - 40, 18, FontWeight.Bold, CyberHud.Mono), p0.X + 20, p0.Y + 56, p0.Width - 38, CyG, 18, FontWeight.Bold,
                TextAlignment.Center);
            if (!string.IsNullOrEmpty(t.Hint))
                CyMono(WxFit(Upper(t.Hint), p0.Width - 40, CyNote, FontWeight.SemiBold, CyberHud.Mono), p0.X + 20, p0.Y + 84, p0.Width - 38, CyberHud.Faint,
                    CyNote, FontWeight.SemiBold, TextAlignment.Center);
            return;
        }
        var pal = WeatherGlyphs.Cyber;
        var hero = new RectangleF(c.X, c.Y, 262, 150);
        CyberHud.CardShape(_p, hero, CyberHud.Card2, CyberHud.Edge);
        CyberHud.Brackets(_p, hero, CyY, 10, 5, 2);
        WxGlyph(t.Kind, t.Night, hero.X + 10, hero.Y + 10, 88, pal);
        float tx = hero.X + 106;
        CyValue(tx, hero.Y + 86, t.Temp, CyFit(t.Temp, hero.Right - 16 - tx, 68), t.Stale ? CyG : CyY, "wxdv");
        float hw = hero.Width - 28;
        TextAt(WxFit(Upper(t.Cond), hw, 22, FontWeight.Bold, Theme.Display), hero.X + 14, hero.Y + 118, hw + 2, CyW, 22, FontWeight.Bold,
            TextAlignment.Leading, Theme.Display);
        CyMono(WxFit(t.CyFeels, hw, CyNote, FontWeight.SemiBold, CyberHud.Mono), hero.X + 14, hero.Y + 139, hw + 2, CyG);
        // the gauges, the sun
        float gx = hero.Right + 16, gw = c.Right - gx;
        CyBigGauge(gx, c.Y, gw, "Wind", t.WindArrow, t.WindFrac, CyC);
        CyBigGauge(gx, c.Y + 38, gw, "UV index", t.Uv, t.UvFrac, t.UvValue >= 8 ? CyR : CyY);
        CyBigGauge(gx, c.Y + 76, gw, "Humidity", t.Hum, t.HumFrac, CyC);
        float sy = c.Y + 140;
        CyMono(Upper("Sunrise"), gx, sy, gw / 2, CyG);
        CyMono(t.Rise, gx + gw / 2 - 8, sy, gw / 2, CyY, CyNote, FontWeight.Bold, TextAlignment.Trailing);
        CyMono(Upper("Sunset"), gx + gw / 2 + 8, sy, gw / 2, CyG);
        CyMono(t.Set, gx + gw, sy, gw / 2, CyY, CyNote, FontWeight.Bold, TextAlignment.Trailing);
        // the hours
        var hp = new RectangleF(c.X, c.Y + 162, c.Width, 76);
        CyberHud.CardShape(_p, hp, CyberHud.Card2, CyberHud.Edge);
        float cw = (hp.Width - 16) / WxHours;
        for (int i = 0; i < t.HourN; i++)
        {
            float cx = hp.X + 8 + i * cw;
            CyMono(t.HourT[i], cx, hp.Y + 22, cw, i == 0 ? CyY : CyW, CyNote, FontWeight.Bold, TextAlignment.Center);
            WxGlyph(t.HourK[i], t.HourNight[i], cx + (cw - 24) / 2, hp.Y + 28, 24, pal);
            CyMono(WxFit(Upper(t.HourAt[i]), cw - 2, CyNote, FontWeight.SemiBold, CyberHud.Mono), cx, hp.Y + 66, cw, CyG, CyNote, FontWeight.SemiBold,
                TextAlignment.Center);
        }
        // the days
        float dy = c.Y + 248, dh = c.Bottom - dy, dw = (c.Width - 5 * 8) / WxDays;
        for (int i = 0; i < t.DayN; i++)
        {
            var p = new RectangleF(c.X + i * (dw + 8), dy, dw, dh);
            CyberHud.CardShape(_p, p, CyberHud.Card2, i == 0 ? CyberHud.CyanDim : CyberHud.Edge);
            CyMono(WxFit(Upper(t.DayName[i]), dw - 16, CyNote, FontWeight.Bold, CyberHud.Mono), p.X + 9, p.Y + 20, dw - 14, CyY, CyNote, FontWeight.Bold);
            WxGlyph(t.DayK[i], false, p.X + 5, p.Y + 28, 30, pal);
            CyMono(t.DayHi[i], p.Right - 9, p.Y + 46, dw - 40, CyW, CyNote, FontWeight.Bold, TextAlignment.Trailing);
            CyMono(t.DayLo[i], p.Right - 9, p.Y + 66, dw - 40, CyG, CyNote, FontWeight.SemiBold, TextAlignment.Trailing);
        }
    }

    // a gauge of the opened view: the label and the value over segments
    void CyBigGauge(float x, float y, float w, string label, string value, float frac, uint c)
    {
        string v = Upper(value);
        float vw = CyMonoW(v, CyNote, FontWeight.Bold);
        CyMono(WxFit(Upper(label), w - vw - 12, CyNote, FontWeight.SemiBold, CyberHud.Mono), x, y + 13, w - vw - 10, CyG);
        CyMono(v, x + w, y + 13, vw + 2, CyW, CyNote, FontWeight.Bold, TextAlignment.Trailing);
        CyberHud.Segments(_p, new RectangleF(x, y + 19, w, 9), frac, c, CyberHud.Track, 20, 2);
    }

    // ---- --snapshot's hooks ----

    // what the watch shows now: the temperature, or why there is none
    public string WeatherShown(PanelData d)
    {
        var t = Wxt(d);
        return t.Has ? t.Temp : t.Msg;
    }

    public int WeatherHoursShown(PanelData d) => Wxt(d).HourN;
    public int WeatherDaysShown(PanelData d) => Wxt(d).DayN;
    public bool WeatherStaleShown(PanelData d) => Wxt(d).Stale;
}
