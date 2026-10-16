using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace Orbit;

// --animstrip <dir> [--only-style xp|aero] (ToolsAnim.cs runs this part): the Windows XP and Windows 7 styles' animations as frames
// at chosen moments (XpAeroFx.Fixed: the animations' clock), for looking at them: <dir>/frames/<tag>_<scene>_<n>.png and
// <dir>/strips.txt (tag | scene | file | label | crop x,y,w,h). A script lays them out as labelled strips. Separate config, no VR, no
// Hexpad.
static partial class Tools
{
    static int AnimStripsXpAero(string dir, string only)
    {
        dir = Path.GetFullPath(dir);
        string fdir = Path.Combine(dir, "frames");
        Directory.CreateDirectory(fdir);
        var cfg = new ConfigStore(Path.Combine(dir, "config-xpaero.json"));      // not the Modern / 95 part's
        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        var data = new PanelData { VrConnected = true, VramTotalGB = 16, FakeGpuLoad = 63, FakeVram = 9.4f, VrSince = DateTime.Now.AddMinutes(-83) };
        data.Vr.AppFps = 86; data.Vr.ReprojPct = 4; data.Vr.GpuMs = 9.3f; data.Vr.DisplayHz = 90;
        data.Vr.Batteries.Add(("HMD", 0.71f, false));
        data.Vr.Batteries.Add(("L", 0.82f, false));
        data.Vr.Batteries.Add(("R", 0.18f, true));
        var rng = new Random(3);
        for (int i = 0; i < VrStats.FrameHistory; i++) data.Vr.FrameMs[i] = 7.5f + (float)rng.NextDouble() * 2.5f;
        data.Vr.FrameHead = 0; data.Vr.FrameCount = VrStats.FrameHistory;
        PagesData(data);
        using var wrist = new WristPanel(gpu, painter, cfg);
        wrist.Mic.Fake = true;
        wrist.Mic.Present = true;
        var host = new FakeHost { Cfg = cfg, GpuName = gpu.AdapterName, MonitorList = new List<MonitorInfo>(), Data = data, Wrist = wrist };
        var view = new SettingsView(host);
        var ui = new Ui(painter);
        using var surf = new Surface(gpu, SettingsView.W95W * 2, SettingsView.W95H * 2);
        var ptr = new PointerState();
        var lines = new List<string>();
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        var wptr = (PointerState)typeof(WristPanel).GetField("_ptr", Priv)!.GetValue(wrist);
        var openStart = typeof(WristPanel).GetField("_openStart", Priv)!;
        var closeDetail = typeof(WristPanel).GetMethod("CloseDetail", Priv)!;
        var mines = (MinesGame[])typeof(WristPanel).GetField("_mines", Priv)!.GetValue(wrist);
        var setUpd = typeof(Updater).GetMethod("Set", Priv)!;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = Tools.SnapCulture;

        double off = 0;           // each scene later than the one before (the animations' state is kept by time); 62.4 s: whole loops
        foreach (var sk in new[] { Skins.Xp, Skins.Aero })
        {
            if (only != null && only != sk.Tag && only != sk.Id) continue;
            string tag = sk.Tag;
            cfg.Data.Appearance = new AppearanceConfig { Style = sk.Id, Clock24h = true };
            Theme.Apply(cfg.Data.Appearance);
            bool aero = sk is AeroSkin;
            int n = 0;
            string scene = "";
            void Shot(Surface s, string label, RectangleF crop)
            {
                string f = $"{tag}_{scene}_{n++:00}.png";
                s.SavePng(gpu, Path.Combine(fdir, f));
                lines.Add($"{tag}|{scene}|{f}|{label}|{crop.X},{crop.Y},{crop.Width},{crop.Height}");
            }
            void Begin(string name) { scene = name; n = 0; off += 62.4; }
            void Sheet(double t) { XpAeroFx.Fixed = t + off; view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal); }
            void Watch(double t) { XpAeroFx.Fixed = t + off; wrist.Draw(data); }
            void At(float x, float y) => ptr.Move(x * 2, y * 2);

            // ---- the push button's hot state (the default one: OK), in and out; then the caption's close box ----
            Begin("hover");
            view.OnClose95 = () => { };
            view.Tab = SettingsView.TabGeneral;
            var btns = new RectangleF(2 * 486, 2 * 440, 2 * 264, 2 * 38);
            double T = 998.4;
            At(300, 300); Sheet(T); Sheet(T); Shot(surf, "rest", btns);
            At(541, 459);
            foreach (double dt in new[] { 0.0, 0.03, 0.06, 0.09, 0.15 }) { Sheet(T + 0.1 + dt); if (dt > 0) Shot(surf, $"in +{dt * 1000:0} ms", btns); }
            At(300, 300);
            foreach (double dt in new[] { 0.0, 0.06, 0.12, 0.2, 0.3, 0.45 }) { Sheet(T + 0.5 + dt); if (dt > 0) Shot(surf, $"out +{dt * 1000:0} ms", btns); }
            Begin("close");
            var cap = new RectangleF(2 * 640, 0, 2 * 110, 2 * 32);
            var cx = aero ? new PointF(721, 10) : new PointF(737, 12);
            At(300, 300); Sheet(T); Sheet(T);
            At(cx.X, cx.Y);
            foreach (double dt in new[] { 0.0, 0.04, 0.08, 0.15 }) { Sheet(T + 0.1 + dt); if (dt > 0) Shot(surf, $"in +{dt * 1000:0} ms", cap); }
            At(300, 300);
            foreach (double dt in new[] { 0.0, 0.1, 0.2, 0.3, 0.45 }) { Sheet(T + 0.5 + dt); if (dt > 0) Shot(surf, $"out +{dt * 1000:0} ms", cap); }

            // ---- the drop-down list sliding open (General: Smoothing) ----
            Begin("combo");
            var cb = new RectangleF(2 * 100, 2 * 96, 2 * 240, 2 * 110);
            At(170, 114); Sheet(1000); ptr.Press(); Sheet(1000); ptr.Release(); Sheet(1000.0);
            foreach (double dt in new[] { 0.03, 0.06, 0.09, 0.12, 0.165 }) { Sheet(1000 + dt); Shot(surf, $"+{dt * 1000:0} ms", cb); }
            At(170, 114); ptr.Press(); Sheet(1000.5); ptr.Release(); Sheet(1000.5); At(300, 300); Sheet(1000.6);

            // ---- busy: checking for updates (the marquee), then downloading ----
            Begin("busy");
            setUpd.Invoke(host.Updates, new object[] { Updater.State.Checking, "checking GitHub...", 0f });
            var busy = new RectangleF(2 * 380, 2 * 178, 2 * 350, 2 * 36);
            float step = aero ? 0.25f : 0.1f;
            for (int i = 0; i < 7; i++) { Sheet(1000 + i * step); Shot(surf, $"{i * step * 1000:0} ms", busy); }
            setUpd.Invoke(host.Updates, new object[] { Updater.State.Downloading, "downloading... 45%", 0.45f });
            Sheet(1002); Shot(surf, "downloading 45%", busy);
            setUpd.Invoke(host.Updates, new object[] { Updater.State.Idle, "", 0f });

            // ---- smooth scrolling: What's New's list, three rows down by the wheel ----
            Begin("scroll");
            host.WhatsNewList = WhatsNew.Since("1.8.2");
            host.WhatsNewSub = "Updated from 1.8.2";
            var wn = new RectangleF(2 * 155, 2 * 66, 2 * 440, 2 * 340);
            At(375, 250);
            foreach (double dt in new[] { 0.0, 0.05, 0.1, 0.15, 0.2, 0.3 }) { Sheet(1003 + dt); if (aero && dt > 0) Shot(surf, $"dialog +{dt * 1000:0} ms", wn); }
            Sheet(1004); Shot(surf, "list at rest", wn);
            ptr.Wheel = -1;
            foreach (double dt in new[] { 0.0, 0.03, 0.06, 0.1, 0.14 }) { Sheet(1004.2 + dt); Shot(surf, $"wheel +{dt * 1000:0} ms", wn); }
            host.WhatsNewList = Array.Empty<WhatsNew.Entry>();
            Sheet(1005); Sheet(1005.1);

            // ---- Aero: the default button's throb; the link's hover ----
            if (aero)
            {
                Begin("throb");
                view.Tab = SettingsView.TabGeneral;
                At(300, 300);
                for (int i = 0; i <= 8; i++) { Sheet(998.4 + i * 0.3); Shot(surf, $"{i * 0.3:0.0} s", btns); }
                Begin("link");
                view.Tab = SettingsView.TabLook;
                var lk = new RectangleF(2 * 16, 2 * 404, 2 * 270, 2 * 30);
                At(300, 300); Sheet(1010); Sheet(1010);
                At(80, 421);
                foreach (double dt in new[] { 0.0, 0.04, 0.08, 0.15 }) { Sheet(1010.1 + dt); if (dt > 0) Shot(surf, $"in +{dt * 1000:0} ms", lk); }
                At(300, 300);
                foreach (double dt in new[] { 0.0, 0.1, 0.2, 0.3, 0.45 }) { Sheet(1010.5 + dt); if (dt > 0) Shot(surf, $"out +{dt * 1000:0} ms", lk); }
            }

            // ---- the notification balloon fading in (its card's Vis goes 0 -> 1 over 0.22 s; drawn from half way) ----
            Begin("balloon");
            using (var np = new NotificationPanel(gpu, painter, cfg))
            {
                var note = new Notification { Source = "VRCX", Title = "Friend online", Content = "Sam has joined The Black Cat (Friends+)" };
                np.AddForPreview(note);
                foreach (float v in new[] { 0.55f, 0.65f, 0.75f, 0.85f, 1f })
                {
                    note.Vis = v;
                    XpAeroFx.Fixed = 1020 + off;
                    np.Draw();
                    Shot(np.Surface, $"in {(v - 0.5f) * 0.22f * 1000:0} ms", new RectangleF(0, 0, NotificationPanel.TW, 200));
                }
            }

            // ---- the watch: a face of its own, pages ----
            var wcfg = cfg.Data.Wrist;
            wcfg.Face = "custom";
            WatchPages.Migrate(cfg);
            var face = new List<WatchWidget>();
            void Put(string t, int x, int y, int w = 1, int h = 1) => face.Add(new WatchWidget { Type = t, X = x, Y = y, W = w, H = h });
            Put("time", 0, 0, 2, 1); Put("battery", 2, 0, 2, 1); Put("fps", 4, 0, 2, 1);
            Put("graph", 0, 1, 3, 2); Put("minesweeper", 3, 1, 1, 1); Put("media", 4, 1, 2, 1); Put("cpu", 3, 2, 1, 1);
            Put("keyboard", 0, 3); Put("settings", 1, 3); Put("night", 2, 3); Put("volume", 4, 2, 2, 1); Put("dock", 3, 3);
            wcfg.Widgets = face;
            wcfg.Page = 0;
            wrist.Settle();
            var watchAll = new RectangleF(0, 0, WristPanel.TW, WristPanel.TH);
            Watch(1030); Watch(1030);
            Begin("face");
            Shot(wrist.Surf, "the face", watchAll);

            // the taskbar: the pointer onto page 2's button, along it, away
            Begin("taskbar");
            float ty = WristPanel.TH95 - WatchWidgets.Taskbar95 - 2;
            var bar = new RectangleF(0, 2 * (ty - 2), WristPanel.TW, 2 * (WatchWidgets.Taskbar95 + 4));
            int pages = WatchPages.Count(wcfg);
            float bw = MathF.Floor(MathF.Min(104, (WristPanel.TW95 - 6 - (pages - 1) * 3) / (float)pages));
            float bx = 3 + (bw + 3), byy = ty + 4 + 8;
            wptr.Move(2 * (bx + 10), 2 * byy);
            foreach (double dt in new[] { 0.0, 0.05, 0.1, 0.15 }) { Watch(1031 + dt); if (dt > 0) Shot(wrist.Surf, $"in +{dt * 1000:0} ms", bar); }
            if (aero)
                foreach (float k in new[] { 0.35f, 0.65f, 0.95f })
                {
                    wptr.Move(2 * (bx + bw * k), 2 * byy);
                    Watch(1031.3 + k); Watch(1031.4 + k);
                    Shot(wrist.Surf, $"pointer at {k * 100:0}%", bar);
                }
            wptr.Leave();
            foreach (double dt in new[] { 0.0, 0.1, 0.2, 0.3 }) { Watch(1033 + dt); if (dt > 0) Shot(wrist.Surf, $"out +{dt * 1000:0} ms", bar); }

            // XP: Task Manager's grid scrolling with the frames; Aero: the progress bars' sheen
            if (!aero)
            {
                Begin("graph");
                var gr = new RectangleF(0, 2 * 52, 2 * 162, 2 * 110);
                for (int i = 0; i < 6; i++)
                {
                    for (int f = 0; f < 9; f++)
                    {
                        data.Vr.FrameMs[data.Vr.FrameHead] = 7.5f + (float)rng.NextDouble() * 2.5f + (i == 3 && f == 4 ? 5 : 0);
                        data.Vr.FrameHead = (data.Vr.FrameHead + 1) % VrStats.FrameHistory;
                    }
                    Watch(1040 + i * 0.1);
                    Shot(wrist.Surf, $"+{i * 100} ms (9 frames each)", gr);
                }
            }
            else
            {
                Begin("sheen");
                var bat = new RectangleF(2 * 104, 0, 2 * 108, 2 * 54);
                for (int i = 0; i <= 7; i++) { Watch(1040.4 + i * 0.2); Shot(wrist.Surf, $"{i * 0.2:0.0} s", bat); }
            }

            // the opened window: in from its tile, then (Minesweeper) back into it
            Begin("open");
            double zs = Theme.RetroSkin.ZoomSeconds;
            wrist.Settle();
            wptr.Leave();
            Watch(1050);
            wrist.OpenForTest("minesweeper");
            foreach (float k in new[] { 0.15f, 0.35f, 0.55f, 0.75f, 0.9f, 1.2f })
            {
                openStart.SetValue(wrist, Clock.Now - k * zs);
                Watch(1050 + k * zs);
                Shot(wrist.Surf, k >= 1 ? "open" : $"{k * zs * 1000:0} ms", watchAll);
            }
            if (aero)
            {
                // 7's reveal ripple: a tap on a corner square
                Begin("ripple");
                var g = mines[0];
                g.Reset(7);
                Watch(1060); Watch(1060.01);
                g.Click(0, Clock.Now);
                foreach (double dt in new[] { 0.0, 0.05, 0.1, 0.15, 0.2, 0.3, 0.45 }) { Watch(1060.1 + dt); Shot(wrist.Surf, $"+{dt * 1000:0} ms", watchAll); }
                // the laser onto a hidden square and away: its glass lights up at once and dims slowly
                Begin("minehover");
                var sqp = wrist.MinesSquarePoint(80, 9);
                wptr.Move(sqp.X, sqp.Y);
                foreach (double dt in new[] { 0.0, 0.03, 0.06 }) { Watch(1061 + dt); if (dt > 0) Shot(wrist.Surf, $"in +{dt * 1000:0} ms", new RectangleF(sqp.X - 60, sqp.Y - 40, 100, 70)); }
                wptr.Leave();
                foreach (double dt in new[] { 0.0, 0.1, 0.2, 0.3, 0.4 }) { Watch(1061.5 + dt); if (dt > 0) Shot(wrist.Surf, $"out +{dt * 1000:0} ms", new RectangleF(sqp.X - 60, sqp.Y - 40, 100, 70)); }
                g.Reset();
                Watch(1062);
            }
            Begin("shut");
            closeDetail.Invoke(wrist, null);
            foreach (float k in new[] { 0.15f, 0.35f, 0.55f, 0.75f, 0.9f })
            {
                openStart.SetValue(wrist, Clock.Now - k * zs);
                Watch(1070 + k * zs);
                Shot(wrist.Surf, $"closing {k * zs * 1000:0} ms", watchAll);
            }
            wrist.CloseForTest();

            // Hexpad connecting: the opened window's marquee (made-up state; no pipe is opened)
            Begin("dock");
            var dock0 = data.Dock;
            var conn = new HexpadLink.Snapshot { State = HexpadLink.LinkState.Connecting, Version = 3 };
            data.Dock = conn;
            wrist.Settle();
            wrist.OpenForTest("dock");
            wrist.DockIn.Pose(conn);
            for (int i = 0; i < 5; i++) { Watch(1080 + i * 0.25); Shot(wrist.Surf, $"{i * 250} ms", watchAll); }
            wrist.DockIn.AfterPose();
            wrist.DockIn.Pose(new HexpadLink.Snapshot());
            data.Dock = dock0;
            wcfg.Widgets = null;
            wcfg.Face = "full";
        }
        XpAeroFx.Fixed = null;
        System.Globalization.CultureInfo.CurrentCulture = culture;
        File.WriteAllLines(Path.Combine(dir, "strips.txt"), lines);
        Log.Info($"animstrip: {lines.Count} frames in {fdir}");
        return 0;
    }
}
