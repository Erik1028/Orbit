using System;
using System.Collections.Generic;
using System.Drawing;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The watch's tube in motion (TerminalMotion.cs): it switches on every time the watch comes into view (or --perfbench's Intro)
// and the text types in; its _ and X switch it off; the band rolls and the picture dips now and then while it is on your wrist;
// the big digits leave their afterglow; a pressed F-key flashes inverted.
public sealed partial class WristPanel
{
    double _tOffAt = -10;                   // its _ or X: the tube switched off then
    bool _tInner;                           // CustomTerm drawing the shared custom face inside itself
    string _tFlashId;                       // the F-key clicked last, lit inverted a moment
    double _tFlashAt = -10;
    // the big readouts' afterglow: by where they stand, what they show and what they showed until when
    readonly Dictionary<(float, float, int), (string cur, string prev, double at)> _tGlow = new();

    // on your wrist in VR, or --perfbench looking at it (Intro): the loops run (not in --snapshot's pictures)
    bool TLive => Ov != null || _introStart > 0;
    double TShownAt => Math.Max(Ov?.ShownAt ?? -10, _introStart);

    // a frame starts: what the style keeps between frames is dropped when the Ui was reset (Settle)
    void TBeginFrame()
    {
        if (!T.Fresh(_ui)) return;
        _tGlow.Clear();
        _tFlashId = null;
        _tOffAt = -10;
    }

    // the glass g once everything on it is drawn: the tube switching on / off, the text typing in, the band, the dip.
    // clip: the areas it applies to (null: all of g) - the custom face's cards on a see-through background
    void TScreenFx(RectangleF g, float cursorX, List<RectangleF> clip = null)
    {
        double now = Clock.Now, shown = TShownAt;
        float off = _tOffAt > shown ? (float)Math.Clamp((now - _tOffAt) / T.OffDur, 0, 1) : 0;
        var t = T.TubeAt(shown > 0 ? now - shown : -1, off, true, g.Height);
        if (t.Moving) AnimWake.At(now);
        if (TLive && Motion.Full && t.Open >= 1 && off <= 0)
        {
            T.ScanBand(_p, g, now, clip);
            T.Flicker(_p, g, now, clip);
        }
        if (t.Still) return;
        if (clip == null) { T.Apply(_p, g, t, cursorX); return; }
        for (int i = 0; i < clip.Count; i++)
        {
            _p.PushClip(clip[i]);
            T.Apply(_p, g, t, cursorX);
            _p.PopClip();
        }
    }

    // switch the tube off (the _ and X keys): the watch goes away while it collapses
    void TOff() => _tOffAt = Clock.Now;

    // an F-key clicked: lit inverted for a moment after the click too (a quick tap shows)
    bool TFlashing(string id)
    {
        if (_tFlashId == null || !Motion.Any || id != _tFlashId) return false;
        double left = _tFlashAt + T.FlashDur - Clock.Now;
        if (left <= 0) { _tFlashId = null; return false; }
        AnimWake.At(_tFlashAt + T.FlashDur);
        return true;
    }

    void TFlash(string id)
    {
        _tFlashId = id;
        _tFlashAt = Clock.Now;
        AnimWake.At(Clock.Now);
    }

    // a big readout at x / capTop: the value it showed before fades out under it (Full)
    void TGlowUnder(string s, float x, float capTop, int n)
    {
        if (!Motion.Full || !TLive || s == null) return;
        var key = (x, capTop, n);
        double now = Clock.Now;
        if (!_tGlow.TryGetValue(key, out var g))
        {
            if (_tGlow.Count > 48) _tGlow.Clear();
            _tGlow[key] = (s, null, -10);
            return;
        }
        if (g.cur != s)
        {
            g = (s, g.cur, now);
            _tGlow[key] = g;
        }
        if (g.prev == null) return;
        float a = T.Afterglow(g.at, now);
        if (a <= 0) { _tGlow[key] = (g.cur, null, -10); return; }
        TCaps(g.prev, x, capTop, T.Ph(0.75f * a), n);
    }

    // ---- the custom face: the shared one, then the tube over it ----

    partial void CustomTerm(PanelData d, ref bool done)
    {
        if (_tInner) return;                // the shared face, drawn from here
        done = true;
        TBeginFrame();
        _tCards.Clear();                    // the widgets' cards and the taskbar, as they are drawn (WidgetTerm, TaskbarTerm)
        _tInner = true;
        try { Custom95(d); }
        finally { _tInner = false; }
        // the cards on nothing: the tube shows on them only (between them is the world)
        TScreenFx(new RectangleF(0, 0, TW95, TH95), 6, C.Background == "cards" ? _tCards : null);
    }

    readonly List<RectangleF> _tCards = new();
}
