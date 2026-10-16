using System;
using System.Drawing;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The setup utility's tube in motion (TerminalMotion.cs): it switches on when the VR panel comes into view and the screen types
// in, it collapses when the panel goes away; the band rolls and the picture dips now and then while it is open in VR (the
// desktop window keeps still); the selection bar slides to the item pointed at; a pressed F-key flashes inverted; the update
// check turns a spinner.
public sealed partial class SettingsView
{
    string _tsFlashId;                      // the key clicked last, lit inverted a moment
    double _tsFlashAt = -10;
    float _tBarFrom = float.NaN, _tBarTo = float.NaN;      // the selection bar's slide
    double _tBarAt = -10;
    Ui _tUi;

    void TBeginFrame(Ui ui)
    {
        _tUi = ui;
        if (!T.Fresh(ui)) return;
        _tsFlashId = null;
        _tBarFrom = _tBarTo = float.NaN;
    }

    // the glass g once the screen is drawn: on / off, typing, the band and the dip (the VR panel only)
    void TScreenFx(Painter p, RectangleF g)
    {
        double now = Clock.Now;
        bool vr = ShownAt > 0;
        if (!vr) return;
        float off = Closing ? Math.Clamp(1 - Vis, 0, 1) : 0;
        var t = T.TubeAt(now - ShownAt, off, true, g.Height);
        if (t.Moving) AnimWake.At(now);
        if (Motion.Full && t.Open >= 1 && off <= 0)
        {
            T.ScanBand(p, g, now);
            T.Flicker(p, g, now);
        }
        T.Apply(p, g, t, g.X + 6);
    }

    void TFlash(string id)
    {
        _tsFlashId = id;
        _tsFlashAt = Clock.Now;
        AnimWake.At(Clock.Now);
    }

    bool TFlashing(string id)
    {
        if (_tsFlashId == null || !Motion.Any || id != _tsFlashId) return false;
        if (Clock.Now >= _tsFlashAt + T.FlashDur) { _tsFlashId = null; return false; }
        AnimWake.At(_tsFlashAt + T.FlashDur);
        return true;
    }

    // where the selection bar is drawn on its way to 'to' (70 ms, eased; at once with Motion Off)
    float TBarY(float to)
    {
        double now = Clock.Now;
        if (float.IsNaN(_tBarTo) || !Motion.Any) { _tBarFrom = _tBarTo = to; return to; }
        if (to != _tBarTo)
        {
            float cur = TBarAt(now);
            _tBarFrom = cur;
            _tBarTo = to;
            _tBarAt = now;
        }
        float y = TBarAt(now);
        if (y != _tBarTo) { AnimWake.At(now); if (_tUi != null) _tUi.Animating = true; }
        return y;
    }

    float TBarAt(double now)
    {
        float k = (float)Math.Clamp((now - _tBarAt) / 0.07, 0, 1);
        if (k >= 1) return _tBarTo;
        k = 1 - (1 - k) * (1 - k);
        return MathF.Round(_tBarFrom + (_tBarTo - _tBarFrom) * k);
    }

    // General: while the update check (or the download, the install) runs, a spinner after its line
    void TUpdateSpinner()
    {
        if (!_h.Updates.Busy) return;
        var (_, umsg, _, _) = _h.Updates.Snapshot();
        var p = _tUi.P;
        float w = string.IsNullOrEmpty(umsg) ? 0 : MathF.Min(196, T.It.TextWidth(p, Fit95(_tUi, umsg, 196), false, Ui.F95));
        T.It.Put(p, T.Spinner(), 524 + w + 6, 191 + 1, T.INK);
    }
}
