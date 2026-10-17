using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The preset faces (Full, Analog, Minimal, Time) in the Cyber style: each its own HUD layout of the custom face's Cyber cards
// (WristCyber.cs) - Erik (10-16): the custom face was good, the presets were the modern drawing in an olive frame. A face is a few
// made-up widgets on the 6 x 4 grid's columns, drawn the way the custom face draws a card (its boot, its glitch and flicker, its
// buttons), not clickable as cards (the presets open nothing). Rows 0-2 a touch shorter than the grid's: the BUTTONS card keeps the
// old faces' button row - its tiles lie on the six buttons there (y 354-421), the same actions.
//  - Full: the yellow time card (the tall design - TIME, the zone, SEC, the stripes - its digits centred and the date a black tag:
//    CyberTime's hero), the four readings as 1x1 cards beside it (the frame graph instead: FRAMES), POWER and AUDIO under them,
//    BUTTONS. The Wrist tab's options (performance, batteries, media, the graph) decide which are there.
//  - Analog: the same, the time card a CLOCK card: the HUD dial big at the right in a segmented bezel (the hour so far in yellow),
//    a small yellow readout with the time card's digits and the date at the left.
//  - Minimal: the time card big over the BUTTONS card.
//  - Time: the time card alone (no page), nothing to press.
// Motion as on the custom face: the boot when the watch comes into view, the minute's glitch, a value's flicker (Full); no scan
// sweep - at rest a preset draws only its seconds (asked for just after the next one starts).
public sealed partial class WristPanel
{
    // the face being drawn: its cards' made-up widgets (kept: a widget's id keeps its animations), 12 a face
    const int CfFull = 0, CfAnalog = 1, CfMinimal = 2, CfTime = 3, CfSlots = 12;
    readonly WatchWidget[] _cfCards = new WatchWidget[4 * CfSlots];
    static readonly string[] CfButtonIds = { "keyboard", "desktops", "recenter", "layout", "night", "settings" };     // the old faces' row
    static readonly string[] CfReadings = { "fps", "reproj", "gpu", "vram" };
    const float CfTop = 10, CfBottom = 311, CfGap = 10;

    WatchWidget Cf(int face, int slot, string type, int x, int y, int w, int h)
    {
        ref var c = ref _cfCards[face * CfSlots + slot];
        c ??= new WatchWidget();
        (c.Type, c.X, c.Y, c.W, c.H) = (type, x, y, w, h);
        return c;
    }

    // cells on the grid's columns, in three rows over the buttons' card
    static RectangleF CfCell(int x, int y, int w, int h)
    {
        var c = WatchWidgets.CellRect(x, 0, w, 1, false);
        float rh = (CfBottom - CfTop - 2 * CfGap) / 3;
        return RectangleF.FromLTRB(c.Left, MathF.Round(CfTop + y * (rh + CfGap)), c.Right, MathF.Round(CfTop + (y + h) * (rh + CfGap) - CfGap));
    }

    // Draw: a preset face in Cyber (the custom face draws its own)
    void DrawCyberFace(string face, PanelData d)
    {
        _p.ClipText = false;                    // every string is cut or sized to its place (as on the custom face)
        try { CyberFaceCore(face, d); }
        finally { _p.ClipText = true; Bento.Reset(); }
    }

    void CyberFaceCore(string face, PanelData d)
    {
        var now = Now;
        _big = false;
        Bento.Reset();
        switch (face)
        {
            case "time":
                // the card where the modern one was, taller (the tall design's rows) and as wide as its digits
                CfCard(Cf(CfTime, 0, "time", 0, 0, 4, 2), new RectangleF(80, 95, TW - 160, 250), d, now, true);
                break;
            case "minimal":
                CyberPage();
                CfCard(Cf(CfMinimal, 0, "time", 0, 0, 6, 3), CfCell(0, 0, 6, 3), d, now, true);
                CfButtons(CfMinimal, d, now);
                break;
            default:
                CyberPage();
                CfRows(face == "analog" ? CfAnalog : CfFull, d, now);
                CfButtons(face == "analog" ? CfAnalog : CfFull, d, now);
                break;
        }
        // the seconds (every preset shows them): drawn again just after the next one starts (AnimWake.cs)
        if (TestNow == null) AnimWake.At(Clock.Now + 1.02 - DateTime.Now.Millisecond / 1000.0);
        // no scan sweep on a preset (the custom face's, every 7-12 s: WristCyberFx): its next one kept 6 s off while one is in view
        if (_cyScanNext < Clock.Now + 6) _cyScanNext = Clock.Now + 6;
    }

