using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using Valve.VR;

namespace Orbit;

// Developer tools: --snapshot renders every panel to PNG without VR; --selftest checks the SteamVR side.
static class Tools
{
    sealed class FakeHost : IOrbitHost
    {
        public ConfigStore Cfg { get; init; }
        public bool VrConnected => true;
        public string VrStatus => "SteamVR · 90 Hz";
        public string InputStatus => "ok";
        public string GpuName { get; init; }
        public IReadOnlyList<MonitorInfo> MonitorList { get; init; }
        public PanelData Data { get; init; }
        public WristPanel Wrist { get; init; }
        public KeyboardPanel Keyboard { get; init; }
        public void Recenter() { }
        public void ResetDesktop(DesktopConfig d) { }
        public void ResetWrist() { }
        public void ResetKeyboard() { }
        public void SetDesktopAttach(DesktopConfig d, string a) => d.Place.Attach = a;
        public void SetKeyboardAttach(string a) { }
        public void SetDesktopEnabled(DesktopConfig d, bool on) => d.Enabled = on;
        public void SetAutoStart(bool on) { }
        public string AutoStartStatus => "on: SteamVR starts Orbit and closes it on exit";
        public void ToggleKeyboard() { }
        public void ToggleDesktops() { }
        public void OpenConfigFolder() { }
        public int GroupSize(DesktopConfig d) => d.Group == null ? 1 : Cfg.Data.Desktops.Count(x => x.Group == d.Group);
        public void JoinAllDesktops() { }
        public void SeparateDesktops() { }
        public void FitCurve(DesktopConfig d) { }
        public void DesktopEdited(DesktopConfig d) { }
        public void AppearanceChanged() => Theme.Apply(Cfg.Data.Appearance);
        public bool IsElevated => true;
        public void SetRunAsAdmin(bool on) { }
        public void RestartElevated() { }
        public string NotificationStatus => "listening on UDP 42069";
        public void TestNotification() { }
        List<WindowInfo> _open;
        public IReadOnlyList<WindowInfo> OpenWindows() => _open ??= WindowList.Enumerate();
        public bool IsMirrored(IntPtr h) => Cfg.Data.Windows.Count > 0 && _open?.Count > 1 && _open[1].Hwnd == h;
        public WindowConfig AddWindow(WindowInfo w)
        {
            var wc = new WindowConfig { Title = w.Title, Process = w.Process, Class = w.Class };
            Cfg.Data.Windows.Add(wc);
            return wc;
        }
        public void RemoveWindow(WindowConfig w) => Cfg.Data.Windows.Remove(w);
        public string WindowStatus(WindowConfig w) => w.Enabled ? "live · 1600×900" : "hidden";
        public void ResetWindow(WindowConfig w) { }
        public void SetWindowAttach(WindowConfig w, string a) => w.Place.Attach = a;
        public void SetWindowEnabled(WindowConfig w, bool on) => w.Enabled = on;
        public void SwitchLayout(string name) => Cfg.Data.ActiveLayout = name;
        public Updater Updates { get; } = new();
        public void ForgetLayout(string name) { }
    }

    public static int Snapshot(string dir)
    {
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        // separate config: a snapshot never touches the real one
        var cfg = new ConfigStore(Path.Combine(dir, "config.json"));
        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);

        var data = new PanelData { VrConnected = true, VramTotalGB = gpu.DedicatedVram / (1024f * 1024 * 1024), FakeGpuLoad = 63, FakeVram = 9.4f };
        data.VrSince = DateTime.Now.AddMinutes(-83);
        data.Vr.AppFps = 86; data.Vr.ReprojPct = 4; data.Vr.GpuMs = 9.3f; data.Vr.DisplayHz = 90;
        data.Vr.Batteries.Add(("HMD", 0.71f, false));
        data.Vr.Batteries.Add(("L", 0.82f, false));
        data.Vr.Batteries.Add(("R", 0.18f, true));
        data.Vr.Batteries.Add(("Trk×6", 0.44f, false));
        data.FakeMedia = (true, "Midnight City", "M83", true);
        var frng = new Random(3);
        for (int i = 0; i < VrStats.FrameHistory; i++)
        {
            data.Vr.FrameMs[i] = 7.5f + (float)frng.NextDouble() * 2.5f + (i % 97 == 0 ? 6 : 0);
            data.Vr.FrameFlag[i] = (byte)(i % 97 == 0 ? 1 : i == 300 ? 2 : 0);
        }
        data.Vr.FrameHead = 0; data.Vr.FrameCount = VrStats.FrameHistory;

