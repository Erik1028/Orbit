using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The settings in the Cyber style: a HUD console round the same pages (each page's controls are the shared Ui ones, drawn as the
// HUD's own in UiHud.cs - so whatever a page gets later is part of it too). The chrome:
//  - the window: near black, an olive frame with its corners cut (the cuts yellow), the nav and the page as two cards with the
//    watch's dark-red edge, the page's card on a faint grid;
//  - the header: a reticle for the logo, the name in the condensed bold, a monospace line under it; the SteamVR link as a readout
//    (LINK: STEAMVR 90 HZ, red when it is down) with the live frame rate and the headset's battery beside it; the three buttons
//    (Keyboard / Desktops lit cyan while shown);
//  - the nav: a column of HUD tabs - index, icon, name in capitals; the open one a lit plate with a yellow marker bar and cyan
//    brackets that slides to the tab picked; a SYS block under the tabs (build, link, time in VR) over a strip of hazard stripes.
// Motion (Full): a tab picked - a cyan scan line sweeps down the page and reveals it (its tags type in behind it, UiCyber.cs);
// the VR panel opening / closing - CyberReveal. Reduced: the modern fade; Off: at once.
public sealed partial class SettingsView
{
    static readonly RectangleF CyNav = new(10, 70, 196, 564), CyPage = new(216, 70, 776, 564);
    const float CyCut = 16;
    double _cyTabAt = -10;
    static string[] _cyIdx = Array.Empty<string>();
    static readonly string CyClose = ((char)0xE711).ToString();

    static string CyIndex(int i)
    {
        if (i >= _cyIdx.Length)
        {
            var a = new string[Math.Max(i + 1, 12)];
            for (int k = 0; k < a.Length; k++) a[k] = (k + 1).ToString("00");
            _cyIdx = a;
        }
        return _cyIdx[i];
    }

    // ---- the ground: the frame, the two cards, the grid ----
    void CyberGround(Ui ui)
    {
        var p = ui.P;
        var all = new RectangleF(0, 0, W, H);
        p.FillCut(all, CyberHud.Page, CyCut, CyCut, CyCut, CyCut);
        var f = RectangleF.Inflate(all, -2, -2);
        p.StrokeCut(f, CyberHud.Frame, 2, CyCut - 1, CyCut - 1, CyCut - 1, CyCut - 1);
        // the cuts: yellow
        float c = CyCut - 1, y0 = f.Y + 1, y1 = f.Bottom - 1, x0 = f.X + 1, x1 = f.Right - 1;
        p.Line(x0, y0 + c, x0 + c, y0, CyberHud.Yellow, 3);
        p.Line(x1 - c, y0, x1, y0 + c, CyberHud.Yellow, 3);
        p.Line(x1, y1 - c, x1 - c, y1, CyberHud.Yellow, 3);
        p.Line(x0 + c, y1, x0, y1 - c, CyberHud.Yellow, 3);
        CyberHud.CardShape(p, CyNav, CyberHud.Card, CyberHud.Edge);
        CyberHud.CardShape(p, CyPage, 0xFF0F0D10, CyberHud.Edge);
        // the page's grid: hairlines every 24 px
        uint g = 0xFF1A1619;
        for (float x = CyPage.X + 24; x < CyPage.Right - 8; x += 24) p.Fill(new RectangleF(x, CyPage.Y + 2, 1, CyPage.Height - 4), g);
        for (float y = CyPage.Y + 24; y < CyPage.Bottom - 8; y += 24) p.Fill(new RectangleF(CyPage.X + 2, y, CyPage.Width - 4, 1), g);
        CyberHud.Brackets(p, CyPage, CyberHud.Yellow, 12, 5, 2, topRight: false);
    }

    // ---- the header ----
    void DrawHeaderCyber(Ui ui)
    {
        var p = ui.P;
        // the reticle: a broken cyan ring, the yellow moon
        float cx = 38, cy = 33;
        p.Arc(cx, cy, 13, 0.03f, 0.22f, CyberHud.Cyan, 3.5f);
        p.Arc(cx, cy, 13, 0.3f, 0.4f, CyberHud.Cyan, 3.5f);
        p.Arc(cx, cy, 13, 0.75f, 0.12f, CyberHud.Cyan, 3.5f);
        p.Fill(new RectangleF(cx + 8, cy - 17, 7, 7), CyberHud.Yellow);
        p.Fill(new RectangleF(cx - 1, cy - 1, 2, 2), CyberHud.Cyan);
        p.Text("ORBITOVERLAY", new RectangleF(62, 8, 200, 32), CyberHud.White, 26, FontWeight.Bold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
        CyberHud.MonoAt(p, "SETTINGS // V" + Updater.Current.ToString(3), 63, 53, 190, CyberHud.Grey, 10.5f);

        // the link readout: a yellow LINK tag, the state in monospace (cyan up, red down), the live numbers under it
        bool on = _h.VrConnected;
        float x = 232;
        float tw = CyberHud.Tag(p, x, 13, "LINK", size: 11, h: 17);
        float rw = (OnClose != null ? 516 : 564) - x - tw - 14;
        var (st, ss) = ui.HudFit(_h.VrStatus ?? "", rw, 12.5f, FontWeight.Bold, CyberHud.Mono);
        p.Text(st, new RectangleF(x + tw + 8, 11, rw, 21), on ? CyberHud.Cyan : CyberHud.Red, ss, FontWeight.Bold, TextAlignment.Leading,
            ParagraphAlignment.Center, CyberHud.Mono);
        string live = CyberLiveLine();
        if (live != null) CyberHud.MonoAt(p, live, x, 52, rw + tw + 8, CyberHud.Grey, 10.5f);
        // a short hazard strip under it: the console's one warning-coloured mark up here
        var hz = new RectangleF(x, 58, 64, 5);
        p.Fill(hz, CyberHud.Yellow);
        CyberHud.Hazard(p, hz, CyberHud.Ink, 9);

        float bx = W - 16;
        if (OnClose != null)
        {
            bx -= 40;
            if (ui.HudButton("hdr.close", new RectangleF(bx, 14, 40, 36), null, CyClose, false, true, 14)) OnClose();
            bx -= 8;
        }
        bx -= 132;
        if (ui.HudButton("hdr.desk", new RectangleF(bx, 14, 132, 36), "Desktops",
                C.General.DesktopsVisible ? Theme.IcoShow : Theme.IcoHide, C.General.DesktopsVisible, true, 14, onCyan: true))
            _h.ToggleDesktops();
        bx -= 140;
        if (ui.HudButton("hdr.kb", new RectangleF(bx, 14, 132, 36), "Keyboard", Theme.IcoKeyboard, C.Keyboard.Visible, true, 14, onCyan: true))
            _h.ToggleKeyboard();
        bx -= 140;
        if (ui.HudButton("hdr.rc", new RectangleF(bx, 14, 132, 36), "Recenter", Theme.IcoRecenter, false, _h.VrConnected, 14))
            _h.Recenter();
    }

    // FPS 86/90 · HMD 71% (only what the link has: nothing while SteamVR is down)
    string _cyLive;
    (float, float, float) _cyLiveKey = (-1, -1, -1);

    string CyberLiveLine()
    {
        var d = _h.Data;
        if (!_h.VrConnected || d?.Vr == null) return null;
        float fps = MathF.Round(d.Vr.AppFps), hz = MathF.Round(d.Vr.DisplayHz), bat = -1;
        foreach (var b in d.Vr.Batteries) if (b.label == "HMD") { bat = MathF.Round(b.pct * 100); break; }
        var key = (fps, hz, bat);
        if (key != _cyLiveKey || _cyLive == null)
        {
            _cyLiveKey = key;
            string s = fps > 0 ? $"FPS {fps:0}/{hz:0}" : $"{hz:0} HZ";
            if (bat >= 0) s += $"  ·  HMD {bat:0}%";
            _cyLive = s;
        }
        return _cyLive;
    }

    // ---- the nav: HUD tabs ----
    void DrawNavCyber(Ui ui)
    {
        var p = ui.P;
        float ny = ui.Anim("nav", Tab, 14);
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            bool hov = ui.Hover(r) && Tab != i;
            float hv = ui.Anim(Ui.Id("nav/", i), hov ? 1 : 0, 18);
            p.FillCut(r, Theme.Mix(CyberHud.Card2, CyberHud.Card3, hv), 0, 0, 8, 0);
            p.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.Mix(CyberHud.Line, CyberHud.CyanDim, hv), 1.25f, 0, 0, 7.7f, 0);
            if (hv > 0.01f) CyberHud.Brackets(p, r, Theme.WithAlpha(CyberHud.CyanDim, hv), 6, 4 - 5 * (1 - hv), 2);
            if (ui.Hover(r) && ui.Pressed && Tab != i)
            {
                Tab = i;
                ui.AnimFrom(TabIn, 0);
                _cyTabAt = Clock.Now;
            }
        }
        // the open tab: a lit plate sliding to it, a yellow marker bar, cyan brackets
        var pill = new RectangleF(16, 84 + ny * 50, 180, 42);
        p.FillCut(pill, Theme.Mix(CyberHud.Card3, CyberHud.Cyan, 0.07f), 0, 0, 8, 0);
        p.StrokeCut(RectangleF.Inflate(pill, -0.75f, -0.75f), CyberHud.Cyan, 1.5f, 0, 0, 7.6f, 0);
        p.Fill(new RectangleF(pill.X - 4, pill.Y + 7, 4, pill.Height - 14), CyberHud.Yellow);
        CyberHud.Brackets(p, pill, CyberHud.Cyan, 8, 4, 2);
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            float on = Math.Clamp(1 - MathF.Abs(ny - i), 0, 1);
            CyberHud.MonoAt(p, CyIndex(i), r.X + 10, r.Y + 26, 24, Theme.Mix(CyberHud.Faint, CyberHud.Yellow, on), 11, FontWeight.Bold);
            p.Icon(TabIcons[i], new RectangleF(r.X + 34, r.Y, 24, r.Height), Theme.Mix(CyberHud.Grey, CyberHud.Cyan, on), 17);
            var (s, fs) = ui.HudFit(Tabs[i], r.Width - 76, 15);
            p.Text(s, new RectangleF(r.X + 66, r.Y, r.Width - 70, r.Height), Theme.Mix(CyberHud.Grey, CyberHud.White, on), fs, FontWeight.SemiBold);
        }
        CyberSys(ui, 84 + Tabs.Length * 50 + 6);
    }

    // the SYS block under the tabs: what the app is and how it is doing, in monospace; a hazard strip closing the column
    void CyberSys(Ui ui, float y)
    {
        var p = ui.P;
        float bottom = CyNav.Bottom - 10;
        if (bottom - y < 104) return;          // more tabs than room: the tabs win
        y = bottom - 104;
        float x = 20;
        CyberHud.Tag(p, x, y, "SYS", size: 11, h: 17);
        p.Fill(new RectangleF(x + 44, y + 16, CyNav.Right - x - 54, 1), CyberHud.Line);
        y += 30;
        void Row(string k, string v, uint c)
        {
            CyberHud.MonoAt(p, k, x, y, 60, CyberHud.Faint, 10.5f, FontWeight.SemiBold);
            CyberHud.MonoAt(p, v, x + 58, y, CyNav.Right - x - 66, c, 10.5f, FontWeight.SemiBold);
            y += 17;
        }
        Row("BUILD", _cyBuild ??= Updater.Current.ToString(3), CyberHud.White);
        bool on = _h.VrConnected;
        Row("LINK", on ? "ONLINE" : "OFFLINE", on ? CyberHud.Cyan : CyberHud.Red);
        Row("IN VR", CyberVrTime(), CyberHud.White);
        var hz = new RectangleF(x, bottom - 12, CyNav.Right - x - 14, 7);
        p.Fill(hz, CyberHud.Yellow);
        CyberHud.Hazard(p, hz, CyberHud.Ink, 10);
    }

    string _cyBuild, _cyVr;
    int _cyVrMin = -1;

    string CyberVrTime()
    {
        if (!_h.VrConnected || _h.Data?.VrSince is not DateTime since) return "--:--";
        int min = (int)Math.Max(0, (DateTime.Now - since).TotalMinutes);
        if (min != _cyVrMin || _cyVr == null) { _cyVrMin = min; _cyVr = $"{min / 60:00}:{min % 60:00}"; }
        return _cyVr;
    }

    // ---- a tab picked (Full): the page is revealed under a scan line ----
    const double CyScanTime = 0.24;

    bool CyberScanBegin(Ui ui)
    {
        if (!CyberFx.Full) return false;
        double t = Clock.Now - _cyTabAt;
        if (t < 0 || t >= CyScanTime) return false;
        float k = CyberFx.EaseOut(CyberFx.Clamp01((float)(t / CyScanTime)));
        ui.P.PushClip(new RectangleF(CyPage.X, CyPage.Y, CyPage.Width, CyPage.Height * k));
        ui.Animating = true;
        return true;
    }

    void CyberScanEnd(Ui ui)
    {
        var p = ui.P;
        p.PopClip();
        double t = Clock.Now - _cyTabAt;
        float k = CyberFx.EaseOut(CyberFx.Clamp01((float)(t / CyScanTime)));
        float y = CyPage.Y + CyPage.Height * k;
        CyberFx.ScanH(p, CyPage.X + 3, CyPage.Right - 3, y, CyberHud.Cyan, 1 - 0.5f * k);
        p.Fill(new RectangleF(CyPage.X + 3, y - 4, CyPage.Width - 6, 1), Theme.WithAlpha(CyberHud.Red, 0.5f * (1 - k)));
    }

    // ---- lists over the page (Look > Style / Font): a terminal list, the pick on a cyan bar ----
    static void CyberListBox(Painter p, RectangleF box)
    {
        p.Fill(new RectangleF(box.X - 2, box.Y + 4, box.Width + 4, box.Height + 4), 0x66000000);
        CyberHud.CardShape(p, box, CyberHud.Card, CyberHud.CyanDim);
        CyberHud.Brackets(p, box, CyberHud.Cyan, 10, 4, 2);
    }

    static void CyberListRow(Painter p, RectangleF row, bool on, bool hover)
    {
        if (on)
        {
            p.Fill(row, Theme.WithAlpha(CyberHud.Cyan, 0.14f));
            p.Fill(new RectangleF(row.X, row.Y + 6, 3, row.Height - 12), CyberHud.Cyan);
        }
        else if (hover) p.Fill(row, CyberHud.Card3);
        if (on || hover)
            CyberHud.MonoAt(p, ">", row.X + 4, row.Y + row.Height / 2 + 4.5f, 10, on ? CyberHud.Cyan : CyberHud.Grey, 12, FontWeight.Bold);
    }

    // General's facts: the key in grey monospace capitals, the value in monospace
    static void CyberRow(Painter p, string k, string v, float x, float y)
    {
        p.Text(k.ToUpperInvariant(), new RectangleF(x, y, 100, 26), CyberHud.Grey, 11, FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, CyberHud.Mono);
        p.Text(v, new RectangleF(x + 100, y, 266, 26), CyberHud.White, 12, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, CyberHud.Mono);
    }
}
