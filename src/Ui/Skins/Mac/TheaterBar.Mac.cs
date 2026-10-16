using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The theater bar in the Classic Mac style: Mac OS 8's Control Strip - a Platinum strip in a black outline, its controls side by
// side between engraved dividers, the rounded tab with its grip at the right end.
public sealed partial class TheaterBar
{
    const int TabMacW = 12;

    partial void TheaterWidthMac(ref int w95, ref bool done)
    {
        w95 += TabMacW;
        done = true;
    }

    partial void TheaterMac(ref bool done)
    {
        done = true;
        var m = (MacSkin)Skins.Mac;
        _canvas95 ??= new Surface(_p.Gpu, _w95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(_w95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        // the strip and its tab
        var strip = new RectangleF(0, 0, _w95 - TabMacW + 1, H95);
        _p.Fill(strip, m.Face);
        _p.Fill(new RectangleF(0, 0, strip.Width, 1), 0xFF000000);
        _p.Fill(new RectangleF(0, H95 - 1, strip.Width, 1), 0xFF000000);
        _p.Fill(new RectangleF(0, 0, 1, H95), 0xFF000000);
        _p.Fill(new RectangleF(1, 1, strip.Width - 1, 1), 0xFFFFFFFF);
        _p.Fill(new RectangleF(1, 1, 1, H95 - 2), 0xFFFFFFFF);
        _p.Fill(new RectangleF(1, H95 - 2, strip.Width - 1, 1), MacSkin.Grey3);
        var tab = new RectangleF(_w95 - TabMacW, 0, TabMacW, H95);
        MacSkin.FillRR(_p, tab, 4, MacSkin.Grey2);
        MacSkin.StrokeRR(_p, tab, 4, 0xFF000000, 0xFF000000);
        _p.Fill(new RectangleF(tab.X, 1, 1, H95 - 2), MacSkin.Grey2);
        for (float gy = 9; gy < H95 - 9; gy += 3)
        {
            _p.Fill(new RectangleF(tab.X + 3, gy, 5, 1), MacSkin.Grey5);
            _p.Fill(new RectangleF(tab.X + 4, gy + 1, 5, 1), 0xFFFFFFFF);
        }

        float x = 4, by = (H95 - 22) / 2;
        ITheaterScreen pick = null;
        bool go = false, exit = false;
        if (_theater)
        {
            for (int i = 0; i < _choices.Count; i++, x += Btn95W + 3)
            {
                var s = _choices[i];
                bool on = s == _current;
                if (_ui.Button95(Ui.Id("thb95", i), new RectangleF(x, by, Btn95W, 22), Fit95(Label(i), Btn95W - 30), true, false, on,
                        s is WindowPanel ? W95Icons.Id.Window : W95Icons.Id.Computer) && !on)
                    pick = s;
            }
        }
        else
        {
            go = _ui.Button95("thb95.go", new RectangleF(x, by, Go95W, 22), "Theater", true, true, false, W95Icons.Id.Theater);
            x += Go95W + 3;
        }
        Divider(x + 2);
        x += 8;
        bool changed = false;
        _ui.Label95("Dim:", new RectangleF(x, 10, 26, 15));
        x += 26;
        float dim = C.Dim;
        if (_ui.Track95("thb95.dim", new RectangleF(x, 4, Track95W, 28), ref dim, 0, 0.9f, true, 10)) { C.Dim = dim; changed = true; }
        x += Track95W + 8;
        _ui.Label95("Size:", new RectangleF(x, 10, 30, 15));
        x += 30;
        float size = SizeNow;
        if (_ui.Track95("thb95.size", new RectangleF(x, 4, Track95W, 28), ref size, Theater.MinWidth, Theater.MaxWidth, true, 10)) { C.Width = size; changed = true; }
        x += Track95W + 8;
        bool curved = C.Curved;
        if (_ui.Check95("thb95.curve", x, 11, "Curved", ref curved)) { C.Curved = curved; changed = true; }
        x += 19 + _ui.LabelWidth95("Curved") + 10;
        bool kb = _ui.Button95("thb95.kb", new RectangleF(x, by, Kb95W, 22), "Keyboard", true, false, KbOn, W95Icons.Id.Keyboard);
        x += Kb95W + 3;
        if (_theater) exit = _ui.Button95("thb95.x", new RectangleF(x, by, Exit95W, 22), "Exit");
        _p.End();
        _p.Begin(_surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, _tw, _th));
        _p.End();
        _ui.End();
        Act(pick, go, exit, changed, kb);

        // an engraved line between the strip's parts
        void Divider(float dx)
        {
            _p.Fill(new RectangleF(dx, 4, 1, H95 - 8), MacSkin.Grey5);
            _p.Fill(new RectangleF(dx + 1, 4, 1, H95 - 8), 0xFFFFFFFF);
        }
    }
}