    // Full / Analog over the buttons: the clock (4 x 2) with the readings beside it (or the frame graph), POWER and AUDIO under them.
    // Without the readings POWER goes beside the clock (as tall as it); readings with nothing under them go there instead, the whole
    // width; without both the clock is the whole width.
    void CfRows(int f, PanelData d, DateTime now)
    {
        bool perf = C.ShowPerformance, bat = C.ShowBatteries, media = C.ShowMedia;
        bool batLow = perf && bat, low = media || batLow, perfLow = perf && !low, side = !perfLow && (perf || bat);
        int hh = low || perfLow ? 2 : 3, hw = side ? 4 : 6;
        if (f == CfAnalog) CfCard(Cf(f, 0, "analog", 0, 0, hw, hh), CfCell(0, 0, hw, hh), d, now);
        else CfCard(Cf(f, 0, "time", 0, 0, hw, hh), CfCell(0, 0, hw, hh), d, now, true);
        if (side && !perf) CfCard(Cf(f, 1, "battery", 4, 0, 2, 2), CfCell(4, 0, 2, hh), d, now);
        else if (perf) CfPerf(f, side ? CfCell(4, 0, 2, hh) : CfCell(0, 2, 6, 1), !side, d, now);
        if (!low) return;
        // POWER at the left, AUDIO at the right (its play / pause where the old face had it); a W of 6: previous / next too
        if (batLow) CfCard(Cf(f, 6, "battery", 0, 2, media ? 2 : 6, 1), CfCell(0, 2, media ? 2 : 6, 1), d, now);
        if (media) CfCard(Cf(f, 7, "media", batLow ? 2 : 0, 2, 6, 1), CfCell(batLow ? 2 : 0, 2, batLow ? 4 : 6, 1), d, now);
    }

    // the readings: the frame graph (FRAMES), or the four as cards - 1x1s two by two beside the clock, wider ones in a row under it
    void CfPerf(int f, RectangleF s, bool row, PanelData d, DateTime now)
    {
        if (C.PerfGraph) { CfCard(Cf(f, 1, "graph", row ? 0 : 4, row ? 2 : 0, row ? 6 : 2, row ? 1 : 2), s, d, now); return; }
        int cols = row ? 4 : 2;
        float cw = (s.Width - (cols - 1) * CfGap) / cols, ch = row ? s.Height : MathF.Round((s.Height - CfGap) / 2);
        for (int i = 0; i < 4; i++)
        {
            int cx = i % cols, cy = i / cols;
            float x0 = MathF.Round(s.X + cx * (cw + CfGap)), y0 = cy == 0 ? s.Y : s.Bottom - ch;
            var r = RectangleF.FromLTRB(x0, y0, MathF.Round(s.X + cx * (cw + CfGap) + cw), y0 + ch);
            CfCard(Cf(f, 2 + i, CfReadings[i], row ? cx : 4 + cx, row ? 2 : cy, row ? 2 : 1, 1), r, d, now);
        }
    }

    // the BUTTONS card: the old faces' six, its tiles over where their buttons were
    void CfButtons(int f, PanelData d, DateTime now)
    {
        var w = Cf(f, 11, "buttons", 0, 3, 6, 1);
        w.Items ??= new List<string>(CfButtonIds);
        CfCard(w, RectangleF.FromLTRB(10, 321, TW - 10, TH - 10), d, now);
    }

    // a card as the custom face draws it (WristCustom.DrawPage's Cyber path): the boot's clip and lines round its card and design.
    // hero: the time as a face's big card (CyberTime: the digits centred, the date a tag)
    void CfCard(WatchWidget w, RectangleF r, PanelData d, DateTime now, bool hero = false)
    {
        var k = WatchWidgets.Find(w.Type);
        int id = Wid(w);
        CyCardBegin(id, w, k, r);
        CyberCard(r, k, w, false, false);
        if (!Backgrounds)
        {
            if (k.Id == "analog") CfClock(r, now);
            else if (hero) CyberTime(r, w, now, true);
            else CyberWidget(id, w, k, r, d, now);
        }
        CyCardEnd(id, r);
    }

