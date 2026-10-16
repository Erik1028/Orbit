using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Orbit;

// --perfbench <dir>: what the watch costs per frame, without VR - your own watch layout and look (from a COPY of the
// config; the real one is only read), paced like the headset (90 Hz): the cascade when it comes into view, cold and
// warm, then every widget opened. CPU = drawing + hand-over; GPU = until the GPU has finished it.
// --bench-style <look>: every widget of that face in a look of its own (tint, solid, twotone, header, outline, glass, or mixed: all
// of them in turn), each in another colour - what a fully styled face costs.
// --bench-variant <design>: every clock of the face that has that design (WatchWidgets.Variants) drawn in it - flip, segment, ...
static class PerfBench
{
    public static int Run(string dir, string style = null, string variant = null)
    {
        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        string real = Path.Combine(Log.Dir, "config.json"), copy = Path.Combine(dir, "config.json");
        if (File.Exists(real)) File.Copy(real, copy, true);
        var cfg = new ConfigStore(copy);
        // ORBIT_BENCH_STYLE (or ORBIT_BENCH_LOOK: xp, aero, cyber... any Look > Style id) / ORBIT_BENCH_MOTION (full, reduced, off):
        // your face in that style / motion, on the copy
        if ((Environment.GetEnvironmentVariable("ORBIT_BENCH_STYLE") ?? Environment.GetEnvironmentVariable("ORBIT_BENCH_LOOK")) is { Length: > 0 } bs)
            cfg.Data.Appearance.Style = bs;
        if (Environment.GetEnvironmentVariable("ORBIT_BENCH_MOTION") is { Length: > 0 } bm) cfg.Data.Appearance.Motion = bm;
        Theme.Apply(cfg.Data.Appearance);
        using var gpu = new Gpu(-1);
        using var painter = new Painter(gpu);
        var data = new PanelData { VrConnected = true, VramTotalGB = 16, FakeGpuLoad = 63, FakeVram = 9.4f, VrSince = DateTime.Now.AddMinutes(-83) };
        data.Vr.AppFps = 86; data.Vr.ReprojPct = 4; data.Vr.GpuMs = 9.3f; data.Vr.DisplayHz = 90;
        data.Vr.Batteries.Add(("HMD", 0.71f, false));
        data.Vr.Batteries.Add(("L", 0.82f, false));
        data.Vr.Batteries.Add(("R", 0.18f, true));
        data.FakeMedia = (true, "Midnight City", "M83", true);
        for (int i = 0; i < VrStats.FrameHistory; i++) data.Vr.FrameMs[i] = 7.5f + (i % 7) * 0.3f;
        data.Vr.FrameHead = 0; data.Vr.FrameCount = VrStats.FrameHistory;
        if (style != null && cfg.Data.Wrist.Widgets != null)
        {
            var ws = cfg.Data.Wrist.Widgets;
            for (int i = 0; i < ws.Count; i++)
            {
                ws[i].Style = style == "mixed" ? Bento.Styles[1 + i % (Bento.Styles.Length - 1)] : style;
                ws[i].Color = Bento.Palette[i % Bento.Palette.Length].c;
            }
        }
        if (variant != null)
            foreach (var w in cfg.Data.Wrist.Widgets ?? new List<WatchWidget>())
                if (Array.Exists(WatchWidgets.VariantsOf(w.Type) ?? Array.Empty<WatchWidgets.Variant>(), v => v.Id == variant)) w.Variant = variant;
        using var wrist = new WristPanel(gpu, painter, cfg);
        wrist.Mic.Fake = true;                          // never the real microphone
        wrist.Mic.Present = true;
        CyberFx.Live = wrist.CyberLive = true;          // Cyber: its effects run as on the headset (WristCyberFx.cs)
        using var query = gpu.Device.CreateQuery(new Vortice.Direct3D11.QueryDescription(Vortice.Direct3D11.QueryType.Event));
        Log.Info($"perfbench: style {cfg.Data.Appearance.Style}, face {cfg.Data.Wrist.Face}, widgets " +
                 string.Join(" ", cfg.Data.Wrist.Widgets?.Select(w => $"{w.Type}{w.W}x{w.H}{(w.Style != null ? ":" + w.Style : "")}{(w.Variant != null ? "/" + w.Variant : "")}") ?? Array.Empty<string>()));

        void Phase(string name, double seconds, Action start)
        {
            start();
            var cpu = new List<double>();
            var gpuMs = new List<double>();
            double period = 1 / 90.0, t0 = Clock.Now, next = t0;
            long alloc = 0;
            while (Clock.Now - t0 < seconds)
            {
                double a = Clock.Now;
                long m0 = GC.GetAllocatedBytesForCurrentThread();
                wrist.Draw(data);
                alloc += GC.GetAllocatedBytesForCurrentThread() - m0;
                gpu.Ctx.End(query);
                gpu.Ctx.Flush();
                double b = Clock.Now;
                while (gpu.Ctx.GetData(query, IntPtr.Zero, 0, Vortice.Direct3D11.AsyncGetDataFlags.None).Code != 0) Thread.SpinWait(50);
                double c = Clock.Now;
                cpu.Add((b - a) * 1000);
                gpuMs.Add((c - a) * 1000);
                next += period;
                double sleep = next - Clock.Now;
                if (sleep > 0.002) Thread.Sleep((int)((sleep - 0.001) * 1000));
                while (Clock.Now < next) Thread.SpinWait(20);
            }
            static string S(List<double> l)
            {
                var s = l.OrderBy(x => x).ToList();
                return $"avg {l.Average():0.00} p95 {s[(int)(s.Count * 0.95)]:0.00} max {s[^1]:0.00}";
            }
            Log.Info($"perfbench: {name,-24} {cpu.Count,3} frames | cpu {S(cpu)} | done on gpu {S(gpuMs)} | over 5 ms: {cpu.Count(x => x > 5)}" +
                     $" | allocated {alloc / Math.Max(1, cpu.Count)} B a frame");
        }

        Phase("first look (cold)", 1.4, () => wrist.Intro());
        Phase("look again (warm)", 1.4, () => wrist.Intro());
        int made = painter.GradientsMade;
        Phase("at rest", 1.0, () => { });
        Log.Info($"perfbench: gradient brushes made at rest {painter.GradientsMade - made} (want 0)");
        foreach (var w in cfg.Data.Wrist.Widgets ?? new List<WatchWidget>())
        {
            if (WatchWidgets.Find(w.Type) is not { Button: false }) continue;
            wrist.Settle();
            if (!wrist.OpenForTest(w.Type)) continue;
            Phase("open " + w.Type, 0.6, () => { });
            wrist.CloseForTest();
        }
        // 1.12: the pages the first start adds (on the copy) - each at rest (the Performance page: its 30 s graph), and a slide
        var wc = cfg.Data.Wrist;
        if (wc.Face == "custom")
        {
            WatchPages.Migrate(cfg);
            Tools.PagesData(data);
            for (int i = 1; i < WatchPages.Count(wc); i++)
            {
                wc.Page = i;
                wrist.Settle();
                wrist.Draw(data);
                Phase($"page {i + 1} ({WatchPages.Name(wc, i)}) at rest", 1.0, () => { });
            }
            wc.Page = 1;
            wrist.Settle();
            wrist.Draw(data);
            Phase("slide to the next page", 0.6, () => wrist.GoToPage(2, animate: true));
            wc.Page = 0;
        }
        if (Theme.Cyber) CyberPhases();

        // Cyber's effects one by one on page 1, and how many frames the watch asks for at rest on each page - the Update rule's own
        // (every 0.5 s and each new second) against the ones Cyber adds (CyberMoving / CyberNextFrame: a loop's tick, a sweep, a glitch)
        void CyberPhases()
        {
            wc.Page = 0;
            wrist.Settle();
            wrist.Draw(data);
            Phase("cyber: minute glitch", 0.3, () => wrist.CyberTest("glitch"));
            wrist.Settle(); wrist.Draw(data);
            Phase("cyber: scan sweep", 0.75, () => wrist.CyberTest("scan"));
            wrist.Settle(); wrist.Draw(data);
            Phase("cyber: tap", 0.3, () => wrist.CyberTest("tap"));
            wrist.Settle(); wrist.Draw(data);
            Phase("cyber: button wipe", 0.3, () => data.NightMode = !data.NightMode);
            wrist.Settle(); wrist.Draw(data);
            if (wc.Widgets?.Find(x => WatchWidgets.Find(x.Type) is { Button: false } k && k.Id != "dock") is { } ow)
            {
                Phase("cyber: unfold " + ow.Type, 0.5, () => wrist.CyberTest("open:" + ow.Type));
                wrist.CloseForTest();
            }
            for (int i = 0; i < WatchPages.Count(wc); i++)
            {
                wc.Page = i;
                wrist.Settle();
                wrist.Draw(data);
                // the first 9 s (a page shown: its loops run 8 s), then 12 s more (they rest; the sweeps go on)
                double t0 = Clock.Now, baseNext = 0, cyNext = double.MaxValue;
                int[] all = new int[2], extra = new int[2];
                while (Clock.Now - t0 < 21)
                {
                    double now = Clock.Now;
                    int half = now - t0 < 9 ? 0 : 1;
                    bool baseDue = now >= baseNext, cyDue = now >= cyNext || wrist.CyberMoving;
                    if (baseDue || cyDue)
                    {
                        wrist.Draw(data);
                        all[half]++;
                        if (!baseDue) extra[half]++;
                        else baseNext = Math.Min(now + 0.5, now + 1.02 - DateTime.Now.Millisecond / 1000.0);
                        cyNext = wrist.CyberNextFrame;
                    }
                    double sleep = 1 / 90.0 - (Clock.Now - now);
                    if (sleep > 0.002) Thread.Sleep((int)(sleep * 1000));
                }
                Log.Info($"perfbench: cyber at rest, page {i + 1}: first 9 s {all[0]} frames, {extra[0]} Cyber's own ({extra[0] / 9.0:0.0} a second); " +
                         $"then 12 s {all[1]} frames, {extra[1]} Cyber's own ({extra[1] / 12.0:0.0} a second)");
            }
            wc.Page = 0;
        }
        Log.Info($"perfbench: text formats {painter.FormatCount}, widths {painter.WidthCount}, layouts {painter.LayoutCount}, gradients {painter.GradientCount}");
        return 0;
    }
}
