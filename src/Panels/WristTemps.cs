using System;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The Temperatures widget ("temps"; the readings: Sys/Temps.cs). 1x1: the GPU; 2x1: the CPU and the GPU; 2x2: those, the GPU's hotspot
// and fan, the last 10 minutes as small bars; the 4 x 3 page's S, M, L the same, bigger. A tap opens both big with the last 10 minutes
// as a graph (the hot alert's line on it while the alert is on). A value that cannot be read is "–", and the opened view says why
// ("Needs administrator"...). Amber from 80 °C, red from 90 (the hotspot: 95 / 105).
// Modern: the 1x1 cards' value and line (Cyber: its tag and segments through the same helpers). The retro styles: 95's System Monitor
// (its line charts and status line; XP, 7 and the Mac in their own frames and controls), the Terminal's `sensors` output.
// The texts are made when a sample (every 2 s) or the language changes: a frame at rest makes nothing.
public sealed partial class WristPanel
{
    public const string TempsKindIcon = "";          // Segoe's diagnostic (a pulse): the widget in the editor's palette
    static readonly string[] CapCpuT = { "CPU" }, CapGpuT = { "GPU" };

    sealed class TempText
    {
        public TempReport R;
        public int Lang = -1;
        public string Cpu, Gpu, Hot, Fan, Extra, CpuDeg, GpuDeg, CpuLine, GpuLine, PeakCpu, PeakGpu, Status, TCpu, TGpu, THot, TFan, Head95Cpu, Head95Gpu;
    }
    readonly TempText _tt = new();

    static string Num(float v) => float.IsNaN(v) ? "–" : MathF.Round(v).ToString(CultureInfo.CurrentCulture);
    static string Deg(float v) => float.IsNaN(v) ? "–" : MathF.Round(v).ToString(CultureInfo.CurrentCulture) + " °C";

    // the texts of this sample (made again only for another sample or language)
    TempText TempTexts(PanelData d)
    {
        var r = d.Temps;
        var t = _tt;
        if (ReferenceEquals(r, t.R) && t.Lang == Lang.Version) return t;
        (t.R, t.Lang) = (r, Lang.Version);
        float cpu = r?.Cpu ?? float.NaN, gpu = r?.Gpu ?? float.NaN, hot = r?.Hot ?? float.NaN, fan = r?.Fan ?? float.NaN;
        t.Cpu = Num(cpu);
        t.Gpu = Num(gpu);
        t.Hot = Deg(hot);
        t.Fan = float.IsNaN(fan) ? "–" : Lang.F("{0} rpm", MathF.Round(fan));
        t.CpuDeg = Deg(cpu);
        t.GpuDeg = Deg(gpu);
        t.Extra = Lang.F("Hotspot {0} · fan {1}", t.Hot, t.Fan);
        t.CpuLine = "CPU: " + t.CpuDeg;
        t.GpuLine = "GPU: " + t.GpuDeg;
        float pc = r?.Peak(true) ?? float.NaN, pg = r?.Peak(false) ?? float.NaN;
        t.PeakCpu = float.IsNaN(cpu) ? Lang.T(r?.CpuWhy ?? "Not readable") : Lang.F("peak {0} in 10 min", Deg(pc));
        t.PeakGpu = float.IsNaN(gpu) ? Lang.T(r?.GpuWhy ?? "Not readable") : Lang.F("peak {0} in 10 min", Deg(pg));
        static string Head(string what, string name, string deg, float v, string why) =>
            float.IsNaN(v) ? Lang.F("{0}: – ({1})", what, Lang.T(why ?? "Not readable")) : $"{what}: {deg}" + (name != null ? "  " + name : "");
        t.Head95Cpu = Head("CPU", r?.CpuName, t.CpuDeg, cpu, r?.CpuWhy);
        t.Head95Gpu = Head("GPU", r?.GpuName, t.GpuDeg, gpu, r?.GpuWhy);
        t.Status = Lang.F("Hotspot: {0}   Fan: {1}   Peak: CPU {2}, GPU {3}", t.Hot, t.Fan, Deg(pc), Deg(pg));
        // `sensors`: k10temp's Tctl, amdgpu's edge / junction / fan1
        t.TCpu = float.IsNaN(cpu) ? "Tctl:      N/A" : string.Format(CultureInfo.InvariantCulture, "Tctl:   {0,6:+0.0;-0.0}°C", cpu);
        t.TGpu = float.IsNaN(gpu) ? "edge:      N/A" : string.Format(CultureInfo.InvariantCulture, "edge:   {0,6:+0.0;-0.0}°C", gpu);
        t.THot = float.IsNaN(hot) ? "junction: N/A" : string.Format(CultureInfo.InvariantCulture, "junction:{0,6:+0.0;-0.0}°C", hot);
        t.TFan = float.IsNaN(fan) ? "fan1:      N/A" : string.Format(CultureInfo.InvariantCulture, "fan1:   {0,6:0} RPM", fan);
        return t;
    }

