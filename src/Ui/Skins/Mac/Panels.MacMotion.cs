using System;
using System.Drawing;

namespace Orbit;

// The floating panels' motion in the Classic Mac style (MacMotion.cs): the Room and Hexpad windows zoom out of their middle as
// they come and back in as they go (over their fade); the Control Strip slides out of its tab and back; a note's icon blinks as it
// arrives, the way the Notification Manager blinked it in the menu bar. Only in VR (no overlay: the pictures stay still).

public sealed partial class RoomPanel
{
    // while the panel fades in or out: the zoom rectangles instead of the window (true: drawn)
    bool MacZoom(RectangleF win) => MacSkin.PanelZoom(_p, Ov, win, _vis, !Shown);

    void MacPresent()
    {
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }
}

public sealed partial class DockPanel
{
    bool MacZoom(RectangleF win) => MacSkin.PanelZoom(_p, Ov, win, _vis, !Shown);

    void MacPresent()
    {
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }
}

public sealed partial class TheaterBar
{
    // how far the Control Strip is out of its tab (0: only the tab, 1: all of it), with its fade (in VR)
    float MacStripOut()
    {
        if (Ov == null || !Motion.Any || _vis >= 1) return 1;
        float k = Math.Clamp(_vis, 0, 1);
        return 1 - (1 - k) * (1 - k) * (1 - k);
    }
}

public sealed partial class NotificationPanel
{
    // the note's icon blinks three times as it arrives (Full, in VR): the Notification Manager's blinking icon
    bool MacIconShown(Notification c)
    {
        if (_ov == null || !Motion.Full) return true;
        double t = Clock.Now - c.Shown;
        if (t < 0 || t >= 1.5) return true;
        int step = (int)(t / 0.25);
        AnimWake.At(c.Shown + (step + 1) * 0.25);
        return step % 2 == 0;
    }
}

public sealed partial class MacSkin
{
    // a whole panel window win zooming out of its middle while it fades in (vis 0 -> 1), back into it while it fades out
    // (closing): true when the zoom was drawn (the window is not, then)
    public static bool PanelZoom(Painter p, VrOverlay ov, RectangleF win, float vis, bool closing)
    {
        if (ov == null || !Motion.Any || (vis >= 1 && !closing)) return false;
        float t = Math.Clamp(closing ? 1 - vis : vis, 0, 1);
        p.ClearRect(win);
        if (closing) ZoomRects(p, win, Seed(win), t);
        else ZoomRects(p, Seed(win), win, t);
        AnimWake.At(Clock.Now);
        return true;
    }
}