        using var wrist = new WristPanel(gpu, painter, cfg);
        var sw0 = System.Diagnostics.Stopwatch.StartNew();
        wrist.Draw(data);
        gpu.Ctx.Flush();
        Log.Info($"first wrist draw {sw0.Elapsed.TotalMilliseconds:0.0} ms");
        sw0.Restart(); wrist.Draw(data); gpu.Ctx.Flush();
        Log.Info($"second wrist draw {sw0.Elapsed.TotalMilliseconds:0.0} ms");
        wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist.png"));
        foreach (var face in new[] { "analog", "minimal", "time" })
        {
            cfg.Data.Wrist.Face = face;
            cfg.Data.Wrist.PerfGraph = face == "analog";
            wrist.Draw(data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_{face}.png"));
        }
        cfg.Data.Wrist.Face = "full";
        cfg.Data.Wrist.PerfGraph = true;
        wrist.Draw(data);
        wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_graph.png"));
        cfg.Data.Wrist.PerfGraph = false;

        using var kb = new KeyboardPanel(gpu, painter, cfg);
        kb.Draw();
        kb.Surf.SavePng(gpu, Path.Combine(dir, "keyboard.png"));
        cfg.Data.Keyboard.Language = "en-US";
        kb.ReloadLayout();
        kb.Draw();
        kb.Surf.SavePng(gpu, Path.Combine(dir, "keyboard_en.png"));
        cfg.Data.Keyboard.Language = "windows";
        kb.ReloadLayout();
        kb.PreviewChat("Szia! Mindjárt jövök, csak befejezem ezt :)");
        kb.Draw();
        kb.Surf.SavePng(gpu, Path.Combine(dir, "keyboard_chat.png"));
        kb.PreviewChat("");
        typeof(KeyboardPanel).GetField("_chat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(kb, false);
        var parsed = NotificationListener.Parse(System.Text.Encoding.UTF8.GetBytes(
            "{\"messageType\":1,\"index\":0,\"timeout\":5.0,\"height\":110.0,\"opacity\":1.0,\"volume\":0.7,\"audioPath\":\"\",\"title\":\"<b>Friend online</b>\",\"content\":\"Sam is in The Black Cat\",\"useBase64Icon\":false,\"icon\":\"default\",\"sourceApp\":\"VRCX\"}"));
        Log.Info($"notification parse: title='{parsed?.Title}' content='{parsed?.Content}' source={parsed?.Source} timeout={parsed?.Timeout}");

        var mons = Monitors.Enumerate();
        foreach (var m in mons)
        {
            Log.Info("monitor " + m);
            if (cfg.Data.Desktops.All(d => d.Id != m.Id))
                cfg.Data.Desktops.Add(new DesktopConfig { Id = m.Id, Name = m.Name, Enabled = !m.Virtual });
        }

        // capture each monitor once
        var dev = MonitorCapture.CreateWinRtDevice(gpu);
        int n = 0;
        foreach (var m in mons)
        {
            try
            {
                using var cap = new MonitorCapture(gpu, dev, m);
                bool got = false;
                for (int i = 0; i < 100 && !got; i++) { got = cap.Poll(); if (!got) Thread.Sleep(20); }
                Log.Info($"capture {m.Name}: {(got ? "frame ok" : "NO FRAME")} {cap.Target.W}x{cap.Target.H}");
                if (got) cap.Target.SavePng(gpu, Path.Combine(dir, $"desktop{++n}.png"), premultiplied: false);
            }
            catch (Exception e) { Log.Error($"capture {m}: {e}"); }
        }

        var host = new FakeHost { Cfg = cfg, GpuName = gpu.AdapterName, MonitorList = mons, Data = data, Wrist = wrist, Keyboard = kb };
        var view = new SettingsView(host);
        var ui = new Ui(painter);
        using var surf = new Surface(gpu, 1500, 960);
        var ptr = new PointerState();
        // a used "Bed" profile with night mode for the Layouts tab
        cfg.Data.Layouts[1].Saved = new DateTime(2026, 10, 13, 23, 40, 0);
        cfg.Data.Layouts[1].NightMode = true;
        cfg.Data.Layouts[1].Panels = cfg.Data.Desktops.Select(d => new PanelSnapshot { Kind = "desktop", Id = d.Id, Enabled = true }).ToList();
        data.NightMode = true;
        data.LayoutName = "Desk";
        wrist.Draw(data);
        wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_buttons.png"));
        data.NightMode = false;
        for (int t = 0; t < SettingsView.TabCount; t++)
        {
            view.Tab = t;
            ui.ResetAnimations();          // every picture fully settled (no half-way nav pill / buttons)
            if (view.ShowsWristPreview) wrist.Draw(data);
            if (view.ShowsKeyboardPreview) kb.Draw();
            ui.Begin(ptr, 1.5f);
            painter.Begin(surf.Bitmap, Theme.Bg, 1.5f);
            view.Draw(ui);
            painter.End();
            ui.End();
            surf.SavePng(gpu, Path.Combine(dir, $"settings{t}.png"));
        }
        // the floating in-VR settings panel (has a close button), General tab
        // Windows tab with two mirrored windows (one hidden), then the picker on top of them
        var open = host.OpenWindows();
        Log.Info($"open windows: {open.Count}" + string.Concat(open.Take(12).Select(w => $"\n   {w}")));
        if (open.Count > 1)
        {
            host.AddWindow(open[1]);
            var hidden = host.AddWindow(open[0]);
            hidden.Enabled = false;
            view.Tab = SettingsView.TabWindows;
            for (int pass = 0; pass < 2; pass++)
            {
                view.Picking = pass == 1;
                ui.Begin(ptr, 1.5f);
                painter.Begin(surf.Bitmap, Theme.Bg, 1.5f);
                view.Draw(ui);
                painter.End();
                ui.End();
                surf.SavePng(gpu, Path.Combine(dir, pass == 0 ? "settings_windows.png" : "settings_windows_pick.png"));
            }
            view.Picking = false;
        }
        // one real window capture: the frame must be the window's visible bounds (mouse mapping relies on it)
        foreach (var w in open)
        {
            if (WindowList.IsIconic(w.Hwnd) || !WindowList.Bounds(w.Hwnd, out var b)) continue;
            try
            {
                using var cap = MonitorCapture.ForWindow(gpu, dev, w.Hwnd);
                bool got = false;
                for (int i = 0; i < 100 && !got; i++) { got = cap.Poll(); if (!got) Thread.Sleep(20); }
                if (!got) for (int i = 0; i < 50 && !got; i++) { got = cap.Poll(); if (!got) Thread.Sleep(20); }   // a resize first
                Log.Info($"window capture {w}: {(got ? "frame ok" : "NO FRAME")} {cap.Target.W}x{cap.Target.H}, frame bounds {b.Right - b.Left}x{b.Bottom - b.Top}");
                if (got) cap.Target.SavePng(gpu, Path.Combine(dir, "window1.png"), premultiplied: false);
            }
            catch (Exception e) { Log.Error($"window capture {w}: {e.Message}"); }
            break;
        }
        var panelView = new SettingsView(host) { OnClose = () => { }, Tab = SettingsView.TabGeneral };
        for (int frame = 0; frame < 2; frame++)      // 2nd frame: animations settled
        {
            ui.Begin(ptr, 1.5f);
            painter.Begin(surf.Bitmap, 0x00000000, 1.5f);
            var all = new System.Drawing.RectangleF(0, 0, SettingsView.W, SettingsView.H);
            painter.PushRoundedClip(all, 22);
            panelView.Draw(ui);
            painter.PopRoundedClip();
            painter.Stroke(System.Drawing.RectangleF.Inflate(all, -1, -1), 0x40FFFFFF, 2, 21);
            painter.End();
            ui.End();
        }
        surf.SavePng(gpu, Path.Combine(dir, "settings_panel.png"));
        // cost of one in-VR settings frame, with and without the rounded mask (layer)
        foreach (bool rounded in new[] { true, false, true, false })
        {
            var swb = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < 20; frame++)
            {
                ui.Begin(ptr, 1.5f);
                painter.Begin(surf.Bitmap, 0x00000000, 1.5f);
                var all = new System.Drawing.RectangleF(0, 0, SettingsView.W, SettingsView.H);
                if (rounded) painter.PushRoundedClip(all, 22);
                panelView.Draw(ui);
                if (rounded) painter.PopRoundedClip();
                painter.End();
                ui.End();
            }
            gpu.Ctx.Flush();
            Log.Info($"settings frame ({(rounded ? "rounded" : "plain")}): {swb.Elapsed.TotalMilliseconds / 20:0.00} ms");
        }
        // the laser sweeping over the General tab: which moves need a new picture
        foreach (float sy in new[] { 150f, 330f, 480f })
        {
            var sweep = new PointerState();
            int moves = 0, redraws = 0;
            for (float x = 10; x < SettingsView.W - 10; x += 1.5f)
            {
                sweep.Move(x * 1.5f, sy * 1.5f);
                moves++;
                if (ui.NeedsRedrawForMove(sweep, 1.5f)) redraws++;
            }
            Log.Info($"pointer sweep at y={sy}: {redraws} redraws for {moves} moves");
        }

        // the same panels in another look (pink accent, midnight panels, 70 % opacity)
        cfg.Data.Appearance = new AppearanceConfig { Accent = 0xFFF472B6, PanelColor = 0xFF0F172A, PanelOpacity = 0.7f, Clock24h = false };
        Theme.Apply(cfg.Data.Appearance);
        wrist.Draw(data);
        wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_alt.png"));
        kb.Draw();
        kb.Surf.SavePng(gpu, Path.Combine(dir, "keyboard_alt.png"));
        view.Tab = SettingsView.TabLook;
        wrist.Draw(data);
        ui.Begin(ptr, 1.5f);
        painter.Begin(surf.Bitmap, Theme.Bg, 1.5f);
        view.Draw(ui);
        painter.End();
        ui.End();
        surf.SavePng(gpu, Path.Combine(dir, "settings_alt.png"));
        KeySounds.Export(dir);
        using (var np = new NotificationPanel(gpu, painter, cfg))
        {
            np.AddForPreview(new Notification { Source = "VRCX", Title = "Friend online", Content = "Sam has joined The Black Cat (Friends+)" });
            np.AddForPreview(new Notification { Source = "Discord", Title = "New message", Content = "Alex: are you in VR? The new watch looks great, the cursor is way smoother now." });
            np.Draw();
            np.Surface.SavePng(gpu, Path.Combine(dir, "notifications.png"));
        }
        using (var cur = new SystemCursor(gpu))
        {
            cur.Update(1);
            cur.Surf.SavePng(gpu, Path.Combine(dir, "cursor.png"));
            Log.Info($"cursor hotspot {cur.Hotspot}");
        }
        // ---- the custom face (widgets on the grid): the watch, the Wrist tab, the editor and a scripted edit ----
        var wcfg = cfg.Data.Wrist;
        string face0 = wcfg.Face;
        List<WatchWidget> Gallery(int n)
        {
            var l = new List<WatchWidget>();
            void Put(string t, int x, int y, int w, int h) => l.Add(new WatchWidget { Type = t, X = x, Y = y, W = w, H = h });
            if (n == 0)
            {
                Put("analog", 0, 0, 2, 2); Put("time", 2, 0, 2, 1); Put("date", 2, 1, 2, 1); Put("battery", 4, 0, 2, 2);
                Put("media", 0, 2, 3, 1); Put("graph", 3, 2, 3, 1);
                Put("fps", 0, 3, 1, 1); Put("reproj", 1, 3, 1, 1); Put("gpu", 2, 3, 1, 1); Put("vram", 3, 3, 1, 1);
                Put("night", 4, 3, 1, 1); Put("layout", 5, 3, 1, 1);
            }
            else if (n == 1)
            {
                Put("media", 0, 0, 4, 2); Put("battery", 4, 0, 1, 1); Put("fps", 5, 0, 1, 1); Put("keyboard", 4, 1, 2, 1);
                Put("perf", 0, 2, 6, 1); Put("time", 0, 3, 3, 1); Put("desktops", 3, 3, 2, 1); Put("settings", 5, 3, 1, 1);
            }
            else
            {
                // the newer widgets
                Put("calendar", 0, 0, 2, 2); Put("timer", 2, 0, 2, 2); Put("battery", 4, 0, 2, 2);
                Put("stopwatch", 0, 2, 2, 1); Put("volume", 2, 2, 3, 1); Put("session", 5, 2, 1, 1);
                Put("cpu", 0, 3, 2, 1); Put("ram", 2, 3, 1, 1); Put("calendar", 3, 3, 1, 1); Put("volume", 4, 3, 2, 1);
            }
            return l;
        }
        void SaveCustom(string tag)
        {
            for (int n = 0; n < 3; n++)
                foreach (var bg in new[] { "panel", "cards" })
                {
                    wcfg.Widgets = Gallery(n);
                    wcfg.Background = bg;
                    wrist.Settle();
                    wrist.Draw(data);
                    wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_custom{n}_{bg}{tag}.png"));
                }
            wcfg.Background = "panel";
            wcfg.Face = face0;
            wcfg.Widgets = null;
        }
        wcfg.Face = "custom";
        var culture0 = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");   // docs pictures in English
        SaveCustom("");
        System.Globalization.CultureInfo.CurrentCulture = culture0;
        wcfg.Face = "custom";
        foreach (int g in new[] { 0, 2 })
        {
            wcfg.Widgets = Gallery(g);
            wrist.Settle();
            wrist.Draw(data);
            var swc = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 30; i++) { wrist.Draw(data); gpu.Ctx.Flush(); }
            Log.Info($"custom face {g}: {swc.Elapsed.TotalMilliseconds / 30:0.00} ms per draw (settled)");
            wrist.Intro();
            swc.Restart();
            int frames = 0;
            while (wrist.Animating && swc.Elapsed.TotalSeconds < 3) { wrist.Draw(data); gpu.Ctx.Flush(); frames++; Thread.Sleep(22); }
            Log.Info($"custom face {g}: the cascade settles in {swc.Elapsed.TotalSeconds:0.00} s ({frames} frames at ~45 fps)");
        }
        wcfg.Widgets = Gallery(0);
        wrist.Draw(data);
        wrist.Intro();
        Thread.Sleep(170);
        wrist.Draw(data);
        wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_custom_intro.png"));
        Thread.Sleep(1300);
        wrist.Draw(data);
        wcfg.Face = face0;
        wcfg.Widgets = null;
        void LayoutCheck(string style)
        {
            string f0 = wcfg.Face;
            LayoutCheckRun(wrist, data, wcfg, gpu, dir, style);
            wcfg.Face = f0;
            wcfg.Widgets = null;
        }
        // every opened view once (both styles: see the 95 section), and a tap that opens one / a tap beside it that closes it
        void SaveOpened(string tag)
        {
            var kinds = new[] { ("media", 0), ("battery", 0), ("time", 0), ("calendar", 2), ("timer", 2), ("stopwatch", 2), ("fps", 0), ("cpu", 2), ("volume", 2) };
            foreach (var (type, g) in kinds)
            {
                wcfg.Face = "custom";
                wcfg.Widgets = Gallery(g);
                wrist.Settle();
                if (!wrist.OpenForTest(type)) { Log.Warn("snapshot: no widget " + type); continue; }
                wrist.Draw(data);
                wrist.Draw(data);
                wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_open_{type}{tag}.png"));
                wrist.CloseForTest();
            }
            // gallery 0's media widget (cells 0-2, row 2): a tap on its card, away from its button
            wcfg.Widgets = Gallery(0);
            wrist.Settle();
            var at = new System.Numerics.Vector2(Theme.Classic ? 150 : 150, Theme.Classic ? 270 : 270);
            void TapWrist(System.Numerics.Vector2 p)
            {
                wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
                wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
                wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
            }
            TapWrist(at);
            bool opened = wrist.IsOpen;
            TapWrist(new System.Numerics.Vector2(3, 3));       // beside the card (modern) / the 95 desktop
            Log.Info($"opened widget{tag}: a tap opened it {opened} (want True), a tap beside it closed it {!wrist.IsOpen} (want {(!Theme.Classic).ToString()})");
            wrist.CloseForTest();
            wcfg.Face = face0;
            wcfg.Widgets = null;
        }
        SaveOpened("");
        LayoutCheck("modern");
        string Dump() => string.Join(" ", wcfg.Widgets.Select(w => $"{w.Type}@{w.X},{w.Y}:{w.W}x{w.H}"));
        // Wrist tab with the custom face picked (seeded from "full"), then the editor
        view.Tab = SettingsView.TabWrist;
        void FrameM()
        {
            wrist.Draw(data);
            ui.Begin(ptr, 1.5f);
            painter.Begin(surf.Bitmap, Theme.Bg, 1.5f);
            view.Draw(ui);
            painter.End();
            ui.End();
        }
        void ClickM(float x, float y) { ptr.Move(x * 1.5f, y * 1.5f); FrameM(); ptr.Press(); FrameM(); ptr.Release(); FrameM(); }
        ui.ResetAnimations();
        wcfg.Face = "full";
        ClickM(612 + 10 + (376 - 20) * 4.5f / 5, 92 + 372f * WristPanel.TH / WristPanel.TW + 38 + 18);     // the "Custom" segment
        Log.Info($"custom face: picked '{wcfg.Face}', seeded {wcfg.Widgets?.Count} widgets: {Dump()}");
        surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_custom.png"));
        view.EditingWatch = true;
        ptr.Leave();
        FrameM();
        // remove the fps widget, move gpu into its place, stretch gpu to 2 x 1 by its corner
        static System.Numerics.Vector2 CellM(int cx, int cy) =>
            new(222 + (16 + cx * (94.6667f + 8) + 47.33f) * 0.75f, 84 + (16 + cy * 104 + 48) * 0.75f);
        var c42 = CellM(4, 2);
        ClickM(c42.X, c42.Y);
        ClickM(724 + 130, 212 + 19);              // Remove
        var gpuW = wcfg.Widgets.FirstOrDefault(w => w.Type == "gpu");
        var c52 = CellM(5, 2);
        ptr.Move(c52.X * 1.5f, c52.Y * 1.5f); FrameM(); ptr.Press(); FrameM();
        ptr.Move(c42.X * 1.5f, c42.Y * 1.5f); FrameM();
        surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_editor_drag.png"));
        ptr.Release(); FrameM();
        Log.Info($"editor: fps removed {wcfg.Widgets.All(w => w.Type != "fps")}, gpu moved to {gpuW?.X},{gpuW?.Y} (want 4,2)");
        var hnd = new System.Numerics.Vector2(222 + (16 + 5 * 102.6667f - 8) * 0.75f - 2, 84 + (16 + 3 * 104 - 8) * 0.75f - 2);
        ptr.Move(hnd.X * 1.5f, hnd.Y * 1.5f); FrameM(); ptr.Press(); FrameM();
        ptr.Move(c52.X * 1.5f, c52.Y * 1.5f); FrameM(); ptr.Release(); FrameM();
        Log.Info($"editor: gpu size {gpuW?.W}x{gpuW?.H} (want 2x1); layout now {Dump()}");
        // no room: every cell is taken, so adding must say so and change nothing
        int before = wcfg.Widgets.Count;
        ClickM(222 + 58, 452 + 15);               // palette: Time
        Log.Info($"editor: add with a full grid kept {wcfg.Widgets.Count == before} ({wcfg.Widgets.Count} widgets)");
        ptr.Leave();
        FrameM();
        surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_editor.png"));
        view.EditingWatch = false;
        wcfg.Face = face0;
        wcfg.Widgets = null;
        view.Tab = 0;