    static StatLevel TempLevel(float c, bool hotspot) =>
        float.IsNaN(c) ? StatLevel.Ok : c >= (hotspot ? 105 : 90) ? StatLevel.Bad : c >= (hotspot ? 95 : 80) ? StatLevel.Warn : StatLevel.Ok;

    // 20 °C .. 100 °C as the line's 0..1 (-1: not read)
    static float TempFrac(float c) => float.IsNaN(c) ? -1 : Math.Clamp((c - 20) / 80, 0, 1);

    uint TempColor(StatLevel lv) => Theme.Cyber ? (lv == StatLevel.Ok ? CyY : lv == StatLevel.Warn ? CyY : CyR) : Bento.Level(lv, RoleColor("temps"));

    // --snapshot's hook: what the widget writes for the GPU and the CPU
    public string TempsShown(PanelData d) { var t = TempTexts(d); return t.Gpu + "/" + t.Cpu; }

    // ================= Modern (and Cyber, through the same helpers) =================

    void TempsOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var t = TempTexts(d);
        var rep = d.Temps;
        float cpu = rep?.Cpu ?? float.NaN, gpu = rep?.Gpu ?? float.NaN;
        if (w.W == 1 && w.H == 1) { TempSmall(r, CapGpuT, t.Gpu, gpu, Ui.Id("wtg", id)); return; }
        float half = MathF.Floor(r.Width / 2), h = w.H >= 2 ? (_big ? 116 : 96) : r.Height;
        TempSmall(new RectangleF(r.X, r.Y, half, h), CapCpuT, t.Cpu, cpu, Ui.Id("wtc", id));
        TempSmall(new RectangleF(r.X + half, r.Y, r.Width - half, h), CapGpuT, t.Gpu, gpu, Ui.Id("wtg", id));
        if (w.H < 2) return;
        // the GPU's hotspot and fan; the last 10 minutes, a row of bars each
        float pad = PadX(r), x = r.X + pad, iw = r.Width - 2 * pad, y = r.Y + h + 18;
        TextAt(PhotoCut(t.Extra, iw, CapSize), x, y, iw + 2, Sub, CapSize, FontWeight.SemiBold);
        float bh = MathF.Floor((r.Bottom - 14 - (y + 10) - 6) / 2);
        if (bh < 6) return;
        TempBars(new RectangleF(x, y + 10, iw, bh), rep, true);
        TempBars(new RectangleF(x, y + 16 + bh, iw, bh), rep, false);
    }

    void TempSmall(RectangleF r, string[] cap, string value, float v, string key)
    {
        bool none = float.IsNaN(v);
        var lv = TempLevel(v, false);
        Small(r, cap, value, none ? null : "°C", TempFrac(v), TempColor(lv), none ? (Theme.Cyber ? CyberHud.Faint : Bento.Faint) : Theme.Cyber ? CyW : Bento.Level(lv, Bento.Text),
            key, lv: lv);
    }

    // the last 10 minutes as bars (the highest of each bar's samples), its colour by level; nothing read yet: the track alone
    void TempBars(RectangleF b, TempReport r, bool cpu)
    {
        _p.Fill(new RectangleF(b.X, b.Bottom - 1, b.Width, 1), Theme.Cyber ? CyberHud.Line : Bento.Track);
        if (r == null || r.Count == 0) return;
        int cols = Math.Max(1, (int)(b.Width / 5));
        float cw = b.Width / cols;
        for (int c = 0; c < cols; c++)
        {
            int i0 = TempReport.History * c / cols, i1 = TempReport.History * (c + 1) / cols, start = TempReport.History - r.Count;
            float m = float.NaN;
            for (int i = Math.Max(i0, start); i < i1; i++)
            {
                float v = cpu ? r.CpuAgo(i - start) : r.GpuAgo(i - start);
                if (!float.IsNaN(v) && !(v <= m)) m = v;
            }
            if (float.IsNaN(m)) continue;
            float h = MathF.Max(2, TempFrac(m) * b.Height);
            _p.Fill(new RectangleF(b.X + c * cw, b.Bottom - h, MathF.Max(1, cw - 1.5f), h), TempColor(TempLevel(m, false)));
        }
    }

    // E: the opened card. Its heading is DrawDetail's; the CPU and the GPU as gauges with what they are beside them (or why they are
    // "–"), the hotspot and fan; the last 10 minutes as two lines under them
    void TempsDetail(RectangleF c, PanelData d)
    {
        var t = TempTexts(d);
        var r = d.Temps;
        float cpu = r?.Cpu ?? float.NaN, gpu = r?.Gpu ?? float.NaN, half = c.Width / 2, gs = 140, tx = gs + 6, tw = half - tx - 10;
        uint faint = Theme.Cyber ? CyberHud.Faint : Bento.Faint, text = Theme.Cyber ? CyW : Bento.Text;
        void One(float x, float v, string value, string head, string name, string note, string key)
        {
            var lv = TempLevel(v, false);
            Gauge(new RectangleF(x, c.Y - 6, gs, gs), TempFrac(v), TempColor(lv), key, value, float.IsNaN(v) ? "" : "°C", float.IsNaN(v) ? faint : text);
            CapText(head, x + tx, c.Y + 33, tw, Sub);
            _p.Text(PhotoCut(name ?? "", tw, SmallText), new RectangleF(x + tx, c.Y + 40, tw, 30), text, SmallText, FontWeight.SemiBold);
            _p.Text(PhotoCut(note, tw, SmallText, FontWeight.Normal), new RectangleF(x + tx, c.Y + 70, tw, 26), float.IsNaN(v) ? (Theme.Cyber ? CyR : Bento.Warn) : Sub,
                SmallText);
        }
        One(c.X, cpu, t.Cpu, "CPU", r?.CpuName, t.PeakCpu, "wdtc");
        One(c.X + half, gpu, t.Gpu, "GPU", r?.GpuName, t.PeakGpu, "wdtg");
        _p.Text(PhotoCut(t.Extra, tw, SmallText, FontWeight.Normal), new RectangleF(c.X + half + tx, c.Y + 96, tw, 26), Sub, SmallText);
        // the graph: 20..100 °C, a line every 20, the alert's threshold dashed while it is on; the CPU and the GPU
        var g = new RectangleF(c.X, c.Y + 150, c.Width, c.Height - 150);
        CapText(Upper("Last 10 minutes"), g.X, g.Y + 16, g.Width, Sub);
        var b = new RectangleF(g.X + 34, g.Y + 28, g.Width - 34, g.Height - 30);
        uint grid = Theme.Cyber ? CyberHud.Line : Bento.Track;
        for (int deg = 20; deg <= 100; deg += 20)
        {
            float y = b.Bottom - (deg - 20) / 80f * b.Height;
            _p.Fill(new RectangleF(b.X, y, b.Width, 1), grid);
            TextAt(TempAxis[deg / 20 - 1], g.X, y + 5, 30, Sub, CapSize, FontWeight.SemiBold);
        }
        var a = _cfg.Data.General.Alerts;
        if (a is { Hot: true })
        {
            float y = b.Bottom - Math.Clamp((a.HotLimit - 20) / 80f, 0, 1) * b.Height;
            for (float x = b.X; x < b.Right; x += 10) _p.Fill(new RectangleF(x, y, MathF.Min(5, b.Right - x), 1.5f), Theme.Cyber ? CyR : Bento.Bad);
        }
        uint cc = Theme.Cyber ? CyY : Bento.DataPurple, gc = Theme.Cyber ? CyC : Bento.Accent;
        TempLine(b, r, true, cc);
        TempLine(b, r, false, gc);
        // the legend at the graph's top right
        float lw = _p.MeasureWidth("GPU", CapSize, FontWeight.SemiBold);
        _p.Fill(new RectangleF(g.Right - lw - 14, g.Y + 9, 10, 4), gc);
        TextAt("GPU", g.Right - lw, g.Y + 16, lw + 2, Sub, CapSize, FontWeight.SemiBold);
        float cw2 = _p.MeasureWidth("CPU", CapSize, FontWeight.SemiBold);
        _p.Fill(new RectangleF(g.Right - lw - 30 - cw2 - 14, g.Y + 9, 10, 4), cc);
        TextAt("CPU", g.Right - lw - 30 - cw2, g.Y + 16, cw2 + 2, Sub, CapSize, FontWeight.SemiBold);
    }

    static readonly string[] TempAxis = { "20°", "40°", "60°", "80°", "100°" };

    // one history as a line over b (the newest at the right; a sample not read breaks it)
    void TempLine(RectangleF b, TempReport r, bool cpu, uint c, float width = 2)
    {
        if (r == null || r.Count < 2) return;
        float step = b.Width / (TempReport.History - 1), px = float.NaN, py = 0;
        for (int i = 0; i < r.Count; i++)
        {
            float v = cpu ? r.CpuAgo(i) : r.GpuAgo(i);
            float x = b.Right - (r.Count - 1 - i) * step;
            if (float.IsNaN(v)) { px = float.NaN; continue; }
            float y = b.Bottom - Math.Clamp((v - 20) / 80f, 0, 1) * b.Height;
            if (!float.IsNaN(px)) _p.Line(px, py, x, y, c, width);
            (px, py) = (x, y);
        }
    }

    // ================= the retro styles: System Monitor (95; XP, 7 and the Mac in their skins) =================

    // c: the window's inside. light: 7's glass (white words)
    void Temps95(RectangleF c, PanelData d, WatchWidget w, bool light = false)
    {
        var t = TempTexts(d);
        var r = d.Temps;
        float cpu = r?.Cpu ?? float.NaN, gpu = r?.Gpu ?? float.NaN;
        uint ink = light ? 0xFFFFFFFF : Blk95, grey = light ? 0xFFD6E4F5 : Gry95;
        uint Warm(float v) => TempLevel(v, false) switch { StatLevel.Bad => light ? 0xFFFFA48F : 0xFF800000, StatLevel.Warn => light ? 0xFFFFD866 : 0xFF806000, _ => ink };
        if (w.W == 1 && w.H == 1 || c.Width < 70)
        {
            float vy = MathF.Floor(c.Y + (c.Height - 28) / 2);
            _ui.Text95(Fit(t.GpuDeg, c.Width - 2, 13, true), new RectangleF(c.X, vy, c.Width, 15), Warm(gpu), true, TextAlignment.Center, size: 13);
            _ui.Text95("GPU", new RectangleF(c.X, vy + 15, c.Width, 13), grey, false, TextAlignment.Center);
            return;
        }
        if (c.Height < 56)
        {
            // one row: the CPU and the GPU side by side
            float hw = MathF.Floor(c.Width / 2), vy = MathF.Floor(c.Y + (c.Height - 28) / 2);
            _ui.Text95(Fit(t.CpuDeg, hw - 2, 13, true), new RectangleF(c.X, vy, hw, 15), Warm(cpu), true, TextAlignment.Center, size: 13);
            _ui.Text95("CPU", new RectangleF(c.X, vy + 15, hw, 13), grey, false, TextAlignment.Center);
            _ui.Text95(Fit(t.GpuDeg, hw - 2, 13, true), new RectangleF(c.X + hw, vy, c.Width - hw, 15), Warm(gpu), true, TextAlignment.Center, size: 13);
            _ui.Text95("GPU", new RectangleF(c.X + hw, vy + 15, c.Width - hw, 13), grey, false, TextAlignment.Center);
            return;
        }
        // System Monitor's bar charts: a line each, its bar under it; the hotspot and fan; the GPU's last 10 minutes
        float y = c.Y + 2;
        _ui.Text95(Fit(t.CpuLine, c.Width - 8), new RectangleF(c.X + 4, y, c.Width - 8, 13), Warm(cpu));
        _ui.Progress95(new RectangleF(c.X + 4, y + 15, c.Width - 8, 10), MathF.Max(0, TempFrac(cpu)), Navy95c);
        _ui.Text95(Fit(t.GpuLine, c.Width - 8), new RectangleF(c.X + 4, y + 29, c.Width - 8, 13), Warm(gpu));
        _ui.Progress95(new RectangleF(c.X + 4, y + 44, c.Width - 8, 10), MathF.Max(0, TempFrac(gpu)), Navy95c);
        _ui.Text95(Fit(t.Extra, c.Width - 8), new RectangleF(c.X + 4, y + 58, c.Width - 8, 13), grey);
        var ch = new RectangleF(c.X + 4, y + 74, c.Width - 8, c.Bottom - 2 - (y + 74));
        if (ch.Height >= 12) Chart95(ch, r, false, "");
    }

    // a chart pane: white in its sunken box, a dotted line every 20 °C, the history as a line (95: its own colour)
    void Chart95(RectangleF box, TempReport r, bool cpu, string label)
    {
        _p.Fill(box, Wht95);
        _p.Bevel(box, Painter.Edge.Sunken);
        var b = RectangleF.Inflate(box, -3, -3);
        for (int deg = 40; deg < 100; deg += 20)
        {
            float gy = MathF.Floor(b.Bottom - (deg - 20) / 80f * b.Height);
            for (float x = b.X; x < b.Right; x += 4) _p.Fill(new RectangleF(x, gy, 1, 1), Gry95);
        }
        TempLine(b, r, cpu, cpu ? 0xFF800000 : 0xFF000080, 1);
        if (label.Length > 0) _ui.Text95(Fit(label, b.Width - 4), new RectangleF(b.X + 2, b.Y, b.Width - 4, 13), Blk95);
    }

    // Opened, in the window the style draws around it (title, X, OK): System Monitor - a line chart each for the CPU and the GPU, their
    // name and now / why not at the chart's top, the hotspot, fan and peaks on the status line beside OK
    void Temps95Detail(RectangleF c, PanelData d)
    {
        var t = TempTexts(d);
        var r = d.Temps;
        float sw = c.Right - 75 - 8 - c.X;
        _ui.Text95(Fit(t.Status, sw), new RectangleF(c.X, c.Bottom + 10, sw, 13), Blk95);
        float ph = MathF.Floor((c.Height - 4) / 2);
        Chart95(new RectangleF(c.X, c.Y, c.Width, ph), r, true, t.Head95Cpu);
        Chart95(new RectangleF(c.X, c.Y + ph + 4, c.Width, ph), r, false, t.Head95Gpu);
    }

    // ================= the Terminal: `sensors` =================

    void TTemps(RectangleF c, PanelData d, WatchWidget w)
    {
        var t = TempTexts(d);
        var r = d.Temps;
        float gpu = r?.Gpu ?? float.NaN, cpu = r?.Cpu ?? float.NaN;
        uint Ink(float v) => TempLevel(v, false) == StatLevel.Ok ? T.INK : T.HOT;
        if (w.W == 1 && w.H == 1 || c.Width < 70) { TValue(c, float.IsNaN(gpu) ? "N/A" : t.Gpu + "°C", "edge", Ink(gpu)); return; }
        float x = c.X + 2, y = c.Y + 2, cw = c.Width - 4;
        if (c.Height < 56)
        {
            TCaps(TFit(t.TCpu, cw), x, MathF.Floor(c.Y + (c.Height - 18) / 2), Ink(cpu));
            TCaps(TFit(t.TGpu, cw), x, MathF.Floor(c.Y + (c.Height - 18) / 2) + 11, Ink(gpu));
            return;
        }
        // k10temp, then amdgpu: as `sensors` prints them
        TCaps("k10temp-pci", x, y, T.DIM);
        TCaps(TFit(t.TCpu, cw), x, y + 11, Ink(cpu));
        TCaps("amdgpu-pci", x, y + 27, T.DIM);
        TCaps(TFit(t.TGpu, cw), x, y + 38, Ink(gpu));
        if (y + 49 + 7 <= c.Bottom) TCaps(TFit(t.THot, cw), x, y + 49, TempLevel(r?.Hot ?? float.NaN, true) == StatLevel.Ok ? T.INK : T.HOT);
        if (y + 60 + 7 <= c.Bottom) TCaps(TFit(t.TFan, cw), x, y + 60, T.MID);
    }
}
