using System;
using System.Drawing;

namespace Orbit;

// The green terminal's motion (Look > Motion), kept VR-comfortable: no strobing, small amplitudes, the loops slow and drawn at a
// low frame rate, nothing at all with Motion Off.
// - The CRT switching on when a screen comes into view: a bright line across the middle of the black glass opens into the raster
//   (0.2 s; Reduced: the picture just opens, no bright line). Switching off when it goes away: the picture collapses into the
//   line, the line into a dot, the dot fades.
// - The text typed in after it came on, a line a frame (Full).
// - The block cursor blinking, half a second lit, half dark (Full; otherwise it stays lit).
// - A faint band of brighter phosphor rolling down the glass, 10 steps a second (Full; the watch and the setup utility).
// - Now and then (every ~10 s) the picture dips very slightly for a few hundredths of a second, the tube's supply (Full).
// - Big digits that change leave the old ones fading out in the phosphor (Full), an F-key turns inverted when it is pressed and
//   stays so a moment after the click, a busy line turns an ASCII spinner | / - \ (Full; otherwise a steady *).
public sealed partial class TerminalSkin
{
    public override bool AnimatesFade => Motion.Any;

    public const double OnDur = 0.2, OffDur = 0.18, LineDt = 1 / 90.0, FlashDur = 0.09, GlowDur = 0.28;
    public const float LineH = 11;

    // where a tube is at a moment
    public struct Tube
    {
        public float Open;      // 0..1: how much of the glass' height the raster covers (1: all of it)
        public float Glow;      // 0..1: the raster's brightness while it opens / collapses (the bright line at its edges)
        public float Width;     // 0..1: the collapsed line's width at the very end of power-off (1: right across)
        public float Dot;       // 0..1: the last dot's brightness
        public float Typed;     // how far down from the glass' top the text is typed in (+inf: all of it)
        public bool Moving;     // it wants the next frame
        public bool Still => Open >= 1 && float.IsPositiveInfinity(Typed);
    }

    static float EaseOut(float k) => 1 - (1 - k) * (1 - k) * (1 - k);
    static float EaseIn(float k) => k * k;

    // since: seconds since the screen came into view (anything negative: long ago); off: 0..1 how far it is switched off;
    // type: its text is typed in after the power-on (Full); height: the glass' height
    public static Tube TubeAt(double since, float off, bool type, float height)
    {
        var t = new Tube { Open = 1, Width = 1, Typed = float.PositiveInfinity };
        if (!Motion.Any) return t;
        if (off > 0)
        {
            // the picture collapses to the line (first 55 %), the line shrinks to a dot (to 85 %), the dot fades
            float a = Math.Clamp(off / 0.55f, 0, 1), b = Math.Clamp((off - 0.55f) / 0.3f, 0, 1), c = Math.Clamp((off - 0.85f) / 0.15f, 0, 1);
            t.Open = 1 - EaseIn(a);
            t.Glow = Motion.Full ? a : 0;
            t.Width = 1 - EaseIn(b);
            t.Dot = b >= 1 ? 1 - c : 0;
            if (!Motion.Full && a >= 1) t.Width = 0;
            t.Moving = off < 1;
            return t;
        }
        if (since < 0) return t;
        if (since < OnDur)
        {
            float k = (float)(since / OnDur);
            t.Open = EaseOut(k);
            t.Glow = Motion.Full ? 1 - k : 0;
            if (Motion.Full && type) t.Typed = 0;           // the raster comes on blank; the text follows
            t.Moving = true;
            return t;
        }
        if (Motion.Full && type)
        {
            float typed = (float)(Math.Floor((since - OnDur) / LineDt) + 1) * LineH;
            if (typed < height) { t.Typed = typed; t.Moving = true; }
        }
        return t;
    }

    // the tube's state over the glass g once the screen is drawn on it; cx: where the typing cursor stands
    public static void Apply(Painter p, RectangleF g, in Tube t, float cx)
    {
        if (t.Still) return;
        if (t.Typed < g.Height)
        {
            // what is not typed yet is still dark; the cursor at the start of the next line
            float y = g.Y + t.Typed;
            p.Fill(new RectangleF(g.X, y, g.Width, g.Bottom - y), BG);
            if (t.Open >= 1 && y + 10 <= g.Bottom) p.Fill(new RectangleF(cx, y + 1, 5, 9), INK);
        }
        if (t.Open >= 1) return;
        // the raster: a band about the middle, dark glass above and below it
        float bh = MathF.Max(1, MathF.Round(g.Height * t.Open));
        float by = MathF.Floor(g.Y + (g.Height - bh) / 2);
        p.Fill(new RectangleF(g.X, g.Y, g.Width, by - g.Y), BG);
        p.Fill(new RectangleF(g.X, by + bh, g.Width, g.Bottom - by - bh), BG);
        if (t.Width < 1 || t.Dot > 0)
        {
            // the end of power-off: the line shrinking to the middle, then a dot fading
            p.Fill(new RectangleF(g.X, by, g.Width, bh), BG);
            float w = MathF.Round(g.Width * t.Width);
            if (w >= 2) p.Fill(new RectangleF(MathF.Floor(g.X + (g.Width - w) / 2), by, w, 1), HOT);
            else if (t.Dot > 0) p.Fill(new RectangleF(MathF.Floor(g.X + g.Width / 2) - 1, by, 2, 1), Ph(1.3f, (uint)(t.Dot * 255)));
            return;
        }
        if (t.Glow > 0)
        {
            // the raster glowing while it opens (or collapses), its edges the brightest
            p.Fill(new RectangleF(g.X, by, g.Width, bh), Ph(1, (uint)(t.Glow * 0x38)));
            uint edge = Ph(1.3f, (uint)(MathF.Min(1, t.Glow * 1.4f) * 255));
            p.Fill(new RectangleF(g.X, by, g.Width, 1), edge);
            if (bh > 1) p.Fill(new RectangleF(g.X, by + bh - 1, g.Width, 1), edge);
        }
    }

