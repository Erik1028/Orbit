using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The weather widget's settings (Sys/Weather.cs): where - a city or "lat,lon", typed in the desktop window (the settings have no text
// box of their own: a small Windows dialog asks for it; in VR it is shown only) - what that was found as, and °C / °F. Modern and Cyber:
// rows under the watch on the Wrist tab; the 95 family: a "Weather..." button there and its dialog over the page.
public sealed partial class SettingsView
{
    // the desktop window's question for a text: title, what to type, the text so far, then (null: cancelled)
    public Action<string, string, string, Action<string>> AskText;

    bool _wx95;                  // the 95 family's Weather dialog is open
    static readonly string[] UnitNames = { "°C", "°F" };
    static readonly string[] UnitNames95 = { "&Celsius (°C, km/h)", "&Fahrenheit (°F, mph)" };

    bool Fahrenheit => C.Weather?.Units == "f";

    void SetUnits(bool f)
    {
        if (f == Fahrenheit) return;
        C.Weather ??= new WeatherConfig();
        C.Weather.Units = f ? "f" : null;
        if (C.Weather.Location == null && C.Weather.Units == null) C.Weather = null;
        _h.Cfg.MarkDirty();
    }

    // the desktop window asks; the answer is the setting (empty: Hexpad's place again)
    void AskLocation()
    {
        AskText?.Invoke("Weather location", "A city, or latitude,longitude (47.5,19.04). Leave it empty to use the place set in Hexpad.",
            C.Weather?.Location ?? "", text =>
            {
                if (text == null) return;
                text = text.Trim();
                C.Weather ??= new WeatherConfig();
                C.Weather.Location = text.Length == 0 ? null : text;
                if (C.Weather.Location == null && C.Weather.Units == null) C.Weather = null;
                _h.Cfg.MarkDirty();
            });
    }

    // the setting as it is shown on its button / in its box
    string LocationShown => C.Weather?.Location is { Length: > 0 } l ? l : Lang.T("(not set)");

    // what it was found as, or why there is nothing (the report the watch has)
    string _wxFoundOf, _wxFoundSet;
    WeatherReport _wxFoundFor;
    int _wxFoundLang = -1;
    string WeatherFound()
    {
        var r = _h.Data?.Weather;
        string set = C.Weather?.Location;
        if (ReferenceEquals(r, _wxFoundFor) && ReferenceEquals(set, _wxFoundSet) && _wxFoundLang == Lang.Version && _wxFoundOf != null) return _wxFoundOf;
        _wxFoundFor = r;
        _wxFoundSet = set;
        _wxFoundLang = Lang.Version;
        string place = r == null || r.Place.Length == 0 ? null : r.Region.Length > 0 ? r.Place + ", " + r.Region : r.Place;
        _wxFoundOf = r == null || r.Status == WxStatus.Off ? (set == null ? Lang.T("Empty: the place set in Hexpad") : "")
            : r.Status == WxStatus.NoLocation ? Lang.T("Set a location (none here or in Hexpad)")
            : r.Status == WxStatus.NotFound ? Lang.F("Not found: {0}", r.Query)
            : place == null ? Lang.T("Looking up...")
            : r.FromHexpad ? Lang.F("Hexpad's place: {0}", place)
            : Lang.F("Found: {0}", place);
        return _wxFoundOf;
    }

    // ---- Modern / Cyber: the Wrist tab's rows (under the watch's own settings, the right column) ----

    void WeatherRows(Ui ui, float x, float y, float w)
    {
        ui.Label(new RectangleF(x, y, w, 22), "Weather location");
        bool desk = AskText != null;
        if (ui.Button("w.wxloc", new RectangleF(x, y + 24, w - 112, 38), LocationShown, "\uE707", false, desk, 14)) AskLocation();
        int u = Fahrenheit ? 1 : 0;
        if (ui.Segmented("w.wxunit", new RectangleF(x + w - 100, y + 24, 100, 38), UnitNames, ref u)) SetUnits(u == 1);
        string found = WeatherFound();
        if (!desk) found = found.Length > 0 ? found + Lang.T(" · type it in the desktop window") : Lang.T("Type it in the desktop window");
        ui.Label(new RectangleF(x, y + 66, w, 18), found, Theme.Dim, 12);
    }

    // ---- the 95 family: a button on the Wrist tab, its dialog ----

    void Weather95(Ui ui, bool inertWas)
    {
        if (ui.Button95("w95.wx", new RectangleF(254, 392, 100, 23), "We&ather...")) _wx95 = true;
        ui.Inert = inertWas;
        if (_wx95) WeatherDialog95(ui);
    }

    // --snapshot: the 95 family's dialog open / shut
    public bool WeatherDialogOpen { get => _wx95; set => _wx95 = value; }

    void WeatherDialog95(Ui ui)
    {
        var c = Window95(ui, new RectangleF(165, 110, 420, 214), "Weather");
        float x = c.X + 10, y = c.Y + 10, cw = c.Width - 20;
        bool desk = AskText != null;
        ui.Label95("&Location:", new RectangleF(x, y + 4, 64, 13));
        ui.Edit95(new RectangleF(x + 66, y, cw - 66 - 88, RowH), LocationShown);
        if (ui.Button95("wx95.loc", new RectangleF(c.Right - 10 - 82, y - 1, 82, 23), "&Change...", desk)) AskLocation();
        ui.Text95(Fit95(ui, WeatherFound(), cw - 66), new RectangleF(x + 66, y + 26, cw - 66, 13), Grey95);
        y += 52;
        ui.Label95("Units:", new RectangleF(x, y + 1, 64, 13));
        int u = Fahrenheit ? 1 : 0;
        if (ui.Radios95("wx95.u", x + 66, y, UnitNames95, ref u)) SetUnits(u == 1);
        y += 30;
        ui.Text95(desk ? "A city, or latitude,longitude (47.5,19.04). Empty: the place set in Hexpad. Never guessed from your IP address - a VPN " +
                         "would put it somewhere else."
                       : "Type the place in the desktop settings window (Change...). Empty: the place set in Hexpad. Never guessed from your " +
                         "IP address - a VPN would put it somewhere else.",
            new RectangleF(x, y, cw, 44), K95, false, TextAlignment.Leading, true, true);
        if (ui.Button95("wx95.ok", new RectangleF(c.Right - 10 - 75, c.Bottom - 31, 75, 23), "OK", true, isDefault: true)) _wx95 = false;
    }
}
