using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The settings in the Cyber style: a HUD console round the same pages (each page's controls are the shared Ui ones, drawn as the
// HUD's own in UiHud.cs - so whatever a page gets later is part of it too). The chrome:
//  - the window: near black, an olive frame with its corners cut (the cuts yellow), the nav and the page as two cards with the
//    watch's dark-red edge, the page's card on a faint grid;
//  - the header: the logo in brackets, the name in the condensed bold, a monospace line under it; the SteamVR link as a readout
//    (LINK: STEAMVR 90 HZ, red when it is down) with the live frame rate and the headset's battery beside it; the three buttons
//    (Keyboard / Desktops lit cyan while shown);
//  - the nav: a column of HUD tabs - index, icon, name in capitals; the open one a lit plate with a yellow marker bar and cyan
//    brackets that slides to the tab picked; a SYS block under the tabs (build, link, time in VR) over a strip of hazard stripes.
// Motion (Full): a tab picked - a cyan scan line sweeps down the page and reveals it, the page rising the last few px into place,
// the grid lighting up in the scanner's wake and a few glitch bars riding the line (its tags type in behind it, UiCyber.cs); the page's
// label decodes; the nav's plate slides over with a trail of its outline. The VR panel opening / closing - CyberReveal. Reduced: the
// modern fade; Off: at once.
public sealed partial class SettingsView
{
    static readonly RectangleF CyNav = new(10, 70, 196, 564), CyPage = new(216, 70, 776, 564);
    const float CyCut = 16;
    double _cyTabAt = -10;
    static string[] _cyIdx = Array.Empty<string>();
    static readonly string CyClose = ((char)0xE711).ToString();
    static readonly string CyMin = ((char)0xE921).ToString();       // ChromeMinimize
    public const float CaptionH = 64;                               // the Modern / Cyber header: it drags the borderless window (SettingsWindow)

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

    // the page's label on its card's top edge, right: its number and name (a tab picked: it decodes from noise, Full)
    void CyberPageLabel(Ui ui)
    {
        var p = ui.P;
        string label = CyLabel(Tab);
        double lt = Clock.Now - _cyTabAt;
        if (CyberFx.Full && lt >= 0 && lt < CyScanTime) label = CyDecode(label, (float)(lt / CyScanTime));
        float lw = CyberHud.MonoWidth(p, label, 10.5f, FontWeight.Bold) + 16;
        var lr = new RectangleF(CyPage.Right - 24 - lw, CyPage.Y - 7, lw, 14);
        p.Fill(lr, CyberHud.Page);
        p.Text(label, lr, CyberHud.Grey, 10.5f, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono);
        p.Fill(new RectangleF(lr.X - 6, CyPage.Y - 2, 4, 4), CyberHud.Yellow);
    }

    readonly System.Collections.Generic.Dictionary<(int, string, string), string> _cyLabels = new();
    readonly System.Collections.Generic.Dictionary<(string, int), string> _cyNoise = new();
    const string CyNoise = "#%&*+=<>/\\|01X$";

    // s decoding at k (0..1): the first part as it is, the rest a jitter of HUD characters that changes every ~30 ms (each string made
    // once per step)
    string CyDecode(string s, float k)
    {
        int step = (int)(k * 8), n = Math.Clamp((int)(CyberFx.EaseOut(k) * s.Length), 0, s.Length);
        if (n >= s.Length) return s;
        if (_cyNoise.TryGetValue((s, step), out var d)) return d;
        if (_cyNoise.Count > 96) _cyNoise.Clear();
        var c = s.ToCharArray();
        for (int i = n; i < c.Length; i++)
            if (c[i] != ' ') c[i] = CyNoise[(int)(CyberFx.Hash(i, step) * CyNoise.Length) % CyNoise.Length];
        return _cyNoise[(s, step)] = new string(c);
    }

    // "03 // LAYOUTS"
    string CyLabel(int tab)
    {
        tab = Math.Clamp(tab, 0, Tabs.Length - 1);
        var key = (tab, Tabs[tab], Lang.Code);
        if (!_cyLabels.TryGetValue(key, out var s)) _cyLabels[key] = s = CyIndex(tab) + " // " + Lang.Upper(Tabs[tab]);
        return s;
    }

    // ---- the header ----
    void DrawHeaderCyber(Ui ui)
    {
        var p = ui.P;
        if (!_cyKept) CyberHeaderStill(ui);
        // the live numbers under the link's state (FPS, the headset's battery)
        bool winBtns = OnClose == null && OnClose95 != null;      // the desktop window: no Windows frame in Cyber, its own minimise / close
        float x = 232, tw = CyberHud.TagWidth(p, "LINK", 11), rw = (OnClose != null ? 516 : winBtns ? 468 : 564) - x - tw - 14;
        string live = CyberLiveLine();
        if (live != null) CyberHud.MonoAt(p, live, x, 52, rw + tw + 8, CyberHud.Grey, 10.5f);

        float bx = W - 16;
        if (OnClose != null)
        {
            bx -= 40;
            if (ui.HudButton("hdr.close", new RectangleF(bx, 14, 40, 36), null, CyClose, false, true, 14)) OnClose();
            bx -= 8;
        }
        else if (winBtns)
        {
            bx -= 40;
            if (ui.HudButton("hdr.wclose", new RectangleF(bx, 14, 40, 36), null, CyClose, false, true, 14)) OnClose95();
            bx -= 48;
            if (ui.HudButton("hdr.wmin", new RectangleF(bx, 14, 40, 36), null, CyMin, false, true, 14)) OnMinimize?.Invoke();
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

    // what in the header does not move: the logo (Erik's, Painter.Logo) in a pair of cyan brackets, the name, the link readout - a
    // yellow LINK tag and the state in monospace (cyan up, red down) - and a short hazard strip, the console's one warning-coloured mark
    void CyberHeaderStill(Ui ui)
    {
        var p = ui.P;
        var logo = new RectangleF(19, 13, 38, 38);
        p.Logo(logo);
        CyberHud.Brackets(p, RectangleF.Inflate(logo, 4, 4), CyberHud.Cyan, 8, 0, 2);
        p.Text("ORBITOVERLAY", new RectangleF(62, 8, 200, 32), CyberHud.White, 26, FontWeight.Bold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
        if (_cyVersionLang != Lang.Code || _cyVersion == null) { _cyVersionLang = Lang.Code; _cyVersion = Lang.Upper("Settings") + " // V" + Updater.CurrentText; }
        CyberHud.MonoAt(p, _cyVersion, 63, 53, 190, CyberHud.Grey, 10.5f);
        bool on = _h.VrConnected;
        float x = 232;
        float tw = CyberHud.Tag(p, x, 13, "LINK", size: 11, h: 17);
        bool winBtns = OnClose == null && OnClose95 != null;
        float rw = (OnClose != null ? 516 : winBtns ? 468 : 564) - x - tw - 14;
        var (st, ss) = ui.HudFit(_h.VrStatus ?? "", rw, 12.5f, FontWeight.Bold, CyberHud.Mono);
        p.Text(st, new RectangleF(x + tw + 8, 11, rw, 21), on ? CyberHud.Cyan : CyberHud.Red, ss, FontWeight.Bold, TextAlignment.Leading,
            ParagraphAlignment.Center, CyberHud.Mono);
        var hz = new RectangleF(x, 58, 64, 5);
        p.Fill(hz, CyberHud.Yellow);
        CyberHud.Hazard(p, hz, CyberHud.Ink, 9);
    }

    string _cyVersion, _cyVersionLang;

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
    float[] _cyNavHv = Array.Empty<float>();

    void DrawNavCyber(Ui ui)
    {
        var p = ui.P;
        float ny = ui.Anim("nav", Tab, 14);
        if (_cyNavHv.Length != Tabs.Length) _cyNavHv = new float[Tabs.Length];
        bool rest = MathF.Abs(ny - Tab) < 0.001f;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            bool hov = ui.Hover(r) && Tab != i;
            float hv = _cyNavHv[i] = ui.Anim(Ui.Id("nav/", i), hov ? 1 : 0, 18);
            if (hv > 0.01f) rest = false;
            if (ui.Hover(r) && ui.Pressed && Tab != i)
            {
                Tab = i;
                ui.AnimFrom(TabIn, 0);
                _cyTabAt = Clock.Now;
                rest = false;
            }
        }
        if (_cyKept && rest) return;                 // the kept picture has it as it is
        if (_cyKept) CyberHud.CardShape(p, CyNav, CyberHud.Card, CyberHud.Edge);       // over the kept one: its own card first
        CyberNav(ui, ny, _cyNavHv);
    }

    // the tabs (hv: each one's hover), the open one's plate at ny, the SYS block
    void CyberNav(Ui ui, float ny, float[] hvs)
    {
        var p = ui.P;
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            float hv = hvs[i];
            p.FillCut(r, Theme.Mix(CyberHud.Card2, CyberHud.Card3, hv), 0, 0, 8, 0);
            p.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.Mix(CyberHud.Line, CyberHud.CyanDim, hv), 1.25f, 0, 0, 7.7f, 0);
            if (hv > 0.01f) CyberHud.Brackets(p, r, Theme.WithAlpha(CyberHud.CyanDim, hv), 6, 4 - 5 * (1 - hv), 2);
        }
        // the open tab: a lit plate sliding to it, a yellow marker bar, cyan brackets (Full: two ghosts of its outline trailing it)
        float run = Tab - ny;
        if (CyberFx.Full && MathF.Abs(run) > 0.02f)
            for (int g = 1; g <= 2; g++)
            {
                var gr = new RectangleF(16, 84 + (ny - run * 0.22f * g) * 50, 180, 42);
                p.StrokeCut(RectangleF.Inflate(gr, -0.75f, -0.75f), Theme.WithAlpha(CyberHud.Cyan, MathF.Min(1, MathF.Abs(run)) * 0.45f / g), 1.5f, 0, 0, 7.6f, 0);
            }
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
        Row("BUILD", _cyBuild ??= Updater.CurrentText, CyberHud.White);
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

    // ---- the console at rest, kept (Erik, 10-16: "better performance overall") ----
    // The frame, the cards and the grid, the header's still parts and the nav at rest (its tabs, the open one's plate, the SYS block) go
    // into a texture of their own at the frame's scale, made again when any of them changes (the tab, the look, the language, the
    // link, the minute in VR). A frame draws that texture and over it only what moves: the page and its label, the live numbers, the
    // header's buttons, the nav while it is hovered or sliding. Drawn as before: a scale that is not whole pixels, a --snapshot text
    // check (it reads every text as it is drawn).
    Surface _cyKeep;
    int _cyKeepSig;
    bool _cyKept;                 // this frame draws the kept picture
    float[] _cyNavRest = Array.Empty<float>();

    void CyberKeep(Ui ui)
    {
        var p = ui.P;
        _cyKept = false;
        float sc = p.Scale;
        int pw = (int)MathF.Round(W * sc), ph = (int)MathF.Round(H * sc);
        if (MathF.Abs(pw - W * sc) > 0.01f || MathF.Abs(ph - H * sc) > 0.01f || p.FitSpy != null || Painter.FitAll != null || p.TextSpy != null || p.NoText)
            return;
        var h = new HashCode();
        h.Add(Theme.Version); h.Add(Lang.Code); h.Add(Tab); h.Add(sc); h.Add(_h.VrConnected); h.Add(_h.VrStatus); h.Add(CyberVrTime());
        h.Add(OnClose != null); h.Add(OnClose95 != null);
        int sig = h.ToHashCode();
        if (_cyKeep == null || _cyKeep.W != pw || _cyKeep.H != ph || sig != _cyKeepSig)
        {
            if (_cyKeep == null || _cyKeep.W != pw || _cyKeep.H != ph) { _cyKeep?.Dispose(); _cyKeep = new Surface(p.Gpu, pw, ph); _cyKeepSig = 0; }
            if (!p.BeginAside(_cyKeep.Bitmap)) return;
            if (_cyNavRest.Length != Tabs.Length) _cyNavRest = new float[Tabs.Length];
            CyberGround(ui);
            CyberHeaderStill(ui);
            CyberNav(ui, Tab, _cyNavRest);
            p.EndAside();
            _cyKeepSig = sig;
        }
        _cyKept = true;
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
        ui.P.PushOffset(0, 8 * (1 - k));            // what the line has revealed rises the last few px into place
        ui.Animating = true;
        return true;
    }

    void CyberScanEnd(Ui ui)
    {
        var p = ui.P;
        p.PopScale();
        p.PopClip();
        double t = Clock.Now - _cyTabAt;
        float k = CyberFx.EaseOut(CyberFx.Clamp01((float)(t / CyScanTime)));
        float y = CyPage.Y + CyPage.Height * k, x0 = CyPage.X + 3, x1 = CyPage.Right - 3;
        // the grid lights up in the line's wake (the ground's lines, every 24 px)
        float gy0 = MathF.Max(CyPage.Y + 2, y - 110);
        uint g = Theme.WithAlpha(CyberHud.Cyan, 0.09f * (1 - 0.6f * k));
        for (float gx = CyPage.X + 24; gx < CyPage.Right - 8; gx += 24) p.Fill(new RectangleF(gx, gy0, 1, y - gy0), g);
        for (float gy = CyPage.Y + 24; gy < y; gy += 24) if (gy > gy0) p.Fill(new RectangleF(x0, gy, x1 - x0, 1), g);
        // the line: its glow trail, a red edge above and a cyan one below (the split), a few bars of glitch riding it
        CyberFx.ScanH(p, x0, x1, y, CyberHud.Cyan, 1 - 0.5f * k);
        p.Fill(new RectangleF(x0, y - 4, x1 - x0, 1), Theme.WithAlpha(CyberHud.Red, 0.55f * (1 - k)));
        p.Fill(new RectangleF(x0, y + 2, x1 - x0, 1), Theme.WithAlpha(CyberHud.Cyan, 0.4f * (1 - k)));
        int step = (int)(t / 0.035);
        for (int i = 0; i < 4; i++)
        {
            float bx = x0 + CyberFx.Hash(i, step) * (x1 - x0 - 120), bw = 30 + CyberFx.Hash(i + 7, step) * 90, by = y - 12 + CyberFx.Hash(i + 13, step) * 18;
            uint bc = i == 0 ? CyberHud.Yellow : i % 2 == 0 ? CyberHud.Red : CyberHud.Cyan;
            p.Fill(new RectangleF(bx, by, bw, 1 + (i & 1) * 2), Theme.WithAlpha(bc, 0.5f * (1 - k)));
        }
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
            CyberHud.MonoAt(p, ">", row.X + 5, row.Y + row.Height / 2 + 4.5f, 10, on ? CyberHud.Cyan : CyberHud.Grey, 12, FontWeight.Bold);
    }

    // General's facts: the key in grey monospace capitals (each made once), the value in monospace
    static readonly System.Collections.Generic.Dictionary<string, string> _cyRowKeys = new();

    static void CyberRow(Painter p, string k, string v, float x, float y)
    {
        if (!_cyRowKeys.TryGetValue(k, out var uk)) _cyRowKeys[k] = uk = k.ToUpperInvariant();
        p.Text(uk, new RectangleF(x, y, 100, 26), CyberHud.Grey, 11, FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, CyberHud.Mono);
        p.Text(v, new RectangleF(x + 100, y, 266, 26), CyberHud.White, 12, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, CyberHud.Mono);
    }

    // ---- Look in Cyber: only what Cyber uses (Erik, 1.13.2: "why is there a colour picker if it does nothing") ----
    // The accent, the panel colour, the themes and Font are Modern's: Cyber has its own palette and type, so they are not offered
    // here. What is: the style (with the watch beside it, live), the laser, night mode, the panel opacity, Motion; Default look
    // puts back what this page sets (the opacity, the laser colour) and leaves Modern's colours alone.
    static readonly RectangleF CyStyleField = new(232, 108, 356, 38);
    static RectangleF CyStyleList => new(CyStyleField.X, CyStyleField.Bottom + 6, CyStyleField.Width, StyleMenu.Ids.Length * FontRow + 12);
    public static RectangleF CyStyleFieldForTest => CyStyleField;
    public static RectangleF CyStyleListForTest => CyStyleList;
    public static readonly RectangleF CyLookPreview = new(242, 168, 336, 336f * WristPanel.TH / WristPanel.TW);

    void DrawLookCyber(Ui ui)
    {
        var p = ui.P;
        var a = C.Appearance;
        float lx = 232, rx = 612;
        bool changed = false;
        // left: the style, the watch in it, the reset
        ui.Section(new RectangleF(lx, 84, 360, 20), "Style");
        {
            var field = CyStyleField;
            bool click = ui.Clickable("look.style", field, out bool hover, out _);
            float hv = ui.Anim("look.style#h", hover || _stylesOpen ? 1 : 0, 18);
            p.FillCut(field, Theme.Mix(CyberHud.Page, CyberHud.Card3, hv), 0, 0, 9, 0);
            p.StrokeCut(RectangleF.Inflate(field, -0.6f, -0.6f), _stylesOpen ? CyberHud.Cyan : Theme.Mix(CyberHud.CyanDim, CyberHud.Cyan, hv), 1.25f, 0, 0, 8.7f, 0);
            if (hv > 0.01f) CyberHud.Brackets(p, field, Theme.WithAlpha(CyberHud.Cyan, hv), 7, 4 - 5 * (1 - hv), 2);
            StyleChip(p, new RectangleF(field.X + 12, field.Y + 9, 28, 20), StyleMenu.IndexOf(a.Style));
            var (sn, ss) = ui.HudFit(StyleMenu.Names[StyleMenu.IndexOf(a.Style)], field.Width - 100, 17);
            p.Text(sn, new RectangleF(field.X + 50, field.Y, field.Width - 88, field.Height), CyberHud.White, ss, FontWeight.SemiBold);
            p.Icon(_stylesOpen ? Theme.IcoUp : Theme.IcoDown, new RectangleF(field.Right - 36, field.Y, 24, field.Height), CyberHud.Cyan, 14);
            if (click) _stylesOpen = true;
        }
        var prev = CyLookPreview;
        CyberPreviewFrame(p, RectangleF.Inflate(prev, 8, 8));
        p.Image(_h.Wrist.Surf.Bitmap, prev);
        if (_h.Wrist.Animating) ui.Animating = true;
        ui.Label(new RectangleF(lx, prev.Bottom + 18, 356, 40),
            Lang.F("{0} has its own palette and type: the accent, panel colour and font choices are Modern's.", CyberHud.Name), Theme.Dim, 12, FontWeight.Normal, true);
        if (ui.Button("look.reset", new RectangleF(lx, prev.Bottom + 70, 170, 38), "Default look", Theme.IcoRecenter, false, true, 14))
        {
            var d = new AppearanceConfig();
            a.PanelOpacity = d.PanelOpacity;
            a.LaserColor = d.LaserColor;
            changed = true;
        }

        // right: the laser, night mode, the panel opacity, Motion
        ui.Section(new RectangleF(rx, 84, 360, 20), "Laser");
        if (ui.Swatch("las.acc", rx + 18, 126, 14, Theme.Accent, a.LaserColor == 0, "A")) { a.LaserColor = 0; changed = true; }
        for (int i = 0; i < Theme.AccentPresets.Length - 1; i++)
        {
            uint c = Theme.AccentPresets[i];
            if (ui.Swatch(Ui.Id("las", i), rx + 18 + (i + 1) * 36, 126, 11, c, a.LaserColor != 0 && (a.LaserColor & 0xFFFFFF) == (c & 0xFFFFFF)))
            { a.LaserColor = c; changed = true; }
        }
        float th = a.LaserThickness;
        if (ui.Slider("las.th", new RectangleF(rx - 10, 152, 376, 58), "Thickness", ref th, 1.5f, 8f, f => $"{f:0.0} mm"))
        { a.LaserThickness = MathF.Round(th * 2) / 2; changed = true; }
        bool always = a.LaserAlways;
        if (ui.Toggle("las.always", new RectangleF(rx - 10, 214, 376, 54), "Always show the beam", ref always,
                "otherwise only while pointing at a panel"))
        { a.LaserAlways = always; changed = true; }
        ui.Section(new RectangleF(rx, 288, 360, 20), "Night mode");
        bool night = a.NightMode;
        if (ui.Toggle("night", new RectangleF(rx - 10, 310, 376, 54), "Night mode", ref night, "dims every panel in VR - also on the watch (moon)"))
            a.NightMode = night;
        float nb = a.NightBrightness;
        if (ui.Slider("night.b", new RectangleF(rx - 10, 370, 376, 58), "Night brightness", ref nb, 0.15f, 0.9f, f => $"{f * 100:0}%"))
            a.NightBrightness = nb;
        float op = a.PanelOpacity;
        if (ui.Slider("pan.op", new RectangleF(rx - 10, 438, 376, 58), "Panel opacity", ref op, 0.4f, 1f, f => $"{f * 100:0}%"))
        { a.PanelOpacity = op; changed = true; }
        ui.Section(new RectangleF(rx, 566, 360, 20), "Motion");
        int mi = MotionIndex(a.Motion);
        p.Text(MotionHints[mi], new RectangleF(rx + 96, 566, 260, 20), Theme.Dim, 12, FontWeight.Normal, TextAlignment.Trailing);
        if (ui.Segmented("look.motion", new RectangleF(rx, 588, 356, 38), MotionNames, ref mi))
        {
            a.Motion = Motion.Id((MotionLevel)mi);
            changed = true;
        }
        if (changed) _h.AppearanceChanged();
    }

    // a preview (the watch, the keyboard) in a HUD frame: a dark card, its edge, cyan brackets, the grid's ticks along the top
    static void CyberPreviewFrame(Painter p, RectangleF r)
    {
        CyberHud.CardShape(p, r, CyberHud.Card, CyberHud.Edge);
        CyberHud.Brackets(p, RectangleF.Inflate(r, 4, 4), CyberHud.Cyan, 12, 0, 2);
        for (float x = r.X + 24; x < r.Right - 24; x += 24) p.Fill(new RectangleF(x, r.Y + 2, 1, 4), CyberHud.Line);
    }

    // the editor's selected widget in Cyber: where it is and how big, as a readout (its Modern look - style, colour - is not offered:
    // Cyber draws every widget its own way)
    void CyberWidgetInfo(Ui ui, WatchWidget sel, float x, float y, float w)
    {
        var p = ui.P;
        void Row(string k, string v)
        {
            CyberHud.MonoAt(p, k, x, y, 70, CyberHud.Grey, 12.5f, FontWeight.SemiBold);
            CyberHud.MonoAt(p, v, x + 64, y, w - 64, CyberHud.White, 12.5f, FontWeight.SemiBold);
            y += 20;
        }
        Row("CELL", $"{(char)('A' + sel.X)}{sel.Y + 1}");
        Row("SIZE", $"{sel.W} × {sel.H}");
        ui.Label(new RectangleF(x, y + 2, w, 40), Lang.F("{0} draws every widget its own way.", CyberHud.Name), Theme.Dim, 12, FontWeight.Normal, true);
    }

    // ---- the editor's grid over the watch (Cyber) ----
    static void CyberCell(Painter p, RectangleF cr, bool picked)
    {
        var r = RectangleF.Inflate(cr, -2, -2);
        if (picked) p.FillCut(r, Theme.WithAlpha(CyberHud.Cyan, 0.18f), 0, 0, 6, 0);
        CyberFx.Corners(p, r, picked ? CyberHud.Cyan : 0x60FFFFFF, MathF.Min(7, r.Width * 0.18f), picked ? 2 : 1.25f);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, a = picked ? 7 : 4;
        uint c = picked ? CyberHud.Cyan : 0x50FFFFFF;
        p.Fill(new RectangleF(cx - a, cy - 0.6f, 2 * a, 1.2f), c);
        p.Fill(new RectangleF(cx - 0.6f, cy - a, 1.2f, 2 * a), c);
    }

    static void CyberTarget(Painter p, RectangleF gr, bool ok)
    {
        uint c = ok ? CyberHud.Cyan : CyberHud.Red;
        p.FillCut(gr, Theme.WithAlpha(c, 0.24f), 0, 0, 8, 0);
        p.StrokeCut(RectangleF.Inflate(gr, -1, -1), c, 2, 0, 0, 7.4f, 0);
        CyberFx.Corners(p, gr, c, 9, 2, 3);
    }

    static readonly System.Numerics.Vector2[] _cyNotch = new System.Numerics.Vector2[3];

    static void CyberSelection(Painter p, RectangleF sr, RectangleF handle)
    {
        p.StrokeCut(RectangleF.Inflate(sr, 1, 1), CyberHud.Cyan, 2, 0, 0, 7, 0);
        CyberFx.Corners(p, sr, CyberHud.Yellow, 10, 2.5f, 5);
        // the handle: a notch pointing out of the corner
        _cyNotch[0] = new(handle.Right + 2, handle.Y - 2); _cyNotch[1] = new(handle.Right + 2, handle.Bottom + 2); _cyNotch[2] = new(handle.X - 2, handle.Bottom + 2);
        p.Polygon(_cyNotch, 3, CyberHud.Cyan);
    }

    static string[] _cyCols = { "A", "B", "C", "D", "E", "F", "G", "H" }, _cyRows = { "1", "2", "3", "4", "5", "6" };

    static void CyberGridLabels(Painter p, RectangleF prev, GridSpec grid, float sx, float sy)
    {
        for (int x = 0; x < grid.Cols && x < _cyCols.Length; x++)
        {
            var c = WatchWidgets.CellRect(x, 0, 1, 1, false, grid);
            p.Text(_cyCols[x], new RectangleF(prev.X + c.X * sx, prev.Y - 10, c.Width * sx, 10), CyberHud.Grey, 9, FontWeight.Bold,
                TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono);
        }
        for (int y = 0; y < grid.Rows && y < _cyRows.Length; y++)
        {
            var c = WatchWidgets.CellRect(0, y, 1, 1, false, grid);
            p.Text(_cyRows[y], new RectangleF(prev.X - 10, prev.Y + c.Y * sy, 10, c.Height * sy), CyberHud.Grey, 9, FontWeight.Bold,
                TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono);
        }
    }
}
