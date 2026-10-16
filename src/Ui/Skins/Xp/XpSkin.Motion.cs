using System;
using System.Drawing;

namespace Orbit;

// Windows XP's own motion, as its Performance Options had it (Look > Motion asks first; XpAeroFx keeps the state):
// - "Animate windows when minimizing and maximizing": the watch's opened window comes out of its tile as XP's title bar flying
//   from the tile to the window (and back into the tile for Minesweeper, the room and Hexpad, as 95 did);
// - "Slide open combo boxes": a drop-down list slides out of its box (XpSkin.DropList);
// - "Smooth-scroll list boxes": a list glides to its new row;
// - "Fade or slide ToolTips into view": the notification balloon fades in and out (NotificationPanel.Xp);
// - the themed controls' hot state (the orange rim) comes on and goes off in a tenth of a second (XpSkin);
// - the marquee progress bar while something is busy (Windows Update's "checking"), the green blocks gliding through the trough;
// - Task Manager's graphs scroll with their samples (WristPanel.Xp).
public sealed partial class XpSkin
{
    public override void BeginCanvas(Painter p, object canvas) => XpAeroFx.Begin(canvas);
    public override double NextFrame => XpAeroFx.NextFrame;

    // the window zoom: 95's time; none at all with Motion off (the snapshots: 95's, outline and all - their taps are timed by it)
    public override double ZoomSeconds => XpAeroFx.Settled || XpAeroFx.Moves ? 0.22 : 0;

    // the title bar on its way: the Luna caption at the top of the rectangle between the tile and the window
    public override bool ZoomFrame(Painter p, RectangleF r, float t)
    {
        if (XpAeroFx.Settled) return false;
        int rows = (int)Math.Clamp(MathF.Round(r.Height), 6, 21);
        CaptionBar(p, MathF.Round(r.X), MathF.Round(r.Right), MathF.Round(r.Y), rows, true, false);
        return true;
    }

    // a tenth of a second and a bit to the new row, slowing at the end
    public override float ListScroll(Painter p, RectangleF box, int top) => XpAeroFx.Ease(XpAeroFx.Scroll, box, top, 0.14f);

    // busy: how far, as the green blocks; not known, as the marquee
    public override bool BusyBar(Painter p, RectangleF r, float frac)
    {
        if (frac >= 0) Progress(p, r, frac, 0xFF000080);
        else Marquee(p, r);
        return true;
    }

    // XP's marquee (PBS_MARQUEE): a run of five green blocks gliding through the white trough from the left, again and again, a
    // block at a time ten times a second (only with Motion Full; else the run waits at the start)
    public const float MarqueeSteps = 10;

    public void Marquee(Painter p, RectangleF r)
    {
        p.Fill(RectangleF.Inflate(r, -1, -1), 0xFFFFFFFF);
        Outline(p, r, 0xFF8E8F8F, 1, Face);
        var inner = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
        if (inner.Height < 1 || inner.Width < 2) return;
        float bw = Math.Clamp(MathF.Round(inner.Height * 0.8f), 3, 8);
        int n = (int)((inner.Width + 2) / (bw + 2));
        const int run = 5;
        int step = 0;
        if (XpAeroFx.Loops)
        {
            step = (int)(Math.Floor(XpAeroFx.Now * MarqueeSteps) % (n + run));
            XpAeroFx.WantTick(MarqueeSteps);
        }
        int first = step - run + 1;
        if (!XpAeroFx.Loops) first = 0;
        int h = (int)inner.Height;
        for (int row = 0; row < h; row++)
        {
            uint c = Along(h == 1 ? 0.4f : row / (float)(h - 1), BlockAt, GreenBlock);
            for (int i = Math.Max(0, first); i < Math.Min(n, first + run); i++)
                p.Fill(new RectangleF(inner.X + i * (bw + 2), inner.Y + row, bw, 1), c);
        }
    }
}
