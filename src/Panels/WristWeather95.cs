using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The weather in the retro styles that draw the shared 95 windows (95, XP, the Mac; 7 and the Terminal have their own: Skins/Aero,
// Skins/Terminal). 1995 had no weather program, so this is one made of its parts: the widget a little window with the era's 16 / 32 px
// pictures (pixel art in the VGA colours; XP's smooth ones), the temperature bold, the conditions, the day's highest and lowest, the next
// days (2x2) or hours (4x1). Opened, a dialog: now with its picture, a column of details (feels like, humidity, wind, UV, sunrise,
// sunset), the next 12 hours in a white box, 6 days as raised panels, where and when in the button row beside OK.
public sealed partial class WristPanel
{
    // the picture: XP's smooth, the others' pixels
    void Pic95(WxKind k, bool night, float x, float y, int size)
    {
        if (Theme.Retro == RetroStyle.XP) WeatherGlyphs.Draw(_p, k, night, new RectangleF(x, y, size, size), WeatherGlyphs.Colour, true);
        else WeatherGlyphs.Pixel(_p, k, night, x, y, size);
    }

    // a text cut to w in the 95 font, kept with the weather's other texts
    string WxFit95(string s, float w, bool bold = false)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var key = (s, w, Ui.F95, bold ? -1 : -2);
        if (_wxFits.TryGetValue(key, out var done)) return done;
        if (_wxFits.Count > 256) _wxFits.Clear();
        return _wxFits[key] = Fit(s, w, Ui.F95, bold);
    }

    // c: the window's inside (the style's frame is drawn)
    void Weather95(RectangleF c, PanelData d, WatchWidget w)
    {
        var t = Wxt(d);
        if (!t.Has)
        {
            bool two = c.Height >= 30 && c.Width >= 90 && !string.IsNullOrEmpty(t.Hint);
            float y = MathF.Floor(c.Y + (c.Height - (two ? 28 : 13)) / 2);
            _ui.Text95(WxFit95(c.Width >= 90 ? t.Msg : t.Short, c.Width - 4), new RectangleF(c.X, y, c.Width, 13), Blk95, false, TextAlignment.Center);
            if (two) _ui.Text95(WxFit95(t.Hint, c.Width - 4), new RectangleF(c.X, y + 15, c.Width, 13), Gry95, false, TextAlignment.Center);
            return;
        }
        uint ink = t.Stale ? Gry95 : Blk95;
        if (c.Width < 70)
        {
            // a 1x1: the picture over the temperature
            int s = c.Height >= 56 ? 32 : 16;
            float y = MathF.Floor(c.Y + (c.Height - s - 16) / 2);
            Pic95(t.Kind, t.Night, MathF.Floor(c.X + (c.Width - s) / 2), y, s);
            _ui.Text95(t.Temp, new RectangleF(c.X, y + s + 1, c.Width, 15), ink, true, TextAlignment.Center, size: 13);
            return;
        }
        bool tall = c.Height >= 70;
        // now: the picture, the temperature bold, the conditions (and the day's highest / lowest where there is a line for them)
        float nowW = tall || c.Width < 150 ? c.Width : MathF.Min(c.Width, 128);
        float ny = tall ? c.Y + 1 : MathF.Floor(c.Y + (c.Height - 44) / 2);
        Pic95(t.Kind, t.Night, c.X + 2, tall ? ny : MathF.Floor(c.Y + (c.Height - 32) / 2), 32);
        float tx = c.X + 38, tw = nowW - 40;
        _ui.Text95(t.TempU, new RectangleF(tx, ny, tw, 18), ink, true, TextAlignment.Leading, size: 16);
        _ui.Text95(WxFit95(t.Cond, tw), new RectangleF(tx, ny + 18, tw, 13), Blk95);
        if (!tall || c.Height >= 100) _ui.Text95(WxFit95(t.HiLoSlash, tw), new RectangleF(tx, ny + 31, tw, 13), Gry95);
        if (!tall)
        {
            // a wide one: the next hours beside it
            if (nowW >= c.Width) return;
            float hx = c.X + nowW + 4, hw = c.Right - hx;
            int n = Math.Min(t.HourN, (int)(hw / 24));
            float cw = n > 0 ? hw / n : 0, hy = MathF.Floor(c.Y + (c.Height - 42) / 2);
            for (int i = 0; i < n; i++)
            {
                float x = hx + i * cw;
                _ui.Text95(WxFit95(t.HourShort[i], cw), new RectangleF(x, hy, cw, 13), i == 0 ? Blk95 : Gry95, false, TextAlignment.Center);
                Pic95(t.HourK[i], t.HourNight[i], MathF.Floor(x + (cw - 16) / 2), hy + 13, 16);
                _ui.Text95(t.HourT[i], new RectangleF(x, hy + 29, cw, 13), ink, false, TextAlignment.Center);
            }
            return;
        }
        // a tall one: the next days along the bottom
        int nd = Math.Min(t.DayN, Math.Max(1, (int)(c.Width / 34)));
        float dw = c.Width / nd, dy = c.Bottom - 43;
        _p.EtchedLine(c.X + 2, c.Right - 2, dy - 3);
        for (int i = 0; i < nd; i++)
        {
            float x = c.X + i * dw;
            _ui.Text95(WxFit95(t.DayName[i], dw - 2), new RectangleF(x, dy, dw, 13), Blk95, false, TextAlignment.Center);
            Pic95(t.DayK[i], false, MathF.Floor(x + (dw - 16) / 2), dy + 14, 16);
            _ui.Text95(WxFit95(t.DayHiLo[i], dw - 2), new RectangleF(x, dy + 30, dw, 13), ink, false, TextAlignment.Center);
        }
    }

    // Opened, in the window the style draws around it (title, X, OK): c is its inside, the OK row under it
    void Weather95Detail(RectangleF c, PanelData d)
    {
        var t = Wxt(d);
        // the button row's left: where, and when it was read (red when it is an old report)
        float sw = c.Right - 75 - 8 - c.X;
        string status = t.Has ? t.Status95 : t.Msg;
        _ui.Text95(WxFit95(status, sw), new RectangleF(c.X, c.Bottom + 10, sw, 13), t.Stale ? 0xFF800000 : Blk95);
        if (!t.Has)
        {
            W95Icons.Draw(_p, W95Icons.Id.Info32, c.X + 12, c.Y + 24);
            _ui.Text95(WxFit95(t.Msg, c.Width - 64, true), new RectangleF(c.X + 56, c.Y + 26, c.Width - 60, 15), Blk95, true);
            if (!string.IsNullOrEmpty(t.Hint)) _ui.Text95(WxFit95(t.Hint, c.Width - 64), new RectangleF(c.X + 56, c.Y + 44, c.Width - 60, 13), Blk95);
            return;
        }
        uint ink = t.Stale ? Gry95 : Blk95;
        // now
        Pic95(t.Kind, t.Night, c.X + 2, c.Y + 3, 32);
        _ui.Text95(t.TempU, new RectangleF(c.X + 40, c.Y, 90, 22), ink, true, TextAlignment.Leading, size: 18);
        _ui.Text95(WxFit95(t.Cond, 92), new RectangleF(c.X + 40, c.Y + 22, 92, 13), Blk95);
        _ui.Text95(WxFit95(t.HiLoSlash, 130), new RectangleF(c.X + 2, c.Y + 38, 130, 13), Gry95);
        // the details: three columns, each a name over its value
        float dx = c.X + 134, colW = MathF.Floor((c.Right - dx) / 3);
        Cell95(dx, c.Y, colW, "Feels like", t.Feels, ink);
        Cell95(dx + colW, c.Y, colW, "Humidity", t.Hum, ink);
        Cell95(dx + 2 * colW, c.Y, colW, "Wind", t.Wind, ink);
        Cell95(dx, c.Y + 27, colW, "UV index", t.Uv, ink);
        Cell95(dx + colW, c.Y + 27, colW, "Sunrise", t.Rise, ink);
        Cell95(dx + 2 * colW, c.Y + 27, colW, "Sunset", t.Set, ink);
        _p.EtchedLine(c.X, c.Right, c.Y + 56);
        // the next 12 hours in a white box
        var hb = new RectangleF(c.X, c.Y + 60, c.Width, 44);
        _p.Fill(hb, Wht95);
        _p.Bevel(hb, Painter.Edge.Sunken);
        var hi = RectangleF.Inflate(hb, -2, -2);
        float cw = hi.Width / WxHours;
        for (int i = 0; i < t.HourN; i++)
        {
            float x = hi.X + i * cw;
            _ui.Text95(WxFit95(t.HourShort[i], cw), new RectangleF(x, hi.Y, cw, 12), i == 0 ? Blk95 : Gry95, false, TextAlignment.Center);
            Pic95(t.HourK[i], t.HourNight[i], MathF.Floor(x + (cw - 16) / 2), hi.Y + 12, 16);
            _ui.Text95(t.HourT[i], new RectangleF(x, hi.Y + 28, cw, 12), ink, false, TextAlignment.Center);
        }
        // 6 days
        float dy = c.Y + 107, dh = c.Bottom - dy, dw = MathF.Floor((c.Width - 5 * 3) / WxDays);
        for (int i = 0; i < t.DayN; i++)
        {
            var p = new RectangleF(c.X + i * (dw + 3), dy, dw, dh);
            _p.Fill(p, Theme.W95Face);
            _p.Bevel(p, Painter.Edge.Raised);
            _ui.Text95(WxFit95(t.DayName[i], dw - 4, i == 0), new RectangleF(p.X, p.Y + 1, dw, 13), Blk95, i == 0, TextAlignment.Center);
            Pic95(t.DayK[i], false, MathF.Floor(p.X + (dw - 16) / 2), p.Y + 14, 16);
            _ui.Text95(WxFit95(t.DayHiLo[i], dw - 4), new RectangleF(p.X, p.Y + 29, dw, 13), ink, false, TextAlignment.Center);
        }
    }

    // a detail: its name, its value bold under it
    void Cell95(float x, float y, float w, string name, string value, uint ink)
    {
        _ui.Text95(WxFit95(name, w - 4), new RectangleF(x, y, w - 2, 13), Gry95);
        bool bold = _ui.Width95(value, true) <= w - 4;          // (the Mac's bold is wide: plain where bold does not fit)
        _ui.Text95(WxFit95(value, w - 4, bold), new RectangleF(x, y + 13, w - 2, 13), ink, bold);
    }
}