    // ---- the analog face's clock ----
    // The dial as tall as the card at its right, in its bezel; at the left the CLOCK tag with the zone, a small yellow readout (the
    // time card's digits: the split, the slice, the minute's glitch) and the date under it (the date card's lines)
    void CfClock(RectangleF r, DateTime now)
    {
        float pad = PadX(r), s = MathF.Min(r.Height - 20, r.Width * 0.6f);
        var box = new RectangleF(r.Right - 10 - s, r.Y + (r.Height - s) / 2, s, s);
        float x = r.X + pad, right = MathF.Min(box.X - 16, x + 280);
        CyberCaptionRow(r, x, right, UtcForms(now)[1], CyG, CapClock);
        float ra = s / 2, bw = MathF.Max(4, MathF.Round(ra * 0.065f)), aw = MathF.Max(2, MathF.Round(ra * 0.03f * 2) / 2);
        CfBezel(box.X + ra, box.Y + ra, ra - 0.5f - bw, ra - 0.5f, now);
        CyberDialFace(box.X + ra, box.Y + ra, (ra - bw - 5 - aw / 2) / 1.07f, now, aw);
        float top = r.Y + 38, bottom = r.Bottom - 14, rh = MathF.Round((bottom - top - 12) * 0.56f);
        CfReadout(new RectangleF(x, top, right - x, rh), now);
        CyDateBody(x, right, top + rh + 12, bottom, false, now);
    }

    // The bezel: 60 segments round the dial, leaning like the bars' - the hour so far yellow, this minute white, the rest dark (the
    // boot fills it round like a bar)
    readonly Vector2[] _cfLit = new Vector2[240], _cfOff = new Vector2[240], _cfNow = new Vector2[4];

    void CfBezel(float cx, float cy, float r0, float r1, DateTime now)
    {
        int m = now.Minute, lit = _cyU < 99 ? (int)(m * CyberFx.EaseOut(CyIn(0.18f, 0.4f))) : m;
        const float span = 1f / 60, gap = span * 0.32f, lean = span * 0.22f;
        Vector2 At(float t, float rr) => new(cx + rr * MathF.Sin(t * MathF.Tau), cy - rr * MathF.Cos(t * MathF.Tau));
        int nl = 0, no = 0;
        bool mark = lit == m;
        for (int i = 0; i < 60; i++)
        {
            float t0 = i * span + gap / 2, t1 = (i + 1) * span - gap / 2;
            Vector2[] q;
            int k;
            if (i < lit) { q = _cfLit; k = 4 * nl++; }
            else if (i == m && mark) { q = _cfNow; k = 0; }
            else { q = _cfOff; k = 4 * no++; }
            q[k] = At(t0, r0); q[k + 1] = At(t0 + lean, r1); q[k + 2] = At(t1 + lean, r1); q[k + 3] = At(t1, r0);
        }
        _p.Quads(_cfOff, no, CyberHud.Track);
        _p.Quads(_cfLit, nl, CyY);
        if (mark) _p.Quads(_cfNow, 1, CyW);
    }

    // the readout: a small time card - the digits as big as it allows, the seconds after them (AM / PM over the seconds)
    void CfReadout(RectangleF r, DateTime now)
    {
        var edge = (_cyEdgeR, _cyEdgeC, _cyEdgeTL, _cyEdgeBR);      // (CyberTimeCard makes itself the outline the boot draws in: the clock's card stays it)
        CyberTimeCard(r, CyEdgeShown);
        (_cyEdgeR, _cyEdgeC, _cyEdgeTL, _cyEdgeBR) = edge;
        bool h24 = _cfg.Data.Appearance.Clock24h;
        var culture = CultureInfo.CurrentCulture;
        string t = Clk(now, h24 ? "HH:mm" : "h:mm", culture), sec = Clk(now, "ss", culture);
        var y = CyTimeRect(r);
        float x = r.X + 12, right = y.Right - 10, top = r.Y + 10, floor = y.Bottom - CyHazard(r) - 1 - 7, cap = CyCap;
        float ss = r.Height >= 100 ? 22 : 18, sideW = CyMonoW("00", ss, FontWeight.Bold) + 8;
        float w100 = _p.MeasureWidth(h24 ? "00:00" : "12:00", 100, FontWeight.Bold, Theme.Display);
        float size = MathF.Max(16, MathF.Floor(MathF.Min((floor - top) / cap, 100 * (right - x - sideW - 12) / w100) * 2) / 2);
        float slack = MathF.Max(8, MathF.Min(12, MathF.Ceiling(size * 0.075f)));
        float by = top + (floor - top + size * cap) / 2;
        var (g, seed) = CyTimeGlitch(now);
        float dw = CyGlitch(t, x, by, size, y, g, seed);
        CyMono(sec, x + dw + slack, by, sideW, CyInk, ss, FontWeight.Bold);
        if (!h24) CyMono(now.Hour < 12 ? "AM" : "PM", x + dw + slack, by - size * cap + CyberHud.CapH(_p, CyNote, FontWeight.Bold), sideW, CyInk,
            CyNote, FontWeight.Bold);
    }
}
