using System;
using System.Drawing;

namespace Orbit;

// Windows 7's own motion (Look > Motion asks first; XpAeroFx keeps the state):
// - the glass hover glow on buttons, caption buttons (the close box's red spilling round it), tabs, check boxes, sliders, the
//   Explorer items and Control Panel links: up in about a tenth of a second, away in less than half a second (AeroSkin, the
//   screens' own controls);
// - windows open with 7's quick zoom and fade (from 90 % size and transparent to whole, 200 ms) and close the other way
//   (the watch's opened widgets: WristPanel.Aero; What's New);
// - the default button throbs (a slow blue breath), the progress bar's light sweeps through it, the marquee glides while
//   something is busy - loops, Motion Full only, drawn at a low rate and only while they are on show;
// - a drop-down list slides open, a list glides to its row, the notification balloon fades in and out;
// - the superbar's page buttons light up where the pointer is, in the laser's colour; Minesweeper's squares glow under the
//   laser and open in a ripple from the square tapped.
public sealed partial class AeroSkin
{
    public override void BeginCanvas(Painter p, object canvas) => XpAeroFx.Begin(canvas);
    public override double NextFrame => XpAeroFx.NextFrame;

    // the watch's opened window: 7 draws the window itself, zooming and fading (0.2 s); none at all with Motion off (the
    // snapshots: 95's zoom, outline and all - their taps are timed by it)
    public override double ZoomSeconds => XpAeroFx.Settled ? 0.22 : XpAeroFx.Moves ? 0.2 : 0;
    public override bool LiveZoom => XpAeroFx.Moves;

    public override float ListScroll(Painter p, RectangleF box, int top) => XpAeroFx.Ease(XpAeroFx.Scroll, box, top, 0.14f);

    // busy: how far, as 7's green bar; not known, as its marquee
    public override bool BusyBar(Painter p, RectangleF r, float frac)
    {
        if (frac >= 0) Bar7(p, r, frac, BarGreen);
        else Marquee7(p, r);
        return true;
    }

    // 7's marquee: a green glow gliding through the empty trough, one pass every 2 s, drawn 30 times a second (Motion Full; else
    // it rests in the middle)
    public static void Marquee7(Painter p, RectangleF r)
    {
        Bar7(p, r, 0, BarGreen);
        var i = RectangleF.Inflate(r, -1, -1);
        float sw = MathF.Round(Math.Clamp(i.Width * 0.35f, 16, 60));
        float k = 0.5f;
        if (XpAeroFx.Loops)
        {
            k = (float)(XpAeroFx.Now % 2.0 / 2.0);
            XpAeroFx.WantTick(30);
        }
        float x = MathF.Round(i.X - sw + k * (i.Width + sw));
        var seg = new RectangleF(x, i.Y, sw, i.Height);
        p.PushClip(i);
        p.GradientRaw(seg, 0x0037C953, 0xFF37C953, 0x0037C953, 0, 0);
        p.GradientRaw(new RectangleF(seg.X, seg.Y, seg.Width, MathF.Round(seg.Height * 0.5f)), 0x00FFFFFF, 0x80FFFFFF, 0x00FFFFFF, 0, 0);
        p.PopClip();
    }
}
