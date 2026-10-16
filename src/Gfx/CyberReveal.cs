using System;
using System.Drawing;

namespace Orbit;

// A Cyber panel coming into view and going (the VR settings panel, the keyboard, the Room and Hexpad panels, What's new): drawn
// over what the panel draws, in its own texture, while SteamVR fades the overlay as before.
//  - opening (Full): four yellow corner brackets snap out from the centre to the panel's corners (0.13 s), then a cyan scan line
//    sweeps down and reveals the panel behind it, a faint grid flashing on the rows it has just passed and a few glitch bars riding
//    it (0.05-0.3 s); the brackets fade once it is in. Reduced: only the brackets (the panel is there at once). Off: nothing.
//  - closing (Full, while the overlay fades out): the picture folds into a cyan line across the middle, which then shrinks to a
//    point. Reduced / Off: the fade alone.
// While it runs it asks for the next frame (AnimWake); at rest it is not there - no frame, no cost.
static class CyberReveal
{
    public const double OpenTime = 0.3;

    public struct State
    {
        public bool Clipped;            // a clip was pushed (End pops it)
        public int Kind;                // 0 nothing, 1 opening, 2 closing
        public double T;
        public float Vis;
        public RectangleF All;
    }

    // shownAt: when the panel came into view (Clock.Now); vis: its fade 0..1; closing: it is on its way out
    public static State Begin(Painter p, RectangleF all, double shownAt, float vis, bool closing)
    {
        var st = new State { All = all, Vis = vis };
        if (!Theme.Cyber || !CyberFx.Any) return st;
        double now = Clock.Now;
        if (closing && vis < 1)
        {
            if (!CyberFx.Full) return st;
            st.Kind = 2;
            float e = CyberFx.EaseInOut(CyberFx.Clamp01((vis - 0.18f) / 0.82f));
            float h = all.Height * e;
            p.PushClip(new RectangleF(all.X, all.Y + (all.Height - h) / 2, all.Width, h));
            st.Clipped = true;
            AnimWake.At(now);
            return st;
        }
        double t = now - shownAt;
        if (t < 0 || t >= OpenTime + 0.12) return st;
        st.Kind = 1;
        st.T = t;
        AnimWake.At(now);
        if (CyberFx.Full)
        {
            float scan = CyberFx.EaseInOut(CyberFx.Ramp(t, 0.05, OpenTime - 0.05));
            if (scan < 1)
            {
                p.PushClip(new RectangleF(all.X, all.Y, all.Width, all.Height * scan));
                st.Clipped = true;
            }
        }
        return st;
    }

    // Begin would draw something now (a panel that caches its picture draws it all while it does)
    public static bool Running(double shownAt, float vis, bool closing)
    {
        if (!Theme.Cyber || !CyberFx.Any) return false;
        if (closing && vis < 1) return CyberFx.Full;
        double t = Clock.Now - shownAt;
        return t >= 0 && t < OpenTime + 0.12;
    }

    // where the opening's scan line is (texture px; it has not started: the top)
    public static float ScanY(in State st) =>
        st.All.Y + st.All.Height * CyberFx.EaseInOut(CyberFx.Ramp(st.T, 0.05, OpenTime - 0.05));

    public static void End(Painter p, in State st)
    {
        if (st.Clipped) p.PopClip();
        var a = st.All;
        if (st.Kind == 2)
        {
            // the fold: its two edges, then one line shrinking to the middle
            float e = CyberFx.EaseInOut(CyberFx.Clamp01((st.Vis - 0.18f) / 0.82f));
            float h = a.Height * e, cy = a.Y + a.Height / 2;
            if (e > 0.01f)
            {
                p.Fill(new RectangleF(a.X + 6, cy - h / 2 - 1, a.Width - 12, 2), Theme.WithAlpha(CyberHud.Cyan, 0.9f));
                p.Fill(new RectangleF(a.X + 6, cy + h / 2 - 1, a.Width - 12, 2), Theme.WithAlpha(CyberHud.Cyan, 0.9f));
            }
            else
            {
                float k = CyberFx.Clamp01(st.Vis / 0.18f), w = a.Width * k;
                p.Fill(new RectangleF(a.X + (a.Width - w) / 2, cy - 1.5f, w, 3), CyberHud.Cyan);
            }
            return;
        }
        if (st.Kind != 1) return;
        double t = st.T;
        // the brackets: from a small box in the middle out to the corners, then they fade
        float k1 = CyberFx.EaseOut(CyberFx.Ramp(t, 0, 0.13));
        float fade = 1 - CyberFx.Ramp(t, OpenTime - 0.06, 0.18);
        if (fade > 0.01f)
        {
            float cx = a.X + a.Width / 2, cy = a.Y + a.Height / 2;
            float w0 = MathF.Min(80, a.Width * 0.2f), h0 = MathF.Min(50, a.Height * 0.2f);
            float w = w0 + (a.Width - 12 - w0) * k1, h = h0 + (a.Height - 12 - h0) * k1;
            float arm = MathF.Min(28, MathF.Min(a.Width, a.Height) * 0.08f) * (0.5f + 0.5f * k1);
            CyberFx.Corners(p, new RectangleF(cx - w / 2, cy - h / 2, w, h), Theme.WithAlpha(CyberHud.Yellow, fade), arm, 3);
        }
        if (!CyberFx.Full) return;
        float scan = CyberFx.EaseInOut(CyberFx.Ramp(t, 0.05, OpenTime - 0.05));
        if (scan <= 0 || scan >= 1) return;
        float y = a.Y + a.Height * scan;
        // a faint grid on the rows just revealed, the line itself with a red / cyan split
        float gy0 = MathF.Max(a.Y, y - 90);
        uint g = Theme.WithAlpha(CyberHud.Cyan, 0.07f);
        for (float x = a.X + 24; x < a.Right - 4; x += 24) p.Fill(new RectangleF(x, gy0, 1, y - gy0), g);
        for (float gy = y - 12; gy > gy0; gy -= 24) p.Fill(new RectangleF(a.X + 4, gy, a.Width - 8, 1), g);
        CyberFx.ScanH(p, a.X + 4, a.Right - 4, y, CyberHud.Cyan, 1);
        p.Fill(new RectangleF(a.X + 4, y - 4, a.Width - 8, 1), Theme.WithAlpha(CyberHud.Red, 0.55f));
        p.Fill(new RectangleF(a.X + 4, y + 2, a.Width - 8, 1), Theme.WithAlpha(CyberHud.Cyan, 0.45f));
        // a few bars of glitch riding the line, somewhere else every ~35 ms
        int step = (int)(t / 0.035);
        for (int i = 0; i < 5; i++)
        {
            float bw = a.Width * (0.04f + 0.12f * CyberFx.Hash(i + 7, step)), bx = a.X + 4 + CyberFx.Hash(i, step) * (a.Width - 8 - bw);
            float by = y - 14 + CyberFx.Hash(i + 13, step) * 20;
            uint bc = i == 0 ? CyberHud.Yellow : i % 2 == 0 ? CyberHud.Red : CyberHud.Cyan;
            p.Fill(new RectangleF(bx, by, bw, 1 + (i & 1) * 2), Theme.WithAlpha(bc, 0.5f * (1 - scan)));
        }
    }
}
