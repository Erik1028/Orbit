using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

public interface IOrbitHost
{
    ConfigStore Cfg { get; }
    bool VrConnected { get; }
    string VrStatus { get; }
    string InputStatus { get; }
    string GpuName { get; }
    IReadOnlyList<MonitorInfo> MonitorList { get; }
    PanelData Data { get; }
    WristPanel Wrist { get; }
    KeyboardPanel Keyboard { get; }
    void Recenter();
    void ResetDesktop(DesktopConfig d);
    void ResetWrist();
    void ResetKeyboard();
    void SetDesktopAttach(DesktopConfig d, string attach);
    void SetKeyboardAttach(string attach);
    void SetDesktopEnabled(DesktopConfig d, bool on);
    void SetAutoStart(bool on);
    string AutoStartStatus { get; }
    void ToggleKeyboard();
    void ToggleDesktops();
    void OpenConfigFolder();
    int GroupSize(DesktopConfig d);
    void JoinAllDesktops();
    void SeparateDesktops();
    void FitCurve(DesktopConfig d);
    void DesktopEdited(DesktopConfig d);
    void AppearanceChanged();
    bool IsElevated { get; }
    void SetRunAsAdmin(bool on);
    void RestartElevated();
    string NotificationStatus { get; }
    string StatusLinePreview { get; }      // what the chatbox status line would say now
    string RoomStatus { get; }             // VRChat log / VRCX found?
    void ToggleRoom();
    void TheaterEdited();          // theater settings changed in the window: a screen that plays follows at once
    IReadOnlyList<WhatsNew.Entry> WhatsNewList { get; }    // after an update until OK: what changed (empty = nothing)
    string WhatsNewSub { get; }
    void DismissWhatsNew();
    void TestNotification();
    Updater Updates { get; }
    void SwitchLayout(string name);
    void ForgetLayout(string name);
    IReadOnlyList<WindowInfo> OpenWindows();
    bool IsMirrored(IntPtr hwnd);
    WindowConfig AddWindow(WindowInfo w);
    void RemoveWindow(WindowConfig w);
    string WindowStatus(WindowConfig w);
    void ResetWindow(WindowConfig w);
    void SetWindowAttach(WindowConfig w, string attach);
    void SetWindowEnabled(WindowConfig w, bool on);
}

// The settings screen. Same code draws the desktop window and the SteamVR dashboard tab.
// Logical canvas: 1000 x 640.
public sealed partial class SettingsView
{
    public const float W = 1000, H = 640;
    static readonly string[] Tabs = { "Desktops", "Windows", "Layouts", "Wrist", "Keyboard", "Look", "VRChat", "General" };
    static readonly string[] TabIcons = { Theme.IcoMonitor, Theme.IcoWindow, Theme.IcoLayouts, Theme.IcoWatch, Theme.IcoKeyboard, Theme.IcoColor, "\uE8BD", Theme.IcoSettings };
    public const int TabDesktops = 0, TabWindows = 1, TabLayouts = 2, TabWrist = 3, TabKeyboard = 4, TabLook = 5, TabVrchat = 6, TabGeneral = 7;
    public static int TabCount => Tabs.Length;
    static readonly string[] AttachIds = { "world", "left", "right", "head" };
    static readonly string[] AttachNames = { "World", "Left hand", "Right hand", "Head" };

    readonly IOrbitHost _h;
    public int Tab;
    string _selected;    // desktop id

    public Action OnClose;   // set when shown as the in-VR panel: draws a close button

    public SettingsView(IOrbitHost host) { _h = host; }

    public bool ShowsWristPreview => Tab == TabWrist;
    public bool ShowsKeyboardPreview => Tab == TabKeyboard;

    Config C => _h.Cfg.Data;

    // "What's new": after an update (until OK), or opened from General
    bool _whatsNewManual;
    float _wnScroll;
    IReadOnlyList<WhatsNew.Entry> WhatsNewShown => _whatsNewManual ? WhatsNew.Latest(4) : _h.WhatsNewList;
    string WhatsNewSubShown => _whatsNewManual ? $"OrbitOverlay {Updater.Current.ToString(3)} and the versions before it" : _h.WhatsNewSub;

    void CloseWhatsNew()
    {
        if (_whatsNewManual) _whatsNewManual = false;
        else _h.DismissWhatsNew();
        _wnScroll = 0;
    }

    public void Draw(Ui ui)
    {
        var p = ui.P;
        var wn = WhatsNewShown;
        bool modal = wn != null && wn.Count > 0;
        ui.Inert = modal;
        p.Fill(new RectangleF(0, 0, W, H), Theme.Bg);
        DrawHeader(ui);
        if (Theme.Classic) DrawNavClassic(ui);
        else DrawNav(ui);

        switch (Tab)
        {
            case TabDesktops: DrawDesktops(ui); break;
            case TabWindows: DrawWindows(ui); break;
            case TabLayouts: DrawLayouts(ui); break;
            case TabWrist: DrawWrist(ui); break;
            case TabKeyboard: DrawKeyboard(ui); break;
            case TabLook: DrawLook(ui); break;
            case TabVrchat: DrawVrchat(ui); break;
            default: DrawGeneral(ui); break;
        }
        if (modal)
        {
            ui.Inert = false;
            p.Fill(new RectangleF(0, 0, W, H), 0x99000000);
            if (WhatsNewView.DrawModern(ui, new RectangleF(160, 40, 680, 560), wn, WhatsNewSubShown, ref _wnScroll, Theme.Panel)) CloseWhatsNew();
        }
        if (ui.Changed) _h.Cfg.MarkDirty();
    }

