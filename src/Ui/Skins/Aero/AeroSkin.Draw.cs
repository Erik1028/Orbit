using System;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// Windows 7's bigger pieces, shared by the Aero screens (WristPanel.Aero, SettingsView.Aero, KeyboardPanel.Aero...): a whole glass
// window with rounded corners, the caption button group, Windows Media Player's round buttons, the clock gadget's dial, a Task
// Manager graph and the frame-time line of a gadget.
public sealed partial class AeroSkin
{
    // a window drawn from scratch: rounded glass (6 px corners), a dark outline, the glass's sheen and streaks, the client area
    // (capH below the top, 4 px in from the sides and the bottom) filled with 'client'; returns the client area
    public static RectangleF Window7(Painter p, RectangleF win, float capH, uint client, float side = 4, bool active = true, bool shadow = false)
    {
        p.Smooth(true);
        if (shadow) p.FillRound(new RectangleF(win.X + 1, win.Y + 2, win.Width, win.Height), A(0xFF000000, 0.22f), 7);     // the drop shadow
        p.FillRound(win, GlassEdge, 6);
        p.Smooth(false);
        var g = RectangleF.Inflate(win, -1, -1);
        p.PushRoundedClip(g, 5);
        Glass7(p, g, active);
        p.PopRoundedClip();
        p.Smooth(true);
        p.StrokeRound(new RectangleF(g.X + 0.5f, g.Y + 0.5f, g.Width - 1, g.Height - 1), A(0xFFFFFFFF, 0.5f), 1, 5);
        p.Smooth(false);
        var cl = new RectangleF(win.X + side, win.Y + capH, win.Width - 2 * side, win.Height - capH - side);
        p.Fill(RectangleF.Inflate(cl, 1, 1), 0xFF7F96B3);
        p.Fill(cl, client);
        return cl;
    }

    // the caption buttons of a normal 7 window: minimise, maximise (joined) and the wide red close box, hanging from the top
    // edge. kind: what each button is; returns their rectangles (for the hit areas)
    public static void CaptionGroup(Painter p, RectangleF[] r, CaptionKind[] kind, bool[] hover, bool[] down)
    {
        for (int i = 0; i < r.Length; i++) CaptionBox(p, r[i], kind[i], true, down[i], hover[i]);
    }

    // a round button of Windows Media Player 12: blue glass (the play button) or silver; the glyph white or dark
    public static void Orb(Painter p, float cx, float cy, float rad, bool blue, bool hover, bool down, string[] glyph, bool enabled)
    {
        var box = new RectangleF(cx - rad, cy - rad, 2 * rad, 2 * rad);
        p.Smooth(true);
        p.Circle(cx, cy + 1.2f, rad + 0.6f, A(0xFF000000, 0.22f));
        p.Circle(cx, cy, rad, blue ? 0xFF143F78 : 0xFF7D8DA3);
        p.Smooth(false);
        var inner = RectangleF.Inflate(box, -1, -1);
        p.PushRoundedClip(inner, rad - 1);
        uint top, bottom;
        if (blue)
        {
            top = !enabled ? 0xFF9DB7D6 : down ? 0xFF3C7FCF : hover ? 0xFF8CC8FF : 0xFF63ACF0;
            bottom = !enabled ? 0xFF6B8CB6 : down ? 0xFF0B3F86 : hover ? 0xFF2E7FD8 : 0xFF1A5EB4;
        }
        else
        {
            top = down ? 0xFFD3E0EE : hover ? 0xFFFFFFFF : 0xFFFBFCFE;
            bottom = down ? 0xFFA7BCD6 : hover ? 0xFFCFE4F8 : 0xFFC6D4E6;
        }
        p.GradientRaw(inner, top, bottom);
        p.Smooth(true);
        p.Circle(cx, cy - rad * 0.62f, rad * 0.78f, A(0xFFFFFFFF, blue ? 0.30f : 0.55f));
        if (blue && !down) p.Circle(cx, cy + rad * 1.05f, rad * 0.75f, A(0xFF9FE0FF, 0.35f));      // the light in its lower edge
        p.Smooth(false);
        p.PopRoundedClip();
        if (glyph == null) return;
        float gx = MathF.Floor(cx - glyph[0].Length / 2f + 0.5f), gy = MathF.Floor(cy - glyph.Length / 2f + 0.5f);
        if (blue) p.Pixels(glyph, gx, gy + 1, A(0xFF0B2E5C, 0.5f));
        p.Pixels(glyph, gx, gy, blue ? 0xFFFFFFFF : enabled ? 0xFF22364E : 0xFF9AA8B8);
    }

