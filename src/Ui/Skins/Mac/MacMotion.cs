using System;
using System.Drawing;
using Vortice.Direct2D1;

namespace Orbit;

// Classic Mac motion (Look > Motion), as System 7 / Mac OS 8 moved:
// - ZoomRects: the Finder's zoom when a window opens or closes - a run of dotted outlines (QuickDraw's grey pattern) from where it
//   comes from to the window, about a dozen steps with four of them in flight, accelerating; an opened widget's window, the
//   watch, the settings, the Room and Hexpad panels (Motion Reduced too: it shows where a window went).
// - The menu blink: a picked item flashes twice before the menu goes (General Controls' "Menu blinking"; Full).
// - The wristwatch cursor, its hand going round, where something is busy (Hexpad being looked for); Mac OS 8's indeterminate
//   progress bar - the barber pole - while the update check runs (Full; still otherwise).
// - The Control Strip sliding out of its tab and back in; the window shade rolling the watch up into its title bar.
// - The Happy Mac in Minesweeper reacting to every move, the Notification Manager's icon blinking as a note arrives (Full).
public sealed partial class MacSkin
{
    public override bool AnimatesFade => Motion.Any;

    // ---- ZoomRects ----

    public const double ZoomDur = 0.2;
    const int ZoomSteps = 12, ZoomTrail = 4;
    static readonly float[] ZoomAt = MakeZoom();

    // the outlines' places 0..1: each a quarter bigger a step than the one before (the zoom gathers speed)
    static float[] MakeZoom()
    {
        var f = new float[ZoomSteps + 1];
        double end = Math.Pow(1.25, ZoomSteps) - 1;
        for (int k = 0; k <= ZoomSteps; k++) f[k] = (float)((Math.Pow(1.25, k) - 1) / end);
        return f;
    }

    // the outlines in flight at t (0..1) of a zoom from 'from' to 'to'
    public static void ZoomRects(Painter p, RectangleF from, RectangleF to, float t)
    {
        int cur = (int)(Math.Clamp(t, 0, 1) * (ZoomSteps + ZoomTrail));
        for (int k = cur - ZoomTrail + 1; k <= cur; k++)
        {
            if (k < 0 || k > ZoomSteps) continue;
            float f = ZoomAt[k];
            var r = new RectangleF(MathF.Round(from.X + (to.X - from.X) * f), MathF.Round(from.Y + (to.Y - from.Y) * f),
                MathF.Round(from.Width + (to.Width - from.Width) * f), MathF.Round(from.Height + (to.Height - from.Height) * f));
            DottedFrame(p, r);
        }
    }

    // a window's outline in QuickDraw's grey pattern, XORed onto the screen: dots that show dark on light and light on dark - in
    // VR, over any world, every other pixel black and white
    public static void DottedFrame(Painter p, RectangleF r)
    {
        if (r.Width < 2 || r.Height < 2) return;
        p.FillChecker(new RectangleF(r.X, r.Y, r.Width, 1), 0xFF000000, 0xFFFFFFFF);
        p.FillChecker(new RectangleF(r.X, r.Bottom - 1, r.Width, 1), 0xFF000000, 0xFFFFFFFF);
        p.FillChecker(new RectangleF(r.X, r.Y + 1, 1, r.Height - 2), 0xFF000000, 0xFFFFFFFF);
        p.FillChecker(new RectangleF(r.Right - 1, r.Y + 1, 1, r.Height - 2), 0xFF000000, 0xFFFFFFFF);
    }

    // a small rectangle in the middle of r (where a window zooms out of when it has no icon to come from)
    public static RectangleF Seed(RectangleF r) =>
        new(MathF.Round(r.X + r.Width / 2 - 8), MathF.Round(r.Y + r.Height / 2 - 6), 16, 12);

    // an opened widget's window: the zoom rectangles instead of the 1995 grey outline (nothing with Motion Off)
    public override bool WindowZoom(Painter p, RectangleF from, RectangleF to, float t)
    {
        if (Motion.Any) ZoomRects(p, from, to, t);
        return true;
    }

    // ---- the menu blink ----

    const double BlinkStep = 0.045;
    const int BlinkSteps = 4;               // off, on, off, on - then the menu is gone
    const string FreshKey = "mac.motion";
    Ui _menuUi;
    RectangleF _menuList;
    string[] _menuItems;
    int _menuRow;
    double _menuAt;

    public override void MenuPicked(Ui ui, RectangleF list, string[] items, int row)
    {
        if (!Motion.Full || items == null || row < 0 || row >= items.Length) return;
        (_menuUi, _menuList, _menuItems, _menuRow, _menuAt) = (ui, list, items, row, Clock.Now);
        ui.SpringFrom(FreshKey, 0);         // a reset of the Ui's animations (a snapshot settling) ends the blink
        ui.Animating = true;
    }