    // the 95 skin: the tabs as a white list box, the chosen one in navy
    void DrawNavClassic(Ui ui)
    {
        var p = ui.P;
        var box = new RectangleF(14, 80, 184, Tabs.Length * 40 + 8);
        p.Fill(box, Theme.W95Window);
        p.Bevel(box, Painter.Edge.Sunken);
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(box.X + 4, box.Y + 4 + i * 40, box.Width - 8, 40);
            if (ui.Hover(r) && ui.Pressed) Tab = i;
            bool on = Tab == i;
            if (on) p.Fill(r, Theme.W95Navy);
            uint fg = on ? 0xFFFFFFFF : Theme.W95Dark;
            p.Icon(TabIcons[i], new RectangleF(r.X + 10, r.Y, 24, r.Height), fg, 17);
            p.Text(Tabs[i], new RectangleF(r.X + 44, r.Y, r.Width - 48, r.Height), fg, 15);
        }
        p.Fill(new RectangleF(212, 76, p.Px, H - 92), Theme.W95Shadow);
        p.Fill(new RectangleF(212 + p.Px, 76, p.Px, H - 92), Theme.W95Hi);
    }

    void DrawNav(Ui ui)
    {
        var p = ui.P;
        // nav: the selection pill slides to the chosen tab
        float ny = ui.Anim("nav", Tab, 14);
        var pill = new RectangleF(16, 84 + ny * 50, 180, 42);
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            float hv = ui.Anim(Ui.Id("nav/", i), ui.Hover(r) && Tab != i ? 1 : 0, 18);
            if (hv > 0) p.Fill(r, Theme.WithAlpha(Theme.Panel, hv), 10);
            if (ui.Hover(r) && ui.Pressed) Tab = i;
        }
        p.Fill(pill, Theme.Panel3, 10);
        p.Fill(new RectangleF(pill.X, pill.Y + 10, 4, pill.Height - 20), Theme.Accent, 2);
        for (int i = 0; i < Tabs.Length; i++)
        {
            var r = new RectangleF(16, 84 + i * 50, 180, 42);
            float on = Math.Clamp(1 - MathF.Abs(ny - i), 0, 1);
            p.Icon(TabIcons[i], new RectangleF(r.X + 14, r.Y, 24, r.Height), Theme.Mix(Theme.Dim, Theme.Accent, on), 18);
            p.Text(Tabs[i], new RectangleF(r.X + 50, r.Y, r.Width - 50, r.Height), Theme.Mix(Theme.Dim, Theme.Text, on), 16,
                on > 0.5f ? FontWeight.SemiBold : FontWeight.Normal);
        }
        p.Line(212, 76, 212, H - 16, Theme.Border, 1);
    }

    void DrawHeader(Ui ui)
    {
        var p = ui.P;
        if (Theme.Classic) { DrawHeaderClassic(ui); return; }
        p.Fill(new RectangleF(0, 0, W, 64), Theme.Panel);
        p.Line(0, 64, W, 64, Theme.Border, 1);
        // logo: ring + moon
        p.Ring(40, 32, 13, Theme.Accent, 3.5f);
        p.Circle(51, 21, 4.5f, Theme.Accent2);
        p.Text("OrbitOverlay", new RectangleF(64, 0, 180, 64), Theme.Text, 22, FontWeight.SemiBold);
        bool on = _h.VrConnected;
        float px = 64 + p.MeasureWidth("OrbitOverlay", 22, FontWeight.SemiBold) + 16;       // the pill right after the name
        var pill = new RectangleF(px, 19, MathF.Min(Math.Max(190, ui.P.MeasureWidth(_h.VrStatus, 13) + 36), 500 - px), 26);
        p.Fill(pill, on ? 0x2234D399u : 0x22FFFFFFu, 13);
        p.Circle(pill.X + 14, pill.Y + 13, 4.5f, on ? Theme.Good : Theme.Faint);
        p.Text(_h.VrStatus, new RectangleF(pill.X + 26, pill.Y, pill.Width - 30, pill.Height), on ? Theme.Text : Theme.Dim, 13);

        float x = W - 16;
        if (OnClose != null)
        {
            x -= 40;
            if (ui.Button("hdr.close", new RectangleF(x, 14, 40, 36), null, "", false, true, 14))
                OnClose();
            x -= 8;
        }
        x -= 132;
        if (ui.Button("hdr.desk", new RectangleF(x, 14, 132, 36), "Desktops",
                C.General.DesktopsVisible ? Theme.IcoShow : Theme.IcoHide, C.General.DesktopsVisible, true, 14))
            _h.ToggleDesktops();
        x -= 140;
        if (ui.Button("hdr.kb", new RectangleF(x, 14, 132, 36), "Keyboard", Theme.IcoKeyboard, C.Keyboard.Visible, true, 14))
            _h.ToggleKeyboard();
        x -= 140;
        if (ui.Button("hdr.rc", new RectangleF(x, 14, 132, 36), "Recenter", Theme.IcoRecenter, false, _h.VrConnected, 14))
            _h.Recenter();
    }

    // the 95 skin: in VR a navy caption with the close box (the desktop window has the real one),
    // then a toolbar - the SteamVR state in a sunken status field and the three buttons
    void DrawHeaderClassic(Ui ui)
    {
        var p = ui.P;
        bool caption = OnClose != null;
        float by = 18;
        if (caption)
        {
            var cap = new RectangleF(4, 4, W - 8, 24);
            p.Caption(cap, "OrbitOverlay Settings", 13);
            var close = new RectangleF(cap.Right - 22, cap.Y + 3, 19, 18);
            if (ui.Button("hdr.close", close, null, "\uE8BB", false, true, 7)) OnClose();
            by = 33;
        }
        const float bh = 28;
        bool on = _h.VrConnected;
        var st = new RectangleF(16, by, Math.Max(190, ui.P.MeasureWidth(_h.VrStatus, 13) + 36), bh);
        p.Bevel(st, Painter.Edge.Sunken);
        p.Fill(new RectangleF(st.X + 10, st.Y + 10, 8, 8), on ? 0xFF00A000 : Theme.W95Shadow);
        p.Text(_h.VrStatus, new RectangleF(st.X + 26, st.Y, st.Width - 30, st.Height), Theme.W95Dark, 13);
        float x = W - 16 - 132;
        if (ui.Button("hdr.desk", new RectangleF(x, by, 132, bh), "Desktops",
                C.General.DesktopsVisible ? Theme.IcoShow : Theme.IcoHide, C.General.DesktopsVisible, true, 14))
            _h.ToggleDesktops();
        x -= 140;
        if (ui.Button("hdr.kb", new RectangleF(x, by, 132, bh), "Keyboard", Theme.IcoKeyboard, C.Keyboard.Visible, true, 14))
            _h.ToggleKeyboard();
        x -= 140;
        if (ui.Button("hdr.rc", new RectangleF(x, by, 132, bh), "Recenter", Theme.IcoRecenter, false, _h.VrConnected, 14))
            _h.Recenter();
        p.EtchedLine(4, W - 4, 68);
    }

    // ---------------- Desktops ----------------
    void DrawDesktops(Ui ui)
    {
        var p = ui.P;
        var mons = _h.MonitorList;
        ui.Section(new RectangleF(232, 80, 400, 22), "Monitors");
        var map = new RectangleF(232, 104, 752, 118);
        p.Fill(map, Theme.Panel, 12);
        if (mons.Count == 0) { p.Text("No monitors found", map, Theme.Dim, 15, FontWeight.Normal, TextAlignment.Center); return; }
        if (_selected == null || !mons.Any(m => m.Id == _selected)) _selected = mons.FirstOrDefault(m => !m.Virtual)?.Id ?? mons[0].Id;

        int minX = mons.Min(m => m.X), minY = mons.Min(m => m.Y), maxX = mons.Max(m => m.X + m.W), maxY = mons.Max(m => m.Y + m.H);
        float s = Math.Min((map.Width - 40) / (maxX - minX), (map.Height - 24) / (maxY - minY));
        float ox = map.X + (map.Width - (maxX - minX) * s) / 2, oy = map.Y + (map.Height - (maxY - minY) * s) / 2;
        int n = 1;
        foreach (var m in mons)
        {
            var dc = C.Desktops.FirstOrDefault(d => d.Id == m.Id);
            var r = new RectangleF(ox + (m.X - minX) * s + 3, oy + (m.Y - minY) * s + 3, m.W * s - 6, m.H * s - 6);
            bool sel = m.Id == _selected;
            bool en = dc?.Enabled ?? false;
            bool hover = ui.Hover(r);
            uint fill = en ? Theme.Mix(Theme.Panel2, Theme.Accent, hover ? 0.28f : 0.2f) : (hover ? Theme.Panel3 : Theme.Panel2);
            p.Fill(r, fill, 8);
            p.Stroke(r, sel ? Theme.Accent : Theme.Border, sel ? 2.5f : 1, 8);
            p.Text(n.ToString(), new RectangleF(r.X, r.Y + 6, r.Width, r.Height * 0.5f), en ? Theme.Text : Theme.Dim, 22, FontWeight.SemiBold, TextAlignment.Center);
            p.Text(m.Name + (m.Virtual ? " (virtual)" : ""), new RectangleF(r.X + 4, r.Y + r.Height * 0.5f, r.Width - 8, r.Height * 0.4f),
                Theme.Dim, 12, FontWeight.Normal, TextAlignment.Center);
            if (dc != null && dc.Group != null && _h.GroupSize(dc) > 1)
                p.Icon(Theme.IcoLink, new RectangleF(r.Right - 26, r.Y + 4, 22, 22), Theme.Accent, 13);
            if (hover && ui.Pressed) _selected = m.Id;
            n++;
        }

        var mon = mons.First(m => m.Id == _selected);
        var cfg = C.Desktops.FirstOrDefault(d => d.Id == mon.Id);
        if (cfg == null) return;
        int gs = _h.GroupSize(cfg);
        p.Text($"{mon.Name}", new RectangleF(232, 232, 420, 30), Theme.Text, 19, FontWeight.SemiBold);
        p.Text($"{mon.W}×{mon.H}  ·  {mon.Device.Replace(@"\\.\", "")}{(mon.Primary ? "  ·  primary" : "")}{(gs > 1 ? $"  ·  joined row of {gs}" : "")}",
            new RectangleF(232, 260, 460, 22), Theme.Dim, 13);
        bool en2 = cfg.Enabled;
        if (ui.Toggle("d.en", new RectangleF(714, 234, 270, 46), "Show in VR", ref en2))
            _h.SetDesktopEnabled(cfg, en2);

        float lx = 222, rx = 608, cw = 376;
        float y = 292;
        float v = cfg.Width;
        if (ui.Slider("d.w", new RectangleF(lx, y, cw, 58), "Size", ref v, 0.3f, 3f, f => $"{f:0.00} m wide", cfg.Enabled))
        {
            cfg.Width = v;
            _h.DesktopEdited(cfg);
        }
        v = cfg.Opacity;
        if (ui.Slider("d.o", new RectangleF(lx, y + 62, cw, 58), "Opacity", ref v, 0.2f, 1f, f => $"{f * 100:0}%", cfg.Enabled)) cfg.Opacity = v;
        v = cfg.Curvature;
        float width = cfg.Width;
        if (ui.Slider("d.c", new RectangleF(lx, y + 124, cw, 58), "Curvature", ref v, 0f, 0.5f,
                f => f < 0.005f ? "flat" : $"bend radius {width / (2 * MathF.PI * f):0.00} m", cfg.Enabled))
        {
            cfg.Curvature = v < 0.005f ? 0 : v;
            _h.DesktopEdited(cfg);
        }

        ui.Label(new RectangleF(rx + 10, y, cw, 22), "Attached to");
        int ai = Math.Max(0, Array.IndexOf(AttachIds, cfg.Place.Attach ?? "world"));
        if (ui.Segmented("d.att", new RectangleF(rx + 10, y + 24, cw - 20, 38), AttachNames, ref ai, cfg.Enabled))
            _h.SetDesktopAttach(cfg, AttachIds[ai]);
        bool mouse = cfg.Mouse;
        if (ui.Toggle("d.m", new RectangleF(rx, y + 70, cw, 52), "Mouse control", ref mouse, "laser moves the real cursor, trigger clicks", cfg.Enabled))
            cfg.Mouse = mouse;
        bool live = _h.VrConnected && cfg.Enabled;
        if (ui.Button("d.reset", new RectangleF(rx + 10, y + 134, 168, 38), "Reset position", Theme.IcoRecenter, false, live, 14))
            _h.ResetDesktop(cfg);
        if (ui.Button("d.fit", new RectangleF(rx + 188, y + 134, 178, 38), "Curve around me", null, false, live && (cfg.Place.Attach ?? "world") == "world", 14))
            _h.FitCurve(cfg);

        p.Line(232, 498, 984, 498, Theme.Border, 1);
        float fps = C.General.CaptureFps;
        bool smart = C.General.SmartCapture;
        if (ui.Slider("g.fps", new RectangleF(lx, 506, cw, 58), smart ? "Capture rate (smart is on)" : "Capture rate", ref fps, 15, 90,
                f => $"{Math.Round(f):0} fps", !smart))
            C.General.CaptureFps = (int)Math.Round(fps);
        if (ui.Toggle("g.smart", new RectangleF(lx, 566, cw, 54), "Smart frame rate", ref smart,
                "where you look or point: headset rate · edge 15 · behind 4 fps"))
            C.General.SmartCapture = smart;
        ui.Section(new RectangleF(rx + 10, 506, cw, 20), "Arrangement");
        if (ui.Button("d.join", new RectangleF(rx + 10, 530, 236, 38), "Join into one curved screen", Theme.IcoLink, true, _h.VrConnected, 14))
            _h.JoinAllDesktops();
        if (ui.Button("d.sep", new RectangleF(rx + 256, 530, 110, 38), "Separate", null, false, true, 14))
            _h.SeparateDesktops();
        ui.Label(new RectangleF(rx + 10, 576, cw - 10, 50),
            "Drop a monitor next to another one and it snaps into the row. Hold B / Y while grabbing to pull one out.",
            Theme.Dim, 12, FontWeight.Normal, true);
    }

    // ---------------- Wrist ----------------
    // ---------------- Layouts ----------------
    string _selLayout;

    string LayoutSummary(LayoutProfile l)
    {
        int mons, wins;
        bool night;
        // only monitors that are connected and windows still mirrored count
        bool Monitor(string id) => _h.MonitorList.Any(m => m.Id == id);
        bool Window(string id) => C.Windows.Any(w => w.Id == id);
        if (l.Name == C.ActiveLayout)           // the one in use = what is around you right now
        {
            mons = C.Desktops.Count(d => d.Enabled && Monitor(d.Id));
            wins = C.Windows.Count(w => w.Enabled);
            night = C.Appearance.NightMode;
        }
        else if (l.Saved == null) return "not used yet";
        else
        {
            mons = l.Panels.Count(s => s.Kind == "desktop" && s.Enabled && Monitor(s.Id));
            wins = l.Panels.Count(s => s.Kind == "window" && s.Enabled && Window(s.Id));
            night = l.NightMode;
        }
        var parts = new List<string> { mons == 1 ? "1 monitor" : $"{mons} monitors" };
        if (wins > 0) parts.Add(wins == 1 ? "1 window" : $"{wins} windows");
        if (night) parts.Add("night mode");
        return string.Join("  ·  ", parts);
    }

    void DrawLayouts(Ui ui)
    {
        var p = ui.P;
        var list = C.Layouts;
        float lx = 232, lw = 352;
        if (_selLayout == null || !list.Any(l => l.Name == _selLayout)) _selLayout = C.ActiveLayout;
        ui.Section(new RectangleF(lx, 80, lw, 22), "Layout profiles");
        float y = 106;
        foreach (var l in list)
        {
            var r = new RectangleF(lx, y, lw, 72);
            bool active = l.Name == C.ActiveLayout;
            if (ui.Card(Ui.Id("lay.", l.Name), r, l.Name == _selLayout, out _)) _selLayout = l.Name;
            p.Text(l.Name, new RectangleF(r.X + 18, r.Y + 8, 200, 28), Theme.Text, 17, FontWeight.SemiBold);
            if (active)
            {
                var pill = new RectangleF(r.Right - 88, r.Y + 12, 74, 22);
                p.Fill(pill, Theme.Mix(Theme.Panel2, Theme.Accent, 0.25f), 11);
                p.Text("IN USE", pill, Theme.Accent, 11, FontWeight.SemiBold, TextAlignment.Center);
            }
            p.Text(LayoutSummary(l), new RectangleF(r.X + 18, r.Y + 38, lw - 30, 24), Theme.Dim, 12);
            y += 82;
        }
        ui.Label(new RectangleF(lx, y + 4, lw, 120),
            "Each profile remembers where your monitors, windows and keyboard are, their size and curve, and night mode. " +
            "Whatever you change is kept in the profile you are in when you switch away.",
            Theme.Dim, 13, FontWeight.Normal, true);

        var sel = list.First(l => l.Name == _selLayout);
        bool isActive = sel.Name == C.ActiveLayout;
        float wx = 602, cw = 382;
        p.Text(sel.Name, new RectangleF(wx + 10, 78, cw - 10, 30), Theme.Text, 20, FontWeight.SemiBold);
        p.Text(isActive ? "in use now" : sel.Saved is { } t ? $"last used {t:MMM d, HH:mm}" : "not used yet",
            new RectangleF(wx + 10, 106, cw - 10, 20), Theme.Dim, 13);
        if (ui.Button("lay.use", new RectangleF(wx + 10, 140, cw - 10, 46), isActive ? "In use" : $"Switch to {sel.Name}",
                isActive ? Theme.IcoCheck : Theme.IcoLayouts, !isActive, !isActive, 15))
            _h.SwitchLayout(sel.Name);
        bool onWrist = sel.OnWrist;
        if (ui.Toggle("lay.wrist", new RectangleF(wx, 198, cw, 54), "On the watch", ref onWrist, "its button cycles through these profiles"))
            sel.OnWrist = onWrist;
        if (ui.Button("lay.forget", new RectangleF(wx + 10, 264, 210, 38), "Forget this layout", Theme.IcoDelete, false,
                sel.Saved != null && !isActive, 14))
            _h.ForgetLayout(sel.Name);
        ui.Label(new RectangleF(wx + 10, 318, cw - 10, 150), isActive
                ? "You are using this one now: move, resize and bend things as you like - the profile keeps it when you switch to another one."
                : sel.Saved == null
                ? "The first time you switch to it, everything comes to where you are looking - lying in bed, that is above you - and is remembered from then on."
                : "Switching puts every panel back where it was in this profile. Recenter still brings the whole arrangement in front of you.",
            Theme.Dim, 13, FontWeight.Normal, true);
        DrawTheater(ui, lx, wx, cw);
    }

    // theater mode's settings (the same ones as on the bar under a screen in VR) and the bar itself on / off
    void DrawTheater(Ui ui, float lx, float wx, float cw)
    {
        var p = ui.P;
        var t = C.Theater;
        p.Line(lx, 440, 984, 440, Theme.Border, 1);
        ui.Section(new RectangleF(lx, 450, 352, 22), "Theater mode");
        bool b = t.Bar;
        if (ui.Toggle("th.bar", new RectangleF(lx - 10, 474, 362, 54), "Bar under the monitors", ref b,
                "point at a monitor's bottom: Theater + settings")) t.Bar = b;
        b = t.Curved;
        if (ui.Toggle("th.curve", new RectangleF(lx - 10, 530, 362, 54), "Curved", ref b, "bent around you, else flat")) { t.Curved = b; _h.TheaterEdited(); }
        float v = t.Dim;
        if (ui.Slider("th.dim", new RectangleF(wx, 470, cw, 58), "Dim the world", ref v, 0, 0.9f, f => f < 0.005f ? "off" : $"{f * 100:0}%"))
        { t.Dim = v; _h.TheaterEdited(); }
        v = t.Width;
        if (ui.Slider("th.size", new RectangleF(wx, 532, cw, 58), "Screen size", ref v, Theater.MinWidth, Theater.MaxWidth, f => $"{f:0.0} m wide"))
        { t.Width = v; _h.TheaterEdited(); }
    }

    // ---------------- Windows ----------------
    const int MaxWindows = 6;
    string _selWin;          // WindowConfig id
    public bool Picking;     // right side lists the open windows
    IReadOnlyList<WindowInfo> _pickOpen;
    int _pickPage;

    void DrawWindows(Ui ui)
    {
        var p = ui.P;
        var list = C.Windows;
        float lx = 232, lw = 352;
        if (_selWin != null && !list.Any(w => w.Id == _selWin)) _selWin = null;
        if (_selWin == null && list.Count > 0) _selWin = list[0].Id;

        ui.Section(new RectangleF(lx, 80, lw, 22), "Mirrored windows");
        float y = 106;
        foreach (var wc in list)
        {
            var r = new RectangleF(lx, y, lw, 52);
            if (ui.Card(Ui.Id("win.", wc.Id), r, wc.Id == _selWin && !Picking, out _)) { _selWin = wc.Id; Picking = false; }
            string st = _h.WindowStatus(wc);
            p.Circle(r.X + 20, r.Y + 26, 5, st.StartsWith("live") ? Theme.Good : wc.Enabled ? Theme.Warn : Theme.Faint);
            p.Text(Fit(p, string.IsNullOrEmpty(wc.Title) ? wc.Process : wc.Title, lw - 50, 15, FontWeight.SemiBold),
                new RectangleF(r.X + 36, r.Y + 5, lw - 44, 24), wc.Enabled ? Theme.Text : Theme.Dim, 15, FontWeight.SemiBold);
            p.Text(Fit(p, $"{wc.Process}  ·  {st}", lw - 50, 12), new RectangleF(r.X + 36, r.Y + 28, lw - 44, 18), Theme.Dim, 12);
            y += 60;
        }
        if (ui.Button("win.add", new RectangleF(lx, y + 2, 196, 40), "Add a window", Theme.IcoAdd, Picking || list.Count == 0, list.Count < MaxWindows, 14))
        {
            Picking = true;
            _pickPage = 0;
        }
        ui.Label(new RectangleF(lx, y + 58, lw, 110), list.Count == 0
                ? "Mirror one app window instead of a whole monitor: a browser, Discord, a video player... It follows the window when it moves or resizes on your PC."
                : "Clicking a mirrored window brings it to the front on your PC, so the click lands in it. Grip moves it, the stick bends it, two hands resize it.",
            Theme.Dim, 13, FontWeight.Normal, true);

        if (Picking || list.Count == 0) DrawWindowPicker(ui, list.Count > 0);
        else DrawWindowSettings(ui, list.First(w => w.Id == _selWin));
    }

    void DrawWindowPicker(Ui ui, bool canCancel)
    {
        var p = ui.P;
        float rx = 612, rw = 372;
        // while pressing / releasing, the list that was drawn is used (it refreshes every second, and a
        // shifted list must not turn a click into a different window)
        var open = _pickOpen != null && (ui.Pressed || ui.Down || ui.Released || ui.Interacting) ? _pickOpen : (_pickOpen = _h.OpenWindows());
        const int per = 8;
        int pages = Math.Max(1, (open.Count + per - 1) / per);
        _pickPage = Math.Clamp(_pickPage, 0, pages - 1);
        ui.Section(new RectangleF(rx, 80, rw, 22), "Pick a window to mirror");
        if (open.Count == 0)
            p.Text("No windows open", new RectangleF(rx, 106, rw, 48), Theme.Dim, 14);
        for (int i = 0; i < per; i++)
        {
            int idx = _pickPage * per + i;
            if (idx >= open.Count) break;
            var w = open[idx];
            var r = new RectangleF(rx, 106 + i * 54, rw, 48);
            bool mirrored = _h.IsMirrored(w.Hwnd);
            if (ui.Card(Ui.Id("pick.", (int)(long)w.Hwnd), r, false, out _) && !mirrored && C.Windows.Count < MaxWindows)
            {
                _selWin = _h.AddWindow(w).Id;
                Picking = false;
            }
            p.Text(Fit(p, w.Title, rw - (mirrored ? 60 : 28), 14, FontWeight.SemiBold), new RectangleF(r.X + 14, r.Y + 4, rw - 28, 22),
                mirrored ? Theme.Dim : Theme.Text, 14, FontWeight.SemiBold);
            p.Text(mirrored ? w.Process + "  ·  already mirrored" : w.Process, new RectangleF(r.X + 14, r.Y + 26, rw - 28, 17), Theme.Dim, 12);
            if (mirrored) p.Icon(Theme.IcoCheck, new RectangleF(r.Right - 38, r.Y, 26, r.Height), Theme.Accent, 15);
        }
        float by = 106 + per * 54 + 2;
        if (pages > 1)
        {
            if (ui.Button("pick.prev", new RectangleF(rx, by, 44, 38), null, Theme.IcoLeft, false, _pickPage > 0, 13)) _pickPage--;
            p.Text($"{_pickPage + 1} / {pages}", new RectangleF(rx + 48, by, 64, 38), Theme.Dim, 13, FontWeight.Normal, TextAlignment.Center);
            if (ui.Button("pick.next", new RectangleF(rx + 116, by, 44, 38), null, Theme.IcoRight, false, _pickPage < pages - 1, 13)) _pickPage++;
        }
        if (canCancel && ui.Button("pick.cancel", new RectangleF(rx + rw - 110, by, 110, 38), "Cancel", null, false, true, 14))
            Picking = false;
    }

    void DrawWindowSettings(Ui ui, WindowConfig wc)
    {
        var p = ui.P;
        float wx = 602, cw = 382;
        p.Text(Fit(p, string.IsNullOrEmpty(wc.Title) ? wc.Process : wc.Title, cw - 10, 18, FontWeight.SemiBold),
            new RectangleF(wx + 10, 78, cw - 10, 28), Theme.Text, 18, FontWeight.SemiBold);
        p.Text(Fit(p, $"{wc.Process}  ·  {_h.WindowStatus(wc)}", cw - 10, 13), new RectangleF(wx + 10, 104, cw - 10, 20), Theme.Dim, 13);
        bool en = wc.Enabled;
        if (ui.Toggle("wn.en", new RectangleF(wx, 132, cw, 44), "Show in VR", ref en)) _h.SetWindowEnabled(wc, en);
        float v = wc.Width;
        if (ui.Slider("wn.w", new RectangleF(wx, 180, cw, 58), "Size", ref v, 0.15f, 3f, f => $"{f:0.00} m wide", wc.Enabled)) wc.Width = v;
        v = wc.Opacity;
        if (ui.Slider("wn.o", new RectangleF(wx, 240, cw, 58), "Opacity", ref v, 0.2f, 1f, f => $"{f * 100:0}%", wc.Enabled)) wc.Opacity = v;
        v = wc.Curvature;
        float width = wc.Width;
        if (ui.Slider("wn.c", new RectangleF(wx, 300, cw, 58), "Curvature", ref v, 0f, 0.5f,
                f => f < 0.005f ? "flat" : $"bend radius {width / (2 * MathF.PI * f):0.00} m", wc.Enabled))
            wc.Curvature = v < 0.005f ? 0 : v;
        ui.Label(new RectangleF(wx + 10, 364, cw, 22), "Attached to");
        int ai = Math.Max(0, Array.IndexOf(AttachIds, wc.Place.Attach ?? "world"));
        if (ui.Segmented("wn.att", new RectangleF(wx + 10, 388, cw - 10, 38), AttachNames, ref ai, wc.Enabled))
            _h.SetWindowAttach(wc, AttachIds[ai]);
        bool mouse = wc.Mouse;
        if (ui.Toggle("wn.m", new RectangleF(wx, 434, cw, 52), "Mouse control", ref mouse, "laser moves the real cursor, trigger clicks", wc.Enabled))
            wc.Mouse = mouse;
        if (ui.Button("wn.reset", new RectangleF(wx + 10, 498, 168, 38), "Reset position", Theme.IcoRecenter, false, _h.VrConnected && wc.Enabled, 14))
            _h.ResetWindow(wc);
        if (ui.Button("wn.rm", new RectangleF(wx + 188, 498, cw - 188, 38), "Stop mirroring", Theme.IcoDelete, false, true, 14))
        {
            _h.RemoveWindow(wc);
            _selWin = null;
        }
    }

    // long window titles: cut with an ellipsis to fit
    readonly Dictionary<(string, float, float, FontWeight), string> _fit = new();

    string Fit(Painter p, string s, float maxW, float size, FontWeight w = FontWeight.Normal)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var key = (s, maxW, size, w);
        if (_fit.TryGetValue(key, out var r)) return r;
        if (p.MeasureWidth(s, size, w) <= maxW) r = s;
        else
        {
            int lo = 0, hi = s.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (p.MeasureWidth(s[..mid].TrimEnd() + "…", size, w) <= maxW) lo = mid; else hi = mid - 1;
            }
            if (lo > 0 && char.IsHighSurrogate(s[lo - 1])) lo--;           // don't split an emoji
            r = s[..lo].TrimEnd() + "…";
        }
        if (_fit.Count > 400) _fit.Clear();
        _fit[key] = r;
        return r;
    }

    void DrawWrist(Ui ui)
    {
        if (EditingWatch && C.Wrist.Face == "custom") { DrawWristEditor(ui); return; }
        var p = ui.P;
        var w = C.Wrist;
        bool custom = w.Face == "custom";
        float lx = 222, cw = 376, rx = 602;
        bool b = w.Enabled;
        if (ui.Toggle("w.en", new RectangleF(lx, 84, cw, 50), "Wrist watch", ref b)) w.Enabled = b;
        ui.Label(new RectangleF(lx + 10, 138, cw, 22), "Hand");
        int hi = w.Hand == "right" ? 1 : 0;
        if (ui.Segmented("w.hand", new RectangleF(lx + 10, 160, cw - 20, 36), new[] { "Left", "Right" }, ref hi, w.Enabled))
        {
            w.Hand = hi == 1 ? "right" : "left";
            w.Offset = null;
        }
        b = w.AlwaysVisible;
        if (ui.Toggle("w.always", new RectangleF(lx, 204, cw, 52), "Always visible", ref b, "otherwise it fades in when you look at it", w.Enabled)) w.AlwaysVisible = b;
        float v = w.Width;
        if (ui.Slider("w.size", new RectangleF(lx, 262, cw, 58), "Size", ref v, 0.07f, 0.25f, f => $"{f * 100:0} cm", w.Enabled)) w.Width = v;
        b = w.ShowPerformance;
        if (ui.Toggle("w.perf", new RectangleF(lx, 326, cw, 46), "Frame timing & GPU", ref b, null, w.Enabled && !custom)) w.ShowPerformance = b;
        b = w.ShowBatteries;
        if (ui.Toggle("w.bat", new RectangleF(lx, 374, cw, 46), "Batteries", ref b, null, w.Enabled && !custom)) w.ShowBatteries = b;
        b = w.ShowMedia;
        if (ui.Toggle("w.media", new RectangleF(lx, 422, cw, 46), "Now playing", ref b, null, w.Enabled && !custom)) w.ShowMedia = b;
        b = w.Locked;
        if (ui.Toggle("w.lock", new RectangleF(lx, 470, cw, 54), "Lock position", ref b, "the grip won't move or resize the watch", w.Enabled))
            w.Locked = b;
        if (ui.Button("w.reset", new RectangleF(lx + 10, 534, 170, 38), "Reset position", Theme.IcoRecenter, false, w.Enabled, 14))
            _h.ResetWrist();

        var prev = WristPreview(p, 92);
        string[] faces = { "full", "analog", "minimal", "time", "custom" };
        ui.Label(new RectangleF(rx + 10, prev.Bottom + 16, cw, 22), "Watch face");
        int fi = Math.Max(0, Array.IndexOf(faces, w.Face ?? "full"));
        if (ui.Segmented("w.face", new RectangleF(rx + 10, prev.Bottom + 38, cw - 20, 36), new[] { "Full", "Analog", "Minimal", "Time", "Custom" }, ref fi, w.Enabled))
            ChooseFace(faces[fi]);
        b = C.Appearance.Clock24h;
        if (ui.Toggle("w.24h", new RectangleF(rx, prev.Bottom + 82, cw, 44), "24-hour clock", ref b, null, w.Enabled)) C.Appearance.Clock24h = b;
        if (custom)
        {
            if (_h.Wrist.Animating) ui.Animating = true;      // keep the springy preview moving
            // the custom face: its own widgets, set up in the layout editor
            if (ui.Button("w.edit", new RectangleF(rx + 10, prev.Bottom + 132, cw - 20, 38), "Edit layout", Theme.IcoLayouts, true, w.Enabled, 14))
                EditingWatch = true;
            return;
        }
        ui.Label(new RectangleF(rx + 10, prev.Bottom + 132, 120, 36), "Performance");
        int pg = w.PerfGraph ? 1 : 0;
        if (ui.Segmented("w.graph", new RectangleF(rx + 130, prev.Bottom + 132, cw - 140, 36), new[] { "Numbers", "Graph" }, ref pg, w.Enabled && w.ShowPerformance))
            w.PerfGraph = pg == 1;
    }

    RectangleF WristPreview(Painter p, float y)
    {
        var prev = new RectangleF(612, y, 372, 372f * WristPanel.TH / WristPanel.TW);
        p.Fill(RectangleF.Inflate(prev, 10, 10), Theme.Panel, 18);
        p.Image(_h.Wrist.Surf.Bitmap, prev);
        return prev;
    }

    // ---------------- Keyboard ----------------
    void DrawKeyboard(Ui ui)
    {
        var p = ui.P;
        var k = C.Keyboard;
        float lx = 222, rx = 608, cw = 376;
        bool b = k.Visible;
        if (ui.Toggle("k.vis", new RectangleF(lx, 84, cw, 50), "Show keyboard", ref b)) _h.ToggleKeyboard();
        float v = k.Width;
        if (ui.Slider("k.size", new RectangleF(lx, 144, cw, 58), "Size", ref v, 0.3f, 1.2f, f => $"{f * 100:0} cm wide")) k.Width = v;
        ui.Label(new RectangleF(rx + 10, 86, cw, 22), "Attached to");
        int ai = Math.Max(0, Array.IndexOf(AttachIds, k.Place.Attach ?? "world"));
        if (ai == 3) ai = 0;
        if (ui.Segmented("k.att", new RectangleF(rx + 10, 110, cw - 20, 38), AttachNames.Take(3).ToArray(), ref ai))
            _h.SetKeyboardAttach(AttachIds[ai]);
        if (ui.Button("k.reset", new RectangleF(rx + 10, 162, 170, 38), "Reset position", Theme.IcoRecenter, false, _h.VrConnected, 14))
            _h.ResetKeyboard();

        b = k.Sounds;
        if (ui.Toggle("k.snd", new RectangleF(lx, 210, cw, 50), "Key sounds", ref b)) k.Sounds = b;
        int si = k.SoundStyle == "muted" ? 1 : 0;
        if (ui.Segmented("k.style", new RectangleF(rx + 10, 216, 190, 38), new[] { "Click", "Muted" }, ref si, k.Sounds))
        {
            k.SoundStyle = si == 1 ? "muted" : "click";
            _h.Keyboard.PlayClick(false);
        }
        if (ui.Button("k.test", new RectangleF(rx + 210, 216, 156, 38), "Test sound", null, false, k.Sounds, 14))
            _h.Keyboard.PlayClick(false);
        v = k.SoundVolume;
        if (ui.Slider("k.vol", new RectangleF(lx, 264, cw, 58), "Volume", ref v, 0.05f, 1f, f => $"{f * 100:0}%", k.Sounds)) k.SoundVolume = v;
        if (k.Sounds && ui.Released && ui.Hover(new RectangleF(lx, 264, cw, 58))) _h.Keyboard.PlayClick(false);
        b = k.AutoShow;
        if (ui.Toggle("k.auto", new RectangleF(rx, 266, cw, 54), "Pop up for text fields", ref b, "when a click in VR lands in one; hides again after"))
            k.AutoShow = b;

        float pw = 540, ph = pw * KeyboardPanel.TH / KeyboardPanel.TW;      // leaves room for the line below
        var prev = new RectangleF(232 + (752 - pw) / 2, 336, pw, ph);
        p.Fill(RectangleF.Inflate(prev, 8, 8), Theme.Panel, 16);
        p.Image(_h.Keyboard.Surf.Bitmap, prev);
        ui.Label(new RectangleF(232, prev.Bottom + 12, 752, 36),
            "Keys follow the active Windows layout. Shift / Ctrl / Alt / AltGr / Win: once = next key, twice = locked. " +
            "Typing goes to the focused window.", Theme.Dim, 12, FontWeight.Normal, true);
    }

    // ---------------- Look ----------------
    void DrawLook(Ui ui)
    {
        var p = ui.P;
        var a = C.Appearance;
        float lx = 232, rx = 612;
        bool changed = false;

        // left: accent (presets + any hue), panels
        ui.Section(new RectangleF(lx, 84, 360, 20), "Accent colour");
        for (int i = 0; i < Theme.AccentPresets.Length; i++)
        {
            uint c = Theme.AccentPresets[i];
            if (ui.Swatch(Ui.Id("acc", i), lx + 18 + (i % 5) * 46, 126 + (i / 5) * 42, 15, c, (a.Accent & 0xFFFFFF) == (c & 0xFFFFFF)))
            { a.Accent = c; changed = true; }
        }
        float hue = Theme.Hue(a.Accent);
        if (ui.Slider("acc.hue", new RectangleF(lx - 10, 190, 376, 58), "Custom colour", ref hue, 0, 359.9f, f => $"{f:0}°"))
        { a.Accent = Theme.Hsv(hue, 0.72f, 1f); changed = true; }
        // a rainbow under the slider track
        for (int i = 0; i < 60; i++)
            p.Fill(new RectangleF(lx + i * (356 / 60f), 248, 356 / 60f + 0.5f, 4), Theme.Hsv(i * 6, 0.72f, 1f));

        ui.Section(new RectangleF(lx, 266, 360, 20), "Panel colour");
        for (int i = 0; i < Theme.PanelPresets.Length; i++)
        {
            uint c = Theme.PanelPresets[i];
            float cx = lx + 18 + i * 58;
            if (ui.Swatch(Ui.Id("pan", i), cx, 306, 15, Theme.Mix(c, 0xFFFFFFFF, 0.06f), (a.PanelColor & 0xFFFFFF) == (c & 0xFFFFFF)))
            { a.PanelColor = c; changed = true; }
            p.Text(Theme.PanelNames[i], new RectangleF(cx - 29, 326, 58, 18), Theme.Dim, 11, FontWeight.Normal, TextAlignment.Center);
        }
        float op = a.PanelOpacity;
        if (ui.Slider("pan.op", new RectangleF(lx - 10, 350, 376, 58), "Panel opacity", ref op, 0.4f, 1f, f => $"{f * 100:0}%"))
        { a.PanelOpacity = op; changed = true; }
        if (ui.Button("look.reset", new RectangleF(lx, 430, 170, 38), "Default look", Theme.IcoRecenter, false, true, 14))
        {
            C.Appearance = a = new AppearanceConfig
            {
                LaserThickness = a.LaserThickness, LaserAlways = a.LaserAlways, Clock24h = a.Clock24h,
                NightMode = a.NightMode, NightBrightness = a.NightBrightness, Style = a.Style,
            };
            changed = true;
        }
        ui.Section(new RectangleF(lx, 488, 360, 20), "Style");
        int si = a.Style == "classic" ? 1 : 0;
        if (ui.Segmented("look.style", new RectangleF(lx, 512, 356, 38), new[] { "Modern", "Windows 95" }, ref si))
        {
            a.Style = si == 1 ? "classic" : "modern";
            changed = true;
        }
        if (Theme.Classic)
            ui.Label(new RectangleF(lx, 556, 356, 40), "Windows 95 has its own colours; the ones above are for Modern.",
                Theme.Dim, 12, FontWeight.Normal, true);

        // right: one-click themes, laser
        ui.Section(new RectangleF(rx, 84, 360, 20), "Themes");
        for (int i = 0; i < Theme.ThemePresets.Length; i++)
        {
            var (name, acc, pan) = Theme.ThemePresets[i];
            var r = new RectangleF(rx + (i % 3) * 124, 108 + (i / 3) * 50, 116, 42);
            bool sel = (a.Accent & 0xFFFFFF) == (acc & 0xFFFFFF) && (a.PanelColor & 0xFFFFFF) == (pan & 0xFFFFFF);
            if (ui.Card(Ui.Id("theme", i), r, sel, out _)) { a.Accent = acc; a.PanelColor = pan; changed = true; }
            p.Circle(r.X + 20, r.Y + 21, 10, Theme.Mix(pan, 0xFFFFFFFF, 0.12f));
            p.Circle(r.X + 32, r.Y + 21, 10, acc);
            p.Text(name, new RectangleF(r.X + 48, r.Y, r.Width - 52, r.Height), Theme.Text, 14, FontWeight.SemiBold);
        }

        ui.Section(new RectangleF(rx, 220, 360, 20), "Laser");
        if (ui.Swatch("las.acc", rx + 18, 260, 14, Theme.Classic ? Theme.ClassicLaser : Theme.Accent, a.LaserColor == 0, "A")) { a.LaserColor = 0; changed = true; }
        for (int i = 0; i < Theme.AccentPresets.Length - 1; i++)
        {
            uint c = Theme.AccentPresets[i];
            if (ui.Swatch(Ui.Id("las", i), rx + 18 + (i + 1) * 36, 260, 11, c, a.LaserColor != 0 && (a.LaserColor & 0xFFFFFF) == (c & 0xFFFFFF)))
            { a.LaserColor = c; changed = true; }
        }
        float th = a.LaserThickness;
        if (ui.Slider("las.th", new RectangleF(rx - 10, 290, 376, 58), "Thickness", ref th, 1.5f, 8f, f => $"{f:0.0} mm"))
        { a.LaserThickness = MathF.Round(th * 2) / 2; changed = true; }
        bool always = a.LaserAlways;
        if (ui.Toggle("las.always", new RectangleF(rx - 10, 356, 376, 54), "Always show the beam", ref always,
                "otherwise only while pointing at a panel"))
        { a.LaserAlways = always; changed = true; }
        ui.Section(new RectangleF(rx, 428, 360, 20), "Night mode");
        bool night = a.NightMode;
        if (ui.Toggle("night", new RectangleF(rx - 10, 450, 376, 54), "Night mode", ref night, "dims every panel in VR - also on the watch (moon)"))
            a.NightMode = night;
        float nb = a.NightBrightness;
        if (ui.Slider("night.b", new RectangleF(rx - 10, 512, 376, 58), "Night brightness", ref nb, 0.15f, 0.9f, f => $"{f * 100:0}%"))
            a.NightBrightness = nb;
        if (changed) _h.AppearanceChanged();
    }

    // ---------------- General ----------------
    // VRChat: the chatbox status line, who is in the room, and keeping clicks from the game
    void DrawVrchat(Ui ui)
    {
        var p = ui.P;
        var cb = C.Chatbox;
        var rm = C.Room;
        var g = C.General;
        float lx = 222, cw = 376, rx = 602, rw = 382;
        ui.Section(new RectangleF(lx + 10, 84, cw, 22), "Chatbox status line");
        bool b = cb.StatusLine;
        if (ui.Toggle("vc.status", new RectangleF(lx, 108, cw, 54), "Status line", ref b, "over your head in VRChat (needs OSC on in VRChat)"))
            cb.StatusLine = b;
        b = cb.ShowMedia;
        if (ui.Toggle("vc.media", new RectangleF(lx, 164, cw, 44), "Now playing", ref b)) cb.ShowMedia = b;
        b = cb.ShowTime;
        if (ui.Toggle("vc.time", new RectangleF(lx, 210, cw, 44), "Clock", ref b)) cb.ShowTime = b;
        b = cb.ShowVrTime;
        if (ui.Toggle("vc.vrtime", new RectangleF(lx, 256, cw, 44), "Time in VR", ref b)) cb.ShowVrTime = b;
        ui.Label(new RectangleF(lx + 10, 306, cw - 20, 20), cb.StatusLine ? "It says now:" : "It would say:");
        string prev = _h.StatusLinePreview;
        var box = new RectangleF(lx + 10, 330, cw - 20, 64);
        p.Fill(box, Theme.Panel, 10);
        p.Text(string.IsNullOrEmpty(prev) ? "nothing - the chatbox stays empty" : prev, RectangleF.Inflate(box, -12, -6),
            string.IsNullOrEmpty(prev) ? Theme.Dim : Theme.Text, 14, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, null, true);
        ui.Section(new RectangleF(lx + 10, 412, cw, 22), "Controllers");
        b = g.BlockGameInput;
        if (ui.Toggle("vc.block", new RectangleF(lx, 436, cw, 54), "Keep clicks from the game", ref b, "while a laser is on an OrbitOverlay panel"))
            g.BlockGameInput = b;

        ui.Section(new RectangleF(rx + 10, 84, rw, 22), "Who is in the room");
        b = rm.Enabled;
        if (ui.Toggle("vc.room", new RectangleF(rx, 108, rw, 54), "Follow VRChat's log", ref b, _h.RoomStatus)) rm.Enabled = b;
        b = rm.NotifyFriends;
        if (ui.Toggle("vc.nf", new RectangleF(rx, 164, rw, 54), "Tell me when friends come and go", ref b, "friends from VRCX", rm.Enabled))
            rm.NotifyFriends = b;
        b = rm.NotifyStarred;
        if (ui.Toggle("vc.ns", new RectangleF(rx, 220, rw, 54), "... and starred people", ref b, "star them in the Room panel", rm.Enabled))
            rm.NotifyStarred = b;
        b = rm.NotifyEveryone;
        if (ui.Toggle("vc.ne", new RectangleF(rx, 276, rw, 44), "... and everyone", ref b, null, rm.Enabled)) rm.NotifyEveryone = b;
        if (ui.Button("vc.open", new RectangleF(rx + 10, 336, 220, 38), rm.Visible ? "Hide the Room panel" : "Show the Room panel", "\uE716", false,
                _h.VrConnected && rm.Enabled, 14))
            _h.ToggleRoom();
    }

    void DrawGeneral(Ui ui)
    {
        var p = ui.P;
        var g = C.General;
        float lx = 222, cw = 376;
        ui.Label(new RectangleF(lx + 10, 84, cw, 22), "Laser on");
        int li = g.LaserHand == "left" ? 1 : g.LaserHand == "right" ? 2 : 0;
        if (ui.Segmented("g.laser", new RectangleF(lx + 10, 108, cw - 20, 38), new[] { "Both hands", "Left", "Right" }, ref li))
            g.LaserHand = li == 1 ? "left" : li == 2 ? "right" : "both";
        ui.Label(new RectangleF(lx + 10, 152, 120, 38), "Smoothing");
        int sm = Math.Clamp(g.LaserSmoothing, 0, 3);
        if (ui.Segmented("g.smooth", new RectangleF(lx + 110, 152, cw - 120, 38), new[] { "Off", "Low", "Med", "High" }, ref sm))
            g.LaserSmoothing = sm;
        bool b = g.Haptics;
        if (ui.Toggle("g.hap", new RectangleF(lx, 196, cw, 44), "Haptic ticks", ref b)) g.Haptics = b;
        b = g.QuickToggle;
        if (ui.Toggle("g.quick", new RectangleF(lx, 242, cw, 54), "Double-tap both grips", ref b, "hides / shows the monitors and the keyboard")) g.QuickToggle = b;
        b = g.Osc;
        if (ui.Toggle("g.osc", new RectangleF(lx, 298, cw, 54), "VRChat avatar parameters (OSC)", ref b,
                $"XSOverlay-style, for DXOverlay · 127.0.0.1:{g.OscPort}")) g.Osc = b;
        b = g.AutoStart;
        if (ui.Toggle("g.auto", new RectangleF(lx, 354, cw, 54), "Start with SteamVR", ref b,
                b ? (_h.VrConnected ? _h.AutoStartStatus : "applied the next time SteamVR runs") : "start OrbitOverlay yourself"))
            _h.SetAutoStart(b);
        b = g.RunAsAdmin;
        string adminHint = _h.IsElevated ? "running as admin: works in admin windows too"
            : g.RunAsAdmin ? "not elevated right now - restart to apply" : "admin windows (e.g. Task Manager) ignore the VR mouse";
        if (ui.Toggle("g.admin", new RectangleF(lx, 410, cw, 54), "Run as administrator", ref b, adminHint))
            _h.SetRunAsAdmin(b);
        b = g.PauseWhenOff;
        if (ui.Toggle("g.pause", new RectangleF(lx, 466, cw, 46), "Pause when the headset is off", ref b)) g.PauseWhenOff = b;
        float by = 520;
        if (g.RunAsAdmin && !_h.IsElevated)
        {
            if (ui.Button("g.elevate", new RectangleF(lx + 10, by, 200, 38), "Restart as admin", Theme.IcoRecenter, true, true, 14))
                _h.RestartElevated();
            by += 48;
        }
        if (ui.Button("g.folder", new RectangleF(lx + 10, by, 200, 38), "Open config folder", Theme.IcoFolder, false, true, 14))
            _h.OpenConfigFolder();

        float rx = 618, y = 88;
        void Row(string k, string v)
        {
            p.Text(k, new RectangleF(rx, y, 100, 26), Theme.Dim, 13);
            p.Text(v, new RectangleF(rx + 100, y, 266, 26), Theme.Text, 13);
            y += 28;
        }
        Row("SteamVR", _h.VrStatus);
        Row("Input", _h.InputStatus);
        Row("GPU", _h.GpuName);
        Row("Monitors", string.Join(", ", _h.MonitorList.Select(m => m.Name)));
        Row("Config", "%LOCALAPPDATA%\\Orbit");
        // version + updates: one button that checks, then offers the update
        var (us, umsg, uprog, ulatest) = _h.Updates.Snapshot();
        p.Text("Version", new RectangleF(rx, y, 100, 26), Theme.Dim, 13);
        p.Text(Updater.Current.ToString(3), new RectangleF(rx + 100, y, 70, 26), Theme.Text, 13);
        bool avail = us == Updater.State.Available;
        string ulabel = us switch
        {
            Updater.State.Checking => "Checking...",
            Updater.State.Downloading => $"{uprog * 100:0}%",
            Updater.State.Starting => "Installing...",
            Updater.State.Available => $"Update to {ulatest}",
            _ => "Check for updates",
        };
        if (ui.Button("g.upd", new RectangleF(rx + 176, y - 3, 190, 32), ulabel, avail ? Theme.IcoDownload : Theme.IcoRecenter, avail,
                !_h.Updates.Busy, 13))
        {
            if (avail) _h.Updates.Apply();
            else _h.Updates.Check();
        }
        y += 28;
        if (!string.IsNullOrEmpty(umsg))
            p.Text(umsg, new RectangleF(rx + 100, y - 2, 176, 20), us == Updater.State.Failed ? Theme.Warn : Theme.Dim, 12);
        if (ui.Button("g.whatsnew", new RectangleF(rx + 276, y - 4, 90, 26), "What's new", null, false, true, 12)) { _whatsNewManual = true; _wnScroll = 0; }

        ui.Section(new RectangleF(rx, 268, 360, 20), "Notifications");
        b = g.Notifications;
        if (ui.Toggle("g.notif", new RectangleF(rx - 10, 290, 376, 54), "Show notifications", ref b,
                g.Notifications ? _h.NotificationStatus : "XSOverlay format (VRCX, Discord plugins...)")) g.Notifications = b;
        b = g.NotificationSound;
        if (ui.Toggle("g.nsnd", new RectangleF(rx - 10, 346, 250, 44), "Chime", ref b, null, g.Notifications)) g.NotificationSound = b;
        if (ui.Button("g.ntest", new RectangleF(rx + 252, 351, 114, 34), "Test", "\uEA8F", false, g.Notifications && _h.VrConnected, 13))
            _h.TestNotification();
        float na = g.NotificationAngle;
        if (ui.Slider("g.nang", new RectangleF(rx - 10, 392, 376, 58), "Height", ref na, 5, 35,
                f => $"{Math.Round(f):0}° below eye level", g.Notifications))
            g.NotificationAngle = MathF.Round(na);

        ui.Section(new RectangleF(rx, 460, 360, 20), "Controls");
        string[] lines =
        {
            "Trigger: click  ·  B / Y: right click  ·  stick: scroll",
            "Grip: grab & move  ·  stick ↕ distance, ↔ bend",
            "2nd hand's grip: resize  ·  B/Y + grip: leave row",
            "Both grips twice: hide / show monitors + keyboard",
        };
        y = 484;
        foreach (var l in lines) { p.Text(l, new RectangleF(rx, y, 366, 24), Theme.Dim, 13); y += 24; }
    }
}
