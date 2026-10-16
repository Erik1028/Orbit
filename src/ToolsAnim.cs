using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;

namespace Orbit;

// --animstrip <dir>: the animations as frame strips, drawn in real time (~90 Hz) and saved at a few moments each - the watch's
// cascade, a card under the laser and pressed, a widget opening and closing, a tile going on, the page dots, a cover changing, the
// clocks' changes, a notification coming in, a charging bolt, the settings' tab switch and toggle; the same opening with Motion
// reduced / off; the 95 style's zoom outlines. <strip>_<frame>_<ms>.png, for looking at (nothing is checked). Its own config.
static partial class Tools
{
    public static int AnimStrips(string dir)
    {
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        var cfg = new ConfigStore(Path.Combine(dir, "config.json"));
        cfg.Data.Appearance = new AppearanceConfig { Clock24h = true };
        Theme.Apply(cfg.Data.Appearance);
        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        var data = new PanelData { VrConnected = true, VramTotalGB = 16, FakeGpuLoad = 63, FakeVram = 9.4f, VrSince = DateTime.Now.AddMinutes(-83) };
        data.Vr.AppFps = 86; data.Vr.ReprojPct = 4; data.Vr.GpuMs = 9.3f; data.Vr.DisplayHz = 90;
        data.Vr.Batteries.Add(("HMD", 0.71f, false));
        data.Vr.Batteries.Add(("L", 0.82f, false));
        data.Vr.Batteries.Add(("R", 0.18f, true));
        data.FakeMedia = (true, "Midnight City", "M83", true);
        PagesData(data);
        using var wrist = new WristPanel(gpu, painter, cfg);
        wrist.Mic.Fake = true;
        wrist.Mic.Present = true;
        wrist.OnAction = id =>
        {
            if (id == "night") data.NightMode = !data.NightMode;
            else if (id == "keyboard") data.KeyboardVisible = !data.KeyboardVisible;
        };
        var wc = cfg.Data.Wrist;
        wc.Face = "custom";

        // draws 'frame' about 90 times a second and saves the first frame drawn at or after each moment of 'at' (ms from now); logs
        // what a frame cost (drawn and handed to the GPU)
        void Run(string name, int[] at, Action frame, Action<string> save)
        {
            var sw = Stopwatch.StartNew();
            double sum = 0, max = 0;
            int n = 0;
            for (int k = 0; k < at.Length;)
            {
                double a = Clock.Now;
                frame();
                gpu.Ctx.Flush();
                double ms = (Clock.Now - a) * 1000;
                if (n++ > 0) { sum += ms; max = Math.Max(max, ms); }      // the first: the change itself (a layout, a bitmap)
                double t = sw.Elapsed.TotalMilliseconds;
                if (t >= at[k]) { save(Path.Combine(dir, $"{name}_{k}_{(int)t}.png")); k++; }
                Thread.Sleep(9);
            }
            Log.Info($"animstrip: {name,-18} {n,3} frames, a frame {sum / Math.Max(1, n - 1):0.00} ms on average, {max:0.00} ms at most");
        }
        void W() => wrist.Draw(data);
        void SaveW(string p) => wrist.Surf.SavePng(gpu, p);
        void RunW(string name, params int[] at) => Run(name, at, W, SaveW);
        void Settled() { wrist.PointerLeave(Hand.Left); wrist.Settle(); W(); W(); }
        Vector2 Mid(int x, int y, int w, int h, bool classic = false)
        {
            var c = WatchWidgets.CellRect(x, y, w, h, classic);
            float s = classic ? 2 : 1;
            return new Vector2((c.X + c.Width / 2) * s, (c.Y + c.Height / 2) * s);
        }
        void Down(Vector2 p) { wrist.PointerMove(Hand.Left, p); W(); wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); }
        void Up(Vector2 p) => wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p);
        List<WatchWidget> Face(params (string t, int x, int y, int w, int h)[] cells)
        {
            var l = new List<WatchWidget>();
            foreach (var (t, x, y, w, h) in cells) l.Add(new WatchWidget { Type = t, X = x, Y = y, W = w, H = h });
            return l;
        }
        var face = Face(("time", 0, 0, 2, 2), ("battery", 2, 0, 2, 2), ("media", 4, 0, 2, 2), ("volume", 0, 2, 2, 1), ("cpu", 2, 2, 1, 1),
            ("ram", 3, 2, 1, 1), ("notifications", 4, 2, 2, 2), ("night", 0, 3, 1, 1), ("keyboard", 1, 3, 1, 1), ("buttons", 2, 3, 2, 1));
        int[] quick = { 0, 40, 80, 120, 170, 230, 320, 500 };

        // ---- modern ----
        wc.Widgets = face;
        Settled();
        wrist.Intro();
        RunW("m01_cascade", 0, 60, 120, 200, 300, 420, 600, 900);

        Settled();
        var bat = Mid(2, 0, 2, 2);
        wrist.PointerMove(Hand.Left, bat);
        RunW("m02_hover", 0, 40, 80, 140, 220);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, bat);
        RunW("m03_press", 0, 30, 60, 100, 160);
        Up(bat);
        RunW("m04_open", quick);
        wrist.PointerMove(Hand.Left, new Vector2(3, 3)); W();
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, new Vector2(3, 3)); W();
        Up(new Vector2(3, 3));
        RunW("m05_close", quick);

        Settled();
        var night = Mid(0, 3, 1, 1);
        Down(night); W(); Up(night);
        RunW("m06_tile_on", 0, 40, 80, 120, 170, 240, 340);

        wc.Widgets = Face(("media", 0, 0, 4, 1), ("battery", 4, 0, 2, 1), ("media", 0, 1, 6, 1));
        data.FakeArt = FakeCover();
        Settled();
        var art2 = FakeCover();
        for (int i = 0; i < art2.Length; i++) art2[i] = (art2[i] & 0xFF000000) | ((art2[i] & 0xFF) << 16) | (art2[i] & 0xFF00) | ((art2[i] >> 16) & 0xFF);
        data.FakeArt = art2;
        typeof(WristPanel).GetField("_artVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(wrist, -99);
        RunW("m07_cover", 0, 50, 100, 160, 240, 360, 520);
        wc.Widgets = face;

        Settled();
        data.Notes.Add("VRCX", "Kite joined", "The Black Cat", DateTime.Now);
        RunW("m08_note_in", 0, 40, 80, 130, 200, 300, 450);

        Settled();
        RunW("m09_bolt", 0, 400, 800, 1200, 1600, 2000, 2400);

        // the clocks' designs: a second and a minute go by (10:04:59 -> 10:05:00)
        wc.Widgets = Face(("time", 0, 0, 3, 2), ("time", 3, 0, 3, 2), ("time", 0, 2, 3, 2), ("time", 3, 2, 3, 2));
        wc.Widgets[0].Variant = "segment"; wc.Widgets[1].Variant = "words"; wc.Widgets[2].Variant = "flip"; wc.Widgets[3].Variant = "standard";
        wrist.TestNow = new DateTime(2026, 10, 15, 10, 4, 59);
        Settled();
        wrist.TestNow = new DateTime(2026, 10, 15, 10, 5, 0);
        RunW("m10_clocks", 0, 40, 80, 120, 170, 240, 330, 480);
        wrist.TestNow = null;

        // the page dots and the slide (three pages)
        wc.Widgets = face;
        wc.Pages = new List<WatchPage>
        {
            new() { Name = "Two", Widgets = Face(("room", 0, 0, 3, 2), ("graph", 3, 0, 3, 2), ("media", 0, 2, 6, 2)) },
            new() { Name = "Three", Widgets = Face(("calendar", 0, 0, 3, 2), ("timer", 3, 0, 3, 2)) },
        };
        wc.Page = 0;
        Settled();
        wrist.GoToPage(1, animate: true);
        RunW("m11_page", 0, 40, 80, 120, 160, 220, 300, 450);
        wc.Pages = null;
        wc.Page = 0;

        // the same widget opening with Look > Motion reduced, then off
        foreach (var lvl in new[] { "reduced", "off" })
        {
            cfg.Data.Appearance.Motion = lvl;
            Theme.Apply(cfg.Data.Appearance);
            wc.Widgets = face;
            Settled();
            wrist.Intro();
            Down(bat); W(); Up(bat);
            RunW($"m12_open_{lvl}", 0, 40, 80, 170, 320);
            wrist.CloseForTest();
        }
        cfg.Data.Appearance.Motion = null;
        Theme.Apply(cfg.Data.Appearance);

        // ---- the settings: a tab picked in the list, a toggle, a theme card picked, Motion picked ----
        var host = new FakeHost { Cfg = cfg, GpuName = gpu.AdapterName, MonitorList = new List<MonitorInfo>(), Data = data, Wrist = wrist };
        var view = new SettingsView(host) { Tab = SettingsView.TabWrist };
        var ui = new Ui(painter);
        var ptr = new PointerState();
        using var surf = new Surface(gpu, 1000, 640);
        void S() { ui.Begin(ptr, 1); painter.Begin(surf.Bitmap, Theme.Bg, 1); view.Draw(ui); painter.End(); ui.End(); }
        void SaveS(string p) => surf.SavePng(gpu, p);
        void ClickS(float x, float y) { ptr.Move(x, y); S(); ptr.Press(); S(); ptr.Release(); }
        S(); S();
        ClickS(100, 84 + 5 * 50 + 21);          // Look in the list
        Run("s01_tab", new[] { 0, 30, 60, 100, 150, 220 }, S, SaveS);
        ClickS(800, 383);                       // "Always show the beam"
        Run("s02_toggle", new[] { 0, 40, 80, 130, 200, 300 }, S, SaveS);
        ClickS(612 + 124 + 58, 108 + 21);       // the Ocean theme
        Run("s03_card", new[] { 0, 40, 80, 130, 200, 300 }, S, SaveS);
        ClickS(612 + 356 / 3f * 1.5f, 607);     // Motion: Reduced
        Run("s04_motion", new[] { 0, 40, 80, 150 }, S, SaveS);
        cfg.Data.Appearance.Motion = null;
        cfg.Data.Appearance.LaserAlways = false;
        Theme.Apply(cfg.Data.Appearance);

        // ---- Windows 95: the zoom outline opening a window from its widget and back (OK) ----
        cfg.Data.Appearance = new AppearanceConfig { Style = "classic", Clock24h = true };
        Theme.Apply(cfg.Data.Appearance);
        wc.Widgets = Face(("time", 0, 0, 2, 1), ("battery", 4, 0, 2, 2), ("media", 0, 2, 3, 1), ("volume", 3, 2, 3, 1), ("night", 0, 3, 1, 1),
            ("keyboard", 1, 3, 1, 1), ("cpu", 2, 1, 2, 1));
        Settled();
        var b95 = Mid(4, 0, 2, 2, true);
        Down(b95); W(); Up(b95);
        RunW("w01_open", 0, 40, 80, 120, 160, 200, 260);
        var win = WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, true);
        var ok = new Vector2((win.Right - 8 - 37) * 2, (win.Bottom - 8 - 11) * 2);
        Down(ok); W(); Up(ok);
        RunW("w02_close", 0, 40, 80, 120, 160, 200, 260);
        Settled();
        var n95 = Mid(0, 3, 1, 1, true);
        Down(n95); W(); Up(n95);
        RunW("w03_button", 0, 40, 100);
        // Motion off: no zoom outline, the window at once
        var face95 = wc.Widgets;
        cfg.Data.Appearance.Motion = "off";
        Theme.Apply(cfg.Data.Appearance);
        Settled();
        Down(b95); W(); Up(b95);
        RunW("w04_open_off", 0, 40, 100);
        wrist.CloseForTest();
        cfg.Data.Appearance.Motion = null;
        Theme.Apply(cfg.Data.Appearance);
        // busy: Hexpad still connecting - the hourglass in its windows
        data.Dock = new HexpadLink.Snapshot { State = HexpadLink.LinkState.Connecting };
        wc.Widgets = Face(("dock", 0, 0, 2, 1), ("dock", 2, 0, 1, 1), ("time", 3, 0, 3, 1));
        Settled();
        RunW("w05_busy", 0);
        data.Dock = null;
        wc.Widgets = face95;
        cfg.Data.Appearance = new AppearanceConfig { Clock24h = true };
        Theme.Apply(cfg.Data.Appearance);
        Log.Info("animstrip: written to " + dir);
        return 0;
    }
}