        // the Windows 95 skin: every screen once more
        cfg.Data.Appearance = new AppearanceConfig { Style = "classic", Clock24h = true };
        Theme.Apply(cfg.Data.Appearance);
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        for (int t = 0; t < SettingsView.TabCount; t++)
        {
            view.Tab = t;
            ui.ResetAnimations();
            if (view.ShowsWristPreview) wrist.Draw(data);
            if (view.ShowsKeyboardPreview) kb.Draw();
            view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
            surf.SavePng(gpu, Path.Combine(dir, $"settings{t}_95.png"));
        }
        // a drop-down list opened by a click (General: Smoothing), then the panel version
        view.Tab = SettingsView.TabGeneral;
        var combo = new System.Numerics.Vector2(170 * 2, 114 * 2);
        ptr.Move(combo.X, combo.Y);
        view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        ptr.Press();
        view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        ptr.Release();
        view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        ptr.Move(combo.X, combo.Y + 13 * 2 * 3);
        view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        surf.SavePng(gpu, Path.Combine(dir, "settings_combo_95.png"));
        void Frame95() => view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        void Click95(float x, float y) { ptr.Move(x * 2, y * 2); Frame95(); ptr.Press(); Frame95(); ptr.Release(); Frame95(); Frame95(); }
        // pick "High" (item 3) in the open list
        int smooth0 = cfg.Data.General.LaserSmoothing;
        Click95(170, 126 + 3 * 13 + 6);
        Log.Info($"95 drop-down: picked smoothing {cfg.Data.General.LaserSmoothing} (want 3, was {smooth0})");
        // a click outside an open list only closes it: the check box under the pointer must not change
        bool hap0 = cfg.Data.General.Haptics;
        Click95(170, 114);                  // open again
        Click95(34, 140);                   // "Haptic ticks", outside the list
        bool hap1 = cfg.Data.General.Haptics;
        Click95(34, 140);                   // now it is a real click
        Log.Info($"95 drop-down: outside click kept haptics {hap0}->{hap1} (want same), next click {cfg.Data.General.Haptics} (want {!hap0})");
        cfg.Data.General.LaserSmoothing = smooth0;
        cfg.Data.General.Haptics = hap0;
        ptr.Leave();
        ui.ResetAnimations();
        panelView.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, 0x00000000);
        surf.SavePng(gpu, Path.Combine(dir, "settings_panel_95.png"));
        foreach (var face in new[] { "full", "analog", "minimal", "time" })
        {
            cfg.Data.Wrist.Face = face;
            cfg.Data.Wrist.PerfGraph = face == "analog";
            wrist.Draw(data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_{face}_95.png"));
        }
        cfg.Data.Wrist.Face = "full";
        cfg.Data.Wrist.PerfGraph = false;
        kb.Draw();
        kb.Surf.SavePng(gpu, Path.Combine(dir, "keyboard_95.png"));
        using (var np = new NotificationPanel(gpu, painter, cfg))
        {
            np.AddForPreview(new Notification { Source = "VRCX", Title = "Friend online", Content = "Sam has joined The Black Cat (Friends+)" });
            np.Draw();
            np.Surface.SavePng(gpu, Path.Combine(dir, "notifications_95.png"));
        }
        SaveOpened("_95");
        LayoutCheck("95");
        // the custom face in the 95 style: the watch, the Wrist page and its editor with a selection
        wcfg.Face = "custom";
        SaveCustom("_95");
        wcfg.Face = "custom";
        wcfg.Widgets = Gallery(0);
        view.Tab = SettingsView.TabWrist;
        ui.ResetAnimations();
        wrist.Draw(data);
        view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal);
        surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_custom_95.png"));
        view.EditingWatch = true;
        void Frame95W() { wrist.Draw(data); view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal); }
        // click the battery window (cells 4-5, rows 0-1): 95 cell 4 starts at 4 + 4 * 52.67
        ptr.Move((31 + 4 + 4 * 52.67f + 40) * 2, (82 + 4 + 30) * 2);
        Frame95W(); ptr.Press(); Frame95W(); ptr.Release(); Frame95W();
        ptr.Leave(); Frame95W();
        surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_editor_95.png"));
        view.EditingWatch = false;
        wcfg.Face = face0;
        wcfg.Widgets = null;
        System.Globalization.CultureInfo.CurrentCulture = culture;
        Log.Info("snapshot written to " + dir);
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

    // Every widget kind at every size, alone on a watch with no background ("cards"), and every opened view
    // without its clip: anything drawn outside the card sticks out (a non-transparent pixel there), and in the
    // modern style anything within a few pixels of the card's edge is too close to it. Logged, one line each.
    static int LayoutCheckRun(WristPanel wrist, PanelData data, WristConfig wcfg, Gpu gpu, string dir, string style)
    {
        bool classic = Theme.Classic;
        int problems = 0;
        float k = classic ? 2 : 1;                       // 95: the canvas is shown 2x
        uint[] Read()
        {
            string tmp = Path.Combine(dir, "_layout.png");
            wrist.Surf.SavePng(gpu, tmp);
            uint[] px;
            using (var bmp = new System.Drawing.Bitmap(tmp))
            {
                px = new uint[bmp.Width * bmp.Height];
                var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, (int[])(object)px, 0, px.Length);
                bmp.UnlockBits(bd);
            }
            File.Delete(tmp);
            return px;
        }
        // pixels outside 'r' (grown by 'grow') that are not transparent; and pixels in the band [inset, inset2) inside r
        // that differ from the card's own colour (the colour at its centre-left edge band, sampled)
        (int outside, string where) Outside(uint[] px, System.Drawing.RectangleF r, float grow, System.Drawing.RectangleF? also = null)
        {
            int n = 0; string where = null;
            for (int y = 0; y < WristPanel.TH; y++)
                for (int x = 0; x < WristPanel.TW; x++)
                {
                    if (x >= r.Left - grow && x < r.Right + grow && y >= r.Top - grow && y < r.Bottom + grow) continue;
                    if (also is { } a && x >= a.Left - grow && x < a.Right + grow && y >= a.Top - grow && y < a.Bottom + grow) continue;
                    if ((px[y * WristPanel.TW + x] >> 24) > 24) { n++; where ??= $"({x},{y})"; }
                }
            return (n, where);
        }
        (int close, string where) TooClose(uint[] px, System.Drawing.RectangleF r, float from, float to)
        {
            // the card colour: the most common colour in the band
            var counts = new Dictionary<uint, int>();
            var band = new List<(int, int)>();
            for (int y = (int)r.Top; y < (int)r.Bottom; y++)
                for (int x = (int)r.Left; x < (int)r.Right; x++)
                {
                    float dx = MathF.Min(x - r.Left, r.Right - 1 - x), dy = MathF.Min(y - r.Top, r.Bottom - 1 - y), d = MathF.Min(dx, dy);
                    // skip the rounded corners (the card's own curve lives there)
                    if (dx < 26 && dy < 26) continue;
                    if (d < from || d >= to) continue;
                    uint c = px[y * WristPanel.TW + x];
                    band.Add((x, y));
                    counts[c] = counts.TryGetValue(c, out int v) ? v + 1 : 1;
                }
            if (band.Count == 0) return (0, null);
            uint card = 0; int best = -1;
            foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; card = kv.Key; }
            int n = 0; string where = null;
            foreach (var (x, y) in band)
            {
                uint c = px[y * WristPanel.TW + x];
                int dr = (int)((c >> 16) & 255) - (int)((card >> 16) & 255), dg = (int)((c >> 8) & 255) - (int)((card >> 8) & 255), db = (int)(c & 255) - (int)(card & 255);
                if (Math.Abs(dr) + Math.Abs(dg) + Math.Abs(db) > 24) { n++; where ??= $"({x},{y})"; }
            }
            return (n, where);
        }
        wcfg.Face = "custom";
        wcfg.Background = "cards";
        foreach (var kind in WatchWidgets.Kinds)
            foreach (var (w, h) in kind.Sizes)
            {
                wcfg.Widgets = new List<WatchWidget> { new() { Type = kind.Id, X = 0, Y = 0, W = w, H = h } };
                wrist.Settle();
                wrist.Draw(data);
                wrist.Draw(data);
                var px = Read();
                var r = WatchWidgets.CellRect(0, 0, w, h, classic);
                r = new System.Drawing.RectangleF(r.X * k, r.Y * k, r.Width * k, r.Height * k);
                var (o, ow) = Outside(px, r, classic ? 0 : 1.5f);
                var (cl, cw) = classic || kind.Button ? (0, null) : TooClose(px, r, 3, 8);
                if (o > 0 || cl > 0)
                {
                    problems++;
                    Log.Warn($"layout {style}: {kind.Id} {w}x{h}: {(o > 0 ? $"{o} px outside the card, first at {ow}" : "")}{(o > 0 && cl > 0 ? "; " : "")}{(cl > 0 ? $"{cl} px within 8 px of its edge, first at {cw}" : "")}");
                }
            }
        // the opened views: a 2x2 (or the smallest) widget of each, opened, without clip / dimming
        wrist.LayoutTest = true;
        foreach (var kind in WatchWidgets.Kinds)
        {
            if (kind.Button) continue;
            var (w, h) = kind.Sizes[kind.Default];
            wcfg.Widgets = new List<WatchWidget> { new() { Type = kind.Id, X = 0, Y = 0, W = w, H = h } };
            wrist.Settle();
            if (!wrist.OpenForTest(kind.Id)) continue;
            wrist.Draw(data);
            wrist.Draw(data);
            var px = Read();
            var t = classic ? new System.Drawing.RectangleF(16, 12, (WristPanel.TW95 - 16) * 2, (WristPanel.TH95 - 12) * 2)
                : WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, false);
            var own = WatchWidgets.CellRect(0, 0, w, h, classic);
            own = new System.Drawing.RectangleF(own.X * k, own.Y * k, own.Width * k, own.Height * k);
            var (o, ow) = Outside(px, t, classic ? 0 : 1.5f, own);
            var (cl, cw) = classic ? (0, null) : TooClose(px, t, 3, 14);
            if (o > 0 || cl > 0)
            {
                problems++;
                Log.Warn($"layout {style}: opened {kind.Id}: {(o > 0 ? $"{o} px outside the card, first at {ow}" : "")}{(o > 0 && cl > 0 ? "; " : "")}{(cl > 0 ? $"{cl} px within 14 px of its edge, first at {cw}" : "")}");
            }
            wrist.CloseForTest();
        }
        wrist.LayoutTest = false;
        wcfg.Background = "panel";
        Log.Info($"layout {style}: {problems} problem(s)");
        return problems;
    }

    // --wintest DIR: the desktop settings window in both styles (with its own config in DIR), on screen
    // for a moment each: a screenshot, its size at this DPI, and what the 95 caption answers to
    // WM_NCHITTEST (it must drag the window, but not over its ? and X boxes).
    public static int WinTest(string dir)
    {
        Directory.CreateDirectory(dir);
        var cfg = new ConfigStore(Path.Combine(dir, "config.json"));
        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        var data = new PanelData { VrConnected = true };
        using var wrist = new WristPanel(gpu, painter, cfg);
        using var kb = new KeyboardPanel(gpu, painter, cfg);
        var mons = Monitors.Enumerate();
        foreach (var m in mons)
            if (cfg.Data.Desktops.All(d => d.Id != m.Id))
                cfg.Data.Desktops.Add(new DesktopConfig { Id = m.Id, Name = m.Name, Enabled = !m.Virtual });
        var host = new FakeHost { Cfg = cfg, GpuName = gpu.AdapterName, MonitorList = mons, Data = data, Wrist = wrist, Keyboard = kb };
        var view = new SettingsView(host);
        using var win = new SettingsWindow(gpu, painter, view, () =>
        {
            if (view.ShowsWristPreview) wrist.Draw(data);
            if (view.ShowsKeyboardPreview) kb.Draw();
        }, System.Drawing.SystemIcons.Application);
        void Pump(double seconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds) { System.Windows.Forms.Application.DoEvents(); win.Tick(Clock.Now); Thread.Sleep(15); }
        }
        int Hit(float cx, float cy)
        {
            var p = win.PointToScreen(new System.Drawing.Point((int)cx, (int)cy));
            return (int)SendMessage(win.Handle, 0x84, IntPtr.Zero, (IntPtr)((p.Y << 16) | (p.X & 0xFFFF)));
        }
        int step = 0;
        foreach (var style in new[] { "classic", "modern", "classic" })
        {
            cfg.Data.Appearance.Style = style;
            Theme.Apply(cfg.Data.Appearance);
            win.ShowAndFocus();
            win.TopMost = true;
            Pump(1.5);
            float k = win.ClientSize.Width / (float)SettingsView.W95W;
            Log.Info($"{style}: border {win.FormBorderStyle}, client {win.ClientSize.Width}x{win.ClientSize.Height}, dpi {win.DeviceDpi}, " +
                     $"hit caption={Hit(100 * k, 10 * k)} help={Hit(720 * k, 12 * k)} close={Hit(737 * k, 12 * k)} page={Hit(100 * k, 100 * k)}");
            var r = win.RectangleToScreen(win.ClientRectangle);
            using var bmp = new System.Drawing.Bitmap(r.Width, r.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, System.Drawing.Point.Empty, r.Size);
            bmp.Save(Path.Combine(dir, $"window{++step}_{style}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        win.AllowClose = true;
        win.Close();
        return 0;
    }

    // Registers a temporary NON-elevated copy of the task, runs it with arguments ("--osctest 9018",
    // which a listener can catch), then deletes it. Proves the COM task path without a UAC prompt.
    public static int TaskTest()
    {
        const string name = "Orbit (test, temporary)";
        if (!Elevation.InstallTask(highest: false, name: name)) return 2;
        try
        {
            bool ready = Elevation.TaskReady(name);
            bool ran = Elevation.RunTask("--osctest 9018", name);
            Log.Info($"task test: ready={ready} ran={ran}");
            Thread.Sleep(3000);
            return ready && ran ? 0 : 3;
        }
        finally { Elevation.RemoveTask(name); }
    }

    // Simulated hand tremor + tracking noise at 90 Hz through each smoothing level: jitter left on a
    // 1920 px monitor 1.07 m wide at 1.2 m while holding still, and how far the cursor trails behind
    // slow / medium / fast sweeps (ms behind the hand, measured once the sweep is steady).
    // An off-screen window with a text box, a read-only text box and a button: classifies each
    // directly (class name / accessibility role) and through the focus hook.
    public static int FocusTest()
    {
        const int OBJID_CLIENT = -4;
        var form = new System.Windows.Forms.Form
        {
            StartPosition = System.Windows.Forms.FormStartPosition.Manual, Location = new System.Drawing.Point(-6000, -6000),
            Size = new System.Drawing.Size(400, 200), ShowInTaskbar = false, Text = "Orbit focus test",
        };
        var text = new System.Windows.Forms.TextBox { Left = 10, Top = 10, Width = 200 };
        var ro = new System.Windows.Forms.TextBox { Left = 10, Top = 40, Width = 200, ReadOnly = true, Text = "read only" };
        var button = new System.Windows.Forms.Button { Left = 10, Top = 70, Text = "OK" };
        var multi = new System.Windows.Forms.RichTextBox { Left = 220, Top = 10, Width = 150, Height = 80 };
        form.Controls.AddRange(new System.Windows.Forms.Control[] { text, ro, button, multi });
        form.Show();
        System.Windows.Forms.Application.DoEvents();
        foreach (var (name, c) in new (string, System.Windows.Forms.Control)[] { ("text box", text), ("read-only box", ro), ("button", button), ("rich text", multi) })
        {
            int quick = TextFocusWatcher.Classify(c.Handle, OBJID_CLIENT, 0, false, out _);
            int deep = TextFocusWatcher.Classify(c.Handle, OBJID_CLIENT, 0, true, out bool vague);
            Log.Info($"classify {name,-14} quick={quick,2} deep={deep,2}{(vague ? " (vague)" : "")}");
        }
        using var w = new TextFocusWatcher(skipOwnProcess: false) { Trace = s => Log.Info("  " + s) };
        w.Start();
        w.InspectFor(30);
        Thread.Sleep(200);
        form.Activate();
        foreach (var (name, c) in new (string, System.Windows.Forms.Control)[] { ("text box", text), ("button", button), ("read-only box", ro), ("rich text", multi) })
        {
            c.Focus();
            for (int i = 0; i < 20; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(15); }
            var (seq, state, _) = w.Snapshot();
            Log.Info($"focus {name,-14} -> event #{seq} state {state}");
        }
        form.Close();
        return 0;
    }

    // Runs one background component alone and logs the process's private memory every 10 s,
    // then forces a full GC: growth that a GC gives back = garbage / RCWs, growth that stays = a leak.
    public static int LeakTest(string what, int seconds)
    {
        static long Priv() { using var p = System.Diagnostics.Process.GetCurrentProcess(); return p.PrivateMemorySize64; }
        IDisposable comp = what switch
        {
            "gpu" => new GpuStats(),
            "media" => new MediaInfo { Active = true },
            _ => null,
        };
        long start = Priv();
        Log.Info($"leaktest {what}: start private {start / 1024} KB");
        for (int t = 10; t <= seconds; t += 10)
        {
            Thread.Sleep(10000);
            Log.Info($"leaktest {what}: {t,3} s  private {Priv() / 1024} KB  (+{(Priv() - start) / 1024} KB)  GCs {GC.CollectionCount(0)}");
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Thread.Sleep(500);
        Log.Info($"leaktest {what}: after full GC  private {Priv() / 1024} KB  (+{(Priv() - start) / 1024} KB)");
        if (comp is MediaInfo mi)
        {
            var snap = mi.Snapshot();
            Log.Info($"leaktest media: {mi.Reads} reads, has={snap.has} playing={snap.playing} title='{snap.title}' art={(snap.art != null ? "yes" : "no")}");
        }
        comp?.Dispose();
        return 0;
    }

    public static int FilterTest()
    {
        const float dt = 1f / 90, pxPerRad = 1920 / 1.07f * 1.2f;
        for (int level = 0; level <= 3; level++)
        {
            var rng = new Random(1);
            var f = new RayFilter();
            double sum = 0, sum2 = 0; int n = 0;
            for (int i = 0; i < 900; i++)
            {
                float t = i * dt;
                // 0.12 deg noise + 0.08 deg 8 Hz tremor around straight ahead
                float jx = (float)(Gauss(rng) * 0.0021 + 0.0014 * Math.Sin(2 * Math.PI * 8 * t));
                float jy = (float)(Gauss(rng) * 0.0021 + 0.0014 * Math.Cos(2 * Math.PI * 7 * t));
                var (_, d) = f.Filter(Vector3.Zero, Vector3.Normalize(new Vector3(jx, jy, -1)), dt, level);
                if (i < 180) continue;
                double ax = Math.Atan2(d.X, -d.Z);
                sum += ax; sum2 += ax * ax; n++;
            }
            double sd = Math.Sqrt(sum2 / n - (sum / n) * (sum / n));
            var lags = new System.Collections.Generic.List<string>();
            foreach (float speed in new[] { 0.1f, 0.5f, 2.0f })
            {
                var f2 = new RayFilter();
                double lag = 0;
                int frames = Math.Min(270, (int)(2.5f / speed / dt));    // stay below 2.5 rad of sweep
                for (int i = 0; i < frames; i++)
                {
                    float ang = i * dt * speed;
                    var (_, d) = f2.Filter(Vector3.Zero, new Vector3(MathF.Sin(ang), 0, -MathF.Cos(ang)), dt, level);
                    lag = (ang - Math.Atan2(d.X, -d.Z)) / speed;
                }
                lags.Add($"{speed:0.0} rad/s {lag * 1000:0} ms");
            }
            Log.Info($"smoothing {level}: jitter {sd * 180 / Math.PI:0.000} deg = {sd * pxPerRad:0.0} px | lag {string.Join(", ", lags)}");
        }
        return 0;
    }

    static double Gauss(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());

    // Sends one of each OSC message type to 127.0.0.1:<port> (for checking the wire format).
    public static int OscTest(int port)
    {
        using var osc = new OscSender();
        osc.Send(0, port, new (string, object)[] { ("isOverlayOpen", true), ("isKeyboardOpen", false), ("openOverlayCount", 3), ("leftControllerBattery", 0.75f) });
        return 0;
    }

    // Pretends to be a game: submits a dark gradient to both eyes so the dashboard closes and
    // overlays can be seen (and timed) the way they look over VRChat.
    public static int FakeScene(bool openDashboardTab)
    {
        var e = EVRInitError.None;
        OpenVR.Init(ref e, EVRApplicationType.VRApplication_Scene);
        if (e != EVRInitError.None) { Log.Error("scene init: " + e); return 3; }
        try
        {
            int adapter = -1;
            OpenVR.System.GetDXGIOutputInfo(ref adapter);
            using var gpu = new Gpu(adapter);
            using var painter = new Painter(gpu);
            uint w = 0, h = 0;
            OpenVR.System.GetRecommendedRenderTargetSize(ref w, ref h);
            using var eye = new Surface(gpu, (int)w, (int)h);
            painter.Begin(eye.Bitmap, 0xFF20304A);
            for (int y = 0; y < h; y += 8)
                painter.Fill(new System.Drawing.RectangleF(0, y, w, 8), Theme.Mix(0xFF2B4A6E, 0xFF101820, y / (float)h));
            painter.End();
            var tex = new Texture_t { handle = eye.Tex.NativePointer, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Gamma };
            var bounds = new VRTextureBounds_t { uMin = 0, vMin = 0, uMax = 1, vMax = 1 };
            var render = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            var game = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool dashDone = false;
            while (sw.Elapsed.TotalSeconds < 12)
            {
                OpenVR.Compositor.WaitGetPoses(render, game);
                OpenVR.Compositor.Submit(EVREye.Eye_Left, ref tex, ref bounds, EVRSubmitFlags.Submit_Default);
                OpenVR.Compositor.Submit(EVREye.Eye_Right, ref tex, ref bounds, EVRSubmitFlags.Submit_Default);
                if (openDashboardTab && !dashDone && sw.Elapsed.TotalSeconds > 4)
                {
                    dashDone = true;
                    OpenVR.Overlay.ShowDashboard(OrbitApp.AppKey + ".settings");
                    Log.Info("ShowDashboard called");
                }
            }
            Log.Info("scene done");
            return 0;
        }
        finally { OpenVR.Shutdown(); }
    }

    // Needs SteamVR running (a headset or the null driver). Shows a test overlay and checks
    // which way ComputeOverlayIntersection reports V, plus frame sync and timing calls.
    public static int SelfTest()
    {
        if (!VrSession.RuntimeRunning()) { Log.Error("SteamVR is not running"); return 2; }
        using var vr = new VrSession();
        if (!vr.TryConnect(Path.Combine(AppContext.BaseDirectory, "assets", "actions.json"), out var err)) { Log.Error("init: " + err); return 3; }
        using var gpu = new Gpu(vr.AdapterIndex);
        using var painter = new Painter(gpu);
        using var surf = new Surface(gpu, 512, 256);
        painter.Begin(surf.Bitmap, Theme.Panel2);
        painter.Fill(new System.Drawing.RectangleF(0, 0, 512, 64), Theme.Accent);       // top band
        painter.Text("Orbit self-test (top = cyan)", new System.Drawing.RectangleF(0, 64, 512, 192), Theme.Text, 30,
            Vortice.DirectWrite.FontWeight.SemiBold, Vortice.DirectWrite.TextAlignment.Center);
        painter.End();

        using var ov = new VrOverlay("erik.orbit.selftest", "Orbit self-test");
        ov.SetTexture(surf.Tex);
        ov.SetMouseScale(512, 256);
        ov.SetWidth(1.0f);               // -> 0.5 m tall
        var at = Matrix4x4.CreateTranslation(0, 1.5f, -2f);
        ov.SetAbsolute(at);
        ov.Show();
        Thread.Sleep(300);

        // ray 0.15 m above the centre and 0.25 m right: top-left uv should be (0.75, 0.2)
        var p = new VROverlayIntersectionParams_t
        {
            vSource = new HmdVector3_t { v0 = 0.25f, v1 = 1.65f, v2 = 0 },
            vDirection = new HmdVector3_t { v0 = 0, v1 = 0, v2 = -1 },
            eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding,
        };
        var r = new VROverlayIntersectionResults_t();
        bool hit = OpenVR.Overlay.ComputeOverlayIntersection(ov.Handle, ref p, ref r);
        Log.Info($"intersection hit={hit} dist={r.fDistance:0.000} uv=({r.vUVs.v0:0.000},{r.vUVs.v1:0.000}) point=({r.vPoint.v0:0.00},{r.vPoint.v1:0.00},{r.vPoint.v2:0.00})");
        if (hit)
            Log.Info(Math.Abs(r.vUVs.v1 - 0.8f) < 0.05f ? "UV CONVENTION: bottom-left origin (flip needed) -> UvFlipV=true is right"
                : Math.Abs(r.vUVs.v1 - 0.2f) < 0.05f ? "UV CONVENTION: top-left origin -> UvFlipV must be false" : "UV CONVENTION: unexpected");

        // curved overlay: does it bend around an axis r in front of its origin (+Z)?
        using (var cv = new VrOverlay("erik.orbit.selftest.curve", "Orbit self-test curve"))
        {
            const float w = 1.0f, c = 0.2f;
            float rad = w / (2 * MathF.PI * c), alpha = w / (2 * rad);
            cv.SetTexture(surf.Tex);
            cv.SetMouseScale(512, 256);
            cv.SetWidth(w);
            cv.SetCurvature(c);
            cv.SetAbsolute(Matrix4x4.CreateTranslation(0, 1.5f, -rad));
            cv.Show();
            Thread.Sleep(300);
            foreach (float k in new[] { 0f, 0.5f, 0.9f, -0.9f })
            {
                float phi = k * alpha;
                var cp = new VROverlayIntersectionParams_t
                {
                    vSource = new HmdVector3_t { v0 = 0, v1 = 1.5f, v2 = 0 },
                    vDirection = new HmdVector3_t { v0 = MathF.Sin(phi), v1 = 0, v2 = -MathF.Cos(phi) },
                    eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding,
                };
                var cr = new VROverlayIntersectionResults_t();
                bool ch = OpenVR.Overlay.ComputeOverlayIntersection(cv.Handle, ref cp, ref cr);
                Log.Info($"curve k={k,5:0.0}: hit={ch} dist={cr.fDistance:0.000} (axis model expects {rad:0.000}) u={cr.vUVs.v0:0.000} (expects {0.5f + k / 2:0.000})");
            }
        }

        var sync = OpenVR.Overlay.WaitFrameSync(100);
        Log.Info($"WaitFrameSync: {sync}");
        vr.Update();
        Log.Info($"HMD valid={vr.HmdValid} pos={vr.Hmd.Translation} hz={vr.DisplayHz} input={vr.InputStatus} left={vr.LeftIndex} right={vr.RightIndex}");
        var stats = new VrStats();
        stats.Update(vr, 1); Thread.Sleep(1100); stats.Update(vr, 2.2);
        Log.Info($"stats fps={stats.AppFps} reproj={stats.ReprojPct} gpu={stats.GpuMs}ms batteries={string.Join(",", stats.Batteries)}");

        int holdMs = 4000;
        Log.Info($"holding the test overlay for {holdMs} ms");
        Thread.Sleep(holdMs);
        return 0;
    }
}
