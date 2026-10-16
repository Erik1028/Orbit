using System;
using System.Drawing;

namespace Orbit;

// The watch's motion in the Classic Mac style (MacMotion.cs): its window zooms out of the middle every time the watch comes into
// view (or --perfbench's Intro), the close box zooms it back in, the collapse box rolls it up into its title bar (the window
// shade); the Happy Mac reacts to every move; the Hexpad widget wears the wristwatch while Hexpad is being looked for.
public sealed partial class WristPanel
{
    double _mCloseAt = -10, _mShadeAt = -10;    // its close box / collapse box clicked then
    bool _mInner;                               // CustomMac drawing the shared custom face inside itself
    double _mOhUntil = -10;                     // the Happy Mac's "oh" after a move, until then
    int _mMinesSeen = -1;
    MinesGame _mMinesGame;

    double MShownAt => Math.Max(Ov?.ShownAt ?? -10, _introStart);
    bool MLive => Ov != null || _introStart > 0;

    void MBeginFrame()
    {
        if (!MacSkin.Fresh(_ui)) return;
        _mCloseAt = _mShadeAt = -10;
        _mOhUntil = -10;
        _mMinesSeen = -1;
    }

    // the window w zooming out of its middle as the watch comes into view, back into it after the close box: true while it does
    // (then only the zoom is drawn)
    bool MZoom(RectangleF w)
    {
        if (!Motion.Any) return false;
        double now = Clock.Now, shown = MShownAt;
        double z = now - shown;
        if (_mCloseAt > shown)
        {
            double c = (now - _mCloseAt) / MacSkin.ZoomDur;
            if (c < 1)
            {
                MacSkin.ZoomRects(_p, w, MacSkin.Seed(w), (float)c);
                AnimWake.At(now);
            }
            return true;                    // closed: nothing more until it is gone
        }
        if (shown <= 0 || z >= MacSkin.ZoomDur) return false;
        MacSkin.ZoomRects(_p, MacSkin.Seed(w), w, (float)(z / MacSkin.ZoomDur));
        AnimWake.At(now);
        return true;
    }

    // the window shade after the collapse box: how much of the window's height still shows (its full height: none)
    float MShade(RectangleF w)
    {
        if (_mShadeAt <= MShownAt) return w.Height;
        double now = Clock.Now;
        float k = Motion.Any ? (float)Math.Clamp((now - _mShadeAt) / 0.15, 0, 1) : 1;
        if (k < 1) AnimWake.At(now);
        return MathF.Round(w.Height + (24 - w.Height) * (1 - (1 - k) * (1 - k)));
    }

    // the shaded window's bottom edge at h
    void MShadeEdge(RectangleF w, float h)
    {
        _p.Fill(new RectangleF(w.X, w.Y + h - 1, w.Width, 1), 0xFF000000);
        _p.Fill(new RectangleF(w.X + 1, w.Y + h - 2, w.Width - 2, 1), MacSkin.Grey5);
    }

    // the custom face: the shared one, zooming out of the middle as the watch comes into view
    partial void CustomMac(PanelData d, ref bool done)
    {
        if (_mInner) return;
        done = true;
        MBeginFrame();
        if (MZoom(new RectangleF(0, 0, TW95, TH95))) return;
        _mInner = true;
        try { Custom95(d); }
        finally { _mInner = false; }
    }

    // the Happy Mac's face: "oh" a moment after every move (the square opened, the flag set), then happy again (in VR)
    MineArt.Face MacFace(MinesGame g, MineArt.Face face)
    {
        double now = Clock.Now;
        if (g != _mMinesGame || _mMinesSeen < 0) { _mMinesGame = g; _mMinesSeen = g.Version; return face; }
        if (g.Version != _mMinesSeen)
        {
            _mMinesSeen = g.Version;
            if (MLive && Motion.Any && g.State == MinesGame.Phase.Playing) _mOhUntil = now + 0.18;
        }
        if (face == MineArt.Face.Smile && now < _mOhUntil)
        {
            AnimWake.At(_mOhUntil);
            return MineArt.Face.Oh;
        }
        return face;
    }

    // Hexpad being looked for: the wristwatch going round over its name
    bool MacDockWait(RectangleF r, RectangleF c, PanelData d, WatchWidget w, int id)
    {
        if (DockShows(d) || d.Dock?.State != HexpadLink.LinkState.Connecting) return false;
        if (_ui.Clickable(Ui.Id("wdk", id), r, out _, out _)) OpenDetail(w);
        float top = MathF.Floor(c.Y + (c.Height - 30) / 2);
        MacSkin.WatchCursor(_p, MathF.Floor(c.X + (c.Width - 16) / 2), top);
        _ui.Text95(Fit("Connecting", c.Width - 4), new RectangleF(c.X, top + 17, c.Width, 13), MacBlack, false, Vortice.DirectWrite.TextAlignment.Center);
        return true;
    }
}