    // the desktop clock gadget: a white face in a dark glossy rim, the hour marks, dark hands and the red second hand
    public static void Clock7(Painter p, float cx, float cy, float r, DateTime now)
    {
        p.Smooth(true);
        p.Circle(cx + 0.8f, cy + 1.5f, r + 0.5f, A(0xFF000000, 0.28f));
        p.Circle(cx, cy, r, 0xFF16304F);
        float rim = MathF.Max(2, MathF.Round(r * 0.09f));
        p.Circle(cx, cy, r - 1, 0xFF2E5582);
        p.Circle(cx, cy - 0.5f, r - rim * 0.55f, 0xFF4A74A6);
        p.Circle(cx, cy, r - rim, 0xFFDCE4EE);
        p.Circle(cx - r * 0.06f, cy - r * 0.08f, (r - rim) * 0.93f, 0xFFF2F5F9);
        p.Circle(cx - r * 0.1f, cy - r * 0.14f, (r - rim) * 0.78f, 0xFFFBFCFD);
        float fr = r - rim;
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * MathF.Tau, sx = MathF.Sin(a), sy = -MathF.Cos(a);
            bool major = i % 3 == 0;
            float r0 = fr * (major ? 0.74f : 0.80f), r1 = fr * 0.90f;
            p.RoundLine(cx + sx * r0, cy + sy * r0, cx + sx * r1, cy + sy * r1, 0xFF1D2F45, major ? MathF.Max(1.6f, r * 0.055f) : MathF.Max(1, r * 0.03f));
        }
        float s = now.Second, m = now.Minute + s / 60f, hr = now.Hour % 12 + m / 60f;
        void Hand(float frac, float len, float tail, float w, uint c)
        {
            float a = frac * MathF.Tau, sx = MathF.Sin(a), sy = -MathF.Cos(a);
            p.RoundLine(cx - sx * tail, cy - sy * tail, cx + sx * len, cy + sy * len, c, w);
        }
        Hand(hr / 12f, fr * 0.50f, fr * 0.08f, MathF.Max(2, r * 0.085f), 0xFF1B2838);
        Hand(m / 60f, fr * 0.76f, fr * 0.08f, MathF.Max(1.6f, r * 0.06f), 0xFF1B2838);
        Hand(s / 60f, fr * 0.82f, fr * 0.18f, MathF.Max(1, r * 0.025f), 0xFFD9352B);
        p.Circle(cx, cy, MathF.Max(1.6f, r * 0.07f), 0xFF1B2838);
        p.Circle(cx, cy, MathF.Max(0.8f, r * 0.03f), 0xFFD9352B);
        p.Smooth(false);
    }

    // a dashed line across (classic Painter draws DashedLine solid)
    public static void Dashes(Painter p, float x1, float x2, float y, uint c, float dash = 3, float gap = 3)
    {
        for (float x = x1; x < x2; x += dash + gap) p.Fill(new RectangleF(x, y, MathF.Min(dash, x2 - x), 1), c);
    }

    static readonly Vector2[] _pts = new Vector2[VrStats.FrameHistory + 2];

    // the frames as a line: on glass a white line over a soft fill (a gadget), else Task Manager's green on black with its grid;
    // the budget (one refresh) dashed
    public static void FrameLine(Painter p, VrStats v, RectangleF r, bool glass)
    {
        float budget = 1000f / Math.Max(1, v.DisplayHz), top = budget * 2;
        if (!glass)
        {
            p.Fill(r, 0xFF000000);
            for (float gx = r.X + 6; gx < r.Right; gx += 12) p.Fill(new RectangleF(gx, r.Y, 1, r.Height), 0xFF00602A);
            for (float gy = r.Y + 6; gy < r.Bottom; gy += 12) p.Fill(new RectangleF(r.X, gy, r.Width, 1), 0xFF00602A);
        }
        int n = v.FrameCount;
        float by = MathF.Round(r.Bottom - r.Height * (budget / top));
        Dashes(p, r.X + 2, r.Right - 2, by, glass ? A(0xFFFFFFFF, 0.6f) : 0xFFC8C800);
        if (n < 2) return;
        float step = r.Width / (VrStats.FrameHistory - 1);
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            int idx = (v.FrameHead - n + i + VrStats.FrameHistory) % VrStats.FrameHistory;
            float ms = Math.Clamp(v.FrameMs[idx], 0, top);
            _pts[k++] = new Vector2(r.Right - (n - 1 - i) * step, r.Bottom - 1 - ms / top * (r.Height - 2));
        }
        p.Smooth(true);
        p.PushClip(r);
        if (glass)
        {
            // the area under the line
            var poly = new Vector2[k + 2];
            Array.Copy(_pts, poly, k);
            poly[k] = new Vector2(_pts[k - 1].X, r.Bottom);
            poly[k + 1] = new Vector2(_pts[0].X, r.Bottom);
            p.Polygon(poly, A(0xFFFFFFFF, 0.16f));
        }
        p.Polyline(_pts, k, glass ? 0xFFFFFFFF : 0xFF2CFF4C, glass ? 1.4f : 1.1f);
        p.PopClip();
        p.Smooth(false);
    }

    // white text with a dark halo, centred (the superbar's labels, text on dark glass)
    public void GlowLight(Painter p, string s, RectangleF r, float size = Ui.F95, TextAlignment al = TextAlignment.Center)
    {
        if (string.IsNullOrEmpty(s)) return;
        var g = new RectangleF(r.X, r.Y - 3, r.Width, r.Height + 6);
        uint halo = A(0xFF000000, 0.28f);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (dx != 0 || dy != 0) p.Text(s, new RectangleF(g.X + dx, g.Y + dy, g.Width, g.Height), halo, size, FontWeight.Normal, al, ParagraphAlignment.Center, Font);
        p.Text(s, g, 0xFFFFFFFF, size, FontWeight.Normal, al, ParagraphAlignment.Center, Font);
    }

    // a vertical gradient through a circle (the clip makes the round edge)
    public static void Disc(Painter p, float cx, float cy, float rad, uint top, uint bottom)
    {
        var box = new RectangleF(cx - rad, cy - rad, 2 * rad, 2 * rad);
        p.PushRoundedClip(box, rad);
        p.GradientRaw(box, top, bottom);
        p.PopRoundedClip();
    }
}
