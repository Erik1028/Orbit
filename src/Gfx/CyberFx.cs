using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace Orbit;

// Cyber's motion: the shapes and timing every Cyber animation is made of (the watch's in WristCyberFx.cs, the settings' in
// UiCyber.cs, the keyboard's in KeyboardCyber.cs). A HUD that comes alive, not a light show: outlines drawn in from their
// corners, a scan line revealing what is under it, captions typed letter by letter, numbers counting up, segments filling
// block by block, a short glitch on the yellow time card when the minute changes. Small amplitudes, nothing that flashes faster
// than ~3 Hz, nothing over the whole view.
// Look > Motion (Motion.cs): Full - everything; Reduced - only the short transitions that help to follow what happened (a view
// unfolding from its tile, a button's fill wiping on / off, the bracket snap of a tap); Off - nothing moves, the final picture
// at once.
static class CyberFx
{
    // The real app (OrbitApp) - and the bench / --animshot tools that ask for it. Off (every --snapshot check): no effect starts by
    // itself and every transition is drawn finished, so the pictures and their checks are the settled ones.
    public static bool Live;

    public static bool Full => Live && Motion.Full;      // decorative, looping or flashing effects
    public static bool Any => Live && Motion.Any;        // short transitions

    public static float Clamp01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;

    // where t is in [from, from + len], 0..1
    public static float Ramp(double t, double from, double len) => len <= 0 ? (t >= from ? 1 : 0) : Clamp01((float)((t - from) / len));

    public static float EaseOut(float t) { t = Clamp01(t); float u = 1 - t; return 1 - u * u * u; }

    public static float EaseInOut(float t) { t = Clamp01(t); return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2; }

    // the same pseudo-random 0..1 for the same (a, b): a glitch's jitter without a Random (and the same picture for the same step)
    public static float Hash(int a, int b)
    {
        unchecked
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u + 0x9E3779B9u;
            h = (h ^ (h >> 15)) * 2246822519u;
            h = (h ^ (h >> 13)) * 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    // ---- numbers counting up ----
    // s with its (first) number at k of its value - "86" at 0.5 is "43", "9.4" "4.7", "71%" "36%" - in 12 steps, each string made
    // once (a value seen again counts up without new strings)
    const int CountSteps = 12;
    static readonly Dictionary<(string, int), string> _counts = new();

    public static string CountUp(string s, float k)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int step = (int)(Clamp01(k) * CountSteps);
        if (step >= CountSteps) return s;
        if (_counts.TryGetValue((s, step), out var c)) return c;
        if (_counts.Count > 512) _counts.Clear();
        return _counts[(s, step)] = Scale(s, step / (double)CountSteps);
    }

