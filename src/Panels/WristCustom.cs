using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;

namespace Orbit;

// The custom face: the widgets the user put on the 6 x 4 grid (WatchWidgets), in both styles.
// Modern: Orbit's own look - the accent and the logo's indigo, values as orbits with a satellite - and motion
// (widgets cascade in when you look at the watch, spring into a new place, digits roll, orbits fill).
// Windows 95: little 95 windows on a teal desktop (or windows only), no motion - 1995 had none.
public sealed partial class WristPanel
{
    bool _layoutChecked, _layoutSeen;
    public readonly SystemStats Stats = new();
    public readonly SystemVolume Volume = new();

    List<WatchWidget> Layout
    {
        get
        {
            if (C.Widgets == null) { C.Widgets = WatchWidgets.Seed(C, "full"); _cfg.MarkDirty(); }
            else if (!_layoutChecked) C.Widgets = WatchWidgets.Clean(C.Widgets);     // a hand-edited config
            _layoutChecked = true;
            return C.Widgets;
        }
    }

    bool HasWidget(string type)
    {
        if (C.Widgets == null) return false;
        foreach (var w in C.Widgets) if (w?.Type == type) return true;      // a hand-edited list can hold a null
        return false;
    }

    // the frame-time samples are only collected while something shows them
    public bool WantsFrames => C.Face == "custom" ? HasWidget("graph") || DetailIs("perf") : C.ShowPerformance && C.PerfGraph && C.Face is "full" or "analog";

    // stable ids for a widget's animations (the list order changes while editing)
    readonly Dictionary<WatchWidget, int> _wid = new();
    int _widNext;
    int Wid(WatchWidget w)
    {
        if (!_wid.TryGetValue(w, out int id)) _wid[w] = id = ++_widNext;
        return id;
    }

    float FitSize(string s, float maxW, float maxSize, FontWeight w, string family)
    {
        if (string.IsNullOrEmpty(s)) return maxSize;
        float w100 = _p.MeasureWidth(s, 100, w, family);
        float size = w100 <= 0 ? maxSize : MathF.Max(8, MathF.Min(maxSize, 100 * maxW / w100));
        return MathF.Floor(size * 2) / 2;          // half-pixel steps: a handful of text formats, not one per size
    }

