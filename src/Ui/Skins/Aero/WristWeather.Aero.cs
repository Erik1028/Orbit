using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The weather in Windows 7: its desktop Weather gadget. A glossy card the colour of the sky (blue by day, deep blue by night, grey
// under clouds, slate in rain, icy in snow, violet in a storm), the weather's picture big in its top-left corner, the temperature in
// big white Segoe UI Light, the place under it; a big gadget has the next days (a wide one the next hours) on a darker strip of glass.
// Opened: the gadget's flyout - the same card, big: now with its details beside it, the next 12 hours and 6 days on dark glass, and
// a close box at the top right (a gadget has no title bar). Drawn in 7's own way; white text with a soft shadow, as the gadget had.
public sealed partial class WristPanel
{
    static (uint top, uint bottom) Sky7(WxKind k, bool night) => k switch
    {
        WxKind.Clear or WxKind.Partly => night ? (0xFF2B4170, 0xFF0F1D3D) : (0xFF5FA8EE, 0xFF2766B8),
        WxKind.Storm => (0xFF5F5486, 0xFF2C2550),
        WxKind.Snow => (0xFF7FA3CC, 0xFF46699A),
        WxKind.Rain or WxKind.Drizzle => (0xFF6A84A2, 0xFF33496A),
        _ => night ? (0xFF3D4A5E, 0xFF1C2433) : (0xFF8197B0, 0xFF4E6178),
    };

    const uint White7 = 0xFFFFFFFF, Pale7 = 0xFFDCE9F8, Glass7 = 0x4004112A;

    // white text with the gadget's soft shadow under it
    void Text7(string s, RectangleF r, float size, TextAlignment al = TextAlignment.Leading, uint c = White7, bool light = false, bool bold = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        string fam = light ? AeroSkin.LightFont : Ae.Font;
        var wt = bold ? FontWeight.Bold : FontWeight.Normal;
        _p.Text(s, new RectangleF(r.X + 1, r.Y + 1, r.Width, r.Height), 0x70000000, size, wt, al, ParagraphAlignment.Center, fam);
        _p.Text(s, r, c, size, wt, al, ParagraphAlignment.Center, fam);
    }

    // a strip of darker glass (the gadget's forecast band)
    void Glass7Strip(RectangleF r)
    {
        _p.Fill(r, Glass7);
        _p.Fill(new RectangleF(r.X, r.Y, r.Width, 1), 0x30FFFFFF);
    }

    void WeatherTileAero(RectangleF r, PanelData d, WatchWidget w)
    {
        var t = Wxt(d);
        var (top, bottom) = t.Has ? Sky7(t.Kind, t.Night) : (0xFF4F97E2, 0xFF2A69BE);
        AeroSkin.Tile(_p, r, top, bottom);
        var c = RectangleF.Inflate(r, -4, -3);
        if (!t.Has)
        {
            bool two = c.Width >= 90 && c.Height >= 40 && !string.IsNullOrEmpty(t.Hint);
            float y = MathF.Floor(c.Y + (c.Height - (two ? 32 : 16)) / 2);
            Text7(WxFit(c.Width >= 90 ? t.Msg : t.Short, c.Width - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(c.X, y, c.Width, 16), 11, TextAlignment.Center);
            if (two) Text7(WxFit(t.Hint, c.Width - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(c.X, y + 16, c.Width, 16), 11, TextAlignment.Center, Pale7);
            return;
        }
        uint ink = t.Stale ? Pale7 : White7;
        if (c.Width < 70)
        {
            // a 1x1: the picture, the temperature under it
            float s = MathF.Min(c.Height - 16, 40);
            WeatherGlyphs.Draw(_p, t.Kind, t.Night, new RectangleF(MathF.Floor(c.X + (c.Width - s) / 2), c.Y, s, s), WeatherGlyphs.Colour, true);
            Text7(t.Temp, new RectangleF(c.X, c.Bottom - 17, c.Width, 17), 13, TextAlignment.Center, ink, false, true);
            return;
        }
        bool tall = c.Height >= 70, wide = !tall && c.Width >= 150;
        float band = tall ? MathF.Min(46, c.Height * 0.42f) : 0;
        float nowH = c.Height - band;
        // the picture, big in the top-left corner; the temperature beside it in Segoe UI Light, the place (or the conditions) under it
        float gs = MathF.Min(nowH - 2, wide ? 44 : MathF.Min(c.Width * 0.46f, 52));
        WeatherGlyphs.Draw(_p, t.Kind, t.Night, new RectangleF(c.X - 1, c.Y + (nowH - gs) / 2 - 1, gs, gs), WeatherGlyphs.Colour, true);
        float nowW = wide ? MathF.Min(c.Width, 112) : c.Width;
        float tx = c.X + gs + 2, tw = c.X + nowW - tx;
        float ts = MathF.Min(MathF.Floor(nowH * 0.5f), 30);
        while (ts > 13 && WAero(t.Temp, ts, FontWeight.Normal, AeroSkin.LightFont) > tw) ts--;
        float ty = MathF.Floor(c.Y + (nowH - ts * 1.25f - 15) / 2);
        Text7(t.Temp, new RectangleF(tx, ty, tw + 2, ts * 1.25f), ts, TextAlignment.Trailing, ink, ts >= 18);
        Text7(WxFit(t.PlaceForms[0], tw, 11, FontWeight.Normal, Ae.Font), new RectangleF(tx - 8, ty + ts * 1.25f, tw + 10, 15), 11, TextAlignment.Trailing, Pale7);
        if (tall)
        {
            // the next days on the darker strip
            var b = new RectangleF(r.X + 1, c.Bottom - band, r.Width - 2, band + 2);
            Glass7Strip(b);
            int n = Math.Min(t.DayN, Math.Max(1, (int)(b.Width / 34)));
            float dw = (b.Width - 4) / n, ps = MathF.Min(band - 30, 20);
            for (int i = 0; i < n; i++)
            {
                float x = b.X + 2 + i * dw;
                Text7(WxFit(t.DayName[i], dw - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(x, b.Y + 1, dw, 14), 11, TextAlignment.Center, Pale7);
                WeatherGlyphs.Draw(_p, t.DayK[i], false, new RectangleF(MathF.Floor(x + (dw - ps) / 2), b.Y + 15, ps, ps), WeatherGlyphs.Colour, true);
                Text7(WxFit(t.DayHiLo[i], dw - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(x, b.Bottom - 16, dw, 14), 11, TextAlignment.Center);
            }
        }
        else if (wide)
        {
            // the next hours on the darker strip at the right
            var b = new RectangleF(c.X + nowW + 4, r.Y + 1, r.Right - 1 - (c.X + nowW + 4), r.Height - 2);
            Glass7Strip(b);
            int n = Math.Min(t.HourN, (int)(b.Width / 28));
            float cw = n > 0 ? b.Width / n : 0, ps = MathF.Min(b.Height - 30, 18);
            for (int i = 0; i < n; i++)
            {
                float x = b.X + i * cw;
                Text7(WxFit(t.HourShort[i], cw - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(x, b.Y + 1, cw, 14), 11, TextAlignment.Center, Pale7);
                WeatherGlyphs.Draw(_p, t.HourK[i], t.HourNight[i], new RectangleF(MathF.Floor(x + (cw - ps) / 2), b.Y + 15, ps, ps), WeatherGlyphs.Colour, true);
                Text7(t.HourT[i], new RectangleF(x, b.Bottom - 15, cw, 14), 11, TextAlignment.Center, ink);
            }
        }
    }

    // the gadget's flyout, the window's whole area
    void WeatherFlyoutAero(RectangleF win, PanelData d)
    {
        var t = Wxt(d);
        var (top, bottom) = t.Has ? Sky7(t.Kind, t.Night) : (0xFF4F97E2, 0xFF2A69BE);
        AeroSkin.Tile(_p, win, top, bottom);
        var rc = new RectangleF(win.Right - 6 - 27, win.Y + 5, 27, 17);
        bool x = _ui.Clickable("wd95x", RectangleF.Inflate(rc, 4, 4), out bool xh, out bool xd);
        AeroSkin.CaptionBox(_p, rc, CaptionKind.Close, true, xd, xh);
        if (x) { CloseDetail(); return; }
        if (!t.Has)
        {
            float my = win.Y + win.Height / 2 - 22;
            Text7(WxFit(t.Msg, win.Width - 40, 15, FontWeight.Normal, Ae.Font), new RectangleF(win.X + 20, my, win.Width - 40, 22), 15, TextAlignment.Center);
            if (!string.IsNullOrEmpty(t.Hint))
                Text7(WxFit(t.Hint, win.Width - 40, 11, FontWeight.Normal, Ae.Font), new RectangleF(win.X + 20, my + 24, win.Width - 40, 16), 11, TextAlignment.Center, Pale7);
            return;
        }
        uint ink = t.Stale ? Pale7 : White7;
        // now: the picture, the temperature big, the place, the conditions, the day's highest / lowest
        WeatherGlyphs.Draw(_p, t.Kind, t.Night, new RectangleF(win.X + 6, win.Y + 6, 70, 70), WeatherGlyphs.Colour, true);
        float tx = win.X + 80, tw = 112;
        Text7(t.TempU, new RectangleF(tx, win.Y + 6, tw, 44), 36, TextAlignment.Leading, ink, true);
        Text7(WxFit(t.Place, tw, 12, FontWeight.Bold, Ae.Font), new RectangleF(tx, win.Y + 50, tw + 4, 16), 12, TextAlignment.Leading, White7, false, true);
        Text7(WxFit(t.Cond, tw, 11, FontWeight.Normal, Ae.Font), new RectangleF(tx, win.Y + 66, tw + 4, 15), 11, TextAlignment.Leading, Pale7);
        Text7(WxFit(t.HiLoSlash, tw, 11, FontWeight.Normal, Ae.Font), new RectangleF(tx, win.Y + 81, tw + 4, 15), 11, TextAlignment.Leading, Pale7);
        // the details: a name and its value on each line
        float dx = win.X + 196, dw = win.Right - 10 - dx;
        Line7(dx, win.Y + 26, dw, "Feels like", t.Feels);
        Line7(dx, win.Y + 39, dw, "Humidity", t.Hum);
        Line7(dx, win.Y + 52, dw, "Wind", t.Wind);
        Line7(dx, win.Y + 65, dw, "UV index", t.Uv);
        Line7(dx, win.Y + 78, dw, "Sunrise", t.Rise);
        Line7(dx, win.Y + 91, dw, "Sunset", t.Set);
        // the next 12 hours, 6 days on dark glass
        var hb = new RectangleF(win.X + 1, win.Y + 110, win.Width - 2, 46);
        Glass7Strip(hb);
        float cw = (hb.Width - 4) / WxHours;
        for (int i = 0; i < t.HourN; i++)
        {
            float cx = hb.X + 2 + i * cw;
            Text7(WxFit(t.HourShort[i], cw - 1, 11, FontWeight.Normal, Ae.Font), new RectangleF(cx, hb.Y + 1, cw, 14), 11, TextAlignment.Center, Pale7);
            WeatherGlyphs.Draw(_p, t.HourK[i], t.HourNight[i], new RectangleF(MathF.Floor(cx + (cw - 16) / 2), hb.Y + 15, 16, 16), WeatherGlyphs.Colour, true);
            Text7(t.HourT[i], new RectangleF(cx, hb.Y + 31, cw, 14), 11, TextAlignment.Center, ink);
        }
        var db = new RectangleF(win.X + 1, hb.Bottom + 4, win.Width - 2, win.Bottom - 1 - hb.Bottom - 4);
        Glass7Strip(db);
        float dcw = (db.Width - 4) / WxDays;
        for (int i = 0; i < t.DayN; i++)
        {
            float cx = db.X + 2 + i * dcw;
            Text7(WxFit(t.DayName[i], dcw - 2, 11, i == 0 ? FontWeight.Bold : FontWeight.Normal, Ae.Font), new RectangleF(cx, db.Y + 1, dcw, 14), 11,
                TextAlignment.Center, White7, false, i == 0);
            WeatherGlyphs.Draw(_p, t.DayK[i], false, new RectangleF(MathF.Floor(cx + (dcw - 20) / 2), db.Y + 15, 20, 20), WeatherGlyphs.Colour, true);
            Text7(WxFit(t.DayHiLo[i], dcw - 2, 11, FontWeight.Normal, Ae.Font), new RectangleF(cx, db.Bottom - 16, dcw, 14), 11, TextAlignment.Center, ink);
        }
    }

    // a detail line of the flyout: its name pale, its value white at the right
    void Line7(float x, float y, float w, string name, string value)
    {
        float vw = WAero(value, 11) + 2;
        Text7(WxFit(Lang.T(name), w - vw - 6, 11, FontWeight.Normal, Ae.Font), new RectangleF(x, y, w - vw - 4, 14), 11, TextAlignment.Leading, Pale7);
        Text7(value, new RectangleF(x + w - vw - 2, y, vw + 2, 14), 11, TextAlignment.Trailing);
    }
}
