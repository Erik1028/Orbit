using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;

namespace Orbit;

// A widget of the custom face, opened: tap a widget's card and it grows over the others (modern: a springy
// zoom over a dimmed watch; 95: the window "zoom" outline, then a 95 dialog) with more to see and to control.
// A tap beside it (modern), its X or OK (95) closes it again. The widget's own small buttons still act directly.
public sealed partial class WristPanel
{
    WatchWidget _open;                  // the widget shown big (null = none)
    bool _closing;
    double _openStart = -10;
    int _calOffset;                     // months browsed away from today in the opened calendar
    readonly List<double> _laps = new();
    const string OpenKey = "wopen";

    // which opened view a widget has (null = it has none: the buttons just act)
    static string DetailOf(string type) => type switch
    {
        "media" => "media",
        "battery" => "battery",
        "time" or "date" or "analog" or "session" => "clock",
        "calendar" => "calendar",
        "timer" => "timer",
        "stopwatch" => "stopwatch",
        "perf" or "graph" or "fps" or "reproj" or "gpu" or "vram" => "perf",
        "cpu" or "ram" => "system",
        "volume" => "volume",
        _ => null,
    };

    bool DetailIs(string kind) => _open != null && DetailOf(_open.Type) == kind;

    void OpenDetail(WatchWidget w)
    {
        _open = w;
        _closing = false;
        _openStart = Clock.Now;
        _calOffset = 0;
        _ui.SpringFrom(OpenKey, 0);
        _dirty = true;
    }

    void CloseDetail()
    {
        if (Theme.Classic) _open = null;      // a 95 dialog just goes away
        else _closing = true;
        _dirty = true;
    }

    // --snapshot: show the first widget of this type opened, at rest
    public bool OpenForTest(string type)
    {
        var w = C.Widgets?.Find(x => x?.Type == type);
        if (w == null) return false;
        _open = w;
        _closing = false;
        _openStart = -10;
        _ui.SpringFrom(OpenKey, 1);
        return true;
    }

    public void CloseForTest() { _open = null; _closing = false; }

    public bool IsOpen => _open != null && !_closing;

    // the card under a widget opens it; registered before the widget's own controls, so those win
    void CardTap(int id, WatchWidget w, WatchWidgets.Kind k, RectangleF r)
    {
        if (k.Button || DetailOf(k.Id) == null) return;
        if (_ui.Clickable(Ui.Id("wco", id), r, out _, out _)) OpenDetail(w);
    }

    static RectangleF Lerp(RectangleF a, RectangleF b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Width + (b.Width - a.Width) * t, a.Height + (b.Height - a.Height) * t);

    // ================= Modern =================

