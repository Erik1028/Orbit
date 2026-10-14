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

    // a title over two lines: as many words as fit on the first, the rest (cut if it must be) on the second
    (string, string) TwoLines(string s, float maxW, float size, FontWeight wt)
    {
        if (_p.MeasureWidth(s, size, wt) <= maxW) return (s, "");
        int cut = -1;
        for (int i = s.IndexOf(' '); i > 0; i = s.IndexOf(' ', i + 1))
        {
            if (_p.MeasureWidth(s[..i], size, wt) > maxW) break;
            cut = i;
        }
        if (cut < 0) return (Ellipsize(s, maxW, size, wt), "");
        return (s[..cut], Ellipsize(s[(cut + 1)..].TrimStart(), maxW, size, wt));
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
        (id == "keyboard" && d.KeyboardVisible) || (id == "desktops" && d.DesktopsVisible) || (id == "night" && d.NightMode) ||
        (id == "status" && d.StatusLine) || (id == "theater" && d.Theater);

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
        if (_wid.Count > list.Count + 32)
        {
            // forget only the widgets that are gone: a live widget must keep its id (and so its animation state),
            // or it would pop in again
            var live = new HashSet<WatchWidget>(list);
            foreach (var gone in new List<WatchWidget>(_wid.Keys)) if (!live.Contains(gone)) _wid.Remove(gone);
        }
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
            case "dock": DockOrbit(r, w, d, id, tone); break;
            case "room": RoomOrbit(r, w, d, id, tone); break;
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

    // a number's fill (0..1 or -1 = no data), the value, its unit, a short caption (the widget's name is shown beside it), and its colour
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
                    "of frames", v.ReprojPct > 25 ? Theme.Bad : v.ReprojPct > 8 ? Theme.Warn : indigo);
            case "gpu":
                return (v.GpuMs < 0 ? -1 : MathF.Min(1, v.GpuMs / budget), v.GpuMs < 0 ? "–" : $"{v.GpuMs:0.0}", "ms",
                    $"of {budget:0.0} ms", v.GpuMs > budget ? Theme.Bad : v.GpuMs > budget * 0.85f ? Theme.Warn : mid);
            case "vram":
                float vram = d.VramUsed;
                return (vram < 0 || d.VramTotalGB <= 0 ? -1 : vram / d.VramTotalGB, vram < 0 ? "–" : $"{vram:0.0}", "GB",
                    d.VramTotalGB > 0 ? $"of {d.VramTotalGB:0} GB" : "in use",
                    vram > 0 && d.VramTotalGB > 0 && vram > d.VramTotalGB * 0.92f ? Theme.Warn : indigo);
            case "cpu":
                float c = Stats.Cpu;
                return (c, c < 0 ? "–" : $"{c * 100:0}%", "CPU", "total load", c > 0.9f ? Theme.Bad : c > 0.75f ? Theme.Warn : accent);
            default:
                float used = Stats.RamUsedGB, total = Stats.RamTotalGB;
                return (used < 0 || total <= 0 ? -1 : used / total, used < 0 ? "–" : $"{used:0.0}", "GB",
                    total > 0 ? $"of {total:0} GB" : "in use", used > 0 && total > 0 && used > total * 0.9f ? Theme.Warn : mid);
        }
    }

    // A plain line: a faint track and, from the left, the value in colour (it glides when the value changes)
    void Meter(RectangleF bar, float frac, uint tint, string key)
    {
        float f = _ui.Spring(key, frac < 0 ? 0 : Math.Clamp(frac, 0, 1), 140, 20, 0.08f);
        float h = bar.Height;
        _p.Fill(bar, Theme.WithAlpha(tint, 0.2f), h / 2);
        if (frac < 0 || f < 0.002f) return;
        _p.Fill(new RectangleF(bar.X, bar.Y, MathF.Max(h, bar.Width * MathF.Min(f, 1)), h), tint, h / 2);
    }

    // a value with a plain line under it (filled to how much it is), the unit or a note small under the line;
    // roll: the key of a value that rolls when it changes (a timer), null for plain text
    void Gauge(RectangleF r, float frac, uint tint, string key, string value, string unit, float valueSize = 0, string roll = null)
    {
        float pad = MathF.Max(10, r.Width * 0.12f), iw = r.Width - 2 * pad;
        if (iw < 16) return;
        bool hasValue = !string.IsNullOrEmpty(value), hasUnit = !string.IsNullOrEmpty(unit);
        float lt = Math.Clamp(r.Height * 0.045f, 4, 10);
        float vs = valueSize > 0 ? valueSize : hasValue ? FitSize(value, iw, Math.Clamp(r.Height * 0.34f, 14, 96), FontWeight.SemiBold, Theme.Display) : 0;
        float us = hasUnit ? FitSize(unit, r.Width - 12, Math.Clamp(r.Height * 0.13f, 11, 22), FontWeight.SemiBold, null) : 0;
        float vh = vs * 1.1f, gap = MathF.Max(5, vs * 0.14f), uh = us * 1.4f;
        float total = (hasValue ? vh + gap : 0) + lt + (hasUnit ? gap + uh : 0);
        float y = r.Y + (r.Height - total) / 2;
        if (hasValue)
        {
            var vr = new RectangleF(r.X, y, r.Width, vh);
            // plain text: a frame rate changes every half second, rolling it would never stop
            if (roll != null) RollText(roll, value, vr, Theme.Text, vs, FontWeight.SemiBold, Theme.Display);
            else _p.Text(value, vr, Theme.Text, vs, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
            y += vh + gap;
        }
        Meter(new RectangleF(r.X + pad, y, iw, lt), frac, tint, key);
        if (hasUnit) _p.Text(unit, new RectangleF(r.X, y + lt + gap, r.Width, uh), Sub, us, FontWeight.SemiBold, TextAlignment.Center);
    }

    // a wide reading: its name small on the left and a note on the right, the value big under them, then its line;
    // returns where the value and the line go
    (RectangleF value, RectangleF line) Reading(RectangleF r, string name, string note)
    {
        float pad = 16, tw = r.Width - 2 * pad, ty = r.Y + (r.Height - 69) / 2;
        float nw = string.IsNullOrEmpty(note) ? 0 : _p.MeasureWidth(note, 13, FontWeight.Normal) + 10;
        if (nw > 0) _p.Text(note, new RectangleF(r.X + pad, ty, tw, 18), Sub, 13, FontWeight.Normal, TextAlignment.Trailing);
        _p.Text(Ellipsize(name.ToUpper(CultureInfo.CurrentCulture), tw - nw, 12, FontWeight.SemiBold), new RectangleF(r.X + pad, ty, tw - nw, 18), Sub, 12,
            FontWeight.SemiBold);
        return (new RectangleF(r.X + pad, ty + 18, tw, 40), new RectangleF(r.X + pad, ty + 64, tw, 5));
    }

    // a small caption at the top of a card, as in the settings ("NOW PLAYING")
    void Header(RectangleF r, string text, float size = 13, bool center = false)
    {
        _p.Text(text.ToUpper(CultureInfo.CurrentCulture), new RectangleF(r.X + 16, r.Y + 10, r.Width - 32, size + 8), Sub, size, FontWeight.SemiBold,
            center ? TextAlignment.Center : TextAlignment.Leading);
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
        // sized so the time and its AM/PM (0.24 of it, 6 px apart) keep 20 px from both sides
        float w100 = _p.MeasureWidth(t, 100, FontWeight.SemiBold, Theme.Display);
        float a100 = ampm == null ? 0 : _p.MeasureWidth(ampm, 24, FontWeight.SemiBold);
        float size = MathF.Max(10, MathF.Min(tr.Height * 0.84f, 100 * (tr.Width - 40 - (ampm == null ? 0 : 12)) / (w100 + a100)));
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
            var day1 = new DateTime(now.Year, now.Month, 1);
            int offset = ((int)day1.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(now.Year, now.Month);
            int weeks = (offset + days + 6) / 7;
            float cw = g.Width / 7, rh = g.Height / (weeks + 1);
            for (int c = 0; c < 7; c++)
            {
                string n = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)((first + c) % 7));
                _p.Text(Capital(n.Substring(0, Math.Min(2, n.Length))), new RectangleF(g.X + c * cw, g.Y, cw, rh), Sub, MathF.Min(13, rh * 0.62f),
                    FontWeight.SemiBold, TextAlignment.Center);
            }
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
                _p.Text(dd.ToString(), cr, today ? tint : Theme.Text, MathF.Min(15, rh * 0.6f), today ? FontWeight.Bold : FontWeight.Normal,
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
            // the time big over its line (the state under it); - / start-pause / + at the bottom
            float bs = 44;
            float by = r.Bottom - bs - 14, gap = 14, cx = r.X + r.Width / 2, x0 = cx - (3 * bs + 2 * gap) / 2;
            Gauge(new RectangleF(r.X, r.Y + 8, r.Width, by - r.Y - 16), frac, tint, Ui.Id("wtr", id), txt, _timer == TimerState.Idle ? "" : state, 0,
                Ui.Id("wtx", id));
            tap = false;
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
            if (w.W >= 2)
            {
                var (vr, lr) = Reading(r, "Timer", state);
                RollText(Ui.Id("wtx", id), txt, vr, Theme.Text, FitSize(txt, vr.Width, 32, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold,
                    Theme.Display, TextAlignment.Leading);
                Meter(lr, frac, tint, Ui.Id("wtr", id));
            }
            else
                Gauge(r, frac, tint, Ui.Id("wtr", id), txt,
                    _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", TimerState.Done => "done", _ => "timer" }, 0, Ui.Id("wtx", id));
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
            int nb = t > 0 && !_swRunning ? 2 : 1;
            float bs = MathF.Min(nb == 2 ? 44 : 50, r.Height - 24);
            var tr = new RectangleF(r.X + 16, r.Y, r.Width - 16 - 12 - nb * bs - (nb - 1) * 8 - 10, r.Height);
            _p.Text(txt, tr, _swRunning ? tint : Theme.Text, FitSize(txt, tr.Width, 34, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold,
                TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
            float by = r.Y + (r.Height - bs) / 2, bx = r.Right - 12 - nb * bs - (nb - 1) * 8;
            if (nb == 2 && Press(Ui.Id("wsr", id), new RectangleF(bx, by, bs, bs), "", null, false, tint, t > 0 && !_swRunning, 18, true)) StopwatchReset();
            if (Press(Ui.Id("wsp", id), new RectangleF(bx + (nb - 1) * (bs + 8), by, bs, bs), _swRunning ? "" : "", null, true, tint, true, 18, true))
                StopwatchTap();
        }
        else
        {
            Header(r, "Stopwatch", 11, true);
            _p.Text(txt, new RectangleF(r.X, r.Y + 22, r.Width, r.Height - 26), _swRunning ? tint : Theme.Text,
                FitSize(txt, r.Width - 16, 30, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, TextAlignment.Center,
                ParagraphAlignment.Center, Theme.Display);
        }
    }

    // "14:20" or "2:20 PM", the way the clock widgets write it
    string ShortClock(DateTime t) =>
        _cfg.Data.Appearance.Clock24h ? t.ToString("HH:mm", CultureInfo.CurrentCulture)
            : t.ToString("h:mm", CultureInfo.CurrentCulture) + (t.Hour < 12 ? " AM" : " PM");

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
                _p.Text($"since {ShortClock(s2)}",
                    new RectangleF(r.X + 16, r.Bottom - 26, r.Width - 32, 20), Sub, 14);
            return;
        }
        Header(r, "In VR", 11, true);
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

    // a line per device, coloured by how full it is: the name and the percentage over it
    void BatteryOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var bats = d.Vr.Batteries;
        if (bats.Count == 0)
        {
            string s = d.VrConnected ? "No battery info" : "SteamVR off";
            _p.Text(s, r, Sub, FitSize(s, r.Width - 16, 15, FontWeight.Normal, null), FontWeight.Normal, TextAlignment.Center);
            return;
        }
        // as many as fit: rows of at least 44 px, columns of at least 90 px (the emptiest ones first)
        int maxRows = Math.Max(1, (int)(r.Height / 44)), maxCols = Math.Max(1, (int)(r.Width / 90));
        if (r.Width < 120) maxRows = 1;             // a 1x1 shows the emptiest one, big
        var pick = PickBatteries(bats, Math.Min(maxRows * maxCols, 8));
        int n = pick.Count;
        if (n == 1)
        {
            // one device gets the whole card: its percentage big over a long line
            var b = bats[pick[0]];
            Gauge(r, b.pct, BatteryColor(b.pct, b.charging), Ui.Id("wbt", id * 8), $"{b.pct * 100:0}%", (b.charging ? "⚡" : "") + b.label);
            return;
        }
        int cols = 1;
        while ((n + cols - 1) / cols > maxRows) cols++;
        int rows = (n + cols - 1) / cols;
        const float pad = 6;
        float cw = (r.Width - 2 * pad) / cols, ch = MathF.Min(64, (r.Height - 2 * pad) / rows);
        float y0 = r.Y + (r.Height - rows * ch) / 2;
        float ts = BatteryRowSize(new SizeF(cw, ch), bats, pick);
        for (int j = 0; j < n; j++)
            BatteryRow(new RectangleF(r.X + pad + j % cols * cw, y0 + j / cols * ch, cw, ch), bats[pick[j]], Ui.Id("wbt", id * 8 + j), ts);
    }

    // a device as a row: the name on the left (a bolt while charging, red when low), the percentage on the right,
    // the line under both
    // the text size of battery rows this big: one size for all of them, small enough that every name fits beside its percentage
    float BatteryRowSize(SizeF cell, List<(string label, float pct, bool charging)> bats, IEnumerable<int> which)
    {
        float ts = Math.Clamp(cell.Height * 0.36f, 13, 34), iw = cell.Width - 20;
        foreach (int i in which)
        {
            var b = bats[i];
            float need = _p.MeasureWidth($"{b.pct * 100:0}%", ts, FontWeight.SemiBold, Theme.Display) + 8 +
                _p.MeasureWidth((b.charging ? "⚡" : "") + b.label, ts * 0.78f, FontWeight.SemiBold);
            if (need > iw) ts = MathF.Max(11, MathF.Floor(ts * iw / need * 0.95f * 2) / 2);
        }
        return ts;
    }

    void BatteryRow(RectangleF cell, (string label, float pct, bool charging) b, string key, float ts)
    {
        const float ip = 10;
        string pct = $"{b.pct * 100:0}%", name = (b.charging ? "⚡" : "") + b.label;
        float lt = Math.Clamp(cell.Height * 0.08f, 4, 8), iw = cell.Width - 2 * ip;
        float th = ts * 1.3f, gap = MathF.Max(4, ts * 0.2f), y = cell.Y + (cell.Height - th - gap - lt) / 2;
        var tr = new RectangleF(cell.X + ip, y, iw, th);
        float pw = _p.MeasureWidth(pct, ts, FontWeight.SemiBold, Theme.Display), ns = ts * 0.78f;
        uint nc = b.charging ? Theme.Good : b.pct <= 0.2f ? Theme.Bad : Sub;
        _p.Text(Ellipsize(name, tr.Width - pw - 8, ns, FontWeight.SemiBold), tr, nc, ns, FontWeight.SemiBold);
        _p.Text(pct, tr, Theme.Text, ts, FontWeight.SemiBold, TextAlignment.Trailing, ParagraphAlignment.Center, Theme.Display);
        Meter(new RectangleF(tr.X, tr.Bottom + gap, tr.Width, lt), b.pct, BatteryColor(b.pct, b.charging), key);
    }

    // who is in the VRChat instance: how many, and the friends among them; a tap opens the Room panel
    static (int people, int friends, string names) RoomCount(PanelData d)
    {
        var s = d.Room;
        if (s == null || !s.InRoom) return (-1, 0, "");
        int f = 0;
        var names = new System.Text.StringBuilder();
        foreach (var p in s.Here)
        {
            if (!p.Friend) continue;
            if (f < 2) names.Append(names.Length > 0 ? ", " : "").Append(p.Name);
            f++;
        }
        if (f > 2) names.Append($" +{f - 2}");
        return (s.Here.Count, f, names.ToString());
    }

    void RoomOrbit(RectangleF r, WatchWidget w, PanelData d, int id, uint tint)
    {
        bool click = _ui.Clickable(Ui.Id("wrm", id), r, out _, out bool held);
        if (held) _p.Fill(r, Theme.WithAlpha(tint, 0.12f), CardRadius);
        if (click) OnAction?.Invoke("room");
        var (n, f, names) = RoomCount(d);
        string value = n < 0 ? "–" : n.ToString();
        string cap = n < 0 ? (d.Room?.LogFound == false ? "no VRChat" : "not in a world") : n == 1 ? "person here" : "people here";
        if (w.W >= 2)
        {
            var (vr, _) = Reading(r, "Room", n < 0 ? "" : d.Room.World);
            _p.Text(value, new RectangleF(vr.X, vr.Y, 80, vr.Height), Theme.Text, FitSize(value, 76, 32, FontWeight.SemiBold, Theme.Display),
                FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, Theme.Display);
            float vw = _p.MeasureWidth(value, FitSize(value, 76, 32, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, Theme.Display) + 10;
            string line = f > 0 ? names : cap;
            _p.Text(Ellipsize(line, vr.Width - vw, 14, FontWeight.SemiBold), new RectangleF(vr.X + vw, vr.Y, vr.Width - vw, vr.Height),
                f > 0 ? Theme.Good : Sub, 14, FontWeight.SemiBold);
            return;
        }
        Header(r, "Room", 11, true);
        _p.Text(value, new RectangleF(r.X, r.Y + 22, r.Width, r.Height - 46), Theme.Text, FitSize(value, r.Width - 20, 34, FontWeight.SemiBold, Theme.Display),
            FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
        string small = f > 0 ? (f == 1 ? "1 friend" : $"{f} friends") : n < 0 ? cap : "here";
        _p.Text(Ellipsize(small, r.Width - 12, 12, FontWeight.SemiBold), new RectangleF(r.X + 6, r.Bottom - 26, r.Width - 12, 18), f > 0 ? Theme.Good : Sub, 12,
            FontWeight.SemiBold, TextAlignment.Center);
    }

    void Room95Widget(RectangleF r, RectangleF c, PanelData d, WatchWidget w, int id)
    {
        if (_ui.Clickable(Ui.Id("wrm", id), r, out _, out _)) OnAction?.Invoke("room");
        var (n, f, names) = RoomCount(d);
        if (w.W >= 2)
        {
            _ui.Text95(Fit(n < 0 ? "Not in a world" : n == 1 ? "1 person" : $"{n} people", c.Width - 8, Ui.F95, true),
                new RectangleF(c.X + 4, c.Y + c.Height / 2 - 14, c.Width - 8, 13), Blk95, true);
            _ui.Text95(Fit(f > 0 ? (f == 1 ? "1 friend" : $"{f} friends") + ": " + names : n < 0 ? "" : d.Room.World, c.Width - 8),
                new RectangleF(c.X + 4, c.Y + c.Height / 2 + 1, c.Width - 8, 13), f > 0 ? 0xFF008000 : Gry95);
            return;
        }
        Value95(c, n < 0 ? "--" : n.ToString(), f > 0 ? $"{f} friend{(f == 1 ? "" : "s")}" : "in room", Blk95);
    }

    static bool DockShows(PanelData d) =>
        d.Dock != null && d.DockFaces != null && d.DockFaces.Any &&
        d.Dock.State is HexpadLink.LinkState.Ok or HexpadLink.LinkState.ScreenOff;

    static string DockState(PanelData d) => d.Dock?.State switch
    {
        HexpadLink.LinkState.NotRunning => "not running",
        HexpadLink.LinkState.OldHexpad => "update Hexpad",
        HexpadLink.LinkState.Unplugged => "unplugged",
        HexpadLink.LinkState.ScreenOff => "screen off",
        HexpadLink.LinkState.Connecting => "connecting",
        _ => "",
    };

    static string DockWhere(HexpadLink.Snapshot s) => s.InFolder ? s.FolderName : s.PageName;

    // the six keys of the dock, 3 x 2, in r (centred); returns the grid's rect
    RectangleF DockGrid(RectangleF r, PanelData d, float gap, float radius, bool pixel)
    {
        float cell = MathF.Floor(MathF.Min((r.Width - 2 * gap) / 3, (r.Height - gap) / 2));
        float gw = 3 * cell + 2 * gap, gh = 2 * cell + gap;
        float x0 = MathF.Floor(r.X + (r.Width - gw) / 2), y0 = MathF.Floor(r.Y + (r.Height - gh) / 2);
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            var k = new RectangleF(x0 + i % 3 * (cell + gap), y0 + i / 3 * (cell + gap), cell, cell);
            _p.Fill(k, 0xFF000000, radius);
            var bmp = pixel ? d.DockFaces.Bitmaps[i] : d.DockFaces.Best(i);      // 95: the dock's own pixels
            if (bmp == null || d.Dock.Dark) continue;
            if (pixel) { _p.ImageNearest(bmp, k); continue; }
            _p.PushRoundedClip(k, radius);
            _p.Image(bmp, k);
            _p.PopRoundedClip();
        }
        return new RectangleF(x0, y0, gw, gh);
    }

    // Hexpad's stream dock on the watch: its keys as they are right now; a tap opens it as a panel
    void DockOrbit(RectangleF r, WatchWidget w, PanelData d, int id, uint tint)
    {
        bool click = _ui.Clickable(Ui.Id("wdk", id), r, out bool hover, out bool held);
        if (held) _p.Fill(r, Theme.WithAlpha(tint, 0.12f), CardRadius);
        if (click) OnAction?.Invoke("dock");
        if (!DockShows(d))
        {
            float s = MathF.Min(34, r.Height * 0.3f);
            _p.Icon("\uE80A", new RectangleF(r.X, r.Y + r.Height * 0.14f, r.Width, r.Height * 0.46f), tint, s);
            string st = DockState(d);
            _p.Text("Hexpad", new RectangleF(r.X + 8, r.Y + r.Height * 0.58f, r.Width - 16, 20), Theme.Text, 14, FontWeight.SemiBold, TextAlignment.Center);
            if (st.Length > 0)
                _p.Text(st, new RectangleF(r.X + 8, r.Y + r.Height * 0.58f + 19, r.Width - 16, 18), Sub,
                    FitSize(st, r.Width - 16, 12, FontWeight.Normal, null), FontWeight.Normal, TextAlignment.Center);
            return;
        }
        var s2 = d.Dock;
        if (w.W >= 2 && w.H == 1)
        {
            // the keys on the left, where it is on the right
            var g = DockGrid(new RectangleF(r.X + 10, r.Y + 10, (r.Height - 20) * 1.55f, r.Height - 20), d, 4, 6, false);
            float tx = g.Right + 12, tw = r.Right - 12 - tx;
            string where = DockWhere(s2);
            _p.Text(Ellipsize(where, tw, 15, FontWeight.SemiBold), new RectangleF(tx, r.Y + r.Height / 2 - 24, tw, 24), Theme.Text, 15, FontWeight.SemiBold);
            string page = s2.Ambient ? "idle" : s2.InFolder ? "folder" : $"{s2.Page + 1} / {s2.Pages}";
            _p.Text(page, new RectangleF(tx, r.Y + r.Height / 2, tw, 22), Sub, 13);
            return;
        }
        if (w.H >= 2)
        {
            var g = DockGrid(new RectangleF(r.X + 12, r.Y + 12, r.Width - 24, r.Height - 52), d, 6, 8, false);
            string where = DockWhere(s2) + (s2.InFolder ? "" : $"  ·  {s2.Page + 1}/{s2.Pages}");
            _p.Text(Ellipsize(where, r.Width - 28, 14, FontWeight.SemiBold), new RectangleF(r.X + 14, r.Bottom - 36, r.Width - 28, 22), Sub, 14,
                FontWeight.SemiBold, TextAlignment.Center);
            return;
        }
        DockGrid(RectangleF.Inflate(r, -9, -9), d, 3, 4, false);
    }
    void MediaOrbit(RectangleF r, WatchWidget w, PanelData d, int id, uint tint)
    {
        var (has, title, artist, playing) = MediaNow(d);
        const float pad = 12;
        bool tall = w.H >= 2;
        float x = r.X + pad;
        if (w.W >= 3 || tall)
        {
            float s = tall ? MathF.Min(r.Height - 2 * pad, r.Width * 0.45f) : r.Height - 2 * pad;
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
            if (has) _p.Text("NOW PLAYING", new RectangleF(x, r.Y + 12, r.Right - pad - x, 20), Sub, 12, FontWeight.SemiBold);
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
            float ns = MathF.Max(13, FitSize("Nothing playing", text.Width, 17, FontWeight.Normal, null));
            _p.Text(Ellipsize("Nothing playing", text.Width, ns, FontWeight.Normal), text, Sub, ns);
            return;
        }
        float half = MathF.Min(text.Height / 2, 34);
        float ty = text.Y + (text.Height - 2 * half) / 2;
        float ts = MathF.Min(21, half * 0.64f), asz = MathF.Min(17, half * 0.52f);
        ts = MathF.Max(MathF.Min(ts, 16), FitSize(title, text.Width, ts, FontWeight.SemiBold, null));
        asz = MathF.Max(MathF.Min(asz, 13), FitSize(artist ?? "", text.Width, asz, FontWeight.Normal, null));
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
            Gauge(r, ok ? lvl : -1, col, Ui.Id("wvg", id), pct, "volume");
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
            Gauge(g, ok ? lvl : -1, col, Ui.Id("wvg", id), pct, "");
            return;
        }
        // speaker (mute), a slider, the level
        if (Press(Ui.Id("wvs", id), new RectangleF(r.X + 12, by, bs, bs), icon, null, muted, Theme.Panel3, ok, 20, true)) Volume.ToggleMute();
        var bar = new RectangleF(r.X + 12 + bs + 24, r.Y + r.Height / 2 - 6, r.Width - bs - 12 - 24 - 24 - 62, 12);
        Slider(Ui.Id("wvb", id), bar, ok ? lvl : 0, col, ok, Volume.Set);
        _p.Text(pct, new RectangleF(r.Right - 12 - 62, r.Y, 62, r.Height), Theme.Text, FitSize(pct, 60, 20, FontWeight.SemiBold, null), FontWeight.SemiBold,
            TextAlignment.Trailing);
    }

    void StatOrbit(RectangleF r, WatchWidget w, WatchWidgets.Kind k, PanelData d, int id)
    {
        if (k.Id == "session") return;
        var (frac, value, unit, caption, tint) = StatInfo(k.Id, d);
        if (w.W == 1)
        {
            // the unit says which one it is: VRAM and RAM are both GB
            string u1 = k.Id switch { "vram" => "GB VRAM", "ram" => "GB RAM", "gpu" => "ms GPU", _ => unit };
            Gauge(r, frac, tint, Ui.Id("wsg", id), value, u1);
            return;
        }
        var (vr, lr) = Reading(r, k.Name, caption);
        string v = unit is "fps" or "ms" or "GB" ? value + " " + unit : value;
        _p.Text(v, vr, Theme.Text, FitSize(v, vr.Width, 32, FontWeight.SemiBold, Theme.Display), FontWeight.SemiBold, TextAlignment.Leading,
            ParagraphAlignment.Center, Theme.Display);
        Meter(lr, frac, tint, Ui.Id("wsg", id));
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
                    // a wide one gets the 10-point font
                    string ds = FitDate95(now, c.Width - 6);
                    float dsz = w.W >= 4 && _ui.Width95(ds, false, 13) <= c.Width - 10 ? 13 : Ui.F95;
                    _ui.Text95(ds, c, Blk95, false, TextAlignment.Center, size: dsz);
                    break;
                case "calendar": Calendar95(c, w, now); break;
                case "analog":
                    Dial95(now, MathF.Floor(c.X + c.Width / 2), MathF.Floor(c.Y + c.Height / 2), MathF.Floor(MathF.Min(c.Width, c.Height) / 2) - 5);
                    break;
                case "timer": Timer95(c, w, id); break;
                case "stopwatch": Stopwatch95(c, w, id); break;
                case "battery": Battery95(c, d, w); break;
                case "media": Media95Widget(c, d, w, id); break;
                case "dock": Dock95Widget(r, c, d, w, id); break;
                case "room": Room95Widget(r, c, d, w, id); break;
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
        float th = MathF.Ceiling(size * 1.2f);
        if (withDate && c.Height - th - 15 > 12)
            tr = new RectangleF(c.X, MathF.Floor(c.Y + (c.Height - th - 15) / 2), c.Width, th);
        BigTime95(time, ampm, tr, size, true);
        if (!withDate) return;
        _ui.Text95(FitDate95(now, c.Width - 6), new RectangleF(c.X, MathF.Min(c.Bottom - 15, tr.Bottom + 1), c.Width, 13), Blk95, false, TextAlignment.Center);
    }

    // the long date when it fits, else without the year, then the short date
    string FitDate95(DateTime now, float maxW)
    {
        var culture = CultureInfo.CurrentCulture;
        var f = culture.DateTimeFormat;
        foreach (string pattern in new[] { f.LongDatePattern, "dddd, " + f.MonthDayPattern, f.MonthDayPattern })
        {
            string s = now.ToString(pattern, culture);
            if (_ui.Width95(s) <= maxW) return s;
        }
        return Fit(now.ToString(f.ShortDatePattern, culture), maxW);
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
            _ui.Text95(now.Day.ToString(), new RectangleF(page.X, head.Bottom + 1, page.Width, page.Bottom - head.Bottom - 19), Blk95, true,
                TextAlignment.Center, size: MathF.Floor(MathF.Min(36, (page.Bottom - head.Bottom - 19) * 0.8f)));
            _ui.Text95(Fit(now.ToString("dddd", culture), page.Width - 4), new RectangleF(page.X, page.Bottom - 19, page.Width, 13), Blk95, false,
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
            bool idle = _timer == TimerState.Idle;
            _ui.Text95(txt, new RectangleF(c.X + 4, c.Y + 3, c.Width - 36, 18), _timer == TimerState.Done ? 0xFF800000 : Blk95, true, TextAlignment.Leading,
                size: 16);
            if (_ui.GlyphButton95(Ui.Id("wtp", id), new RectangleF(c.Right - 30, c.Y + 3, 26, 18), _timer == TimerState.Running ? PauseGlyph : PlayGlyph, true))
                TimerTap();
            float by = c.Bottom - 23;
            float frac = idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
            _ui.Progress95(new RectangleF(c.X + 4, MathF.Floor(c.Y + 21 + (by - c.Y - 21 - 14) / 2), c.Width - 8, 14), frac, Navy95c);
            float rw = _ui.Width95("Reset") + 10, sw = MathF.Floor((c.Width - 8 - rw - 6) / 2);
            if (_ui.Button95(Ui.Id("wtm", id), new RectangleF(c.X + 4, by, sw, 20), "-1")) TimerAdjust(-1);
            if (_ui.Button95(Ui.Id("wtr", id), new RectangleF(c.X + 7 + sw, by, c.Width - 14 - 2 * sw, 20), "Reset", !idle)) TimerReset();
            if (_ui.Button95(Ui.Id("wtq", id), new RectangleF(c.Right - 4 - sw, by, sw, 20), "+1")) TimerAdjust(1);
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
            _ui.Text95(txt, new RectangleF(c.X + 4, c.Y + 3, c.Width - 8, 18), Blk95, true, TextAlignment.Leading, size: 16);
            float bw = MathF.Floor((c.Width - 10) / 2), by = c.Bottom - 22;
            if (_ui.Button95(Ui.Id("wsp", id), new RectangleF(c.X + 2, by, bw, 19), _swRunning ? "Stop" : "Start", true, true)) StopwatchTap();
            if (_ui.Button95(Ui.Id("wsr", id), new RectangleF(c.X + 6 + bw, by, bw, 19), "Reset", t > 0 && !_swRunning)) StopwatchReset();
            return;
        }
        Value95(c, txt, _swRunning ? "running" : _ui.Width95("stopwatch") <= c.Width - 2 ? "stopwatch" : "watch", Blk95);
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
        if (_ui.Track95(Ui.Id("wvt", id), new RectangleF(c.X + 2, c.Y, c.Width - 4, 28), ref v, 0, 1, ok && !Volume.Muted, 6)) Volume.Set(v);
        bool mute = Volume.Muted;
        if (_ui.Check95(Ui.Id("wvk", id), c.X + 4, c.Bottom - 16, "Mute", ref mute, ok)) Volume.ToggleMute();
        _ui.Text95(ok ? $"{lvl * 100:0}%" : "--", new RectangleF(c.Right - 36, c.Bottom - 17, 32, 13), Blk95, false, TextAlignment.Trailing);
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
            bool low = b.pct <= 0.2f && !b.charging;
            if (bar.Width >= 12) _ui.Progress95(bar, b.pct, low ? 0xFF800000 : Theme.W95Navy);
            _ui.Text95($"{b.pct * 100:0}%", new RectangleF(c.Right - 32, y, 30, 12), low ? 0xFF800000 : Blk95, true, TextAlignment.Trailing);
        }
    }

    // the dock in a little 95 window: the key faces shrunk (nearest), where it is beside them; a click opens it
    void Dock95Widget(RectangleF r, RectangleF c, PanelData d, WatchWidget w, int id)
    {
        if (_ui.Clickable(Ui.Id("wdk", id), r, out _, out _)) OnAction?.Invoke("dock");
        if (!DockShows(d))
        {
            W95Icons.Draw(_p, W95Icons.Id.Dock, MathF.Floor(c.X + (c.Width - 16) / 2), MathF.Floor(c.Y + (c.Height - 30) / 2));
            string st = DockState(d);
            if (st.Length == 0 || _ui.Width95(st) > c.Width - 4) st = "Hexpad";       // a 1x1 says just what it is
            _ui.Text95(Fit(st, c.Width - 4), new RectangleF(c.X, MathF.Floor(c.Y + (c.Height - 30) / 2) + 17, c.Width, 13),
                Blk95, false, TextAlignment.Center);
            return;
        }
        var s = d.Dock;
        if (w.W >= 2 && w.H == 1)
        {
            float gh = MathF.Min(c.Height - 6, 30), gw = MathF.Floor((gh - 2) / 2) * 3 + 4;
            var g = DockGrid(new RectangleF(c.X + 3, c.Y + 3, gw, gh), d, 2, 0, true);
            float tx = g.Right + 5, tw = c.Right - 3 - tx;
            _ui.Text95(Fit(DockWhere(s), tw), new RectangleF(tx, c.Y + c.Height / 2 - 14, tw, 13));
            _ui.Text95(Fit(s.Ambient ? "Idle" : s.InFolder ? "Folder" : $"{s.Page + 1} of {s.Pages}", tw), new RectangleF(tx, c.Y + c.Height / 2 + 1, tw, 13), Gry95);
            return;
        }
        DockGrid(RectangleF.Inflate(c, -3, -3), d, 2, 0, true);
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
        _ui.Text95(Fit(line, lcd.Width - 8), new RectangleF(lcd.X + 4, lcd.Y + 2, lcd.Width - 6, 14), has ? 0xFF00FF00 : 0xFF00A000);
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
        if (w.H >= 2 && c.Width >= 150 && c.Bottom - (by + 24) >= 30)
            Volume95Row(new RectangleF(c.X + 4, by + 24, c.Width - 8, 30), Ui.Id("wcv", id));
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
