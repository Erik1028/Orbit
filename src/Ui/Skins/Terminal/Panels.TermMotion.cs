using System;
using System.Drawing;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The floating panels' tubes in motion on the green terminal (TerminalMotion.cs). They are the shared 95 screens drawn by the
// skin; the hooks draw them as they are, then - only while the tube switches on or off - show the canvas once more with the
// tube's state over it (nothing extra at rest).

public sealed partial class DockPanel
{
    bool _tInner;
    double _tOnAt = -10;

    partial void DockTerm(double now, ref bool done)
    {
        if (_tInner) return;
        done = true;
        _tInner = true;
        try { Draw95(now); }
        finally { _tInner = false; }
        if (Ov == null) return;
        _tOnAt = !Ov.Visible ? Clock.Now : Math.Max(_tOnAt, Ov.ShownAt);
        var g = new RectangleF(3, 3, W95 - 6, H95 - 6);
        var t = T.TubeAt(Clock.Now - _tOnAt, Shown ? 0 : Math.Clamp(1 - _vis, 0, 1), true, g.Height);
        T.Present(_p, Surf, _canvas95, W95, H95, g, t);
    }
}

public sealed partial class RoomPanel
{
    bool _tInner;
    double _tOnAt = -10;

    partial void RoomTerm(DateTime now, ref bool done)
    {
        if (_tInner) return;
        done = true;
        _tInner = true;
        try { Draw95(now); }
        finally { _tInner = false; }
        if (Ov == null) return;
        _tOnAt = !Ov.Visible ? Clock.Now : Math.Max(_tOnAt, Ov.ShownAt);
        var g = new RectangleF(3, 3, W95 - 6, H95 - 6);
        var t = T.TubeAt(Clock.Now - _tOnAt, Shown ? 0 : Math.Clamp(1 - _vis, 0, 1), true, g.Height);
        T.Present(_p, Surf, _canvas95, W95, H95, g, t);
    }
}

public sealed partial class TheaterBar
{
    bool _tInner;

    // the bar under a screen: its strip switches on as it fades in, off as it fades out (no text typing: it is one row)
    partial void TheaterTerm(ref bool done)
    {
        if (_tInner) return;
        done = true;
        _tInner = true;
        try { Draw95(); }
        finally { _tInner = false; }
        if (Ov == null || _vis >= 1) return;
        bool closing = Clock.Now >= _showUntil;
        var t = closing ? T.TubeAt(-1, Math.Clamp(1 - _vis, 0, 1), false, H95) : T.TubeAt(_vis * T.OnDur, 0, false, H95);
        T.Present(_p, _surf, _canvas95, _w95, H95, new RectangleF(1, 1, _w95 - 2, H95 - 2), t);
    }
}

public sealed partial class TerminalSkin
{
    // a panel's canvas shown once more (whole pixels, 2x) with the tube's state over its glass g - only while the tube moves
    public static void Present(Painter p, Surface surf, Surface canvas, float w, float h, RectangleF g, in Tube t)
    {
        if (t.Moving) AnimWake.At(Clock.Now);
        if (t.Still || canvas == null) return;
        p.Begin(surf.Bitmap, 0x00000000, 2);
        p.ImageNearest(canvas.Bitmap, new RectangleF(0, 0, w, h));
        Apply(p, g, t, g.X + 6);
        p.End();
    }
}