    public override void MenuClosing(Ui ui)
    {
        if (ui != _menuUi) return;
        double t = Clock.Now - _menuAt;
        if (t >= BlinkStep * BlinkSteps || !ui.HasSpring(FreshKey) || !Theme.Classic) { _menuUi = null; return; }
        int step = (int)(t / BlinkStep);
        DropList(ui.P, _menuList, _menuItems, step % 2 == 1 ? _menuRow : -1, DropRowH);
        ui.Animating = true;
    }

    // ---- the wristwatch cursor ----

    // the watch (16 x 16): 'k' black, 'w' its white face; the hand drawn over it
    static readonly string[] Watch =
    {
        ".....kkkkkk.....",
        ".....kkkkkk.....",
        ".....kkkkkk.....",
        "....kkkkkkkk....",
        "...kwwwwwwwwk...",
        "..kwwwwwwwwwwk..",
        "..kwwwwwwwwwwk..",
        "..kwwwwwwwwwwkk.",
        "..kwwwwwwwwwwkk.",
        "..kwwwwwwwwwwk..",
        "..kwwwwwwwwwwk..",
        "...kwwwwwwwwk...",
        "....kkkkkkkk....",
        ".....kkkkkk.....",
        ".....kkkkkk.....",
        ".....kkkkkk.....",
    };

    // the hand's pixels from the hub (7, 7) in eight places round the dial
    static readonly (int dx, int dy)[][] Hand =
    {
        new[] { (0, -1), (0, -2) },
        new[] { (1, -1), (2, -2) },
        new[] { (1, 0), (2, 0), (3, 0) },
        new[] { (1, 1), (2, 2) },
        new[] { (0, 1), (0, 2), (0, 3) },
        new[] { (-1, 1), (-2, 2) },
        new[] { (-1, 0), (-2, 0), (-3, 0) },
        new[] { (-1, -1), (-2, -2) },
    };

    // the watch at x, y (s pixels a pixel), its hand going round eight times a second (Full; still otherwise)
    public static void WatchCursor(Painter p, float x, float y, int s = 1)
    {
        int frame = 0;
        if (Motion.Full)
        {
            double now = Clock.Now;
            long k = (long)(now * 8);
            frame = (int)(k % 8);
            AnimWake.At((k + 1) / 8.0);
        }
        for (int row = 0; row < Watch.Length; row++)
        {
            string line = Watch[row];
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                int j = i;
                while (j < line.Length && line[j] == c) j++;
                if (c != '.') p.Fill(new RectangleF(x + i * s, y + row * s, (j - i) * s, s), c == 'k' ? 0xFF000000 : 0xFFFFFFFF);
                i = j;
            }
        }
        p.Fill(new RectangleF(x + 7 * s, y + 7 * s, s, s), 0xFF000000);
        foreach (var (dx, dy) in Hand[frame]) p.Fill(new RectangleF(x + (7 + dx) * s, y + (7 + dy) * s, s, s), 0xFF000000);
    }

    // ---- the barber pole: Mac OS 8's progress bar when it cannot say how far ----

    static ID2D1Bitmap _pole;
    static Painter _polePainter;
    const int PoleTile = 16;

    // its stripes slant and move right, 16 px a second in 15 steps (Full; still otherwise)
    public static void BarberPole(Painter p, RectangleF r)
    {
        Rect(p, r, Grey7, Grey7);
        var inner = RectangleF.Inflate(r, -1, -1);
        if (_pole == null || _polePainter != p)
        {
            _pole?.Dispose();
            var px = new uint[PoleTile * PoleTile];
            for (int y = 0; y < PoleTile; y++)
                for (int x = 0; x < PoleTile; x++)
                    px[y * PoleTile + x] = (x + y) % PoleTile < PoleTile / 2 ? 0xFF7070C0u : 0xFFC8C8F0u;
            _pole = p.CreateBitmap(px, PoleTile, PoleTile);
            _polePainter = p;
        }
        int phase = 0;
        if (Motion.Full)
        {
            double now = Clock.Now;
            long k = (long)(now * 15);
            phase = (int)(k % PoleTile);
            AnimWake.At((k + 1) / 15.0);
        }
        p.PushClip(inner);
        for (float x = inner.X - PoleTile + phase; x < inner.Right; x += PoleTile)
            for (float y = inner.Y; y < inner.Bottom; y += PoleTile)
                p.ImageNearest(_pole, new RectangleF(x, y, PoleTile, PoleTile));
        p.Fill(new RectangleF(inner.X, inner.Y, inner.Width, 1), 0x60FFFFFF);
        p.Fill(new RectangleF(inner.X, inner.Bottom - 1, inner.Width, 1), 0x40000000);
        p.PopClip();
    }

    // true once after the Ui's animations were reset (a snapshot settling every picture)
    public static bool Fresh(Ui ui)
    {
        if (ui.HasSpring(FreshKey)) return false;
        ui.SpringFrom(FreshKey, 0);
        return true;
    }
}
