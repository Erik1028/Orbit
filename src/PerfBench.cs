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
        using var query = gpu.Device.CreateQuery(new Vortice.Direct3D11.QueryDescription(Vortice.Direct3D11.QueryType.Event));
        Log.Info($"perfbench: style {cfg.Data.Appearance.Style}, face {cfg.Data.Wrist.Face}, widgets " +
                 string.Join(" ", cfg.Data.Wrist.Widgets?.Select(w => $"{w.Type}{w.W}x{w.H}{(w.Style != null ? ":" + w.Style : "")}{(w.Variant != null ? "/" + w.Variant : "")}") ?? Array.Empty<string>()));

        void Phase(string name, double seconds, Action start)
        {
            start();
            var cpu = new List<double>();
            var gpuMs = new List<double>();
            double period = 1 / 90.0, t0 = Clock.Now, next = t0;
            while (Clock.Now - t0 < seconds)
            {
                double a = Clock.Now;
                wrist.Draw(data);
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
            Log.Info($"perfbench: {name,-24} {cpu.Count,3} frames | cpu {S(cpu)} | done on gpu {S(gpuMs)} | over 5 ms: {cpu.Count(x => x > 5)}");
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
        Log.Info($"perfbench: text formats {painter.FormatCount}, widths {painter.WidthCount}, layouts {painter.LayoutCount}, gradients {painter.GradientCount}");
        return 0;
    }
}