    static string Scale(string s, double k)
    {
        int i0 = 0;
        while (i0 < s.Length && !char.IsAsciiDigit(s[i0])) i0++;
        if (i0 == s.Length) return s;
        long v = 0;
        int dec = -1, i = i0;
        char sep = '.';
        for (; i < s.Length; i++)
        {
            char ch = s[i];
            if (char.IsAsciiDigit(ch)) { v = v * 10 + (ch - '0'); if (dec >= 0) dec++; }
            else if ((ch == '.' || ch == ',') && dec < 0 && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])) { dec = 0; sep = ch; }
            else break;
            if (v > 1_000_000_000) return s;
        }
        long n = (long)Math.Round(v * k);
        string num;
        if (dec <= 0) num = n.ToString(CultureInfo.InvariantCulture);
        else
        {
            long p = 1;
            for (int d = 0; d < dec; d++) p *= 10;
            num = (n / p).ToString(CultureInfo.InvariantCulture) + sep + (n % p).ToString(CultureInfo.InvariantCulture).PadLeft(dec, '0');
        }
        return s[..i0] + num + s[i..];
    }

    // ---- shapes ----

    // L-shaped brackets in all four corners of r, 'arm' long and 'w' wide, 'outset' px outside it (negative: inside)
    public static void Corners(Painter p, RectangleF r, uint c, float arm, float w, float outset = 0)
    {
        float l = r.X - outset, t = r.Y - outset, rt = r.Right + outset, b = r.Bottom + outset;
        p.Fill(new RectangleF(l, t, arm, w), c);
        p.Fill(new RectangleF(l, t, w, arm), c);
        p.Fill(new RectangleF(rt - arm, t, arm, w), c);
        p.Fill(new RectangleF(rt - w, t, w, arm), c);
        p.Fill(new RectangleF(l, b - w, arm, w), c);
        p.Fill(new RectangleF(l, b - arm, w, arm), c);
        p.Fill(new RectangleF(rt - arm, b - w, arm, w), c);
        p.Fill(new RectangleF(rt - w, b - arm, w, arm), c);
    }

    // The outline of a rectangle with cut corners (tl, tr, br, bl; as Painter.StrokeCut draws it round r inset by w / 2) drawn in
    // from its corners: each corner's cut and an arm along both of its sides, k of the way to the middle of the side (1: whole). The
    // arms' ends are lit in 'tip' (0: none) - the pen drawing them.
    public static void OutlineIn(Painter p, RectangleF r, uint c, float w, float tl, float tr, float br, float bl, float k, uint tip = 0)
    {
        k = Clamp01(k);
        if (k <= 0) return;
        float h = w / 2, L = r.X + h, T = r.Y + h, R = r.Right - h, B = r.Bottom - h;
        float top = (R - tr - (L + tl)) / 2 * k, right = (B - br - (T + tr)) / 2 * k, bottom = (R - br - (L + bl)) / 2 * k, left = (B - bl - (T + tl)) / 2 * k;
        // the arms: top (from the top-left cut rightwards, from the top-right one leftwards), right, bottom, left
        p.Fill(new RectangleF(L + tl, T - h, top, w), c);
        p.Fill(new RectangleF(R - tr - top, T - h, top, w), c);
        p.Fill(new RectangleF(R - h, T + tr, w, right), c);
        p.Fill(new RectangleF(R - h, B - br - right, w, right), c);
        p.Fill(new RectangleF(L + bl, B - h, bottom, w), c);
        p.Fill(new RectangleF(R - br - bottom, B - h, bottom, w), c);
        p.Fill(new RectangleF(L - h, T + tl, w, left), c);
        p.Fill(new RectangleF(L - h, B - bl - left, w, left), c);
        if (tl > 0) p.Line(L, T + tl, L + tl, T, c, w);
        if (tr > 0) p.Line(R - tr, T, R, T + tr, c, w);
        if (br > 0) p.Line(R, B - br, R - br, B, c, w);
        if (bl > 0) p.Line(L + bl, B, L, B - bl, c, w);
        if (tip == 0 || k >= 1) return;
        // the pen: a short bright piece at the end of each corner's clockwise arm
        float tw = MathF.Max(w, 2), tlen = 7;
        p.Fill(new RectangleF(L + tl + top - MathF.Min(tlen, top), T - tw / 2, MathF.Min(tlen, top), tw), tip);
        p.Fill(new RectangleF(R - tw / 2, T + tr + right - MathF.Min(tlen, right), tw, MathF.Min(tlen, right)), tip);
        p.Fill(new RectangleF(R - br - bottom, B - tw / 2, MathF.Min(tlen, bottom), tw), tip);
        p.Fill(new RectangleF(L - tw / 2, B - bl - left, tw, MathF.Min(tlen, left)), tip);
    }

    // A horizontal scan line at y across [x0, x1]: a bright line, a fading trail behind it (dir +1: it moves down, the trail above)
    public static void ScanH(Painter p, float x0, float x1, float y, uint c, float a, int dir = 1)
    {
        if (a <= 0.01f || x1 <= x0) return;
        float w = x1 - x0;
        p.Fill(new RectangleF(x0, dir > 0 ? y - 14 : y + 2, w, 12), Theme.WithAlpha(c, 0.07f * a));
        p.Fill(new RectangleF(x0, dir > 0 ? y - 6 : y + 2, w, 4), Theme.WithAlpha(c, 0.14f * a));
        p.Fill(new RectangleF(x0, y - 1, w, 2), Theme.WithAlpha(c, 0.9f * a));
    }

    // the same, vertical (the page swipe's seam): dir +1 - it moves right, the trail at its left
    public static void ScanV(Painter p, float x, float y0, float y1, uint c, float a, int dir)
    {
        if (a <= 0.01f || y1 <= y0) return;
        float h = y1 - y0;
        p.Fill(new RectangleF(dir > 0 ? x - 26 : x + 2, y0, 24, h), Theme.WithAlpha(c, 0.06f * a));
        p.Fill(new RectangleF(dir > 0 ? x - 9 : x + 2, y0, 7, h), Theme.WithAlpha(c, 0.13f * a));
        p.Fill(new RectangleF(x - 1, y0, 2, h), Theme.WithAlpha(c, 0.95f * a));
    }
}