    void DrawDetail(PanelData d, List<WatchWidget> list)
    {
        if (_open == null) return;
        var k = list.Contains(_open) ? WatchWidgets.Find(_open.Type) : null;
        string kind = k == null ? null : DetailOf(k.Id);
        if (kind == null) { _open = null; return; }
        float e = _ui.Spring(OpenKey, _closing ? 0 : 1, 240, 24);
        if (_closing && e < 0.03f) { _open = null; _closing = false; return; }
        float ec = Math.Clamp(e, 0, 1);
        var all = new RectangleF(0, 0, TW, TH);
        _p.Fill(all, Theme.WithAlpha(0xFF000000, 0.62f * ec), 34);
        if (_ui.Clickable("wscrim", all, out _, out _) && !_closing) CloseDetail();        // a tap beside it
        var target = WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, false);
        var r = Lerp(WatchWidgets.Rect(_open, false), target, e);
        _p.Fill(r, Theme.Panel, CardRadius);
        _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.Border, 1.2f, CardRadius);
        _ui.Clickable("wcard", r, out _, out _);           // the card itself is not "beside"
        float ca = Math.Clamp((e - 0.5f) / 0.45f, 0, 1);
        if (ca <= 0.01f) return;
        _halo = Theme.Panel;
        _p.PushClip(r);
        if (ca < 0.999f) _p.PushFade(ca);
        var E = target;
        _p.Text(k.Name, new RectangleF(E.X + 24, E.Y + 16, E.Width - 120, 40), Theme.Text, 24, FontWeight.SemiBold);
        if (Press("wclose", new RectangleF(E.Right - 22 - 46, E.Y + 14, 46, 46), "", null, false, Theme.Accent, !_closing, 18, true))
            CloseDetail();
        var c = new RectangleF(E.X + 24, E.Y + 74, E.Width - 48, E.Height - 96);
        var now = DateTime.Now;
        switch (kind)
        {
            case "media": MediaDetail(c, d); break;
            case "battery": BatteryDetail(c, d); break;
            case "clock": ClockDetail(c, d, now); break;
            case "calendar": CalendarDetail(c, now); break;
            case "timer": TimerDetail(c); break;
            case "stopwatch": StopwatchDetail(c); break;
            case "perf": PerfDetail(c, d); break;
            case "system": SystemDetail(c, d); break;
            case "volume": VolumeDetail(c); break;
        }
        if (ca < 0.999f) _p.PopFade();
        _p.PopClip();
    }

    // a bar with a knob: drag along it (or tap) to set 0..1
    void Slider(string id, RectangleF bar, float value, uint col, bool enabled, Action<float> set)
    {
        _ui.Clickable(id, RectangleF.Inflate(bar, 6, 18), out _, out bool held);
        if (held && enabled) set(Math.Clamp((_ui.Mouse.X - bar.X) / bar.Width, 0, 1));
        float v = Math.Clamp(value, 0, 1), h = bar.Height;
        _p.Fill(bar, Theme.WithAlpha(col, 0.2f), h / 2);
        if (!enabled) return;
        float x = bar.X + bar.Width * v;
        _p.Fill(new RectangleF(bar.X, bar.Y, MathF.Max(h, x - bar.X), h), col, h / 2);
        _p.Circle(x, bar.Y + h / 2, h * 1.5f, _halo);
        _p.Circle(x, bar.Y + h / 2, h, col);
    }

    void MediaDetail(RectangleF c, PanelData d)
    {
        var (has, title, artist, playing) = MediaNow(d);
        Volume.Sample(Clock.Now);
        float s = c.Height;
        var cover = new RectangleF(c.X, c.Y, s, s);
        _p.Fill(cover, Theme.Panel3, 20);
        if (_art != null)
        {
            _p.PushRoundedClip(cover, 20);
            _p.Image(_art, cover);
            _p.PopRoundedClip();
        }
        else _p.Icon("", cover, Sub, s * 0.36f);
        float x = cover.Right + 30, w = c.Right - x;
        if (has)
        {
            _p.Text(Ellipsize(title, w, 30, FontWeight.SemiBold), new RectangleF(x, c.Y + 6, w, 42), Theme.Text, 30, FontWeight.SemiBold);
            _p.Text(Ellipsize(artist ?? "", w, 21, FontWeight.Normal), new RectangleF(x, c.Y + 50, w, 32), Sub, 21);
        }
        else _p.Text("Nothing playing", new RectangleF(x, c.Y + 6, w, 42), Sub, 26);
        string[] ids = { "media.prev", "media.play", "media.next" };
        string[] icons = { "", playing ? "" : "", "" };
        float by = c.Y + 116;
        if (Press("wdm0", new RectangleF(x, by + 10, 64, 64), icons[0], null, false, Theme.Accent, has, 24, true)) OnAction?.Invoke(ids[0]);
        if (Press("wdm1", new RectangleF(x + 78, by, 84, 84), icons[1], null, true, Theme.Accent, has, 32, true)) OnAction?.Invoke(ids[1]);
        if (Press("wdm2", new RectangleF(x + 176, by + 10, 64, 64), icons[2], null, false, Theme.Accent, has, 24, true)) OnAction?.Invoke(ids[2]);
        // the system volume under it
        float lvl = Volume.Level;
        bool ok = lvl >= 0, muted = Volume.Muted;
        float vy = c.Bottom - 56;
        if (Press("wdmv", new RectangleF(x, vy, 52, 52), muted ? "" : "", null, muted, Theme.Panel3, ok, 20, true)) Volume.ToggleMute();
        var bar = new RectangleF(x + 70, vy + 21, w - 70 - 8, 10);
        _p.Text(ok ? (muted ? "Muted" : $"Volume {lvl * 100:0}%") : "No output device", new RectangleF(bar.X, vy - 22, bar.Width, 22), Sub, 15);
        Slider("wdmb", bar, lvl, muted ? Theme.Faint : Theme.Mix(Theme.Accent, Theme.Accent2, 0.5f), ok, Volume.Set);
    }

    void BatteryDetail(RectangleF c, PanelData d)
    {
        var bats = d.Vr.Batteries;
        if (bats.Count == 0)
        {
            _p.Text(d.VrConnected ? "No battery information from SteamVR" : "SteamVR is not running", c, Sub, 20, FontWeight.Normal, TextAlignment.Center);
            return;
        }
        int n = Math.Min(bats.Count, 6), cols = n <= 3 ? n : 3, rows = n <= 3 ? 1 : 2;
        float cw = c.Width / cols, ch = c.Height / rows;
        for (int i = 0; i < n; i++)
        {
            var b = bats[i];
            var cell = new RectangleF(c.X + i % cols * cw, c.Y + i / cols * ch, cw, ch);
            float rad = MathF.Min(cw, ch - 58) / 2 - 8, cx = cell.X + cw / 2, cy = cell.Y + 6 + rad;
            uint col = BatteryColor(b.pct, b.charging);
            Orbit(cx, cy, rad, b.pct, col, Ui.Id("wdb", i), MathF.Max(4, rad * 0.09f));
            _p.Text(b.label, new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), Theme.Text, FitSize(b.label, rad * 1.3f, 26, FontWeight.SemiBold, null),
                FontWeight.SemiBold, TextAlignment.Center);
            _p.Text($"{b.pct * 100:0}%", new RectangleF(cell.X, cy + rad + 6, cw, 28), Theme.Text, 22, FontWeight.SemiBold, TextAlignment.Center);
            string state = b.charging ? "charging" : b.pct <= 0.2f ? "low" : "";
            if (state != "")
                _p.Text(state, new RectangleF(cell.X, cy + rad + 32, cw, 20), b.charging ? Theme.Good : Theme.Bad, 15, FontWeight.SemiBold, TextAlignment.Center);
        }
    }

    void ClockDetail(RectangleF c, PanelData d, DateTime now)
    {
        AnalogOrbit(new RectangleF(c.X - 8, c.Y - 8, c.Height + 16, c.Height + 16), now, Theme.Accent);
        float x = c.X + c.Height + 30, w = c.Right - x;
        bool h24 = _cfg.Data.Appearance.Clock24h;
        var culture = CultureInfo.CurrentCulture;
        string t = now.ToString(h24 ? "HH:mm:ss" : "h:mm:ss", culture);
        float size = FitSize(t, w - (h24 ? 0 : 50), 66, FontWeight.SemiBold, Theme.Display);
        RollText("wdct", t, new RectangleF(x, c.Y, w, 84), Theme.Text, size, FontWeight.SemiBold, Theme.Display, TextAlignment.Leading);
        if (!h24)
            _p.Text(now.Hour < 12 ? "AM" : "PM", new RectangleF(x + _p.MeasureWidth(t, size, FontWeight.SemiBold, Theme.Display) + 8, c.Y + 44, 60, 30), Sub, 20,
                FontWeight.SemiBold);
        _p.Text(Capital(DateText(now)), new RectangleF(x, c.Y + 92, w, 32), Theme.Text, 22, FontWeight.SemiBold);
        _p.Text($"Week {ISOWeek.GetWeekOfYear(now)} · day {now.DayOfYear} of {(DateTime.IsLeapYear(now.Year) ? 366 : 365)}",
            new RectangleF(x, c.Y + 126, w, 26), Sub, 17);
        float y = c.Y + 176;
        if (d.VrSince is { } since)
        {
            _p.Text("In VR", new RectangleF(x, y, w, 22), Sub, 15, FontWeight.SemiBold);
            _p.Text($"{SessionText(d)}  ·  since {since.ToString(h24 ? "HH:mm" : "h:mm tt", culture)}", new RectangleF(x, y + 22, w, 32),
                Theme.Text, 22, FontWeight.SemiBold);
            y += 66;
        }
        if (_timer is TimerState.Running or TimerState.Paused)
        {
            _p.Text("Timer", new RectangleF(x, y, w, 22), Sub, 15, FontWeight.SemiBold);
            _p.Text($"{Clock2(TimerLeft(Clock.Now))} left{(_timer == TimerState.Paused ? " (paused)" : "")}", new RectangleF(x, y + 22, w, 32),
                Theme.Accent, 22, FontWeight.SemiBold);
        }
    }

    void CalendarDetail(RectangleF c, DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        var month = new DateTime(now.Year, now.Month, 1).AddMonths(_calOffset);
        _p.Text(Capital(month.ToString("MMMM yyyy", culture)), new RectangleF(c.X, c.Y - 4, c.Width - 130, 36), Theme.Text, 24, FontWeight.SemiBold);
        if (Press("wdcp", new RectangleF(c.Right - 112, c.Y - 6, 44, 44), "", null, false, Theme.Accent, true, 18, true)) { _calOffset--; _dirty = true; }
        if (Press("wdcn", new RectangleF(c.Right - 58, c.Y - 6, 44, 44), "", null, false, Theme.Accent, true, 18, true)) { _calOffset++; _dirty = true; }
        var g = new RectangleF(c.X, c.Y + 46, c.Width, c.Height - 46);
        int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        float cw = g.Width / 7, rh = g.Height / 7;
        for (int col = 0; col < 7; col++)
        {
            var dow = (DayOfWeek)((first + col) % 7);
            _p.Text(Capital(culture.DateTimeFormat.GetAbbreviatedDayName(dow)), new RectangleF(g.X + col * cw, g.Y, cw, rh), Sub, 16, FontWeight.SemiBold,
                TextAlignment.Center);
        }
        int offset = ((int)month.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(month.Year, month.Month);
        bool thisMonth = month.Year == now.Year && month.Month == now.Month;
        for (int dd = 1; dd <= days; dd++)
        {
            int cell = offset + dd - 1;
            var cr = new RectangleF(g.X + cell % 7 * cw, g.Y + (cell / 7 + 1) * rh, cw, rh);
            var dow = (DayOfWeek)((first + cell % 7) % 7);
            bool today = thisMonth && dd == now.Day, weekend = dow is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (today)
            {
                float d2 = MathF.Min(cw, rh) - 4;
                _p.Fill(new RectangleF(cr.X + (cw - d2) / 2, cr.Y + (rh - d2) / 2, d2, d2), Theme.Accent, d2 / 2);
            }
            _p.Text(dd.ToString(), cr, today ? OnColor(Theme.Accent) : weekend ? Sub : Theme.Text, 19, today ? FontWeight.Bold : FontWeight.Normal,
                TextAlignment.Center);
        }
    }

    void TimerPreset(int minutes)
    {
        _timerTotal = minutes * 60;
        _timerEnd = Clock.Now + _timerTotal;
        _timer = TimerState.Running;
        _dirty = true;
    }

    void TimerDetail(RectangleF c)
    {
        double left = TimerLeft(Clock.Now);
        float frac = _timer == TimerState.Idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
        uint tint = _timer == TimerState.Done ? Theme.Warn : Theme.Accent;
        float rad = c.Height / 2 - 8, cx = c.X + rad + 8, cy = c.Y + c.Height / 2;
        Orbit(cx, cy, rad, frac, tint, "wdtr", MathF.Max(5, rad * 0.06f));
        string txt = _timer == TimerState.Done ? "Done" : Clock2(left);
        RollText("wdtx", txt, new RectangleF(cx - rad, cy - rad * 0.36f, rad * 2, rad * 0.5f), Theme.Text,
            FitSize(txt, rad * 1.4f, 60, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display);
        string state = _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", TimerState.Done => "time is up", _ => $"{_timerTotal / 60} min" };
        _p.Text(state, new RectangleF(cx - rad, cy + rad * 0.16f, rad * 2, 30), Sub, 18, FontWeight.Normal, TextAlignment.Center);
        // quick starts, then - / start-pause / + / reset
        float x = cx + rad + 36, w = c.Right - x;
        _p.Text("Start a timer", new RectangleF(x, c.Y, w, 24), Sub, 15, FontWeight.SemiBold);
        int[] presets = { 1, 3, 5, 10, 15, 30 };
        float pw = (w - 2 * 10) / 3;
        for (int i = 0; i < presets.Length; i++)
            if (Press(Ui.Id("wdtp", i), new RectangleF(x + i % 3 * (pw + 10), c.Y + 30 + i / 3 * 56, pw, 46), null, $"{presets[i]} min", false, Theme.Accent))
                TimerPreset(presets[i]);
        // start-pause first and biggest, then reset, -1 min, +1 min - all within the column
        bool idle = _timer == TimerState.Idle;
        float big = MathF.Min(64, (w - 3 * 8) * 0.31f), small = MathF.Min(50, (w - 3 * 8 - big) / 3), by = c.Bottom - big - 4;
        float sy = by + (big - small) / 2;
        if (Press("wdtg", new RectangleF(x, by, big, big), _timer == TimerState.Running ? "" : "", null, true, tint, true, 26, true))
            TimerTap();
        if (Press("wdtr0", new RectangleF(x + big + 8, sy, small, small), "", null, false, Theme.Accent, !idle, 18, true)) TimerReset();
        if (Press("wdtm", new RectangleF(x + big + small + 16, sy, small, small), "", null, false, Theme.Accent, true, 18, true)) TimerAdjust(-1);
        if (Press("wdtq", new RectangleF(x + big + 2 * small + 24, sy, small, small), "", null, false, Theme.Accent, true, 18, true)) TimerAdjust(1);
    }

    void StopwatchDetail(RectangleF c)
    {
        double now = Clock.Now, t = StopwatchTime(now);
        string txt = StopwatchText(t, true);
        _p.Text(txt, new RectangleF(c.X, c.Y - 6, c.Width - 190, 90), _swRunning ? Theme.Accent : Theme.Text,
            FitSize(txt, c.Width - 200, 72, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
        float bs = 72, bx = c.Right - 2 * bs - 14;
        bool lap = _swRunning;
        if (Press("wdsl", new RectangleF(bx, c.Y + 4, bs, bs), lap ? "" : "", null, false, Theme.Accent, _swRunning || t > 0, 24, true))
        {
            if (lap) _laps.Add(t);
            else { StopwatchReset(); _laps.Clear(); }
        }
        if (Press("wdsp", new RectangleF(bx + bs + 14, c.Y + 4, bs, bs), _swRunning ? "" : "", null, true, Theme.Accent, true, 28, true))
            StopwatchTap();
        // the laps, newest first
        float y = c.Y + 104;
        if (_laps.Count == 0)
            _p.Text(_swRunning ? "Tap the flag for a lap time" : "Laps appear here", new RectangleF(c.X, y, c.Width, 30), Sub, 18);
        for (int i = _laps.Count - 1, row = 0; i >= 0 && row < 6; i--, row++)
        {
            double split = _laps[i] - (i > 0 ? _laps[i - 1] : 0);
            var rr = new RectangleF(c.X, y + row * 38, c.Width, 34);
            _p.Fill(new RectangleF(rr.X, rr.Bottom, rr.Width, 1), Theme.WithAlpha(Theme.Border, 0.8f));
            _p.Text($"Lap {i + 1}", rr, Sub, 18);
            _p.Text(StopwatchText(split, true), new RectangleF(rr.X, rr.Y, rr.Width / 2, rr.Height), Theme.Text, 20, FontWeight.SemiBold, TextAlignment.Trailing);
            _p.Text(StopwatchText(_laps[i], true), rr, Sub, 18, FontWeight.Normal, TextAlignment.Trailing);
        }
    }

    void PerfDetail(RectangleF c, PanelData d)
    {
        float gw = c.Width / 4;
        for (int j = 0; j < 4; j++)
        {
            var (f, val, unit, cap, tint) = StatInfo(PerfIds[j], d);
            Gauge(new RectangleF(c.X + j * gw, c.Y - 6, gw, 128), f, tint, Ui.Id("wdp", j), val, unit);
            _p.Text(cap, new RectangleF(c.X + j * gw, c.Y + 120, gw, 22), Sub, 14, FontWeight.Normal, TextAlignment.Center);
        }
        var g = new RectangleF(c.X - 10, c.Y + 150, c.Width + 20, c.Height - 150);
        GraphOrbit(d.Vr, g, Theme.Accent);
        // the frames behind the graph: average, worst, how many dropped
        var v = d.Vr;
        int n = v.FrameCount, dropped = 0;
        float sum = 0, worst = 0;
        for (int i = 0; i < n; i++)
        {
            int idx = (v.FrameHead - n + i + VrStats.FrameHistory) % VrStats.FrameHistory;
            float ms = v.FrameMs[idx];
            sum += ms;
            worst = MathF.Max(worst, ms);
            if (v.FrameFlag[idx] == 2) dropped++;
        }
        if (n > 0)
            _p.Text($"avg {sum / n:0.0} ms · worst {worst:0.0} ms · {dropped} dropped", new RectangleF(g.X + 16, g.Y + 8, g.Width - 32, 22), Sub, 14,
                FontWeight.Normal, TextAlignment.Trailing);
    }

    void SystemDetail(RectangleF c, PanelData d)
    {
        var (cf, cv, _, _, ct) = StatInfo("cpu", d);
        var (rf, rv, ru, _, rt) = StatInfo("ram", d);
        float half = c.Width / 2;
        Gauge(new RectangleF(c.X, c.Y - 10, 150, 150), cf, ct, "wdsc", cv, "");
        _p.Text("CPU", new RectangleF(c.X + 160, c.Y + 20, half - 170, 26), Sub, 16, FontWeight.SemiBold);
        _p.Text(cv, new RectangleF(c.X + 160, c.Y + 44, half - 170, 50), Theme.Text, 40, FontWeight.SemiBold, TextAlignment.Leading,
            ParagraphAlignment.Center, Theme.Display);
        Gauge(new RectangleF(c.X + half, c.Y - 10, 150, 150), rf, rt, "wdsr", rv, ru);
        _p.Text("Memory", new RectangleF(c.X + half + 160, c.Y + 20, half - 170, 26), Sub, 16, FontWeight.SemiBold);
        string mem = Stats.RamTotalGB > 0 ? $"{Stats.RamUsedGB:0.0} of {Stats.RamTotalGB:0} GB" : "–";
        _p.Text(mem, new RectangleF(c.X + half + 160, c.Y + 44, half - 170, 50), Theme.Text,
            FitSize(mem, half - 172, 26, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
        // the last minute of CPU load
        var g = new RectangleF(c.X, c.Y + 160, c.Width, c.Height - 160);
        _p.Text("CPU, last minute", new RectangleF(g.X, g.Y, g.Width, 22), Sub, 14, FontWeight.SemiBold);
        var b = new RectangleF(g.X, g.Y + 28, g.Width, g.Height - 28);
        _p.Fill(new RectangleF(b.X, b.Bottom, b.Width, 1), Theme.WithAlpha(Theme.Border, 0.9f));
        float step = b.Width / SystemStats.History;
        for (int i = 0; i < Stats.CpuCount; i++)
        {
            int idx = (Stats.CpuHead - Stats.CpuCount + i + SystemStats.History) % SystemStats.History;
            float v = Stats.CpuHistory[idx], h = MathF.Max(2, v * b.Height);
            float x = b.Right - (Stats.CpuCount - i) * step;
            _p.Fill(new RectangleF(x + 1, b.Bottom - h, MathF.Max(2, step - 3), h), v > 0.9f ? Theme.Bad : v > 0.75f ? Theme.Warn : Theme.Accent, 2);
        }
    }

    void VolumeDetail(RectangleF c)
    {
        Volume.Sample(Clock.Now);
        float lvl = Volume.Level;
        bool ok = lvl >= 0, muted = Volume.Muted;
        uint col = muted ? Theme.Faint : Theme.Mix(Theme.Accent, Theme.Accent2, 0.5f);
        Gauge(new RectangleF(c.X - 8, c.Y - 8, c.Height + 16, c.Height + 16), ok ? lvl : -1, col, "wdvg", ok ? (muted ? "Muted" : $"{lvl * 100:0}%") : "–",
            "volume");
        float x = c.X + c.Height + 36, w = c.Right - x;
        _p.Text("System volume", new RectangleF(x, c.Y + 10, w, 26), Sub, 16, FontWeight.SemiBold);
        Slider("wdvs", new RectangleF(x + 8, c.Y + 70, w - 16, 12), lvl, col, ok, Volume.Set);
        if (Press("wdvm", new RectangleF(x, c.Y + 120, 64, 64), "", null, false, Theme.Accent, ok, 22, true)) Volume.Step(-0.05f);
        if (Press("wdvp", new RectangleF(x + 78, c.Y + 120, 64, 64), "", null, false, Theme.Accent, ok, 22, true)) Volume.Step(0.05f);
        if (Press("wdvx", new RectangleF(x, c.Bottom - 64, MathF.Min(w, 220), 56), muted ? "" : "", muted ? "Unmute" : "Mute", muted,
                Theme.Accent, ok, 22)) Volume.ToggleMute();
    }

    // ================= Windows 95 =================

    void DrawDetail95(PanelData d, List<WatchWidget> list)
    {
        if (_open == null) return;
        var k = list.Contains(_open) ? WatchWidgets.Find(_open.Type) : null;
        string kind = k == null ? null : DetailOf(k.Id);
        if (kind == null) { _open = null; return; }
        var win = new RectangleF(8, 6, TW95 - 16, TH95 - 12);
        double t = (Clock.Now - _openStart) / 0.22;
        if (t < 1)
        {
            // the 1995 window zoom: an outline growing from the widget to the window
            var z = Lerp(WatchWidgets.Rect(_open, true), win, (float)t);
            _p.Stroke(new RectangleF(MathF.Round(z.X) + 1, MathF.Round(z.Y) + 1, MathF.Round(z.Width) - 2, MathF.Round(z.Height) - 2), 0xFF808080, 2);
            _ui.Animating = true;
            return;
        }
        _p.Fill(win, Theme.W95Face);
        _p.Bevel(win, Painter.Edge.Window);
        _ui.Caption95(new RectangleF(win.X + 3, win.Y + 3, win.Width - 6, 18), k.Title95);
        if (_ui.CaptionClose95("wd95x", win.Right - 5 - 16, win.Y + 5)) { CloseDetail(); return; }
        if (_ui.Button95("wd95ok", new RectangleF(win.Right - 8 - 75, win.Bottom - 8 - 23, 75, 23), "OK", true, true)) { CloseDetail(); return; }
        var c = new RectangleF(win.X + 8, win.Y + 27, win.Width - 16, win.Height - 27 - 36);
        var now = DateTime.Now;
        switch (kind)
        {
            case "media": Media95Detail(c, d); break;
            case "battery": Battery95Detail(c, d); break;
            case "clock": Clock95Detail(c, now); break;
            case "calendar": Calendar95Detail(c, now); break;
            case "timer": Timer95Detail(c); break;
            case "stopwatch": Stopwatch95Detail(c); break;
            case "perf":
                Graph95(d.Vr, new RectangleF(c.X, c.Y, c.Width, c.Height - 30));
                Status95(d, new RectangleF(c.X, c.Bottom - 26, c.Width, 24));
                break;
            case "system": System95Detail(c); break;
            case "volume": Volume95Detail(c); break;
        }
    }

    void Media95Detail(RectangleF c, PanelData d)
    {
        var (has, title, artist, playing) = MediaNow(d);
        Volume.Sample(Clock.Now);
        var box = new RectangleF(c.X, c.Y, 60, 60);
        var inner = RectangleF.Inflate(box, -2, -2);
        _p.Fill(inner, Wht95);
        if (_art != null) _p.Image(_art, inner);
        else W95Icons.Draw(_p, W95Icons.Id.Note, inner.X + 20, inner.Y + 20);
        _p.Bevel(box, Painter.Edge.Sunken);
        var lcd = new RectangleF(box.Right + 6, c.Y, c.Right - box.Right - 6, 34);
        _p.Fill(lcd, Blk95);
        _p.Bevel(lcd, Painter.Edge.Sunken);
        _ui.Text95(Fit(has ? title : "Nothing playing", lcd.Width - 10), new RectangleF(lcd.X + 5, lcd.Y + 3, lcd.Width - 8, 14), has ? 0xFF00FF00 : 0xFF008000);
        if (has) _ui.Text95(Fit(artist ?? "", lcd.Width - 10), new RectangleF(lcd.X + 5, lcd.Y + 17, lcd.Width - 8, 14), 0xFF00C000);
        string[] ids = { "media.prev", "media.play", "media.next" };
        string[][] glyphs = { PrevGlyph, playing ? PauseGlyph : PlayGlyph, NextGlyph };
        for (int j = 0; j < 3; j++)
            if (_ui.GlyphButton95(Ui.Id("wd95m", j), new RectangleF(lcd.X + j * 32, lcd.Bottom + 6, 30, 20), glyphs[j], has)) OnAction?.Invoke(ids[j]);
        Volume95Row(new RectangleF(c.X, c.Bottom - 34, c.Width, 34));
    }

    // Volume Control's row: "Volume:", a trackbar, Mute
    void Volume95Row(RectangleF r)
    {
        float lvl = Volume.Level;
        bool ok = lvl >= 0;
        _ui.Text95("Volume:", new RectangleF(r.X, r.Y + 3, 50, 15), Blk95, false, TextAlignment.Leading, ok);
        float v = ok ? lvl : 0;
        if (_ui.Track95("wd95vt", new RectangleF(r.X + 50, r.Y, r.Width - 50 - 60, 30), ref v, 0, 1, ok && !Volume.Muted, 11)) Volume.Set(v);
        bool mute = Volume.Muted;
        if (_ui.Check95("wd95vk", r.Right - 54, r.Y + 3, "Mute", ref mute, ok)) Volume.ToggleMute();
    }

    void Battery95Detail(RectangleF c, PanelData d)
    {
        var bats = d.Vr.Batteries;
        if (bats.Count == 0) { _ui.Text95(d.VrConnected ? "No battery information." : "SteamVR is not running.", c, Blk95, false, TextAlignment.Center); return; }
        for (int i = 0; i < Math.Min(bats.Count, 7); i++)
        {
            var b = bats[i];
            float y = c.Y + i * 19;
            _ui.Text95(b.label, new RectangleF(c.X, y, 50, 15));
            _ui.Progress95(new RectangleF(c.X + 52, y + 1, c.Width - 52 - 110, 13), b.pct, b.pct > 0.2f ? Theme.W95Navy : 0xFF800000);
            _ui.Text95($"{b.pct * 100:0}%", new RectangleF(c.Right - 104, y, 34, 15), Blk95, false, TextAlignment.Trailing);
            string state = b.charging ? "Charging" : b.pct <= 0.2f ? "Low" : "";
            _ui.Text95(state, new RectangleF(c.Right - 64, y, 64, 15), b.charging ? Blk95 : 0xFF800000);
        }
    }

    // Date/Time Properties: the month on the left, the clock on the right
    void Clock95Detail(RectangleF c, DateTime now)
    {
        var left = new RectangleF(c.X, c.Y, 160, c.Height);
        _ui.Group95(left, "Date");
        Month95(new RectangleF(left.X + 8, left.Y + 18, left.Width - 16, left.Height - 26), now, now);
        var right = new RectangleF(left.Right + 8, c.Y, c.Right - left.Right - 8, c.Height);
        _ui.Group95(right, "Time");
        float rad = MathF.Floor(MathF.Min(right.Width - 20, right.Height - 56) / 2);
        Dial95(now, MathF.Floor(right.X + right.Width / 2), MathF.Floor(right.Y + 18 + rad), rad);
        string time = now.ToString(_cfg.Data.Appearance.Clock24h ? "H:mm:ss" : "h:mm:ss tt", CultureInfo.CurrentCulture);
        _ui.Edit95(new RectangleF(right.X + 10, right.Bottom - 30, right.Width - 20, 21), time);
    }

    // a month in a white box, today in navy
    void Month95(RectangleF box, DateTime month, DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        var first1 = new DateTime(month.Year, month.Month, 1);
        _ui.Text95(Capital(first1.ToString("MMMM yyyy", culture)), new RectangleF(box.X, box.Y, box.Width, 15), Blk95, true, TextAlignment.Center);
        var g = new RectangleF(box.X, box.Y + 18, box.Width, box.Height - 18);
        _p.Fill(g, Wht95);
        _p.Bevel(g, Painter.Edge.Sunken);
        var gi = RectangleF.Inflate(g, -3, -3);
        int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        float cw = gi.Width / 7, rh = MathF.Floor(gi.Height / 7);
        for (int col = 0; col < 7; col++)
        {
            string n = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)((first + col) % 7));
            _ui.Text95(n.Substring(0, 1).ToUpper(culture), new RectangleF(gi.X + col * cw, gi.Y, cw, rh), Blk95, true, TextAlignment.Center);
        }
        int offset = ((int)first1.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(month.Year, month.Month);
        for (int dd = 1; dd <= days; dd++)
        {
            int cell = offset + dd - 1;
            var cr = new RectangleF(MathF.Floor(gi.X + cell % 7 * cw), gi.Y + (cell / 7 + 1) * rh, MathF.Ceiling(cw), rh);
            bool today = month.Year == now.Year && month.Month == now.Month && dd == now.Day;
            if (today) _p.Fill(cr, Navy95c);
            _ui.Text95(dd.ToString(), cr, today ? Wht95 : Blk95, false, TextAlignment.Center);
        }
    }

    void Calendar95Detail(RectangleF c, DateTime now)
    {
        var month = new DateTime(now.Year, now.Month, 1).AddMonths(_calOffset);
        if (_ui.Button95("wd95cp", new RectangleF(c.X, c.Y, 24, 21), "<")) { _calOffset--; _dirty = true; }
        if (_ui.Button95("wd95cn", new RectangleF(c.Right - 24, c.Y, 24, 21), ">")) { _calOffset++; _dirty = true; }
        Month95(new RectangleF(c.X + 30, c.Y + 3, c.Width - 60, c.Height - 3), month, now);
    }

    void Timer95Detail(RectangleF c)
    {
        double left = TimerLeft(Clock.Now);
        string txt = _timer == TimerState.Done ? "Time is up!" : Clock2(left);
        _ui.Text95(txt, new RectangleF(c.X, c.Y, c.Width, 26), _timer == TimerState.Done ? 0xFF800000 : Blk95, true, TextAlignment.Center, size: 22);
        float frac = _timer == TimerState.Idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
        _ui.Progress95(new RectangleF(c.X, c.Y + 32, c.Width, 16), frac, Navy95c);
        _ui.Text95("Start:", new RectangleF(c.X, c.Y + 58, 40, 15));
        int[] presets = { 1, 3, 5, 10, 15, 30 };
        for (int i = 0; i < presets.Length; i++)
            if (_ui.Button95(Ui.Id("wd95tp", i), new RectangleF(c.X + 40 + i * 41, c.Y + 54, 38, 22), $"{presets[i]}m")) TimerPreset(presets[i]);
        float by = c.Bottom - 24;
        if (_ui.Button95("wd95tg", new RectangleF(c.X, by, 70, 23), _timer == TimerState.Running ? "&Pause" : "&Start")) TimerTap();
        if (_ui.Button95("wd95tr", new RectangleF(c.X + 76, by, 60, 23), "&Reset", _timer != TimerState.Idle)) TimerReset();
        if (_ui.Button95("wd95tm", new RectangleF(c.X + 142, by, 40, 23), "-1")) TimerAdjust(-1);
        if (_ui.Button95("wd95tq", new RectangleF(c.X + 186, by, 40, 23), "+1")) TimerAdjust(1);
    }

    void Stopwatch95Detail(RectangleF c)
    {
        double t = StopwatchTime(Clock.Now);
        _ui.Text95(StopwatchText(t, true), new RectangleF(c.X, c.Y, 140, 26), Blk95, true, TextAlignment.Leading, size: 22);
        if (_ui.Button95("wd95sp", new RectangleF(c.Right - 3 * 64, c.Y + 1, 60, 23), _swRunning ? "S&top" : "&Start", true, true)) StopwatchTap();
        if (_ui.Button95("wd95sl", new RectangleF(c.Right - 2 * 64, c.Y + 1, 60, 23), "&Lap", _swRunning)) _laps.Add(t);
        if (_ui.Button95("wd95sr", new RectangleF(c.Right - 64, c.Y + 1, 60, 23), "&Reset", !_swRunning && t > 0)) { StopwatchReset(); _laps.Clear(); }
        int n = _laps.Count;
        _ui.List95("wd95laps", new RectangleF(c.X, c.Y + 32, c.Width, c.Height - 32), n, -1, (i, r, on) =>
        {
            int li = n - 1 - i;
            double split = _laps[li] - (li > 0 ? _laps[li - 1] : 0);
            _ui.Text95($"Lap {li + 1}", new RectangleF(r.X + 4, r.Y, 60, r.Height));
            _ui.Text95(StopwatchText(split, true), new RectangleF(r.X + 70, r.Y, 70, r.Height));
            _ui.Text95(StopwatchText(_laps[li], true), new RectangleF(r.Right - 80, r.Y, 76, r.Height), Gry95, false, TextAlignment.Trailing);
        });
    }

    // System Monitor: the CPU over the last minute, and the memory
    void System95Detail(RectangleF c)
    {
        var g = new RectangleF(c.X, c.Y, c.Width, c.Height - 44);
        _p.Fill(g, Blk95);
        _p.Bevel(g, Painter.Edge.Sunken);
        var gi = RectangleF.Inflate(g, -3, -3);
        for (float gy = gi.Y + 10; gy < gi.Bottom; gy += 10) _p.Fill(new RectangleF(gi.X, gy, gi.Width, 1), 0xFF008000);
        float step = gi.Width / SystemStats.History;
        for (int i = 0; i < Stats.CpuCount; i++)
        {
            int idx = (Stats.CpuHead - Stats.CpuCount + i + SystemStats.History) % SystemStats.History;
            float h = MathF.Max(1, MathF.Round(Stats.CpuHistory[idx] * gi.Height));
            _p.Fill(new RectangleF(MathF.Floor(gi.Right - (Stats.CpuCount - i) * step), gi.Bottom - h, MathF.Max(1, MathF.Ceiling(step) - 1), h), 0xFF00FF00);
        }
        _ui.Text95(Stats.Cpu >= 0 ? $"CPU usage: {Stats.Cpu * 100:0}%" : "CPU usage: --", new RectangleF(gi.X + 3, gi.Y + 1, 120, 13), 0xFF00FF00);
        float frac = Stats.RamTotalGB > 0 ? Stats.RamUsedGB / Stats.RamTotalGB : 0;
        _ui.Text95(Stats.RamTotalGB > 0 ? $"Memory: {Stats.RamUsedGB:0.0} of {Stats.RamTotalGB:0} GB" : "Memory: --", new RectangleF(c.X, c.Bottom - 38, c.Width, 15));
        _ui.Progress95(new RectangleF(c.X, c.Bottom - 20, c.Width, 16), frac, Navy95c);
    }

    void Volume95Detail(RectangleF c)
    {
        Volume.Sample(Clock.Now);
        float lvl = Volume.Level;
        _ui.Text95(lvl >= 0 ? (Volume.Muted ? "Muted" : $"{lvl * 100:0}%") : "No output device", new RectangleF(c.X, c.Y, c.Width, 26), Blk95, true,
            TextAlignment.Center, size: 22);
        Volume95Row(new RectangleF(c.X, c.Y + 40, c.Width, 34));
        if (_ui.Button95("wd95vm", new RectangleF(c.X, c.Bottom - 24, 60, 23), "- 5%", lvl >= 0)) Volume.Step(-0.05f);
        if (_ui.Button95("wd95vp", new RectangleF(c.X + 66, c.Bottom - 24, 60, 23), "+ 5%", lvl >= 0)) Volume.Step(0.05f);
    }
}