    // ---- the loops (Full, on a screen in VR only) ----

    // a faint band of brighter phosphor rolling down the glass: a pass every 9 s, moved 10 times a second. Its rows come in a few
    // steps of brightness (fewer fills: the watch's at-rest frame stays as cheap as it was)
    const double BandPass = 9, BandSteps = 10;
    const int BandH = 20;
    static readonly (int y, int h, uint c)[] BandParts = MakeBand();

    static (int, int, uint)[] MakeBand()
    {
        var parts = new System.Collections.Generic.List<(int, int, uint)>();
        int level = -1, start = 0;
        for (int i = 0; i <= BandH; i++)
        {
            int l = -1;
            if (i < BandH)
            {
                float a = 1 - MathF.Abs(i - (BandH - 1) / 2f) / (BandH / 2f);
                l = (int)MathF.Round(a * a * 4);         // 0..4
            }
            if (l == level) continue;
            if (level > 0) parts.Add((start, i - start, Ph(1, (uint)(level * 0x05))));
            level = l;
            start = i;
        }
        return parts.ToArray();
    }

    // over the glass g, or only over its areas (the cards of a see-through watch face)
    public static void ScanBand(Painter p, RectangleF g, double now, System.Collections.Generic.List<RectangleF> areas = null)
    {
        double step = Math.Floor(now * BandSteps);
        AnimWake.At((step + 1) / BandSteps);
        float span = g.Height + BandH;
        float top = MathF.Round(g.Y - BandH + (float)(step / BandSteps / BandPass % 1) * span);
        foreach (var (y, h, c) in BandParts)
        {
            var row = RectangleF.Intersect(new RectangleF(g.X, top + y, g.Width, h), g);
            if (row.Height <= 0) continue;
            if (areas == null) { p.Fill(row, c); continue; }
            for (int i = 0; i < areas.Count; i++)
            {
                var part = RectangleF.Intersect(row, areas[i]);
                if (part.Height > 0 && part.Width > 0) p.Fill(part, c);
            }
        }
    }

    // now and then the picture dips a little for a few hundredths of a second: once in every 10 s slot, at a place of its own
    const double FlickSlot = 10, FlickDur = 0.045;
    static readonly uint FlickDim = Sign(0x12000000);

    static double FlickAt(double slot) => slot * FlickSlot + 0.5 + Frac(Math.Sin(slot * 12.9898) * 43758.5453) * (FlickSlot - 1);
    static double Frac(double v) => v - Math.Floor(v);

    public static void Flicker(Painter p, RectangleF g, double now, System.Collections.Generic.List<RectangleF> areas = null)
    {
        double slot = Math.Floor(now / FlickSlot), at = FlickAt(slot);
        if (now >= at && now < at + FlickDur)
        {
            if (areas == null) p.Fill(g, FlickDim);
            else for (int i = 0; i < areas.Count; i++) p.Fill(areas[i], FlickDim);
            AnimWake.At(at + FlickDur);
        }
        else AnimWake.At(now < at ? at : FlickAt(slot + 1));
    }

    // the block cursor: lit, then dark, half a second each (Full; with the wall clock, so it changes with the watch's seconds);
    // always lit otherwise
    public static bool CursorOn()
    {
        if (!Motion.Full) return true;
        double ms = DateTime.Now.TimeOfDay.TotalMilliseconds;
        long k = (long)(ms / 500);
        AnimWake.At(Clock.Now + ((k + 1) * 500 - ms) / 1000 + 0.002);
        return k % 2 == 0;
    }

    // a busy line's spinner: | / - \ eight steps a second (Full); a steady * otherwise
    static readonly string[] SpinChars = { "|", "/", "-", "\\" };

    public static string Spinner()
    {
        if (!Motion.Full) return "*";
        double now = Clock.Now;
        long k = (long)(now * 8);
        AnimWake.At((k + 1) / 8.0);
        return SpinChars[k % 4];
    }

    // a reading that changed: the old one's glow, 0..1, fading over GlowDur (Full)
    public static float Afterglow(double changedAt, double now)
    {
        if (!Motion.Full) return 0;
        float a = 1 - (float)((now - changedAt) / GlowDur);
        if (a <= 0) return 0;
        AnimWake.At(now + 1 / 30.0);
        return a;
    }

    // true once after the Ui's animations were reset (a snapshot settling every picture): what a style keeps between frames
    // (an afterglow, a pressed key's flash) is dropped then
    const string FreshKey = "term.motion";

    public static bool Fresh(Ui ui)
    {
        if (ui.HasSpring(FreshKey)) return false;
        ui.SpringFrom(FreshKey, 0);
        return true;
    }
}