    // "A long song ti…" (never splits an emoji)
    string Ellipsize(string s, float maxW, float size, FontWeight wt, string family = null)
    {
        if (string.IsNullOrEmpty(s) || _p.MeasureWidth(s, size, wt, family) <= maxW) return s;
        int lo = 0, hi = s.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_p.MeasureWidth(s[..mid].TrimEnd() + "…", size, wt, family) <= maxW) lo = mid; else hi = mid - 1;
        }
        if (lo > 0 && char.IsHighSurrogate(s[lo - 1])) lo--;
        return s[..lo].TrimEnd() + "…";
    }

    (bool has, string title, string artist, bool playing) MediaNow(PanelData d)
    {
        bool has; string title, artist; bool playing; uint[] art = null; int ver = -2;
        if (d.FakeMedia is { } f) (has, title, artist, playing) = f;
        else if (d.Media != null) (has, title, artist, playing, art, ver) = d.Media.Snapshot();
        else { has = false; title = artist = null; playing = false; }
        if (ver != _artVersion)
        {
            _artVersion = ver;
            // the version also moves on play / pause and title changes: only a new picture is rebuilt (and fades in)
            if (!ReferenceEquals(art, _artSource) || (art == null) != (_art == null))
            {
                _artSource = art;
                _art?.Dispose();
                _art = art != null ? _p.CreateBitmap(art, MediaInfo.ArtSize, MediaInfo.ArtSize) : null;
                _artChanged = Clock.Now;
            }
        }
        return (has, title, artist, playing);
    }
    double _artChanged = -10;
    uint[] _artSource;

    static bool IsOn(string id, PanelData d) =>
        (id == "keyboard" && d.KeyboardVisible) || (id == "desktops" && d.DesktopsVisible) || (id == "night" && d.NightMode);

    void SampleSources()
    {
        double now = Clock.Now;
        if (HasWidget("cpu") || HasWidget("ram")) Stats.Sample(now);
        if (HasWidget("volume")) Volume.Sample(now);
    }

    // ---- timer and stopwatch: one of each, shared by their widgets; they keep running while the watch is hidden ----

    enum TimerState { Idle, Running, Paused, Done }
    TimerState _timer;
    int _timerTotal = 300;                 // seconds
    double _timerEnd, _timerLeftPaused;
    double _swStart, _swAccum;
    bool _swRunning;

    double TimerLeft(double now) => _timer switch
    {
        TimerState.Running => Math.Max(0, _timerEnd - now),
        TimerState.Paused => _timerLeftPaused,
        TimerState.Done => 0,
        _ => _timerTotal,
    };

    void TimerTap()
    {
        double now = Clock.Now;
        switch (_timer)
        {
            case TimerState.Idle: _timerEnd = now + _timerTotal; _timer = TimerState.Running; break;
            case TimerState.Running: _timerLeftPaused = Math.Max(0, _timerEnd - now); _timer = TimerState.Paused; break;
            case TimerState.Paused: _timerEnd = now + _timerLeftPaused; _timer = TimerState.Running; break;
            default: _timer = TimerState.Idle; break;
        }
        _dirty = true;
    }

    void TimerAdjust(int minutes)
    {
        double now = Clock.Now;
        if (_timer == TimerState.Running) _timerEnd = Math.Max(now + 1, _timerEnd + minutes * 60);
        else if (_timer == TimerState.Paused) _timerLeftPaused = Math.Max(1, _timerLeftPaused + minutes * 60);
        else { _timerTotal = Math.Clamp(_timerTotal + minutes * 60, 60, 99 * 60); _timer = TimerState.Idle; }
        _dirty = true;
    }

    void TimerReset() { _timer = TimerState.Idle; _dirty = true; }

    double StopwatchTime(double now) => _swAccum + (_swRunning ? now - _swStart : 0);

    void StopwatchTap()
    {
        double now = Clock.Now;
        if (_swRunning) { _swAccum += now - _swStart; _swRunning = false; }
        else { _swStart = now; _swRunning = true; }
        _dirty = true;
    }

    void StopwatchReset() { _swRunning = false; _swAccum = 0; _dirty = true; }

    // called every frame from Update (also while the watch is hidden)
    void TickTimers(double now)
    {
        if (_timer == TimerState.Running && now >= _timerEnd)
        {
            _timer = TimerState.Done;
            _dirty = true;
            OnAction?.Invoke("timer.done");
        }
    }

    // something on the face changes faster than twice a second
    bool FastWidgets => (_timer == TimerState.Running && HasWidget("timer")) || (_swRunning && HasWidget("stopwatch")) || _open != null;

    static string Clock2(double seconds)
    {
        int s = (int)Math.Ceiling(Math.Max(0, seconds));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }

    static string StopwatchText(double t, bool tenths)
    {
        int cs = (int)(t * 10);
        int m = cs / 600, s = cs / 10 % 60;
        return tenths ? $"{m}:{s:00}.{cs % 10}" : $"{m}:{s:00}";
    }

    // ================= Modern =================
    // Orbit's own look: the Look tab's accent and the logo's indigo; values drawn as orbits - a thin ring, the
    // value as an arc and a small "satellite" riding at its end; cards and buttons in the panel colours. And
    // motion: widgets cascade in when you look at the watch, spring into a new place, digits roll, orbits fill.

    const uint Sub = Theme.Dim;
    const float CardRadius = 24;       // the watch's 34 px corners minus the 10 px gap: concentric
    double _introStart = -10;
    bool _rolling;
    uint _halo;                        // the card colour behind a satellite (a gap around it)

    // a widget's colour: tones of the user's palette
    static uint ToneColor(int tone) => tone switch
    {
        1 => Theme.Accent2,
        2 => Theme.Mix(Theme.Accent, Theme.Accent2, 0.5f),
        3 => Theme.Good,
        _ => Theme.Accent,
    };

    static uint OnColor(uint fill) => Theme.Luma(fill) > 0.55f ? Theme.OnAccent : 0xFFFFFFFF;

    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s[1..];

    // no motion in progress: everything drawn where it ends up (snapshots, a reset)
    public void Settle()
    {
        _ui.ResetAnimations();
        _layoutSeen = false;
        _introStart = -10;
        _rolls.Clear();
    }

    // replays the cascade (the watch just came into view)
    public void Intro() { _introStart = Clock.Now; _dirty = true; }

    public bool Animating => _ui.Animating || _rolling || Clock.Now - _introStart < 1.2;

    static float EaseOutBack(float t)
    {
        const float c1 = 1.5f, c3 = c1 + 1;
        t -= 1;
        return 1 + c3 * t * t * t + c1 * t * t;
    }

    void DrawCustom(PanelData d)
    {
        var list = Layout;
        SampleSources();
        bool cards = C.Background == "cards";
        if (!cards)
        {
            _p.Fill(new RectangleF(0, 0, TW, TH), Theme.OverlayBg, 34);
            _p.Stroke(new RectangleF(1, 1, TW - 2, TH - 2), 0x26FFFFFF, 2, 33);
        }
        uint cardFill = cards ? Theme.WithAlpha(Theme.Panel, 0.97f) : Theme.WithAlpha(Theme.Panel2, 0.8f);
        _halo = cards ? Theme.Panel : Theme.Mix(Theme.Bg, Theme.Panel2, 0.8f);
        var now = DateTime.Now;
        double t = Clock.Now - _introStart;
        _rolling = false;
        if (_wid.Count > list.Count + 32) _wid.Clear();
        _ui.Inert = _open != null;            // behind an opened widget the watch is only a picture
        for (int i = 0; i < list.Count; i++)
        {
            var w = list[i];
            var k = WatchWidgets.Find(w.Type);
            if (k == null) continue;
            int id = Wid(w);
            var target = WatchWidgets.Rect(w, false);
            // spring to a new place (the editor moved / resized it); a new one pops in
            string kx = Ui.Id("wax", id), ky = Ui.Id("way", id), kw = Ui.Id("waw", id), kh = Ui.Id("wah", id), ks = Ui.Id("was", id);
            if (_layoutSeen && !_ui.HasSpring(ks)) _ui.SpringFrom(ks, 0.4f);
            var r = new RectangleF(_ui.Spring(kx, target.X, 260, 26), _ui.Spring(ky, target.Y, 260, 26),
                _ui.Spring(kw, target.Width, 260, 26), _ui.Spring(kh, target.Height, 260, 26));
            float pop = _ui.Spring(ks, 1, 300, 18);
            // the cascade when the watch comes into view: row by row, a little spring each
            float delay = (w.Y * 6 + w.X) * 0.018f;
            float p = (float)Math.Clamp((t - delay) / 0.42, 0, 1);
            float scale = pop * (0.82f + 0.18f * EaseOutBack(p));
            float alpha = MathF.Min(1, p * 1.6f);
            bool anim = scale < 0.999f || scale > 1.001f || alpha < 0.999f;
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            if (anim)
            {
                _p.PushScale(scale, cx, cy);
                if (alpha < 0.999f) _p.PushFade(alpha);
            }
            if (!k.Button)
            {
                _p.Fill(r, cardFill, CardRadius);
                _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.WithAlpha(Theme.Border, cards ? 1f : 0.6f), 1.2f, CardRadius);
            }
            CardTap(id, w, k, r);
            Widget(i, id, w, k, r, d, now);
            if (anim)
            {
                if (alpha < 0.999f) _p.PopFade();
                _p.PopScale();
            }
        }
        _ui.Inert = false;
        DrawDetail(d, list);
        _layoutSeen = true;
        if (t < 1.2) _ui.Animating = true;
    }

    void Widget(int i, int id, WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, DateTime now)
    {
        uint tone = ToneColor(k.Tone);
        switch (k.Id)
        {
            case "time": TimeOrbit(r, w, now, id); break;
            case "date": DateOrbit(r, now, tone); break;
            case "calendar": CalendarOrbit(r, w, now, tone); break;
            case "analog": AnalogOrbit(r, now, tone); break;
            case "timer": TimerOrbit(r, w, id, tone); break;
            case "stopwatch": StopwatchOrbit(r, w, id, tone); break;
            case "session": SessionOrbit(r, w, d, id); break;
            case "battery": BatteryOrbit(r, w, d, id); break;
            case "media": MediaOrbit(r, w, d, id, tone); break;
            case "volume": VolumeOrbit(r, w, id, tone); break;
            case "graph": GraphOrbit(d.Vr, r, tone); break;
            case "perf":
                float qw = r.Width / 4;
                for (int j = 0; j < 4; j++)
                {
                    var sub = new RectangleF(r.X + j * qw, r.Y, qw, r.Height);
                    var (f, val, unit, _, tint) = StatInfo(PerfIds[j], d);
                    Gauge(sub, f, tint, Ui.Id("wg", id * 8 + j), val, unit);
                }
                break;
            default:
                if (k.Button) ButtonCell(id, w, k, r, d);
                else StatOrbit(r, w, k, d, id);
                break;
        }
    }

    static readonly string[] PerfIds = { "fps", "reproj", "gpu", "vram" };

    // a number's fill (0..1 or -1 = no data), the value, its unit, a longer caption, and its colour
    // (Orbit's amber / red when it matters)
    (float frac, string value, string unit, string caption, uint tint) StatInfo(string id, PanelData d)
    {
        var v = d.Vr;
        float budget = 1000f / Math.Max(1, v.DisplayHz);
        uint accent = Theme.Accent, indigo = Theme.Accent2, mid = Theme.Mix(Theme.Accent, Theme.Accent2, 0.5f);
        switch (id)
        {
            case "fps":
                return (v.AppFps < 0 ? -1 : v.AppFps / Math.Max(1, v.DisplayHz), v.AppFps < 0 ? "–" : $"{v.AppFps:0}", "fps",
                    $"of {v.DisplayHz:0} Hz", v.AppFps >= 0 && v.AppFps < v.DisplayHz * 0.85f ? Theme.Warn : accent);
            case "reproj":
                return (v.ReprojPct < 0 ? -1 : MathF.Min(1, v.ReprojPct / 50), v.ReprojPct < 0 ? "–" : $"{v.ReprojPct:0}%", "reproj.",
                    "frames reprojected", v.ReprojPct > 25 ? Theme.Bad : v.ReprojPct > 8 ? Theme.Warn : indigo);
            case "gpu":
                return (v.GpuMs < 0 ? -1 : MathF.Min(1, v.GpuMs / (budget * 1.25f)), v.GpuMs < 0 ? "–" : $"{v.GpuMs:0.0}", "ms",
                    $"GPU of {budget:0.0} ms", v.GpuMs > budget ? Theme.Bad : v.GpuMs > budget * 0.85f ? Theme.Warn : mid);
            case "vram":
                float vram = d.VramUsed;
                return (vram < 0 || d.VramTotalGB <= 0 ? -1 : vram / d.VramTotalGB, vram < 0 ? "–" : $"{vram:0.0}", "GB",
                    d.VramTotalGB > 0 ? $"VRAM of {d.VramTotalGB:0} GB" : "VRAM",
                    vram > 0 && d.VramTotalGB > 0 && vram > d.VramTotalGB * 0.92f ? Theme.Warn : indigo);
            case "cpu":
                float c = Stats.Cpu;
                return (c, c < 0 ? "–" : $"{c * 100:0}%", "CPU", "processor load", c > 0.9f ? Theme.Bad : c > 0.75f ? Theme.Warn : accent);
            default:
                float used = Stats.RamUsedGB, total = Stats.RamTotalGB;
                return (used < 0 || total <= 0 ? -1 : used / total, used < 0 ? "–" : $"{used:0.0}", "GB",
                    total > 0 ? $"memory of {total:0} GB" : "memory", used > 0 && total > 0 && used > total * 0.9f ? Theme.Warn : mid);
        }
    }

    // An orbit: a thin full ring, the value as an arc from the top, and a small satellite riding at its end.
    void Orbit(float cx, float cy, float rad, float frac, uint tint, string key, float width)
    {
        float f = _ui.Spring(key, frac < 0 ? 0 : Math.Clamp(frac, 0, 1), 140, 20, 0.08f);
        _p.Arc(cx, cy, rad, 0, 1, Theme.WithAlpha(tint, 0.2f), width);
        if (frac < 0 || f < 0.002f) return;
        _p.Arc(cx, cy, rad, 0, f, tint, width);
        float a = f * MathF.Tau, sx = cx + rad * MathF.Sin(a), sy = cy - rad * MathF.Cos(a);
        _p.Circle(sx, sy, width * 1.9f, _halo);
        _p.Circle(sx, sy, width * 1.25f, tint);
    }

    // a number on its orbit: the value in the middle, the unit under it
    void Gauge(RectangleF r, float frac, uint tint, string key, string value, string unit, float valueSize = 0)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        float rad = MathF.Min(r.Width, r.Height) / 2 - 16;       // the satellite stays well inside the card
        if (rad < 8) return;
        Orbit(cx, cy, rad, frac, tint, key, MathF.Max(3, rad * 0.1f));
        bool hasUnit = !string.IsNullOrEmpty(unit);
        if (!string.IsNullOrEmpty(value))
        {
            // plain text: a frame rate changes every half second, rolling it would never stop
            float vs = valueSize > 0 ? valueSize : FitSize(value, rad * 1.4f, rad * 0.56f, FontWeight.SemiBold, Theme.Display);
            var vr = hasUnit ? new RectangleF(cx - rad, cy - rad * 0.55f, rad * 2, rad * 0.8f) : new RectangleF(cx - rad, cy - rad * 0.4f, rad * 2, rad * 0.8f);
            _p.Text(value, vr, Theme.Text, vs, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
        }
        if (hasUnit)
            _p.Text(unit, new RectangleF(cx - rad, cy + rad * 0.2f, rad * 2, rad * 0.42f), Sub,
                FitSize(unit, rad * 1.2f, MathF.Max(11, rad * 0.27f), FontWeight.SemiBold, null), FontWeight.SemiBold, TextAlignment.Center);
    }

    // a small caption at the top of a card, as in the settings ("NOW PLAYING")
    void Header(RectangleF r, string text, float size = 13)
    {
        _p.Text(text.ToUpper(CultureInfo.CurrentCulture), new RectangleF(r.X + 16, r.Y + 10, r.Width - 32, size + 8), Sub, size, FontWeight.SemiBold);
    }

    // Text that rolls when it changes: the digits that changed slide up and out while the new ones slide in.
    // Same length: only the changed characters move.
    sealed class Roll { public string From = "", To = ""; public double Start = -10; }
    readonly Dictionary<string, Roll> _rolls = new();

    void RollText(string key, string s, RectangleF r, uint c, float size, FontWeight wt, string fam, TextAlignment al = TextAlignment.Center)
    {
        if (!_rolls.TryGetValue(key, out var ro)) _rolls[key] = ro = new Roll { From = s, To = s };
        else if (ro.To != s) { ro.From = ro.To; ro.To = s; ro.Start = Clock.Now; }
        float t = (float)((Clock.Now - ro.Start) / 0.45);
        if (t >= 1 || ro.From == ro.To || Theme.Classic)
        {
            _p.Text(s, r, c, size, wt, al, ParagraphAlignment.Center, fam);
            return;
        }
        _rolling = true;
        float e = EaseOutBack(Math.Clamp(t, 0, 1));
        float a = Math.Clamp(e, 0, 1);
        float lift = size * 0.9f;
        _p.PushClip(RectangleF.Inflate(r, 4, 0));
        string from = ro.From;
        if (from.Length == s.Length)
        {
            float tw = _p.MeasureWidth(s, size, wt, fam);
            float x0 = al == TextAlignment.Center ? r.X + (r.Width - tw) / 2 : al == TextAlignment.Trailing ? r.Right - tw : r.X;
            for (int i = 0; i < s.Length; i++)
            {
                float x = x0 + (i == 0 ? 0 : _p.MeasureWidth(s[..i], size, wt, fam));
                float cw = _p.MeasureWidth(s.Substring(i, 1), size, wt, fam) + 6;
                if (s[i] == from[i])
                {
                    _p.Text(s.Substring(i, 1), new RectangleF(x, r.Y, cw, r.Height), c, size, wt, TextAlignment.Leading, ParagraphAlignment.Center, fam);
                    continue;
                }
                _p.Text(from.Substring(i, 1), new RectangleF(x, r.Y - lift * e, cw, r.Height), Theme.WithAlpha(c, 1 - a),
                    size, wt, TextAlignment.Leading, ParagraphAlignment.Center, fam);
                _p.Text(s.Substring(i, 1), new RectangleF(x, r.Y + lift * (1 - e), cw, r.Height), Theme.WithAlpha(c, a),
                    size, wt, TextAlignment.Leading, ParagraphAlignment.Center, fam);
            }
        }
        else
        {
            _p.Text(from, new RectangleF(r.X, r.Y - lift * e, r.Width, r.Height), Theme.WithAlpha(c, 1 - a), size, wt, al,
                ParagraphAlignment.Center, fam);
            _p.Text(s, new RectangleF(r.X, r.Y + lift * (1 - e), r.Width, r.Height), Theme.WithAlpha(c, a), size, wt, al,
                ParagraphAlignment.Center, fam);
        }
        _p.PopClip();
    }

    // Orbit's buttons, springy when pressed: a cell button looks like a card (panel colour, border, accent when
    // on); round ones inside a widget are filled discs (the main one in the accent)
    bool Press(string id, RectangleF r, string icon, string label, bool on, uint tint, bool enabled = true, float iconSize = 24,
        bool circle = false)
    {
        bool click = _ui.Clickable(id, r, out bool hover, out bool held) && enabled;
        float s = _ui.Spring(Ui.Id(id, "#s"), held && enabled ? 0.9f : 1f, 420, 18);
        var b = new RectangleF(r.X + r.Width * (1 - s) / 2, r.Y + r.Height * (1 - s) / 2, r.Width * s, r.Height * s);
        float rad = circle ? MathF.Min(b.Width, b.Height) / 2 : MathF.Min(CardRadius * s, MathF.Min(b.Width, b.Height) / 2);
        uint rest = circle ? Theme.Panel3 : Theme.Panel2;
        uint bg = !enabled ? Theme.WithAlpha(rest, 0.5f) : on ? tint : hover ? Theme.Mix(rest, 0xFFFFFFFF, 0.08f) : rest;
        _p.Fill(b, bg, rad);
        if (!circle && !on) _p.Stroke(RectangleF.Inflate(b, -0.6f, -0.6f), Theme.WithAlpha(Theme.Border, 0.9f), 1.2f, rad);
        uint fg = !enabled ? Theme.Faint : on ? OnColor(tint) : Theme.Text;
        float ls = MathF.Min(18, b.Height * 0.3f);
        if (!string.IsNullOrEmpty(label) && icon != null)
        {
            float tw = _p.MeasureWidth(label, ls, FontWeight.SemiBold);
            float total = iconSize + 8 + tw;
            if (total > b.Width - 16)
            {
                label = Ellipsize(label, b.Width - 32 - iconSize, ls, FontWeight.SemiBold);
                tw = _p.MeasureWidth(label, ls, FontWeight.SemiBold);
                total = iconSize + 8 + tw;
            }
            float x = b.X + (b.Width - total) / 2;
            _p.Icon(icon, new RectangleF(x, b.Y, iconSize, b.Height), fg, iconSize);
            _p.Text(label, new RectangleF(x + iconSize + 8, b.Y, tw + 4, b.Height), fg, ls, FontWeight.SemiBold);
        }
        else if (icon != null) _p.Icon(icon, b, fg, MathF.Round(iconSize * s));
        else if (label != null)
            _p.Text(Ellipsize(label, b.Width - 12, ls, FontWeight.SemiBold), b, fg, ls, FontWeight.SemiBold, TextAlignment.Center);
        return click;
    }

    void ButtonCell(int id, WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d)
    {
        string wid = Ui.Id("wcb", id);
        bool click;
        if (k.Id == "layout")
            click = Press(wid, r, w.W >= 2 ? k.Icon : null, d.LayoutName, false, Theme.Accent, true, 20);
        else
        {
            string icon = k.Id == "desktops" && !d.DesktopsVisible ? Theme.IcoHide : k.Icon;
            click = Press(wid, r, icon, w.W >= 2 ? k.Name : null, IsOn(k.Id, d), Theme.Accent, true, w.W >= 2 ? 22 : 28);
        }
        if (click) OnAction?.Invoke(k.Id);
    }

    void TimeOrbit(RectangleF r, WatchWidget w, DateTime now, int id)
    {
        bool h24 = _cfg.Data.Appearance.Clock24h;
        var culture = CultureInfo.CurrentCulture;
        string t = now.ToString(h24 ? "HH:mm" : "h:mm", culture);
        string ampm = h24 ? null : now.Hour < 12 ? "AM" : "PM";
        var tr = r;
        if (w.H >= 2)
        {
            // the date quietly on top, the time large below it
            float ds = MathF.Min(21, r.Height * 0.105f);
            string date = Capital(DateText(now));
            _p.Text(Ellipsize(date, r.Width - 36, ds, FontWeight.Normal), new RectangleF(r.X + 18, r.Y + 12, r.Width - 36, ds + 10), Sub, ds);
            tr = new RectangleF(r.X, r.Y + ds + 14, r.Width, r.Height - ds - 18);
        }
        float aw = ampm == null ? 0 : 0.26f;
        float w100 = _p.MeasureWidth(t, 100, FontWeight.SemiBold, Theme.Display);
        float size = MathF.Max(10, MathF.Min(tr.Height * 0.84f, 100 * (tr.Width - 32) / (w100 + aw * 100 + 6)));
        float tw = _p.MeasureWidth(t, size, FontWeight.SemiBold, Theme.Display);
        float asz = size * 0.24f, aww = ampm == null ? 0 : _p.MeasureWidth(ampm, asz, FontWeight.SemiBold) + 6;
        float x = tr.X + (tr.Width - tw - aww) / 2;
        RollText(Ui.Id("wtt", id), t, new RectangleF(x, tr.Y, tw + 2, tr.Height), Theme.Text, size, FontWeight.SemiBold, Theme.Display,
            TextAlignment.Leading);
        if (ampm != null)
            _p.Text(ampm, new RectangleF(x + tw + 6, tr.Y + tr.Height / 2 + size * 0.06f, aww + 4, size * 0.3f), Sub, asz, FontWeight.SemiBold);
    }

    void DateOrbit(RectangleF r, DateTime now, uint tint)
    {
        var culture = CultureInfo.CurrentCulture;
        string day = Capital(now.ToString("dddd", culture));
        string date = now.ToString(culture.DateTimeFormat.MonthDayPattern, culture);
        float pad = 18, top = r.Y + r.Height * 0.16f;
        float ds = MathF.Min(17, r.Height * 0.18f);
        _p.Text(Ellipsize(day, r.Width - 2 * pad, ds, FontWeight.SemiBold), new RectangleF(r.X + pad, top, r.Width - 2 * pad, ds + 8), tint, ds,
            FontWeight.SemiBold);
        float bs = FitSize(date, r.Width - 2 * pad, MathF.Min(30, r.Height * 0.34f), FontWeight.SemiBold, Theme.Display);
        _p.Text(date, new RectangleF(r.X + pad, top + ds + 6, r.Width - 2 * pad, bs + 12), Theme.Text, bs, FontWeight.SemiBold,
            TextAlignment.Leading, ParagraphAlignment.Near, Theme.Display);
    }

    void CalendarOrbit(RectangleF r, WatchWidget w, DateTime now, uint tint)
    {
        var culture = CultureInfo.CurrentCulture;
        if (w.H >= 2)
        {
            // the month at a glance, today ringed in the accent
            _p.Text(Capital(now.ToString("MMMM", culture)), new RectangleF(r.X + 16, r.Y + 8, r.Width - 80, 26), Theme.Text, 17, FontWeight.SemiBold);
            _p.Text(now.Year.ToString(), new RectangleF(r.Right - 76, r.Y + 8, 60, 26), Sub, 14, FontWeight.Normal, TextAlignment.Trailing);
            var g = new RectangleF(r.X + 10, r.Y + 36, r.Width - 20, r.Height - 44);
            int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
            float cw = g.Width / 7, rh = g.Height / 7;
            for (int c = 0; c < 7; c++)
            {
                string n = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)((first + c) % 7));
                _p.Text(Capital(n.Substring(0, Math.Min(2, n.Length))), new RectangleF(g.X + c * cw, g.Y, cw, rh), Sub, MathF.Min(13, rh * 0.62f),
                    FontWeight.SemiBold, TextAlignment.Center);
            }
            var day1 = new DateTime(now.Year, now.Month, 1);
            int offset = ((int)day1.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(now.Year, now.Month);
            for (int dd = 1; dd <= days; dd++)
            {
                int cell = offset + dd - 1, col = cell % 7, row = cell / 7 + 1;
                var cr = new RectangleF(g.X + col * cw, g.Y + row * rh, cw, rh);
                bool today = dd == now.Day;
                if (today)
                {
                    float d2 = MathF.Min(cw, rh) - 2;
                    _p.Stroke(new RectangleF(cr.X + (cw - d2) / 2, cr.Y + (rh - d2) / 2, d2, d2), tint, 2, d2 / 2);
                }
                _p.Text(dd.ToString(), cr, today ? tint : Theme.Text, MathF.Min(14, rh * 0.62f), today ? FontWeight.Bold : FontWeight.Normal,
                    TextAlignment.Center);
            }
            return;
        }
        // the weekday small, the day big, and on a wide one the month and the year beside it
        var left = w.W >= 2 ? new RectangleF(r.X, r.Y, r.Height, r.Height) : r;
        string wd = Capital(now.ToString("ddd", culture));
        _p.Text(wd, new RectangleF(left.X, left.Y + left.Height * 0.1f, left.Width, left.Height * 0.24f), tint,
            MathF.Min(16, left.Height * 0.17f), FontWeight.SemiBold, TextAlignment.Center);
        _p.Text(now.Day.ToString(), new RectangleF(left.X, left.Y + left.Height * 0.3f, left.Width, left.Height * 0.62f), Theme.Text,
            left.Height * 0.48f, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
        if (w.W >= 2)
        {
            var rr = new RectangleF(left.Right, r.Y, r.Right - left.Right - 12, r.Height);
            string month = Capital(now.ToString("MMMM", culture));
            _p.Text(Ellipsize(month, rr.Width, 20, FontWeight.SemiBold), new RectangleF(rr.X, rr.Y + rr.Height * 0.24f, rr.Width, 28),
                Theme.Text, 20, FontWeight.SemiBold);
            _p.Text(now.Year.ToString(), new RectangleF(rr.X, rr.Y + rr.Height * 0.24f + 28, rr.Width, 24), Sub, 16);
        }
    }

    // Orbit's dial: dots for the hours (bigger on the quarters), the hands in the text colour, and the seconds
    // as the only coloured thing: a satellite circling the rim
    void AnalogOrbit(RectangleF r, DateTime now, uint tint)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = MathF.Min(r.Width, r.Height) / 2 - 12;
        float rim = rad * 0.94f;
        _p.Arc(cx, cy, rim, 0, 1, Theme.WithAlpha(tint, 0.16f), MathF.Max(1.5f, rad * 0.02f));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * MathF.Tau;
            bool quarter = i % 3 == 0;
            float dr = rad * 0.76f;
            _p.Circle(cx + dr * MathF.Sin(a), cy - dr * MathF.Cos(a), quarter ? MathF.Max(3, rad * 0.05f) : MathF.Max(1.8f, rad * 0.026f),
                quarter ? Theme.Text : Sub);
        }
        float sec = now.Second, m = now.Minute + sec / 60f, h = now.Hour % 12 + m / 60f;
        void Hand(float turns, float len, float width)
        {
            float a = turns * MathF.Tau;
            _p.RoundLine(cx, cy, cx + len * MathF.Sin(a), cy - len * MathF.Cos(a), Theme.Text, width);
        }
        Hand(h / 12, rad * 0.46f, MathF.Max(5, rad * 0.075f));
        Hand(m / 60, rad * 0.66f, MathF.Max(3.5f, rad * 0.048f));
        float sa = sec / 60 * MathF.Tau, sx = cx + rim * MathF.Sin(sa), sy = cy - rim * MathF.Cos(sa);
        _p.Circle(sx, sy, MathF.Max(5, rad * 0.075f), _halo);
        _p.Circle(sx, sy, MathF.Max(3.5f, rad * 0.05f), tint);
        _p.Circle(cx, cy, MathF.Max(4, rad * 0.06f), Theme.Text);
        _p.Circle(cx, cy, MathF.Max(2, rad * 0.03f), tint);
    }

    void TimerOrbit(RectangleF r, WatchWidget w, int id, uint tint)
    {
        double now = Clock.Now, left = TimerLeft(now);
        float frac = _timer == TimerState.Idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
        if (_timer == TimerState.Done) tint = Theme.Warn;
        string txt = _timer == TimerState.Done ? "Done" : Clock2(left);
        string state = _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", TimerState.Done => "tap to reset", _ => "tap to start" };
        bool tap;
        if (w.H >= 2)
        {
            // a big orbit with the time inside; - / start-pause / + under it
            float bs = 46;
            var ringArea = new RectangleF(r.X, r.Y + 6, r.Width, r.Height - bs - 22);
            float rad = MathF.Min(ringArea.Width, ringArea.Height) / 2 - 14, cx = r.X + r.Width / 2, cy = ringArea.Y + ringArea.Height / 2;
            Orbit(cx, cy, rad, frac, tint, Ui.Id("wtr", id), MathF.Max(4, rad * 0.08f));
            RollText(Ui.Id("wtx", id), txt, new RectangleF(cx - rad, cy - rad * 0.42f, rad * 2, rad * 0.62f), Theme.Text,
                FitSize(txt, rad * 1.4f, rad * 0.5f, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display);
            _p.Text(state, new RectangleF(cx - rad, cy + rad * 0.22f, rad * 2, rad * 0.32f), Sub, MathF.Min(14, rad * 0.2f), FontWeight.Normal,
                TextAlignment.Center);
            tap = false;
            float by = r.Bottom - bs - 10, gap = 14, x0 = cx - (3 * bs + 2 * gap) / 2;
            bool idle = _timer == TimerState.Idle;
            if (Press(Ui.Id("wtm", id), new RectangleF(x0, by, bs, bs), idle ? "" : "", null, false, tint, true, 20, true))
            {
                if (idle) TimerAdjust(-1); else TimerReset();
            }
            if (Press(Ui.Id("wtp", id), new RectangleF(x0 + bs + gap, by, bs, bs), _timer == TimerState.Running ? "" : "", null, true,
                    tint, true, 20, true)) tap = true;
            if (Press(Ui.Id("wtq", id), new RectangleF(x0 + 2 * (bs + gap), by, bs, bs), "", null, false, tint, true, 20, true)) TimerAdjust(1);
        }
        else
        {
            var ring = w.W >= 2 ? new RectangleF(r.X, r.Y, r.Height, r.Height) : r;
            float rad = MathF.Min(ring.Width, ring.Height) / 2 - 16, cx = ring.X + ring.Width / 2, cy = ring.Y + ring.Height / 2;
            Orbit(cx, cy, rad, frac, tint, Ui.Id("wtr", id), MathF.Max(3, rad * 0.1f));
            if (w.W >= 2)
            {
                _p.Icon(_timer == TimerState.Running ? "" : "", new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), tint, rad * 0.7f);
                var tr = new RectangleF(ring.Right, r.Y + r.Height * 0.16f, r.Right - ring.Right - 12, r.Height * 0.46f);
                RollText(Ui.Id("wtx", id), txt, tr, Theme.Text, FitSize(txt, tr.Width, 36, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold,
                    Theme.Display, TextAlignment.Leading);
                _p.Text(state, new RectangleF(tr.X, tr.Bottom, tr.Width, r.Height * 0.26f), Sub, 14);
            }
            else
                RollText(Ui.Id("wtx", id), txt, new RectangleF(cx - rad, cy - rad * 0.5f, rad * 2, rad), Theme.Text,
                    FitSize(txt, rad * 1.4f, rad * 0.56f, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display);
            tap = false;           // a tap on the card opens the timer
        }
        if (tap) TimerTap();
    }

    void StopwatchOrbit(RectangleF r, WatchWidget w, int id, uint tint)
    {
        double t = StopwatchTime(Clock.Now);
        bool wide = w.W >= 2;
        string txt = StopwatchText(t, wide);
        if (wide)
        {
            float bs = MathF.Min(52, r.Height - 24);
            var tr = new RectangleF(r.X + 16, r.Y, r.Width - 32 - 2 * bs - 10, r.Height);
            _p.Text(txt, tr, _swRunning ? tint : Theme.Text, FitSize(txt, tr.Width, 34, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold,
                TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
            float by = r.Y + (r.Height - bs) / 2, bx = r.Right - 12 - 2 * bs - 8;
            if (Press(Ui.Id("wsr", id), new RectangleF(bx, by, bs, bs), "", null, false, tint, t > 0 && !_swRunning, 18, true)) StopwatchReset();
            if (Press(Ui.Id("wsp", id), new RectangleF(bx + bs + 8, by, bs, bs), _swRunning ? "" : "", null, true, tint, true, 18, true))
                StopwatchTap();
        }
        else
        {
            Header(r, "Stopwatch", 11);
            _p.Text(txt, new RectangleF(r.X, r.Y + 22, r.Width, r.Height - 26), _swRunning ? tint : Theme.Text,
                FitSize(txt, r.Width - 16, 30, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, TextAlignment.Center,
                ParagraphAlignment.Center, Theme.Display);
        }
    }

    static string SessionText(PanelData d)
    {
        if (d.VrSince is not { } since) return "–";
        var span = DateTime.Now - since;
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}" : $"{Math.Max(0, (int)span.TotalMinutes)} min";
    }

    void SessionOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        string val = SessionText(d);
        if (w.W >= 2)
        {
            Header(r, "Time in VR", 12);
            RollText(Ui.Id("wse", id), val, new RectangleF(r.X + 16, r.Y + 28, r.Width - 32, r.Height * 0.48f), Theme.Text,
                FitSize(val, r.Width - 32, 34, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display, TextAlignment.Leading);
            if (d.VrSince is { } s2)
                _p.Text($"since {s2.ToString(_cfg.Data.Appearance.Clock24h ? "HH:mm" : "h:mm tt", CultureInfo.CurrentCulture)}",
                    new RectangleF(r.X + 16, r.Bottom - 26, r.Width - 32, 20), Sub, 14);
            return;
        }
        Header(r, "In VR", 11);
        RollText(Ui.Id("wse", id), val, new RectangleF(r.X, r.Y + 24, r.Width, r.Height - 30), Theme.Text,
            FitSize(val, r.Width - 16, 30, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display);
    }

    // which batteries fit: all of them, or the emptiest ones (kept in their usual order)
    static List<int> PickBatteries(List<(string label, float pct, bool charging)> bats, int n)
    {
        var order = new List<int>();
        for (int i = 0; i < bats.Count; i++) order.Add(i);
        if (bats.Count <= n) return order;
        order.Sort((a, b) => bats[a].pct.CompareTo(bats[b].pct));
        var keep = order.GetRange(0, n);
        keep.Sort();
        return keep;
    }

    static uint BatteryColor(float pct, bool charging) => charging || pct > 0.5f ? Theme.Good : pct > 0.2f ? Theme.Warn : Theme.Bad;

    // an orbit per device, coloured by how full it is
    void BatteryOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var bats = d.Vr.Batteries;
        if (bats.Count == 0)
        {
            string s = d.VrConnected ? "No battery info" : "SteamVR off";
            _p.Text(s, r, Sub, FitSize(s, r.Width - 16, 15, FontWeight.Normal, null), FontWeight.Normal, TextAlignment.Center);
            return;
        }
        int cols = w.W == 1 && w.H == 1 ? 1 : w.H == 1 ? 2 : w.W == 2 ? 2 : 3;
        int rows = w.H == 1 ? 1 : 2;
        var pick = PickBatteries(bats, cols * rows);
        float cw = r.Width / cols, ch = r.Height / rows;
        for (int j = 0; j < pick.Count; j++)
        {
            var b = bats[pick[j]];
            var cell = new RectangleF(r.X + j % cols * cw, r.Y + j / cols * ch, cw, ch);
            float rad = MathF.Min(cw - 20, ch - 46) / 2;
            if (rad < 8) continue;
            float cx = cell.X + cw / 2, cy = cell.Y + 12 + rad;
            Orbit(cx, cy, rad, b.pct, BatteryColor(b.pct, b.charging), Ui.Id("wbt", id * 8 + j), MathF.Max(3, rad * 0.11f));
            _p.Text(b.label, new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), Theme.Text,
                FitSize(b.label, rad * 1.4f, MathF.Max(11, rad * 0.48f), FontWeight.SemiBold, null), FontWeight.SemiBold, TextAlignment.Center);
            string pct = (b.charging ? "⚡" : "") + $"{b.pct * 100:0}%";
            _p.Text(pct, new RectangleF(cell.X, cy + rad + 4, cw, 20), Sub, 14, FontWeight.SemiBold, TextAlignment.Center);
        }
    }

    // now playing: cover (when there is room), title / artist, play-pause (and previous / next when it fits)
    void MediaOrbit(RectangleF r, WatchWidget w, PanelData d, int id, uint tint)
    {
        var (has, title, artist, playing) = MediaNow(d);
        const float pad = 12;
        bool tall = w.H >= 2;
        float x = r.X + pad;
        if (w.W >= 3 || tall)
        {
            float s = tall ? MathF.Min(r.Height - 2 * pad, r.Width * 0.42f) : r.Height - 2 * pad;
            var cover = new RectangleF(x, r.Y + (r.Height - s) / 2, s, s);
            string fk = Ui.Id("wma", id);
            if (Clock.Now - _artChanged < 0.05) _ui.SpringFrom(fk, 0);      // a new cover fades in
            float fade = _ui.Spring(fk, 1, 120, 18);
            _p.Fill(cover, Theme.Panel3, s * 0.14f);
            if (_art != null)
            {
                _p.PushRoundedClip(cover, s * 0.14f);
                _p.Image(_art, cover, Math.Clamp(fade, 0, 1));
                _p.PopRoundedClip();
            }
            else _p.Icon("", cover, Sub, s * 0.38f);
            x = cover.Right + 14;
        }
        string[] ids = { "media.prev", "media.play", "media.next" };
        string[] icons = { "", playing ? "" : "", "" };
        RectangleF text;
        if (tall)
        {
            float bs = 50, gap = 12;
            float by = r.Bottom - pad - bs;
            for (int j = 0; j < 3; j++)
                if (Press(Ui.Id("wcm", id * 3 + j), new RectangleF(x + j * (bs + gap), by, bs, bs), icons[j], null, j == 1, tint, has, 20, true))
                    OnAction?.Invoke(ids[j]);
            _p.Text("NOW PLAYING", new RectangleF(x, r.Y + 12, r.Right - pad - x, 20), Sub, 12, FontWeight.SemiBold);
            text = new RectangleF(x, r.Y + 34, r.Right - pad - x, by - r.Y - 40);
        }
        else
        {
            float bs = MathF.Min(52, r.Height - 2 * pad);
            int nb = w.W >= 6 ? 3 : 1;
            float bx = r.Right - pad - nb * bs - (nb - 1) * 8;
            for (int j = 0; j < nb; j++)
            {
                int a = nb == 1 ? 1 : j;
                if (Press(Ui.Id("wcm", id * 3 + a), new RectangleF(bx + j * (bs + 8), r.Y + (r.Height - bs) / 2, bs, bs), icons[a], null, a == 1,
                        tint, has, 20, true))
                    OnAction?.Invoke(ids[a]);
            }
            text = new RectangleF(x, r.Y + pad, bx - 12 - x, r.Height - 2 * pad);
        }
        if (text.Width < 24) return;
        if (!has)
        {
            _p.Text(Ellipsize("Nothing playing", text.Width, 17, FontWeight.Normal), text, Sub, 17);
            return;
        }
        float half = MathF.Min(text.Height / 2, 34);
        float ty = text.Y + (text.Height - 2 * half) / 2;
        float ts = MathF.Min(21, half * 0.64f), asz = MathF.Min(17, half * 0.52f);
        _p.Text(Ellipsize(title, text.Width, ts, FontWeight.SemiBold), new RectangleF(text.X, ty, text.Width, half), Theme.Text, ts, FontWeight.SemiBold);
        _p.Text(Ellipsize(artist ?? "", text.Width, asz, FontWeight.Normal), new RectangleF(text.X, ty + half, text.Width, half), Sub, asz);
    }

    void VolumeOrbit(RectangleF r, WatchWidget w, int id, uint tint)
    {
        float lvl = Volume.Level;
        bool muted = Volume.Muted, ok = lvl >= 0;
        string icon = muted ? "" : "";
        string pct = ok ? (muted ? "Muted" : $"{lvl * 100:0}%") : "–";
        uint col = muted ? Theme.Faint : tint;
        if (w.W == 1)
        {
            Gauge(r, ok ? lvl : -1, col, Ui.Id("wvg", id), muted ? "" : pct, muted ? "muted" : "volume");
            if (muted) _p.Icon(icon, new RectangleF(r.X, r.Y + r.Height * 0.22f, r.Width, r.Height * 0.4f), Sub, 22);
            return;
        }
        float bs = MathF.Min(48, r.Height - 28);
        float by = r.Y + (r.Height - bs) / 2;
        if (w.W == 2)
        {
            // - / orbit / +
            if (Press(Ui.Id("wvm", id), new RectangleF(r.X + 10, by, bs, bs), "", null, false, tint, ok, 18, true)) Volume.Step(-0.05f);
            if (Press(Ui.Id("wvp", id), new RectangleF(r.Right - 10 - bs, by, bs, bs), "", null, false, tint, ok, 18, true)) Volume.Step(0.05f);
            var g = new RectangleF(r.X + 10 + bs, r.Y, r.Width - 20 - 2 * bs, r.Height);
            Gauge(g, ok ? lvl : -1, col, Ui.Id("wvg", id), muted ? "" : pct, "");
            if (muted) _p.Icon(icon, new RectangleF(g.X, g.Y + g.Height * 0.25f, g.Width, g.Height * 0.5f), Sub, 18);
            return;
        }
        // speaker (mute), a bar, - +
        if (Press(Ui.Id("wvs", id), new RectangleF(r.X + 12, by, bs, bs), icon, null, muted, Theme.Panel3, ok, 20, true)) Volume.ToggleMute();
        if (Press(Ui.Id("wvm", id), new RectangleF(r.Right - 12 - 2 * bs - 8, by, bs, bs), "", null, false, tint, ok, 18, true)) Volume.Step(-0.05f);
        if (Press(Ui.Id("wvp", id), new RectangleF(r.Right - 12 - bs, by, bs, bs), "", null, false, tint, ok, 18, true)) Volume.Step(0.05f);
        var bar = new RectangleF(r.X + 24 + bs, r.Y + r.Height / 2 + 6, r.Width - 3 * bs - 56, 8);
        float f = _ui.Spring(Ui.Id("wvb", id), ok ? lvl : 0, 160, 22);
        _p.Fill(bar, Theme.WithAlpha(col, 0.2f), 4);
        if (ok)
        {
            float fx = bar.X + bar.Width * Math.Clamp(f, 0, 1);
            _p.Fill(new RectangleF(bar.X, bar.Y, MathF.Max(8, fx - bar.X), bar.Height), col, 4);
            _p.Circle(fx, bar.Y + bar.Height / 2, 9, _halo);
            _p.Circle(fx, bar.Y + bar.Height / 2, 6, col);
        }
        _p.Text(pct, new RectangleF(bar.X, bar.Y - 30, bar.Width, 22), Theme.Text, 17, FontWeight.SemiBold);
    }

    void StatOrbit(RectangleF r, WatchWidget w, WatchWidgets.Kind k, PanelData d, int id)
    {
        if (k.Id == "session") return;
        var (frac, value, unit, caption, tint) = StatInfo(k.Id, d);
        if (w.W == 1) { Gauge(r, frac, tint, Ui.Id("wsg", id), value, unit); return; }
        var g = new RectangleF(r.X, r.Y, r.Height, r.Height);
        Gauge(g, frac, tint, Ui.Id("wsg", id), "", "");
        float tx = g.Right, tw = r.Right - 12 - tx;
        _p.Text(Ellipsize(k.Name.ToUpper(CultureInfo.CurrentCulture), tw, 12, FontWeight.SemiBold), new RectangleF(tx, r.Y + 12, tw, 20), Sub, 12,
            FontWeight.SemiBold);
        string v = unit is "fps" or "ms" or "GB" ? value + " " + unit : value;
        _p.Text(v, new RectangleF(tx, r.Y + 30, tw, 40), Theme.Text, FitSize(v, tw, 30, FontWeight.SemiBold, Theme.Display),
            FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
        _p.Text(Ellipsize(caption, tw, 13, FontWeight.Normal), new RectangleF(tx, r.Bottom - 26, tw, 18), Sub, 13);
    }

    // the last ~5 s of frames as bars; amber over budget / reprojected, red dropped
    void GraphOrbit(VrStats v, RectangleF r, uint tint)
    {
        float budget = 1000f / Math.Max(1, v.DisplayHz), top = budget * 2;
        string label = v.AppFps < 0 ? "waiting for frames" : $"{v.AppFps:0} fps · {v.GpuMs:0.0} ms · {Math.Max(0, v.ReprojPct):0}%";
        _p.Text(Ellipsize(label, r.Width - 32, 15, FontWeight.SemiBold), new RectangleF(r.X + 16, r.Y + 8, r.Width - 32, 22), Theme.Text, 15,
            FontWeight.SemiBold);
        var g = new RectangleF(r.X + 14, r.Y + 34, r.Width - 28, r.Height - 44);
        int n = v.FrameCount;
        float step = g.Width / VrStats.FrameHistory;
        for (int i = 0; i < n; i++)
        {
            int idx = (v.FrameHead - n + i + VrStats.FrameHistory) % VrStats.FrameHistory;
            float ms = v.FrameMs[idx];
            byte flag = v.FrameFlag[idx];
            float h = Math.Clamp(ms / top, 0.03f, 1) * g.Height;
            uint col = flag == 2 ? Theme.Bad : flag == 1 || ms > budget ? Theme.Warn : tint;
            float x = g.Right - (n - i) * step;
            _p.Fill(new RectangleF(x, g.Bottom - h, MathF.Max(1, step - 0.4f), h), col);
        }
        float by = g.Bottom - g.Height * (budget / top);
        _p.Line(g.X, by, g.Right, by, 0x66FFFFFF, 1.2f);
    }

    // ================= Windows 95 =================

    const uint Navy95c = 0xFF000080;

    void Custom95(PanelData d)
    {
        var list = Layout;
        SampleSources();
        if (C.Background != "cards") _p.Fill(new RectangleF(0, 0, TW95, TH95), Theme.W95Teal);     // the desktop
        var now = DateTime.Now;
        _ui.Inert = _open != null;
        for (int i = 0; i < list.Count; i++)
        {
            var w = list[i];
            var k = WatchWidgets.Find(w.Type);
            if (k == null) continue;
            int id = Wid(w);
            var r = WatchWidgets.Rect(w, true);
            CardTap(id, w, k, r);
            if (k.Button) { Button95Widget(id, w, k, r, d); continue; }
            // a little 95 window: with a caption when it is tall enough, a plain raised panel when not
            _p.Fill(r, Theme.W95Face);
            _p.Bevel(r, Painter.Edge.Window);
            var c = RectangleF.Inflate(r, -3, -3);
            if (w.H >= 2)
            {
                var cap = new RectangleF(c.X, c.Y, c.Width, 18);
                _p.Fill(cap, Theme.W95Navy);
                _ui.Text95(Fit(k.Title95, cap.Width - 6, Ui.F95, true), new RectangleF(cap.X + 3, cap.Y, cap.Width - 4, cap.Height), Wht95, true);
                c = new RectangleF(c.X + 1, c.Y + 19, c.Width - 2, c.Height - 20);
            }
            switch (k.Id)
            {
                case "time": Time95(c, now, w.H >= 2); break;
                case "date":
                    var culture = CultureInfo.CurrentCulture;
                    string ds = now.ToString(culture.DateTimeFormat.LongDatePattern, culture);
                    _ui.Text95(Fit(ds, c.Width - 6), c, Blk95, false, TextAlignment.Center);
                    break;
                case "calendar": Calendar95(c, w, now); break;
                case "analog":
                    Dial95(now, MathF.Floor(c.X + c.Width / 2), MathF.Floor(c.Y + c.Height / 2), MathF.Floor(MathF.Min(c.Width, c.Height) / 2) - 5);
                    break;
                case "timer": Timer95(c, w, id); break;
                case "stopwatch": Stopwatch95(c, w, id); break;
                case "battery": Battery95(c, d, w); break;
                case "media": Media95Widget(c, d, w, id); break;
                case "volume": Volume95(c, w, id); break;
                case "graph": Graph95(d.Vr, c); break;
                case "perf": Status95(d, new RectangleF(c.X, MathF.Floor(c.Y + (c.Height - 24) / 2), c.Width, 24)); break;
                case "session":
                    string sv = SessionText(d);
                    Value95(c, sv == "–" ? "--" : sv, w.W >= 2 ? "time in VR" : "in VR", Blk95);
                    break;
                default:
                    var (frac, value, unit, _, tint) = StatInfo(k.Id, d);
                    uint color = tint == Theme.Warn ? 0xFF806000u : tint == Theme.Bad ? 0xFF800000u : Blk95;
                    if (value == "–") value = "--";
                    string cap95 = k.Id switch { "fps" => "fps", "reproj" => "reproj.", "gpu" => "GPU ms", "vram" => "VRAM", "cpu" => "CPU", _ => "RAM" };
                    if (w.W >= 2 && (k.Id is "cpu" or "ram" or "vram") && frac >= 0)
                    {
                        // a System Monitor bar
                        _ui.Text95(Fit($"{cap95}: {value}{(unit == "GB" ? " GB" : "")}", c.Width - 8), new RectangleF(c.X + 4, c.Y + 4, c.Width - 8, 13), color);
                        _ui.Progress95(new RectangleF(c.X + 4, c.Y + 22, c.Width - 8, 14), frac, Navy95c);
                    }
                    else Value95(c, value, cap95, color);
                    break;
            }
        }
        _ui.Inert = false;
        DrawDetail95(d, list);
    }

    void Value95(RectangleF c, string value, string caption, uint color)
    {
        float vy = MathF.Floor(c.Y + (c.Height - 28) / 2);
        _ui.Text95(Fit(value, c.Width - 2, 13, true), new RectangleF(c.X, vy, c.Width, 15), color, true, TextAlignment.Center, size: 13);
        _ui.Text95(Fit(caption, c.Width - 2), new RectangleF(c.X, vy + 15, c.Width, 13), Blk95, false, TextAlignment.Center);
    }

    void Time95(RectangleF c, DateTime now, bool tall)
    {
        bool h24 = _cfg.Data.Appearance.Clock24h;
        string time = now.ToString(h24 ? "H:mm:ss" : "h:mm:ss", CultureInfo.CurrentCulture);
        string ampm = h24 ? null : now.Hour < 12 ? "AM" : "PM";
        bool withDate = tall && c.Height >= 44;
        float avail = c.Width - 8 - (ampm != null ? _ui.Width95(ampm) + 4 : 0);
        float size = MathF.Floor(MathF.Min(c.Height * (withDate ? 0.62f : 0.82f), 100 * avail / _ui.Width95(time, false, 100)));
        size = Math.Max(11, size);
        var tr = withDate ? new RectangleF(c.X, c.Y, c.Width, c.Height - 15) : c;
        BigTime95(time, ampm, tr, size, true);
        if (!withDate) return;
        var culture = CultureInfo.CurrentCulture;
        string ds = now.ToString(culture.DateTimeFormat.LongDatePattern, culture);
        _ui.Text95(Fit(ds, c.Width - 6), new RectangleF(c.X, c.Bottom - 15, c.Width, 13), Blk95, false, TextAlignment.Center);
    }

    // Date/Time Properties' calendar: a white month grid, today in navy
    void Calendar95(RectangleF c, WatchWidget w, DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        if (w.H >= 2 && c.Width < 140)
        {
            // too small for a month grid: a desk calendar's page - the month on top, the day big
            var page = new RectangleF(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4);
            _p.Fill(page, Wht95);
            _p.Bevel(page, Painter.Edge.Sunken);
            var head = new RectangleF(page.X + 2, page.Y + 2, page.Width - 4, 15);
            _p.Fill(head, 0xFF800000);
            _ui.Text95(Fit(now.ToString("MMMM", culture), head.Width - 4, Ui.F95, true), head, Wht95, true, TextAlignment.Center);
            _ui.Text95(now.Day.ToString(), new RectangleF(page.X, head.Bottom + 1, page.Width, page.Bottom - head.Bottom - 16), Blk95, true,
                TextAlignment.Center, size: MathF.Floor(MathF.Min(36, (page.Bottom - head.Bottom - 16) * 0.8f)));
            _ui.Text95(Fit(now.ToString("dddd", culture), page.Width - 4), new RectangleF(page.X, page.Bottom - 16, page.Width, 13), Blk95, false,
                TextAlignment.Center);
            return;
        }
        if (w.H < 2)
        {
            string wd = now.ToString(w.W >= 2 ? "dddd" : "ddd", culture);
            string dd = w.W >= 2 ? now.ToString(culture.DateTimeFormat.MonthDayPattern, culture) : now.Day.ToString();
            float vy = MathF.Floor(c.Y + (c.Height - 31) / 2);
            _ui.Text95(Fit(wd, c.Width - 4), new RectangleF(c.X, vy, c.Width, 13), Blk95, false, TextAlignment.Center);
            _ui.Text95(Fit(dd, c.Width - 4, 16, true), new RectangleF(c.X, vy + 13, c.Width, 18), Blk95, true, TextAlignment.Center, size: 16);
            return;
        }
        var box = new RectangleF(c.X + 2, c.Y + 2, c.Width - 4, c.Height - 4);
        _p.Fill(box, Wht95);
        _p.Bevel(box, Painter.Edge.Sunken);
        var g = RectangleF.Inflate(box, -3, -3);
        int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        float cw = g.Width / 7, rh = MathF.Floor(g.Height / 7);
        for (int col = 0; col < 7; col++)
        {
            string n = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)((first + col) % 7));
            _ui.Text95(n.Substring(0, 1).ToUpper(culture), new RectangleF(g.X + col * cw, g.Y, cw, rh), Blk95, true, TextAlignment.Center);
        }
        var day1 = new DateTime(now.Year, now.Month, 1);
        int offset = ((int)day1.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(now.Year, now.Month);
        for (int dd = 1; dd <= days; dd++)
        {
            int cell = offset + dd - 1;
            var cr = new RectangleF(MathF.Floor(g.X + cell % 7 * cw), g.Y + (cell / 7 + 1) * rh, MathF.Ceiling(cw), rh);
            bool today = dd == now.Day;
            if (today) _p.Fill(cr, Navy95c);
            _ui.Text95(dd.ToString(), cr, today ? Wht95 : Blk95, false, TextAlignment.Center);
        }
    }

    void Timer95(RectangleF c, WatchWidget w, int id)
    {
        double left = TimerLeft(Clock.Now);
        string txt = _timer == TimerState.Done ? "Done!" : Clock2(left);
        if (w.H >= 2)
        {
            _ui.Text95(txt, new RectangleF(c.X, c.Y + 2, c.Width, 20), _timer == TimerState.Done ? 0xFF800000 : Blk95, true, TextAlignment.Center, size: 16);
            float frac = _timer == TimerState.Idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
            _ui.Progress95(new RectangleF(c.X + 4, c.Y + 26, c.Width - 8, 14), frac, Navy95c);
            float by = c.Bottom - 22, bw = MathF.Floor((c.Width - 12) / 3);
            bool idle = _timer == TimerState.Idle;
            if (_ui.Button95(Ui.Id("wtm", id), new RectangleF(c.X + 2, by, bw, 20), idle ? "-1" : "Reset")) { if (idle) TimerAdjust(-1); else TimerReset(); }
            if (_ui.Button95(Ui.Id("wtp", id), new RectangleF(c.X + 6 + bw, by, bw, 20), _timer == TimerState.Running ? "Pause" : "Start", true, true))
                TimerTap();
            if (_ui.Button95(Ui.Id("wtq", id), new RectangleF(c.X + 10 + 2 * bw, by, bw, 20), "+1")) TimerAdjust(1);
            return;
        }
        Value95(c, txt, _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", TimerState.Done => "done", _ => "timer" }, Blk95);
    }

    void Stopwatch95(RectangleF c, WatchWidget w, int id)
    {
        double t = StopwatchTime(Clock.Now);
        string txt = StopwatchText(t, w.W >= 2);
        if (w.W >= 2)
        {
            _ui.Text95(txt, new RectangleF(c.X + 4, c.Y + 2, c.Width - 8, 18), Blk95, true, TextAlignment.Leading, size: 16);
            float bw = MathF.Floor((c.Width - 10) / 2), by = c.Bottom - 20;
            if (_ui.Button95(Ui.Id("wsp", id), new RectangleF(c.X + 2, by, bw, 19), _swRunning ? "Stop" : "Start", true, true)) StopwatchTap();
            if (_ui.Button95(Ui.Id("wsr", id), new RectangleF(c.X + 6 + bw, by, bw, 19), "Reset", t > 0 && !_swRunning)) StopwatchReset();
            return;
        }
        Value95(c, txt, _swRunning ? "running" : "stopwatch", Blk95);
    }

    // Volume Control: a slider and Mute
    void Volume95(RectangleF c, WatchWidget w, int id)
    {
        float lvl = Volume.Level;
        bool ok = lvl >= 0;
        if (w.W == 1)
        {
            Value95(c, ok ? (Volume.Muted ? "Mute" : $"{lvl * 100:0}%") : "--", "volume", Blk95);
            return;
        }
        float v = ok ? lvl : 0;
        if (_ui.Track95(Ui.Id("wvt", id), new RectangleF(c.X + 2, c.Y + 1, c.Width - 4, 30), ref v, 0, 1, ok && !Volume.Muted, 6)) Volume.Set(v);
        bool mute = Volume.Muted;
        if (_ui.Check95(Ui.Id("wvk", id), c.X + 4, c.Bottom - 13, "Mute", ref mute, ok)) Volume.ToggleMute();
        _ui.Text95(ok ? $"{lvl * 100:0}%" : "--", new RectangleF(c.Right - 34, c.Bottom - 14, 32, 13), Blk95, false, TextAlignment.Trailing);
    }

    void Battery95(RectangleF c, PanelData d, WatchWidget w)
    {
        var bats = d.Vr.Batteries;
        if (bats.Count == 0)
        {
            _ui.Text95(Fit(d.VrConnected ? "No battery info" : "SteamVR is off", c.Width - 4), c, Gry95, false, TextAlignment.Center);
            return;
        }
        if (w.W == 1 && w.H == 1)
        {
            var low = bats[0];
            foreach (var b in bats) if (b.pct < low.pct) low = b;
            float y = MathF.Floor(c.Y + (c.Height - 28) / 2);
            _ui.Text95(low.label, new RectangleF(c.X, y, c.Width, 13), Blk95, false, TextAlignment.Center);
            _ui.Text95($"{low.pct * 100:0}%", new RectangleF(c.X, y + 14, c.Width, 15), low.pct > 0.2f ? Blk95 : 0xFF800000, true,
                TextAlignment.Center, size: 13);
            return;
        }
        var pick = PickBatteries(bats, Math.Min(4, (int)(c.Height / 13)));
        float y0 = MathF.Floor(c.Y + (c.Height - pick.Count * 13) / 2);
        for (int i = 0; i < pick.Count; i++)
        {
            var b = bats[pick[i]];
            float y = y0 + i * 13;
            _ui.Text95(b.label, new RectangleF(c.X + 2, y, 34, 12));
            var bar = new RectangleF(c.X + 36, y + 1, c.Width - 36 - 32, 10);
            if (bar.Width >= 12) _ui.Progress95(bar, b.pct, b.pct > 0.2f ? Theme.W95Navy : 0xFF800000);
            _ui.Text95($"{b.pct * 100:0}%", new RectangleF(c.Right - 32, y, 30, 12), Blk95, false, TextAlignment.Trailing);
        }
    }

    // the CD Player: green on black, |<<  >  >>|, and the cover when the window is tall
    void Media95Widget(RectangleF c, PanelData d, WatchWidget w, int id)
    {
        var (has, title, artist, playing) = MediaNow(d);
        float x = c.X;
        if (w.H >= 2 && c.Height >= 48)
        {
            var box = new RectangleF(c.X + 1, c.Y + 1, 36, 36);
            var inner = RectangleF.Inflate(box, -2, -2);
            _p.Fill(inner, Wht95);
            if (_art != null) _p.Image(_art, inner);
            else W95Icons.Draw(_p, W95Icons.Id.Note, inner.X + 8, inner.Y + 8);
            _p.Bevel(box, Painter.Edge.Sunken);
            x = box.Right + 4;
        }
        var lcd = new RectangleF(x, c.Y + 1, c.Right - x - 1, 18);
        _p.Fill(lcd, Blk95);
        _p.Bevel(lcd, Painter.Edge.Sunken);
        string line = has ? (string.IsNullOrEmpty(artist) ? title : $"{title} - {artist}") : "Nothing playing";
        _ui.Text95(Fit(line, lcd.Width - 8), new RectangleF(lcd.X + 4, lcd.Y + 2, lcd.Width - 6, 14), has ? 0xFF00FF00 : 0xFF008000);
        string[] ids = { "media.prev", "media.play", "media.next" };
        string[][] glyphs = { PrevGlyph, playing ? PauseGlyph : PlayGlyph, NextGlyph };
        float by = lcd.Bottom + 3;
        if (by + 18 > c.Bottom + 1) return;
        int nb = lcd.Width >= 86 ? 3 : 1;
        float bx = MathF.Floor(lcd.X + (lcd.Width - (nb * 28 - 2)) / 2);
        for (int j = 0; j < nb; j++)
        {
            int a = nb == 1 ? 1 : j;
            if (_ui.GlyphButton95(Ui.Id("wcm", id * 3 + a), new RectangleF(bx + j * 28, by, 26, 18), glyphs[a], has)) OnAction?.Invoke(ids[a]);
        }
    }

    void Button95Widget(int id, WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d)
    {
        bool click;
        if (k.Id == "layout")
            click = _ui.Button95(Ui.Id("wcb", id), r, Ui.Esc95(Fit(d.LayoutName, r.Width - 8)), true, false, false);
        else
        {
            var icon = k.Id == "desktops" && !d.DesktopsVisible ? W95Icons.Id.ComputerOff : k.Icon95;
            click = _ui.Button95(Ui.Id("wcb", id), r, w.W >= 2 ? Ui.Esc95(Fit(k.Name, r.Width - 30)) : null, true, false, IsOn(k.Id, d), icon);
        }
        if (click) OnAction?.Invoke(k.Id);
    }
}
