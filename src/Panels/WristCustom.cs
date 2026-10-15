using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;

namespace Orbit;

// The custom face: the widgets the user put on the 6 x 4 grid (WatchWidgets), in both styles.
// Modern: the Bento look (Bento.cs) - rounded cards with a caption, the value big and bold, colours by role - and motion
// (widgets cascade in when you look at the watch, spring into a new place, digits roll, lines fill).
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
    // The Bento look (Bento.cs): rounded cards on the watch page, a small UPPERCASE caption at the top-left of each, the value
    // big, bold and left-aligned with one line under it; buttons are tiles. Colours by role - the accent for time and the frame
    // numbers, purple for the CPU, blue for memory - and Ok / Warn / Bad only when a value needs attention, then its text too.
    // Sized for reading at arm's length in VR (the watch is about 13 cm wide): captions 15 px, any other text 17 px or more.
    // And motion: widgets cascade in when you look at the watch, spring into a new place, digits roll.

    const uint Sub = Bento.Sub;
    const float CardRadius = Bento.CardRadius;
    const float CapSize = 15, CapTrack = 1;    // captions: UPPERCASE, SemiBold, 1 px between the letters
    const float CapBase = 25;                  // the caption row's baseline, from the card's top
    const float SmallText = 17;                // the smallest mixed-case text (units, notes, names)
    const float SBase = 64, MBase = 66;        // the value's baseline on a 1x1 / on a one-row wide card
    const float Hero = 30;                     // a 1x1's value: every one this big (a long one shrinks only when it alone cannot fit)
    const float DigitH = 0.7f;                 // how tall digits are, in em (Segoe UI Variable Display)
    const float DigitTop = 0.72f, DigitSpan = 0.735f;   // the digits' real top (the 1's flag) and top to the round ones' overshoot, in em
    const string PlayFill = "\uF5B0", PauseFill = "\uF8AE";   // the play disc's glyphs: Segoe Fluent's filled play / pause
    const string MuteGlyph = "\uE74F";         // a speaker with a cross: a muted volume's value
    const string NotPlaying = "Not playing";  // no media: one wording on every size and opened ("Nothing playing" is too long for a 2x1)
    double _introStart = -10;
    bool _rolling;
    uint _halo;                                // the card colour behind a satellite (a gap around it)

    static uint OnColor(uint fill) => Theme.Luma(fill) > 0.55f ? 0xFF0B1220 : 0xFFFFFFFF;

    static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s[1..];

    // UPPERCASE forms, made once (captions, device names): a ToUpper per caption per frame was most of a frame's garbage
    static readonly Dictionary<string, string> _upper = new();
    static CultureInfo _upperCulture;

    static string Upper(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var culture = CultureInfo.CurrentCulture;
        if (!ReferenceEquals(culture, _upperCulture)) { _upper.Clear(); _upperCulture = culture; }
        if (!_upper.TryGetValue(s, out var u))
        {
            if (_upper.Count > 256) _upper.Clear();
            _upper[s] = u = s.ToUpper(culture);
        }
        return u;
    }

    // "0%" .. "100%", made once
    static readonly string[] PctText = MakePct();
    static string[] MakePct()
    {
        var a = new string[101];
        for (int i = 0; i <= 100; i++) a[i] = i.ToString(CultureInfo.InvariantCulture) + "%";
        return a;
    }
    static string Pct(int n) => n >= 0 && n <= 100 ? PctText[n] : n.ToString(CultureInfo.InvariantCulture) + "%";

    // the captions: each kind's forms, longest first (one array each, not one per frame)
    static readonly string[] CapBattery = { "Battery", "Batt" }, CapBatteries = { "Batteries", "Battery" }, CapTimer = { "Timer" },
        CapStopwatch = { "Stopwatch", "Watch" }, CapSession = { "In VR" }, CapSessionWide = { "Time in VR", "In VR" }, CapRoom = { "Room" },
        CapVolume = { "Volume" }, CapMedia = { "Now playing", "Playing" }, CapGraph = { "Frame time", "Frames" }, CapToday = { "Today" },
        CapHexpad = { "Hexpad" }, CapFps = { "FPS" }, CapReproj = { "Reproj" }, CapGpu = { "GPU" }, CapVram = { "VRAM" }, CapCpu = { "CPU" },
        CapRam = { "RAM" }, WideFps = { "Frame rate", "FPS" }, WideReproj = { "Reprojection", "Reproj" }, WideGpu = { "GPU time", "GPU" },
        WideRam = { "Memory", "RAM" };

    static string[] SmallCaption(string id) => id switch
    {
        "fps" => CapFps, "reproj" => CapReproj, "gpu" => CapGpu, "vram" => CapVram, "cpu" => CapCpu, _ => CapRam,
    };

    static string[] WideCaption(string id) => id switch
    {
        "fps" => WideFps, "reproj" => WideReproj, "gpu" => WideGpu, "vram" => CapVram, "cpu" => CapCpu, _ => WideRam,
    };

    static readonly string[] MediaIds = { "media.prev", "media.play", "media.next" };

    // The words of the date widgets, made when the day (or the language) changes - not on every frame
    DateTime _wordsDay;
    CultureInfo _wordsCulture;
    string _dateLine, _dayName, _monthDay, _weekNote, _year, _dayNum, _monthName, _monthShort;
    readonly string[] _capWeekday = new string[2], _capMonth = new string[2], _capDdd = new string[2];

    void Words(DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        if (now.Date == _wordsDay && ReferenceEquals(culture, _wordsCulture)) return;
        _wordsDay = now.Date;
        _wordsCulture = culture;
        _dateLine = Capital(DateText(now));
        _dayName = Capital(now.ToString("dddd", culture));
        _monthDay = now.ToString(culture.DateTimeFormat.MonthDayPattern, culture);
        _weekNote = $"week {ISOWeek.GetWeekOfYear(now)}";
        _year = now.Year.ToString(culture);
        _dayNum = now.Day.ToString(culture);
        _monthName = Capital(now.ToString("MMMM", culture));
        _monthShort = now.ToString("MMM", culture);
        string ddd = now.ToString("ddd", culture);
        _capWeekday[0] = now.ToString("dddd", culture);
        _capWeekday[1] = ddd;
        _capMonth[0] = now.ToString("MMMM", culture);
        _capMonth[1] = _monthShort;
        _capDdd[0] = ddd;
        _capDdd[1] = ddd.Substring(0, Math.Min(2, ddd.Length));
    }

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

    // Every string on the custom face (opened views included) is cut or sized to fit its place, so it is drawn without a clip per
    // string (Painter.ClipText) - the clips were a third of a frame. The layout check (--snapshot) would see one that sticks out.
    void DrawCustom(PanelData d)
    {
        _p.ClipText = false;
        try { DrawCustomFace(d); }
        finally { _p.ClipText = true; }
    }

    void DrawCustomFace(PanelData d)
    {
        var list = Layout;
        SampleSources();
        bool cards = C.Background == "cards";
        if (!cards)
        {
            _p.Fill(new RectangleF(0, 0, TW, TH), Theme.OverlayBg, Bento.PageRadius);
            _p.Stroke(new RectangleF(1, 1, TW - 2, TH - 2), Bento.PageLine, Bento.PageEdge, Bento.PageRadius - 1);
        }
        // the cards are opaque on the page; floating ones (no page behind) let a little of the world through
        uint cardFill = cards ? Theme.WithAlpha(Bento.Card, 0.96f) : Bento.Card;
        _halo = Bento.Card;
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
            float delay = (w.Y * WatchWidgets.Cols + w.X) * 0.018f;
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
                // a flat card with a faint hairline (without it the cards merge into one dark slab in the headset)
                _p.Fill(r, cardFill, CardRadius);
                _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Bento.Edge, Bento.EdgeWidth, CardRadius - 0.6f);
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
        switch (k.Id)
        {
            case "time": TimeOrbit(r, w, now, id); break;
            case "date": DateOrbit(r, now); break;
            case "calendar": CalendarOrbit(r, w, now); break;
            case "analog": AnalogOrbit(r, now, Theme.Accent); break;
            case "timer": TimerOrbit(r, w, id); break;
            case "stopwatch": StopwatchOrbit(r, w, id); break;
            case "session": SessionOrbit(r, w, d, id); break;
            case "battery": BatteryOrbit(r, w, d, id); break;
            case "media": MediaOrbit(r, w, d, id); break;
            case "dock": DockOrbit(r, w, d, id); break;
            case "room": RoomOrbit(r, w, d, id); break;
            case "volume": VolumeOrbit(r, w, id); break;
            case "graph": GraphOrbit(d.Vr, r, Theme.Accent); break;
            case "perf":
                // four small readings side by side, each with its own caption
                float qw = r.Width / 4;
                for (int j = 0; j < 4; j++) StatS(new RectangleF(r.X + j * qw, r.Y, qw, r.Height), PerfIds[j], d, Ui.Id("wg", id * 8 + j));
                break;
            default:
                if (k.Button) ButtonCell(id, w, k, r, d);
                else StatOrbit(r, w, k, d, id);
                break;
        }
    }

    static readonly string[] PerfIds = { "fps", "reproj", "gpu", "vram" };

    // When a reading needs attention: the thresholds of both styles (95 paints them with Theme.Warn / Bad, see StatInfo;
    // modern with Bento's, and the value's text too). From values already read: a number another thread moves, read twice,
    // can disagree with itself (a value in one colour, its line in another).
    static StatLevel FpsLevel(float fps, float hz) => fps >= 0 && fps < hz * 0.85f ? StatLevel.Warn : StatLevel.Ok;
    static StatLevel ReprojLevel(float pct) => pct > 25 ? StatLevel.Bad : pct > 8 ? StatLevel.Warn : StatLevel.Ok;
    static StatLevel GpuLevel(float ms, float budget) => ms > budget ? StatLevel.Bad : ms > budget * 0.85f ? StatLevel.Warn : StatLevel.Ok;
    static StatLevel VramLevel(float used, float total) => used > 0 && total > 0 && used > total * 0.92f ? StatLevel.Warn : StatLevel.Ok;
    static StatLevel CpuLevel(float c) => c > 0.9f ? StatLevel.Bad : c > 0.75f ? StatLevel.Warn : StatLevel.Ok;
    static StatLevel RamLevel(float used, float total) => used > 0 && total > 0 && used > total * 0.9f ? StatLevel.Warn : StatLevel.Ok;

    // the 95 face's colour for a level (it compares against these constants)
    static uint Tint95(StatLevel l, uint normal) => l switch { StatLevel.Bad => Theme.Bad, StatLevel.Warn => Theme.Warn, _ => normal };

    // A reading, each of its numbers read once: its fill (0..1 or -1 = no data), the value, its unit, a short caption (the widget's
    // name is shown beside it) and how it is
    (float frac, string value, string unit, string caption, StatLevel level) Reading(string id, PanelData d)
    {
        var v = d.Vr;
        float hz = v.DisplayHz, budget = 1000f / Math.Max(1, hz);
        switch (id)
        {
            case "fps":
                float fps = v.AppFps;
                return (fps < 0 ? -1 : fps / Math.Max(1, hz), fps < 0 ? "–" : $"{fps:0}", "fps", $"of {hz:0} Hz", FpsLevel(fps, hz));
            case "reproj":
                float rp = v.ReprojPct;
                return (rp < 0 ? -1 : MathF.Min(1, rp / 50), rp < 0 ? "–" : $"{rp:0}%", "reproj.", "of frames", ReprojLevel(rp));
            case "gpu":
                float ms = v.GpuMs;
                return (ms < 0 ? -1 : MathF.Min(1, ms / budget), ms < 0 ? "–" : $"{ms:0.0}", "ms", $"of {budget:0.0} ms", GpuLevel(ms, budget));
            case "vram":
                float vram = d.VramUsed, vt = d.VramTotalGB;
                return (vram < 0 || vt <= 0 ? -1 : vram / vt, vram < 0 ? "–" : $"{vram:0.0}", "GB", vt > 0 ? $"of {vt:0} GB" : "in use",
                    VramLevel(vram, vt));
            case "cpu":
                float c = Stats.Cpu;
                return (c, c < 0 ? "–" : $"{c * 100:0}%", "CPU", "total load", CpuLevel(c));
            default:
                float used = Stats.RamUsedGB, total = Stats.RamTotalGB;
                return (used < 0 || total <= 0 ? -1 : used / total, used < 0 ? "–" : $"{used:0.0}", "GB", total > 0 ? $"of {total:0} GB" : "in use",
                    RamLevel(used, total));
        }
    }

    // the 95 face's form of a reading: its colour Orbit's amber / red when it matters
    (float frac, string value, string unit, string caption, uint tint) StatInfo(string id, PanelData d)
    {
        var (frac, value, unit, caption, lv) = Reading(id, d);
        uint normal = id switch
        {
            "fps" or "cpu" => Theme.Accent,
            "reproj" or "vram" => Theme.Accent2,
            _ => Theme.Mix(Theme.Accent, Theme.Accent2, 0.5f),
        };
        return (frac, value, unit, caption, Tint95(lv, normal));
    }

    // a reading's colour when all is well: the accent for the frame numbers, purple for the CPU, blue for memory
    static uint RoleColor(string id) => id switch { "cpu" => Bento.DataPurple, "ram" or "vram" => Bento.DataBlue, _ => Theme.Accent };

    // ---- the parts every card is made of ----

    static float PadX(RectangleF r) => r.Width < 120 ? 11 : 14;

    // text with its baseline at 'by'
    void TextAt(string s, float x, float by, float w, uint c, float size, FontWeight wt, TextAlignment al = TextAlignment.Leading, string family = null)
    {
        if (string.IsNullOrEmpty(s)) return;
        var (b, h) = _p.TextLine(size, wt, family);
        _p.Text(s, new RectangleF(x, by - b, w, h), c, size, wt, al, ParagraphAlignment.Near, family);
    }

    // A card's caption: UPPERCASE, letter-spaced, grey, at the top-left (from x) - the first of its forms that fits maxW.
    // Never cut: one that fits nowhere is left out. Returns the width it took.
    float Caption(float x, RectangleF r, float maxW, string[] forms)
    {
        foreach (var f in forms)
        {
            if (string.IsNullOrEmpty(f)) continue;
            string s = Upper(f);
            float w = _p.MeasureTracked(s, CapSize, FontWeight.SemiBold, CapTrack);
            if (w > maxW) continue;
            CapText(s, x, r.Y + CapBase, w, Sub);
            return w;
        }
        return 0;
    }

    float Caption(RectangleF r, float maxW, string[] forms) => Caption(r.X + PadX(r), r, maxW, forms);

    // how wide the first form of a caption that fits maxW is (-1: none fits)
    float CaptionWidth(string[] forms, float maxW)
    {
        foreach (var f in forms)
        {
            if (string.IsNullOrEmpty(f)) continue;
            float w = _p.MeasureTracked(Upper(f), CapSize, FontWeight.SemiBold, CapTrack);
            if (w <= maxW) return w;
        }
        return -1;
    }

    // caption-style text (UPPERCASE already), its baseline at 'by' - also the opened views' small headings; Trailing: it ends at x
    void CapText(string s, float x, float by, float w, uint c, TextAlignment al = TextAlignment.Leading)
    {
        var (b, h) = _p.TextLine(CapSize, FontWeight.SemiBold);
        if (al == TextAlignment.Trailing)
        {
            w = _p.MeasureTracked(s, CapSize, FontWeight.SemiBold, CapTrack);
            x -= w;
        }
        _p.TrackedText(s, new RectangleF(x, by - b, w + 6, h), c, CapSize, FontWeight.SemiBold, CapTrack, TextAlignment.Leading, ParagraphAlignment.Near);
    }

    // the caption row: the caption on the left (its first form that fits beside the note), a note on the right in what is
    // left (grey 17 px, cut to fit, left out when there is no room)
    void CaptionRow(RectangleF r, string note, string[] forms) => CaptionRow(r, r.X + PadX(r), r.Right - PadX(r), note, Sub, forms);

    void CaptionRow(RectangleF r, float x, float right, string note, uint noteColor, string[] forms)
    {
        float iw = right - x;
        float nw = string.IsNullOrEmpty(note) ? 0 : _p.MeasureWidth(note, SmallText, FontWeight.SemiBold);
        float cw = Caption(x, r, nw > 0 ? MathF.Max(iw * 0.4f, iw - nw - 10) : iw, forms);
        if (nw <= 0) return;
        float room = iw - (cw > 0 ? cw + 10 : 0);
        if (room < 36) return;
        TextAt(Ellipsize(note, room, SmallText, FontWeight.SemiBold), right - room, r.Y + CapBase, room, noteColor, SmallText, FontWeight.SemiBold,
            TextAlignment.Trailing);
    }

    // A value and its unit on one baseline: the value bold (as big as 'size', smaller down to 'min' until both fit maxW), the
    // unit grey after it (left out when even the smallest value leaves it no room). roll: the value rolls when it changes.
    // Returns the width it took.
    float ValueUnit(float x, float by, float maxW, string value, string unit, float size, float min, uint vc, string roll = null)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        float uw = string.IsNullOrEmpty(unit) ? 0 : _p.MeasureWidth(unit, SmallText, FontWeight.SemiBold) + 4;
        float vs = FitSize(value, maxW - uw, size, FontWeight.Bold, Theme.Display);
        if (vs < min && uw > 0) { uw = 0; vs = FitSize(value, maxW, size, FontWeight.Bold, Theme.Display); }
        vs = MathF.Max(min, vs);
        float vw = ValueText(x, by, value, vs, vc, roll);
        if (uw > 0) TextAt(unit, x + vw + 4, by, uw + 2, Sub, SmallText, FontWeight.SemiBold);
        return vw + uw;
    }

    // a value, bold, its baseline at 'by' (roll: it rolls when it changes); returns its width
    float ValueText(float x, float by, string value, float vs, uint vc, string roll)
    {
        float vw = _p.MeasureWidth(value, vs, FontWeight.Bold, Theme.Display);
        var (vb, vh) = _p.TextLine(vs, FontWeight.Bold, Theme.Display);
        var vr = new RectangleF(x, by - vb, vw + 4, vh);
        if (roll != null) RollText(roll, value, vr, vc, vs, FontWeight.Bold, Theme.Display, TextAlignment.Leading);
        else _p.Text(value, vr, vc, vs, FontWeight.Bold, TextAlignment.Leading, ParagraphAlignment.Near, Theme.Display);
        return vw;
    }

    // A 1x1's value and unit: the value Hero big (smaller only when it alone cannot fit), the unit on its baseline 17 px, else
    // 15 px; us = 0: no room beside it (the caller puts it up in the caption row, or leaves it out)
    (float vs, float us) HeroFit(string value, string unit, float iw)
    {
        float vs = MathF.Max(20, FitSize(value, iw, Hero, FontWeight.Bold, Theme.Display));
        if (string.IsNullOrEmpty(unit)) return (vs, 0);
        float vw = _p.MeasureWidth(value, vs, FontWeight.Bold, Theme.Display) + 4;
        if (vw + _p.MeasureWidth(unit, SmallText, FontWeight.SemiBold) <= iw) return (vs, SmallText);
        if (vw + _p.MeasureWidth(unit, CapSize, FontWeight.SemiBold) <= iw) return (vs, CapSize);
        return (vs, 0);
    }

    // A plain line: the track and, from the left, the value in colour (it glides when the value changes)
    void Meter(RectangleF bar, float frac, uint tint, string key)
    {
        float f = _ui.Spring(key, frac < 0 ? 0 : Math.Clamp(frac, 0, 1), 140, 20, 0.08f);
        float h = bar.Height;
        _p.Fill(bar, Bento.Track, h / 2);
        if (frac < 0 || f < 0.002f) return;
        _p.Fill(new RectangleF(bar.X, bar.Y, MathF.Max(h, bar.Width * MathF.Min(f, 1)), h), tint, h / 2);
    }

    // The 1x1 card (and a column of the perf strip): caption, the value with its unit, one line under it (meter = null: none).
    // Every 1x1's value is as big as the others (Hero): a unit that does not fit beside it moves up into the caption row, at the
    // right (or, no room there either, is left out - the caption says what it is).
    void Small(RectangleF r, string[] caption, string value, string unit, float frac, uint bar, uint vc, string meter, string roll = null)
    {
        float pad = PadX(r), iw = r.Width - 2 * pad, x = r.X + pad, by = r.Y + SBase;
        var (vs, us) = HeroFit(value, unit, iw);
        float uw = us == 0 && !string.IsNullOrEmpty(unit) ? _p.MeasureWidth(unit, SmallText, FontWeight.SemiBold) : 0;
        if (uw > 0 && CaptionWidth(caption, iw - uw - 10) >= 0)
        {
            Caption(r, iw - uw - 10, caption);
            TextAt(unit, r.Right - pad - uw - 2, r.Y + CapBase, uw + 2, Sub, SmallText, FontWeight.SemiBold, TextAlignment.Trailing);
        }
        else Caption(r, iw, caption);
        if (!string.IsNullOrEmpty(value))
        {
            float vw = ValueText(x, by, value, vs, vc, roll);
            if (us > 0) TextAt(unit, x + vw + 4, by, iw - vw - 2, Sub, us, FontWeight.SemiBold);
        }
        if (meter != null) Meter(new RectangleF(x, r.Bottom - 16, iw, 4), frac, bar, meter);
    }

    // a one-row wide card: caption and a note, the value bigger with its unit, a thicker line under it
    void Wide(RectangleF r, string[] caption, string note, string value, string unit, float frac, uint bar, uint vc, string meter, string roll = null)
    {
        float pad = PadX(r), iw = r.Width - 2 * pad;
        CaptionRow(r, note, caption);
        ValueUnit(r.X + pad, r.Y + MBase, iw, value, unit, 40, 24, vc, roll);
        if (meter != null) Meter(new RectangleF(r.X + pad, r.Bottom - 17, iw, 6), frac, bar, meter);
    }

    // a reading as a 1x1
    void StatS(RectangleF r, string id, PanelData d, string key)
    {
        var (frac, value, _, _, lv) = Reading(id, d);
        bool none = value == "–";
        string unit = none ? null : id switch { "fps" => FpsUnit(d.Vr.DisplayHz), "gpu" => "ms", "vram" or "ram" => "GB", _ => null };
        Small(r, SmallCaption(id), value, unit, frac, Bento.Level(lv, RoleColor(id)), none ? Bento.Faint : Bento.Level(lv, Bento.Text), key);
    }

    // "/90": a 1x1 frame rate's unit, made when the headset's rate changes
    string _fpsUnit;
    float _fpsUnitHz = -1;
    string FpsUnit(float hz)
    {
        if (hz != _fpsUnitHz) { _fpsUnitHz = hz; _fpsUnit = $"/{hz:0}"; }
        return _fpsUnit;
    }

    // A value with a plain line under it (filled to how much it is), the unit or a note small under the line - the opened views'
    // big readings. vc: the value's colour (Warn / Bad when it needs attention, like the cards); unitIcon: a glyph before the
    // unit (a charging bolt); roll: the key of a value that rolls when it changes (a timer), null for plain text
    void Gauge(RectangleF r, float frac, uint tint, string key, string value, string unit, uint vc, float valueSize = 0, string roll = null,
        string unitIcon = null)
    {
        float pad = MathF.Max(10, r.Width * 0.12f), iw = r.Width - 2 * pad;
        if (iw < 16) return;
        bool hasValue = !string.IsNullOrEmpty(value), hasUnit = !string.IsNullOrEmpty(unit);
        float lt = Math.Clamp(r.Height * 0.045f, 4, 10);
        float vs = valueSize > 0 ? valueSize : hasValue ? FitSize(value, iw, Math.Clamp(r.Height * 0.34f, 14, 96), FontWeight.SemiBold, Theme.Display) : 0;
        float iconW = unitIcon != null ? 16 : 0;
        float us = hasUnit ? MathF.Max(SmallText, FitSize(unit, r.Width - 12 - iconW, Math.Clamp(r.Height * 0.13f, SmallText, 22), FontWeight.SemiBold, null)) : 0;
        float vh = vs * 1.1f, gap = MathF.Max(5, vs * 0.14f), uh = us * 1.4f;
        float total = (hasValue ? vh + gap : 0) + lt + (hasUnit ? gap + uh : 0);
        float y = r.Y + (r.Height - total) / 2;
        if (hasValue)
        {
            var vr = new RectangleF(r.X, y, r.Width, vh);
            // plain text: a frame rate changes every half second, rolling it would never stop
            if (roll != null) RollText(roll, value, vr, vc, vs, FontWeight.SemiBold, Theme.Display);
            else _p.Text(value, vr, vc, vs, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display);
            y += vh + gap;
        }
        Meter(new RectangleF(r.X + pad, y, iw, lt), frac, tint, key);
        if (!hasUnit) return;
        var ur = new RectangleF(r.X, y + lt + gap, r.Width, uh);
        if (unitIcon == null) { _p.Text(unit, ur, Sub, us, FontWeight.SemiBold, TextAlignment.Center); return; }
        float tw = MathF.Min(_p.MeasureWidth(unit, us, FontWeight.SemiBold), r.Width - 12 - iconW);
        float ux = r.X + (r.Width - iconW - tw) / 2;
        _p.Icon(unitIcon, new RectangleF(ux - 2, ur.Y, iconW + 2, uh), Bento.Text, 15);
        _p.Text(unit, new RectangleF(ux + iconW, ur.Y, tw + 2, uh), Sub, us, FontWeight.SemiBold);
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

    // at least this big to hit with a laser, whatever is drawn
    static RectangleF MinHit(RectangleF r, float min = 40) =>
        RectangleF.Inflate(r, MathF.Max(0, (min - r.Width) / 2), MathF.Max(0, (min - r.Height) / 2));

    // Orbit's buttons, springy when pressed: round ones inside a widget are discs (Tile; the main one in the accent), square ones
    // (the opened views' presets) tiles with a hairline. On, it is filled: with the accent (accentFill), else with 'fill' (a muted
    // speaker's red, a finished timer's amber).
    bool Press(string id, RectangleF r, string icon, string label, bool on, bool enabled = true, float iconSize = 24, bool circle = false,
        bool accentFill = true, uint fill = 0)
    {
        bool click = _ui.Clickable(id, MinHit(r), out bool hover, out bool held) && enabled;
        float s = _ui.Spring(Ui.Id(id, "#s"), held && enabled ? 0.9f : 1f, 420, 18);
        var b = new RectangleF(r.X + r.Width * (1 - s) / 2, r.Y + r.Height * (1 - s) / 2, r.Width * s, r.Height * s);
        float rad = circle ? MathF.Min(b.Width, b.Height) / 2 : MathF.Min(CardRadius * s, MathF.Min(b.Width, b.Height) / 2);
        uint rest = Bento.Tile, onFill = accentFill ? Bento.AccentFill : fill;
        uint bg = !enabled ? Theme.WithAlpha(rest, 0.5f) : on ? onFill : hover ? Bento.TileHover : rest;
        _p.Fill(b, bg, rad);
        if (!circle && !on) _p.Stroke(RectangleF.Inflate(b, -0.6f, -0.6f), Bento.Edge, Bento.EdgeWidth, rad);
        uint fg = !enabled ? Bento.Faint : on ? (accentFill ? Bento.OnAccent : OnColor(onFill)) : Bento.Text;
        // a label is mixed case: 17 px and up (the size it springs to while held included)
        float ls = MathF.Min(18, MathF.Max(SmallText, r.Height * 0.3f));
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

    // A button that is its card: Tile when off, the accent when on (its label bold then); a 1x1 has its icon at the top-left and
    // its name at the bottom, a wide one both side by side. Springy when pressed.
    bool TileButton(string id, RectangleF r, string icon, string label, bool on, bool wide)
    {
        bool click = _ui.Clickable(id, r, out bool hover, out bool held);
        float s = _ui.Spring(Ui.Id(id, "#s"), held ? 0.92f : 1f, 420, 18);
        bool scaled = MathF.Abs(s - 1) > 0.001f;
        if (scaled) _p.PushScale(s, r.X + r.Width / 2, r.Y + r.Height / 2);
        _p.Fill(r, on ? Bento.AccentFill : hover ? Bento.TileHover : Bento.Tile, CardRadius);
        if (!on) _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Bento.Edge, Bento.EdgeWidth, CardRadius - 0.6f);
        uint fg = on ? Bento.OnAccent : Bento.Text;
        var lw = on ? FontWeight.Bold : FontWeight.SemiBold;
        float pad = PadX(r);
        if (wide)
        {
            const float ic = 22;
            if (icon != null) _p.Icon(icon, new RectangleF(r.X + pad - 2, r.Y, ic + 4, r.Height), fg, ic);
            float lx = r.X + pad + (icon != null ? ic + 10 : 0), room = r.Right - pad - lx;
            float ls = MathF.Max(15, FitSize(label, room, SmallText, lw, null));
            _p.Text(Ellipsize(label, room, ls, lw), new RectangleF(lx, r.Y, room + 2, r.Height), fg, ls, lw);
        }
        else
        {
            const float ic = 24;
            if (icon != null) _p.Icon(icon, new RectangleF(r.X + pad - 2, r.Y + 10, ic + 4, ic + 4), fg, ic);
            float iw = r.Width - 2 * pad;
            float ls = MathF.Max(15, FitSize(label, iw, SmallText, lw, null));
            TextAt(Ellipsize(label, iw, ls, lw), r.X + pad, r.Bottom - 15, iw + 2, fg, ls, lw);
        }
        if (scaled) _p.PopScale();
        return click;
    }

    // a tile's name: the kind's, shorter on a 1x1
    static string TileName(WatchWidgets.Kind k, bool wide) => wide ? k.Name : k.Id switch { "night" => "Night", "status" => "Status", _ => k.Name };

    void ButtonCell(int id, WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d)
    {
        string icon = k.Id == "desktops" && !d.DesktopsVisible ? Theme.IcoHide : k.Icon;
        string label = k.Id == "layout" ? d.LayoutName : TileName(k, w.W >= 2);
        if (TileButton(Ui.Id("wcb", id), r, icon, label, IsOn(k.Id, d), w.W >= 2)) OnAction?.Invoke(k.Id);
    }

    // ---- the widgets ----

    // The time: the date on top (the one header in its own words: grey, 17 px and up; a caption would only say "time"), the
    // time big, bold and left. Two rows and more: the seconds as a line along the bottom with ":42" at its end; one row: the
    // seconds small on the time's baseline (AM / PM there instead, on a 12-hour clock). The digits keep 10 px from the date
    // line (its descenders included) and from the seconds line: they take the size of the space left between them.
    void TimeOrbit(RectangleF r, WatchWidget w, DateTime now, int id)
    {
        bool h24 = _cfg.Data.Appearance.Clock24h;
        var culture = CultureInfo.CurrentCulture;
        string t = now.ToString(h24 ? "HH:mm" : "h:mm", culture);
        string ampm = h24 ? null : now.Hour < 12 ? "AM" : "PM";
        float pad = PadX(r), iw = r.Width - 2 * pad;
        bool tall = w.H >= 2;
        float ds = tall ? MathF.Round(Math.Clamp(r.Height * 0.085f, SmallText, 24)) : SmallText;
        var (db, _) = _p.TextLine(ds, FontWeight.SemiBold);
        float dBase = r.Y + 9 + db;
        Words(now);
        TextAt(Ellipsize(_dateLine, iw, ds, FontWeight.SemiBold), r.X + pad, dBase, iw + 2, Sub, ds, FontWeight.SemiBold);
        const float Gap = 10;
        float top = dBase + ds * 0.24f + Gap, bottom;           // under the date's descenders (a "y", a "g")
        if (tall)
        {
            float lt = r.Height >= 300 ? 6 : 4;
            string sec = ":" + now.ToString("ss", culture);
            float sw = _p.MeasureWidth(":00", SmallText, FontWeight.SemiBold) + 2;
            var line = new RectangleF(r.X + pad, r.Bottom - 18 - lt / 2, iw - sw - 10, lt);
            _p.Fill(line, Bento.Track, lt / 2);
            float f = now.Second / 60f;
            if (f > 0) _p.Fill(new RectangleF(line.X, line.Y, MathF.Max(lt, line.Width * f), lt), Theme.Accent, lt / 2);
            float secBase = line.Y + lt / 2 + SmallText * DigitH / 2;
            TextAt(sec, r.Right - pad - sw - 2, secBase, sw + 2, Sub, SmallText, FontWeight.SemiBold, TextAlignment.Trailing);
            bottom = MathF.Min(line.Y, secBase - SmallText * DigitTop) - Gap;      // above the line and the ":42" at its end
        }
        else bottom = r.Bottom - 14;
        // one row: the seconds (or AM / PM) small beside it; two rows: only AM / PM
        string small = tall ? ampm : ampm ?? now.ToString("ss", culture);
        float ss = 0, sw2 = 0;
        float w100 = _p.MeasureWidth(t, 100, FontWeight.Bold, Theme.Display);
        float size = MathF.Min(100 * iw / w100, (bottom - top) / DigitSpan);
        if (small != null)
        {
            ss = tall ? MathF.Max(SmallText, MathF.Round(size * 0.22f)) : SmallText;
            sw2 = _p.MeasureWidth(small == ampm ? small : "00", ss, FontWeight.SemiBold) + 6;
            size = MathF.Min(100 * (iw - sw2) / w100, (bottom - top) / DigitSpan);
        }
        size = MathF.Max(17, MathF.Floor(size * 2) / 2);
        float by = top + size * DigitTop + (bottom - top - size * DigitSpan) / 2;      // the digits centred between the two
        var (tb, th) = _p.TextLine(size, FontWeight.Bold, Theme.Display);
        float tw = _p.MeasureWidth(t, size, FontWeight.Bold, Theme.Display);
        RollText(Ui.Id("wtt", id), t, new RectangleF(r.X + pad, by - tb, tw + 2, th), Bento.Text, size, FontWeight.Bold, Theme.Display,
            TextAlignment.Leading);
        if (small != null) TextAt(small, r.X + pad + tw + 6, by, sw2, Sub, ss, FontWeight.SemiBold);
    }

    // the date: the weekday in the accent (accent text only this big and bold), the date big under it; the week on the right
    void DateOrbit(RectangleF r, DateTime now)
    {
        float pad = PadX(r), iw = r.Width - 2 * pad;
        Words(now);
        CaptionRow(r, _weekNote, CapToday);
        string day = _dayName, date = _monthDay;
        TextAt(Ellipsize(day, iw, SmallText, FontWeight.Bold), r.X + pad, r.Y + 49, iw + 2, Theme.Accent, SmallText, FontWeight.Bold);
        float bs = MathF.Max(20, FitSize(date, iw, 30, FontWeight.Bold, Theme.Display));
        TextAt(Ellipsize(date, iw, bs, FontWeight.Bold, Theme.Display), r.X + pad, r.Y + 81, iw + 2, Bento.Text, bs, FontWeight.Bold,
            TextAlignment.Leading, Theme.Display);
    }

    void CalendarOrbit(RectangleF r, WatchWidget w, DateTime now)
    {
        var culture = CultureInfo.CurrentCulture;
        float pad = PadX(r), iw = r.Width - 2 * pad;
        Words(now);
        if (w.H >= 2)
        {
            // the month at a glance: its name as the caption with the year beside it, the days under them, today a filled circle
            // in the accent, the weekend grey
            CaptionRow(r, _year, _capMonth);
            var g = new RectangleF(r.X + 10, r.Y + 36, r.Width - 20, r.Height - 46);
            int first = (int)culture.DateTimeFormat.FirstDayOfWeek;
            var day1 = new DateTime(now.Year, now.Month, 1);
            int offset = ((int)day1.DayOfWeek - first + 7) % 7, days = DateTime.DaysInMonth(now.Year, now.Month);
            int weeks = (offset + days + 6) / 7;
            float cw = g.Width / 7, rh = g.Height / (weeks + 1);
            for (int c = 0; c < 7; c++)
            {
                _p.Text(DayHead(culture, (DayOfWeek)((first + c) % 7)), new RectangleF(g.X + c * cw, g.Y, cw, rh), Sub, CapSize,
                    FontWeight.SemiBold, TextAlignment.Center);
            }
            for (int dd = 1; dd <= days; dd++)
            {
                int cell = offset + dd - 1, col = cell % 7, row = cell / 7 + 1;
                var cr = new RectangleF(g.X + col * cw, g.Y + row * rh, cw, rh);
                var dow = (DayOfWeek)((first + col) % 7);
                bool today = dd == now.Day, weekend = dow is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (today)
                {
                    float d2 = MathF.Min(cw, rh) - 1;
                    _p.Circle(cr.X + cw / 2, cr.Y + rh / 2, d2 / 2, Bento.AccentFill);
                }
                _p.Text(DayNumber(dd), cr, today ? Bento.OnAccent : weekend ? Sub : Bento.Text, CapSize, today ? FontWeight.Bold : FontWeight.SemiBold,
                    TextAlignment.Center);
            }
            return;
        }
        // the weekday as the caption, the day big with the month after it (the year on a wide one)
        if (w.W >= 2)
        {
            CaptionRow(r, _year, _capWeekday);
            ValueUnit(r.X + pad, r.Y + MBase, iw, _dayNum, _monthName, 40, 24, Bento.Text);
            return;
        }
        Small(r, _capDdd, _dayNum, _monthShort, 0, 0, Bento.Text, null);
    }

    // "1" .. "31", made once; the month grid's weekday heads ("H", "SZE"), UPPERCASE, two letters at most
    static readonly string[] DayNumbers = MakeDays();
    static string[] MakeDays()
    {
        var a = new string[32];
        for (int i = 0; i < a.Length; i++) a[i] = i.ToString(CultureInfo.InvariantCulture);
        return a;
    }
    static string DayNumber(int d) => d >= 0 && d < DayNumbers.Length ? DayNumbers[d] : d.ToString(CultureInfo.InvariantCulture);

    static readonly string[] _heads = new string[7];
    static CultureInfo _headsCulture;

    static string DayHead(CultureInfo culture, DayOfWeek dow)
    {
        if (!ReferenceEquals(culture, _headsCulture))
        {
            _headsCulture = culture;
            for (int i = 0; i < 7; i++)
            {
                string n = culture.DateTimeFormat.GetShortestDayName((DayOfWeek)i);
                _heads[i] = n.Substring(0, Math.Min(2, n.Length)).ToUpper(culture);
            }
        }
        return _heads[(int)dow];
    }

    // Orbit's dial: dots for the hours (bigger on the quarters), the hands in the text colour, and the seconds
    // as the only coloured thing: a satellite circling the rim
    void AnalogOrbit(RectangleF r, DateTime now, uint tint)
    {
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rad = MathF.Min(r.Width, r.Height) / 2 - 12;
        float rim = rad * 0.94f;
        _p.Arc(cx, cy, rim, 0, 1, Bento.Track, MathF.Max(2, rad * 0.02f));
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * MathF.Tau;
            bool quarter = i % 3 == 0;
            float dr = rad * 0.76f;
            _p.Circle(cx + dr * MathF.Sin(a), cy - dr * MathF.Cos(a), quarter ? MathF.Max(3, rad * 0.05f) : MathF.Max(2, rad * 0.026f),
                quarter ? Bento.Text : Sub);
        }
        float sec = now.Second, m = now.Minute + sec / 60f, h = now.Hour % 12 + m / 60f;
        void Hand(float turns, float len, float width)
        {
            float a = turns * MathF.Tau;
            _p.RoundLine(cx, cy, cx + len * MathF.Sin(a), cy - len * MathF.Cos(a), Bento.Text, width);
        }
        Hand(h / 12, rad * 0.46f, MathF.Max(5, rad * 0.075f));
        Hand(m / 60, rad * 0.66f, MathF.Max(3.5f, rad * 0.048f));
        float sa = sec / 60 * MathF.Tau, sx = cx + rim * MathF.Sin(sa), sy = cy - rim * MathF.Cos(sa);
        _p.Circle(sx, sy, MathF.Max(5, rad * 0.075f), _halo);
        _p.Circle(sx, sy, MathF.Max(3.5f, rad * 0.05f), tint);
        _p.Circle(cx, cy, MathF.Max(4, rad * 0.06f), Bento.Text);
        _p.Circle(cx, cy, MathF.Max(2, rad * 0.03f), tint);
    }

    void TimerOrbit(RectangleF r, WatchWidget w, int id)
    {
        double now = Clock.Now, left = TimerLeft(now);
        float frac = _timer == TimerState.Idle ? 1 : (float)(left / Math.Max(1, Math.Max(_timerTotal, left)));
        bool done = _timer == TimerState.Done;
        uint bar = done ? Bento.Warn : Theme.Accent;
        uint vc = done ? Bento.Warn : _timer == TimerState.Paused ? Sub : Bento.Text;
        string txt = done ? "Done" : Clock2(left);
        string state = _timer switch { TimerState.Running => "running", TimerState.Paused => "paused", TimerState.Done => "time is up", _ => "tap to start" };
        float pad = PadX(r), iw = r.Width - 2 * pad;
        if (w.H >= 2)
        {
            // the time big over its line; - (reset once it runs) / start-pause / + at the bottom
            CaptionRow(r, state, CapTimer);
            const float bs = 44, gap = 14;
            float by = r.Bottom - 14 - bs, x0 = r.X + (r.Width - 3 * bs - 2 * gap) / 2;
            ValueUnit(r.X + pad, r.Y + 94, iw, txt, null, 52, 28, vc, Ui.Id("wtx", id));
            Meter(new RectangleF(r.X + pad, r.Y + 112, iw, 6), frac, bar, Ui.Id("wtr", id));
            bool idle = _timer == TimerState.Idle, tap = false;
            if (Press(Ui.Id("wtm", id), new RectangleF(x0, by, bs, bs), idle ? "\uE738" : "\uE72C", null, false, true, 18, true))
            {
                if (idle) TimerAdjust(-1); else TimerReset();
            }
            if (Press(Ui.Id("wtp", id), new RectangleF(x0 + bs + gap, by, bs, bs), _timer == TimerState.Running ? PauseFill : PlayFill, null, true,
                    true, 18, true)) tap = true;
            if (Press(Ui.Id("wtq", id), new RectangleF(x0 + 2 * (bs + gap), by, bs, bs), "\uE710", null, false, true, 18, true)) TimerAdjust(1);
            if (tap) TimerTap();
            return;
        }
        // a tap on the card opens the timer
        if (w.W >= 2) Wide(r, CapTimer, state, txt, null, frac, bar, vc, Ui.Id("wtr", id), Ui.Id("wtx", id));
        else Small(r, CapTimer, txt, null, frac, bar, vc, Ui.Id("wtr", id), Ui.Id("wtx", id));
    }

    void StopwatchOrbit(RectangleF r, WatchWidget w, int id)
    {
        double t = StopwatchTime(Clock.Now);
        bool wide = w.W >= 2;
        string txt = StopwatchText(t, wide);
        uint vc = _swRunning ? Bento.Text : Sub;
        float pad = PadX(r), iw = r.Width - 2 * pad;
        if (wide)
        {
            // the time left, reset (once stopped) and start-stop right; a dot after the caption while it runs
            int nb = t > 0 && !_swRunning ? 2 : 1;
            const float bs = 44, gap = 8;
            float bx = r.Right - pad - nb * bs - (nb - 1) * gap, by = r.Y + 32;
            float cw = Caption(r, iw, CapStopwatch);
            if (_swRunning) _p.Circle(r.X + pad + cw + 10, r.Y + CapBase - 5, 4, Theme.Accent);
            ValueUnit(r.X + pad, by + bs / 2 + 13, bx - 10 - r.X - pad, txt, null, 36, 20, vc);
            if (nb == 2 && Press(Ui.Id("wsr", id), new RectangleF(bx, by, bs, bs), "\uE72C", null, false, true, 18, true)) StopwatchReset();
            if (Press(Ui.Id("wsp", id), new RectangleF(bx + (nb - 1) * (bs + gap), by, bs, bs), _swRunning ? PauseFill : PlayFill, null, true,
                    true, 18, true))
                StopwatchTap();
            return;
        }
        // a 1x1: its caption in words like the others ("WATCH" when "STOPWATCH" is too long), a dot after it while it runs; a tap on
        // the card opens it
        float cw1 = Caption(r, iw - 14, CapStopwatch);
        if (_swRunning) _p.Circle(r.X + pad + cw1 + 9, r.Y + CapBase - 5, 4, Theme.Accent);
        ValueUnit(r.X + pad, r.Y + SBase, iw, txt, null, Hero, 20, vc);
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

    // The time in VR as a value and a unit - "1:23", or "42" "min" in the first hour - and how far into its current hour it is
    // (the card's line). The words are made once a minute.
    int _sessMinutes = -1;
    string _sessValue, _sessUnit;
    (string value, string unit, float hour) SessionValue(PanelData d)
    {
        if (d.VrSince is not { } since) return ("–", null, -1);
        var span = DateTime.Now - since;
        int minutes = Math.Max(0, (int)span.TotalMinutes);
        if (minutes != _sessMinutes)
        {
            _sessMinutes = minutes;
            if (span.TotalHours >= 1) { _sessValue = $"{(int)span.TotalHours}:{span.Minutes:00}"; _sessUnit = null; }
            else { _sessValue = minutes.ToString(CultureInfo.InvariantCulture); _sessUnit = "min"; }
        }
        return (_sessValue, _sessUnit, (float)(Math.Max(0, span.TotalMinutes) % 60 / 60));
    }

    // a 1x1's one graphic: a thin line, how far into the hour the session is (it starts over at every full hour)
    void SessionOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var (val, unit, hour) = SessionValue(d);
        uint vc = val == "–" ? Bento.Faint : Bento.Text;
        if (w.W >= 2)
        {
            Wide(r, CapSessionWide, d.VrSince is { } s2 ? SinceNote(s2) : null, val, unit, hour, Theme.Accent, vc, Ui.Id("wsl", id), Ui.Id("wse", id));
            return;
        }
        Small(r, CapSession, val, unit, hour, Theme.Accent, vc, Ui.Id("wsl", id), Ui.Id("wse", id));
    }

    // "since 7:08 AM", made when the start or the clock style changes
    DateTime _sinceAt;
    bool _since24;
    string _sinceNote;
    string SinceNote(DateTime since)
    {
        bool h24 = _cfg.Data.Appearance.Clock24h;
        if (since != _sinceAt || h24 != _since24 || _sinceNote == null)
        {
            _sinceAt = since;
            _since24 = h24;
            _sinceNote = "since " + ShortClock(since);
        }
        return _sinceNote;
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

    // the modern watch's battery rule, on the percentage as it is shown (so "20%" never shows in two colours): Ok above 30 %,
    // Warn 16-30 %, Bad at 15 % or less. Charging adds a bolt, never another colour.
    // The modern faces (the fixed ones too) write the percentage from this number, so its text and its colour cannot disagree.
    static int ShownPct(float pct) => (int)Math.Round((double)(pct * 100), MidpointRounding.AwayFromZero);

    static StatLevel BatteryLevel(int shown) => shown <= 15 ? StatLevel.Bad : shown <= 30 ? StatLevel.Warn : StatLevel.Ok;

    static StatLevel BatteryLevel(float pct) => BatteryLevel(ShownPct(pct));

    // the emptiest device (the first of equals)
    static int Emptiest(List<(string label, float pct, bool charging)> bats)
    {
        int best = 0;
        for (int i = 1; i < bats.Count; i++) if (bats[i].pct < bats[best].pct) best = i;
        return best;
    }

    // a device's name the way the cards write it: UPPERCASE grey 15 px, a white bolt in front while charging; returns its width
    float DeviceName(float x, float by, string name, bool charging, float maxW)
    {
        float x0 = x;
        if (charging) { _p.Icon(Bolt, new RectangleF(x - 2, by - 16, 16, 17), Bento.Text, 15); x += 14; }
        string s = Ellipsize(name, maxW - (x - x0), CapSize, FontWeight.SemiBold);
        TextAt(s, x, by, maxW - (x - x0) + 2, Sub, CapSize, FontWeight.SemiBold);
        return x - x0 + _p.MeasureWidth(s, CapSize, FontWeight.SemiBold);
    }

    const string Bolt = "\uE945";

    void BatteryOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var bats = d.Vr.Batteries;
        float pad = PadX(r), iw = r.Width - 2 * pad;
        if (w.W == 1 && w.H == 1)
        {
            // The emptiest one: its percentage as big as every 1x1's value (red / amber text when low) and its line; which one it
            // is (a bolt in front while charging) up in the caption row at the right - or, with no room for both, as the caption.
            var line = new RectangleF(r.X + pad, r.Bottom - 16, iw, 4);
            if (bats.Count == 0)
            {
                Caption(r, iw, CapBattery);
                ValueUnit(r.X + pad, r.Y + SBase, iw, "–", null, Hero, 20, Bento.Faint);
                Meter(line, -1, Bento.Ok, Ui.Id("wbt", id * 8));
                return;
            }
            var b = bats[Emptiest(bats)];
            int shown = ShownPct(b.pct);
            var lv = BatteryLevel(shown);
            string name = Upper(b.label);
            float meta = _p.MeasureWidth(name, CapSize, FontWeight.SemiBold) + (b.charging ? 14 : 0);
            float mx = r.X + pad;
            if (CaptionWidth(CapBattery, iw - meta - 8) >= 0)
            {
                Caption(r, iw - meta - 8, CapBattery);
                mx = r.Right - pad - meta;
            }
            DeviceName(mx, r.Y + CapBase, name, b.charging, r.Right - pad - mx);
            string pct = Pct(shown);
            ValueText(r.X + pad, r.Y + SBase, pct, HeroFit(pct, null, iw).vs, Bento.Level(lv, Bento.Text), null);
            Meter(line, b.pct, Bento.Level(lv, Bento.Ok), Ui.Id("wbt", id * 8));
            return;
        }
        Caption(r, iw, CapBatteries);
        if (bats.Count == 0)
        {
            string s = d.VrConnected ? "No battery info" : "SteamVR off";
            TextAt(Ellipsize(s, iw, SmallText, FontWeight.SemiBold), r.X + pad, r.Y + 58, iw + 2, Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        // as many as fit, a row each (the emptiest ones first): rows of 26 px or more (40 on a tall card), columns of 80 px or more
        bool big = w.H >= 2;
        var area = new RectangleF(r.X + pad, r.Y + 34, iw, r.Bottom - 10 - (r.Y + 34));
        float gapX = big ? 14 : 10;
        int maxRows = Math.Max(1, (int)(area.Height / (big ? 40 : 26))), maxCols = Math.Max(1, (int)((area.Width + gapX) / (80 + gapX)));
        var pick = PickBatteries(bats, Math.Min(maxRows * maxCols, 8));
        int n = pick.Count, cols = 1;
        while ((n + cols - 1) / cols > maxRows) cols++;
        int rows = (n + cols - 1) / cols;
        float cw = (area.Width - (cols - 1) * gapX) / cols, ch = MathF.Min(big ? 52 : 30, area.Height / rows);
        // a narrow cell on a tall card: the name over the percentage (side by side they would cut the name)
        int style = !big ? 0 : cw >= 120 ? 1 : ch >= 44 ? 2 : 0;
        for (int j = 0; j < n; j++)
            BatteryCell(new RectangleF(area.X + j % cols * (cw + gapX), area.Y + j / cols * ch, cw, ch), bats[pick[j]], Ui.Id("wbt", id * 8 + j), style);
    }

    // a device on a card: its name (UPPERCASE grey; a bolt in front while charging) and its percentage (red / amber when low)
    // on one baseline, its line under them
    // style 0: small, side by side (a one-row card: four of them in 2 x 2, digits as small as captions); 1: big, side by side;
    // 2: big, the name over the percentage (a narrow cell)
    void BatteryCell(RectangleF c, (string label, float pct, bool charging) b, string key, int style)
    {
        bool big = style > 0;
        float lt = big ? 6 : 4, ps = style == 1 ? 22 : style == 2 ? 20 : CapSize;
        var bar = new RectangleF(c.X, c.Bottom - lt - 3, c.Width, lt);
        float by = bar.Y - (big ? 7 : 5);
        int shown = ShownPct(b.pct);
        string pct = Pct(shown);
        float pw = _p.MeasureWidth(pct, ps, FontWeight.Bold, Theme.Display);
        var lv = BatteryLevel(shown);
        uint pc = Bento.Level(lv, Bento.Text);
        float ny = by, nr = c.Right - pw - 5;
        if (style == 2)
        {
            TextAt(pct, c.X, by, pw + 4, pc, ps, FontWeight.Bold, TextAlignment.Leading, Theme.Display);
            ny = by - ps * DigitH - 9;
            nr = c.Right;
        }
        else TextAt(pct, c.Right - pw - 4, by, pw + 4, pc, ps, FontWeight.Bold, TextAlignment.Trailing, Theme.Display);
        DeviceName(c.X, ny, Upper(b.label), b.charging, nr - c.X);
        Meter(bar, b.pct, Bento.Level(lv, Bento.Ok), key);
    }

    // the opened view's battery rows: the text size of rows this big - one size for all of them, small enough that every name
    // fits beside its percentage (never under 17 px for the percentage, 15 px for the names)
    float BatteryRowSize(SizeF cell, List<(string label, float pct, bool charging)> bats, int count)
    {
        float ts = Math.Clamp(cell.Height * 0.36f, SmallText, 34), iw = cell.Width - 20;
        for (int i = 0; i < count; i++)
        {
            var b = bats[i];
            float need = _p.MeasureWidth(Pct(ShownPct(b.pct)), ts, FontWeight.SemiBold, Theme.Display) + 8 + (b.charging ? RowBolt(ts) : 0) +
                _p.MeasureWidth(Upper(b.label), RowName(ts), FontWeight.SemiBold);
            if (need > iw) ts = MathF.Max(SmallText, MathF.Floor(ts * iw / need * 0.95f * 2) / 2);
        }
        return ts;
    }

    static float RowName(float ts) => MathF.Max(CapSize, MathF.Round(ts * 0.7f));
    static float RowBolt(float ts) => RowName(ts) + 2;

    // A device as a row of the opened view, written like the cards: its name on the left (UPPERCASE grey, a white bolt in front
    // while charging), the percentage on the right (amber / red when low), the line under both
    void BatteryRow(RectangleF cell, (string label, float pct, bool charging) b, string key, float ts)
    {
        const float ip = 10;
        int shown = ShownPct(b.pct);
        var lv = BatteryLevel(shown);
        string pct = Pct(shown), name = Upper(b.label);
        float lt = Math.Clamp(cell.Height * 0.08f, 4, 8), iw = cell.Width - 2 * ip;
        float th = ts * 1.3f, gap = MathF.Max(4, ts * 0.2f), y = cell.Y + (cell.Height - th - gap - lt) / 2;
        var tr = new RectangleF(cell.X + ip, y, iw, th);
        float pw = _p.MeasureWidth(pct, ts, FontWeight.SemiBold, Theme.Display), ns = RowName(ts);
        float x = tr.X;
        if (b.charging)
        {
            float bw = RowBolt(ts);
            _p.Icon(Bolt, new RectangleF(x - 2, tr.Y, bw + 2, th), Bento.Text, ns);
            x += bw;
        }
        _p.Text(Ellipsize(name, tr.Right - pw - 8 - x, ns, FontWeight.SemiBold), new RectangleF(x, tr.Y, tr.Right - x, th), Sub, ns, FontWeight.SemiBold);
        _p.Text(pct, tr, Bento.Level(lv, Bento.Text), ts, FontWeight.SemiBold, TextAlignment.Trailing, ParagraphAlignment.Center, Theme.Display);
        Meter(new RectangleF(tr.X, tr.Bottom + gap, tr.Width, lt), b.pct, Bento.Level(lv, Bento.Ok), key);
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

    const string Person = "\uE77B";

    // the modern room card's counts and words, worked out once per room snapshot (a new one comes with every change)
    RoomWatcher.Snapshot _roomSeen;
    bool _roomKnown;
    int _roomPeople = -1, _roomFriends;
    string _roomNames = "", _roomValue = "–", _roomFriendsText;

    void RoomNow(PanelData d)
    {
        if (_roomKnown && ReferenceEquals(d.Room, _roomSeen)) return;
        _roomKnown = true;
        _roomSeen = d.Room;
        (_roomPeople, _roomFriends, _roomNames) = RoomCount(d);
        _roomValue = _roomPeople < 0 ? "–" : _roomPeople.ToString(CultureInfo.InvariantCulture);
        _roomFriendsText = _roomFriends > 0 ? _roomFriends.ToString(CultureInfo.InvariantCulture) : null;
    }

    void RoomOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        bool click = _ui.Clickable(Ui.Id("wrm", id), r, out _, out bool held);
        if (held) _p.Fill(r, Theme.WithAlpha(Theme.Accent, 0.12f), CardRadius);
        if (click) OnAction?.Invoke("room");
        RoomNow(d);
        int n = _roomPeople, f = _roomFriends;
        string value = _roomValue;
        uint vc = n < 0 ? Bento.Faint : Bento.Text;
        float pad = PadX(r), iw = r.Width - 2 * pad;
        if (w.W >= 2)
        {
            // the world as the note; how many big, the friends among them (green) or what it is beside it
            string cap = n < 0 ? (d.Room?.LogFound == false ? "no VRChat" : "not in a world") : n == 1 ? "person here" : "people here";
            CaptionRow(r, n < 0 ? null : d.Room.World, CapRoom);
            float vw = ValueUnit(r.X + pad, r.Y + MBase, 90, value, null, 40, 24, vc);
            float lx = r.X + pad + vw + 12, room = r.Right - pad - lx;
            string line = f > 0 ? _roomNames : cap;
            TextAt(Ellipsize(line, room, SmallText, FontWeight.SemiBold), lx, r.Y + MBase, room + 2, f > 0 ? Bento.Ok : Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        // A 1x1: how many; after it the friends among them (a green person and how many), else "here". Its one graphic: a dot per
        // person along the bottom (the friends first, green; as many as fit), empty ones while there is no room to count.
        Caption(r, iw, CapRoom);
        string fc = _roomFriendsText;
        float fw = fc == null ? 0 : _p.MeasureWidth(fc, SmallText, FontWeight.SemiBold) + 20;
        float used = ValueUnit(r.X + pad, r.Y + SBase, iw - fw, value, n < 0 || fc != null ? null : "here", Hero, 20, vc);
        const float dot = 7, pitch = 11;
        int fit = (int)((iw - dot) / pitch) + 1, shown = n < 0 ? fit : Math.Min(n, fit);
        for (int i = 0; i < shown; i++)
            _p.Circle(r.X + pad + dot / 2 + i * pitch, r.Bottom - 14, dot / 2, n < 0 ? Bento.Track : i < f ? Bento.Ok : Sub);
        if (fc == null) return;
        float fx = r.X + pad + used + 6;
        _p.Icon(Person, new RectangleF(fx - 1, r.Y + SBase - 16, 18, 18), Bento.Ok, 15);
        TextAt(fc, fx + 17, r.Y + SBase, fw, Bento.Ok, SmallText, FontWeight.SemiBold);
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

    // why the dock is not shown, in words that fit (17 px): Hexpad's own, else "offline", else cut
    string DockWhy(string st, float maxW)
    {
        if (_p.MeasureWidth(st, SmallText, FontWeight.SemiBold) <= maxW) return st;
        if (_p.MeasureWidth("offline", SmallText, FontWeight.SemiBold) <= maxW) return "offline";
        return Ellipsize(st, maxW, SmallText, FontWeight.SemiBold);
    }

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

    // Hexpad's stream dock on the watch: its keys as they are right now (no caption: the keys say what it is); a tap opens it
    // as a panel
    void DockOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        bool click = _ui.Clickable(Ui.Id("wdk", id), r, out bool hover, out bool held);
        if (held) _p.Fill(r, Theme.WithAlpha(Theme.Accent, 0.12f), CardRadius);
        if (click) OnAction?.Invoke("dock");
        float pad = PadX(r);
        if (!DockShows(d))
        {
            // Not there: laid out like every card - its caption at the top-left, the dock's glyph where a value would be, and why
            // in a 17 px line ("not connected", or what Hexpad says; "offline" where neither fits)
            float iw = r.Width - 2 * pad;
            Caption(r, iw, CapHexpad);
            string st = DockState(d);
            if (st.Length == 0) st = "not connected";
            const float gs = 28;
            if (w.W >= 2 && w.H == 1)
            {
                // the glyph and the line side by side
                _p.Icon("\uE80A", new RectangleF(r.X + pad - 3, r.Y + SBase - 25, gs + 6, gs + 4), Sub, gs);
                float tx = r.X + pad + gs + 12, tw = r.Right - pad - tx;
                TextAt(DockWhy(st, tw), tx, r.Y + SBase - 4, tw + 2, Sub, SmallText, FontWeight.SemiBold);
                return;
            }
            // the glyph, the line under it (along the bottom of a 1x1)
            _p.Icon("\uE80A", new RectangleF(r.X + pad - 3, r.Y + SBase - 25, gs + 6, gs + 4), Sub, gs);
            TextAt(DockWhy(st, iw), r.X + pad, w.H >= 2 ? r.Y + SBase + 30 : r.Bottom - 13, iw + 2, Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        var s2 = d.Dock;
        if (w.W >= 2 && w.H == 1)
        {
            // the keys on the left; the page (or folder) and where in the pages on the right
            var g = DockGrid(new RectangleF(r.X + 10, r.Y + 10, (r.Height - 20) * 1.55f, r.Height - 20), d, 4, 6, false);
            float tx = g.Right + 12, tw = r.Right - 12 - tx;
            TextAt(Ellipsize(DockWhere(s2), tw, SmallText, FontWeight.SemiBold), tx, r.Y + r.Height / 2 - 4, tw + 2, Bento.Text, SmallText, FontWeight.SemiBold);
            string page = s2.Ambient ? "idle" : s2.InFolder ? "folder" : $"{s2.Page + 1} / {s2.Pages}";
            TextAt(Ellipsize(page, tw, SmallText, FontWeight.SemiBold), tx, r.Y + r.Height / 2 + 20, tw + 2, Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        if (w.H >= 2)
        {
            var g = DockGrid(new RectangleF(r.X + 12, r.Y + 12, r.Width - 24, r.Height - 52), d, 6, 8, false);
            string where = DockWhere(s2) + (s2.InFolder ? "" : $"  ·  {s2.Page + 1}/{s2.Pages}");
            _p.Text(Ellipsize(where, r.Width - 28, SmallText, FontWeight.SemiBold), new RectangleF(r.X + 14, r.Bottom - 36, r.Width - 28, 24), Sub, SmallText,
                FontWeight.SemiBold, TextAlignment.Center);
            return;
        }
        DockGrid(RectangleF.Inflate(r, -9, -9), d, 3, 4, false);
    }

    // Now playing: the cover on the left when there is room (three cells and up, or two rows), the caption, title and artist,
    // play / pause (the accent) - with previous / next too when it is six wide or tall
    void MediaOrbit(RectangleF r, WatchWidget w, PanelData d, int id)
    {
        var (has, title, artist, playing) = MediaNow(d);
        float pad = PadX(r);
        bool tall = w.H >= 2;
        float x = r.X + pad;
        if (w.W >= 3 || tall)
        {
            const float m = 12;
            float s = tall ? MathF.Min(r.Height - 2 * m, r.Width * 0.45f) : r.Height - 2 * m;
            var cover = new RectangleF(r.X + m, r.Y + (r.Height - s) / 2, s, s);
            string fk = Ui.Id("wma", id);
            if (Clock.Now - _artChanged < 0.05) _ui.SpringFrom(fk, 0);      // a new cover fades in
            float fade = _ui.Spring(fk, 1, 120, 18);
            float cr = MathF.Max(10, s * 0.1f);
            _p.Fill(cover, Bento.Tile, cr);
            if (_art != null)
            {
                _p.PushRoundedClip(cover, cr);
                _p.Image(_art, cover, Math.Clamp(fade, 0, 1));
                _p.PopRoundedClip();
            }
            else _p.Icon("\uE8D6", cover, Sub, s * 0.36f);
            x = cover.Right + 14;
        }
        var ids = MediaIds;
        string playIcon = playing ? PauseFill : PlayFill;
        float right;          // where the text column ends
        if (tall)
        {
            // previous / play / next at the bottom of the text column
            const float big = 52, sm = 44, gap = 10;
            float by = r.Bottom - 12 - big;
            if (Press(Ui.Id("wcm", id * 3), new RectangleF(x, by + (big - sm) / 2, sm, sm), "\uE892", null, false, has, 18, true))
                OnAction?.Invoke(ids[0]);
            if (Press(Ui.Id("wcm", id * 3 + 1), new RectangleF(x + sm + gap, by, big, big), playIcon, null, true, has, 20, true))
                OnAction?.Invoke(ids[1]);
            if (Press(Ui.Id("wcm", id * 3 + 2), new RectangleF(x + sm + gap + big + gap, by + (big - sm) / 2, sm, sm), "\uE893", null, false,
                    has, 18, true))
                OnAction?.Invoke(ids[2]);
            right = r.Right - pad;
        }
        else
        {
            // one row: play (and previous / next on a six-wide one) on the right; under the caption when there is no cover
            int nb = w.W >= 6 ? 3 : 1;
            const float big = 48, sm = 44, gap = 8;
            float total = nb == 3 ? big + 2 * (sm + gap) : big;
            float bx = r.Right - 12 - total, cy = w.W >= 3 ? r.Y + r.Height / 2 : r.Y + 32 + big / 2;
            for (int j = 0; j < nb; j++)
            {
                int a = nb == 1 ? 1 : j;
                float bs = a == 1 ? big : sm;
                float px = nb == 1 ? bx : bx + (j == 0 ? 0 : j == 1 ? sm + gap : sm + gap + big + gap);
                string icon = a == 0 ? "\uE892" : a == 1 ? playIcon : "\uE893";
                if (Press(Ui.Id("wcm", id * 3 + a), new RectangleF(px, cy - bs / 2, bs, bs), icon, null, a == 1, has, a == 1 ? 20 : 18, true))
                    OnAction?.Invoke(ids[a]);
            }
            right = bx - 10;
        }
        float tw = right - x;
        if (tw < 40) return;
        // the caption over the text column (across the card when the play button sits under it)
        CaptionRow(r, x, w.W >= 3 || tall ? right : r.Right - pad, null, Sub, CapMedia);
        if (!has)
        {
            // one wording on every size (and opened): "Nothing playing" does not fit a 2x1 at 17 px
            TextAt(Ellipsize(NotPlaying, tw, SmallText, FontWeight.SemiBold), x, r.Y + 56, tw + 2, Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        if (tall)
        {
            // a long title takes two lines before it is cut
            float ts = 21, ty = r.Y + 58;
            var (l1, l2) = TwoLines(title ?? "", tw, ts, FontWeight.SemiBold);
            TextAt(l1, x, ty, tw + 2, Bento.Text, ts, FontWeight.SemiBold);
            if (l2.Length > 0) { ty += 26; TextAt(l2, x, ty, tw + 2, Bento.Text, ts, FontWeight.SemiBold); }
            TextAt(Ellipsize(artist ?? "", tw, SmallText, FontWeight.SemiBold), x, ty + 26, tw + 2, Sub, SmallText, FontWeight.SemiBold);
            return;
        }
        TextAt(Ellipsize(title ?? "", tw, SmallText, FontWeight.SemiBold), x, r.Y + 54, tw + 2, Bento.Text, SmallText, FontWeight.SemiBold);
        TextAt(Ellipsize(artist ?? "", tw, SmallText, FontWeight.SemiBold), x, r.Y + 78, tw + 2, Sub, SmallText, FontWeight.SemiBold);
    }

    // The system volume: the level big and as a line (read-only on the card - a tap opens the slider); - / + steps of 5 % on a
    // 2x1, the mute disc in front of them on a 3x1 (no slider on a card). Muted: the card turns red, its value a crossed speaker
    // with the level it comes back to grey beside it.
    void VolumeOrbit(RectangleF r, WatchWidget w, int id)
    {
        float lvl = Volume.Level;
        bool muted = Volume.Muted, ok = lvl >= 0, mutedNow = ok && muted;
        string pct = ok ? VolumePct(lvl) : "–";
        uint vc = ok ? Bento.Text : Bento.Faint, bar = muted ? Bento.Faint : Theme.Accent;
        float pad = PadX(r), iw = r.Width - 2 * pad;
        if (mutedNow)
        {
            _p.Fill(r, Bento.DangerFill, CardRadius);
            _p.Stroke(RectangleF.Inflate(r, -0.6f, -0.6f), Bento.DangerEdge, Bento.EdgeWidth, CardRadius - 0.6f);
        }
        if (w.W == 1)
        {
            if (!mutedNow) { Small(r, CapVolume, pct, null, ok ? lvl : -1, bar, vc, Ui.Id("wvg", id)); return; }
            Caption(r, iw, CapVolume);
            MutedValue(r.X + pad, r.Y + SBase, iw, pct);
            Meter(new RectangleF(r.X + pad, r.Bottom - 16, iw, 4), lvl, bar, Ui.Id("wvg", id));
            return;
        }
        Caption(r, iw, CapVolume);
        const float bs = 44, gap = 6;
        float by = r.Y + 31;
        // the discs from the right: +, -, and on a 3x1 the mute disc a little apart in front of them
        float bx = r.Right - pad - bs;
        if (Press(Ui.Id("wvp", id), new RectangleF(bx, by, bs, bs), "\uE710", null, false, ok, 18, true)) Volume.Step(0.05f);
        bx -= bs + gap;
        if (Press(Ui.Id("wvm", id), new RectangleF(bx, by, bs, bs), "\uE738", null, false, ok, 18, true)) Volume.Step(-0.05f);
        if (w.W >= 3)
        {
            bx -= bs + 12;
            if (Press(Ui.Id("wvs", id), new RectangleF(bx, by, bs, bs), muted ? MuteGlyph : "\uE767", null, muted, ok, 20, true, accentFill: false,
                    fill: Bento.Bad))
                Volume.ToggleMute();
        }
        float vy = by + bs / 2 + Hero * DigitH / 2, room = bx - 10 - (r.X + pad);
        if (mutedNow) MutedValue(r.X + pad, vy, room, pct);
        else ValueUnit(r.X + pad, vy, room, pct, null, Hero, 20, vc);
        Meter(new RectangleF(r.X + pad, r.Bottom - 15, iw, 4), ok ? lvl : -1, bar, Ui.Id("wvg", id));
    }

    // "50%": the volume as the watch writes it (the card and its opened view)
    static string VolumePct(float lvl) => Pct(ShownPct(lvl));

    // a muted volume's value: the crossed speaker as big as a value, red, and the level it comes back to grey after it (when it fits)
    void MutedValue(float x, float by, float maxW, string pct)
    {
        const float gs = Hero;
        _p.Icon(MuteGlyph, new RectangleF(x - 3, by - Hero * DigitH / 2 - gs / 2 - 1, gs + 6, gs + 2), Bento.Bad, gs);
        float tx = x + gs + 6;
        if (tx + _p.MeasureWidth(pct, SmallText, FontWeight.SemiBold) <= x + maxW) TextAt(pct, tx, by, maxW - (tx - x) + 2, Sub, SmallText, FontWeight.SemiBold);
    }

    void StatOrbit(RectangleF r, WatchWidget w, WatchWidgets.Kind k, PanelData d, int id)
    {
        if (k.Id == "session") return;
        if (w.W == 1) { StatS(r, k.Id, d, Ui.Id("wsg", id)); return; }
        var (frac, value, unit, caption, lv) = Reading(k.Id, d);
        bool none = value == "–";
        Wide(r, WideCaption(k.Id), caption, value, !none && unit is "fps" or "ms" or "GB" ? unit : null, frac, Bento.Level(lv, RoleColor(k.Id)),
            none ? Bento.Faint : Bento.Level(lv, Bento.Text), Ui.Id("wsg", id));
    }

    // the last ~5 s of frames as bars: the frame numbers as the caption's note, amber over budget / reprojected, red dropped
    void GraphOrbit(VrStats v, RectangleF r, uint tint)
    {
        float pad = PadX(r);
        // the numbers turn amber / red with the worst of them, not only the bars
        var (note, lv) = FrameNote(v);
        CaptionRow(r, r.X + pad, r.Right - pad, note, Bento.Level(lv, Sub), CapGraph);
        GraphBars(v, new RectangleF(r.X + pad, r.Y + 38, r.Width - 2 * pad, r.Height - 38 - 12), tint);
    }

    // "86 fps · 9,3 ms · 4%" and the worst of their levels (each number read once)
    static (string text, StatLevel level) FrameNote(VrStats v)
    {
        float fps = v.AppFps, ms = v.GpuMs, rp = v.ReprojPct, hz = v.DisplayHz;
        if (fps < 0) return ("waiting for frames", StatLevel.Ok);
        var lv = (StatLevel)Math.Max((int)FpsLevel(fps, hz), Math.Max((int)GpuLevel(ms, 1000f / Math.Max(1, hz)), (int)ReprojLevel(rp)));
        return ($"{fps:0} fps · {ms:0.0} ms · {Math.Max(0, rp):0}%", lv);
    }

    // the opened view's graph: its numbers as a line of text over the bars (amber / red with the worst of them, like the card)
    void GraphPlain(VrStats v, RectangleF r, uint tint)
    {
        var (label, lv) = FrameNote(v);
        _p.Text(Ellipsize(label, r.Width - 32, SmallText, FontWeight.SemiBold), new RectangleF(r.X + 16, r.Y + 6, r.Width - 32, 24),
            Bento.Level(lv, Bento.Text), SmallText, FontWeight.SemiBold);
        GraphBars(v, new RectangleF(r.X + 14, r.Y + 34, r.Width - 28, r.Height - 44), tint);
    }

    void GraphBars(VrStats v, RectangleF g, uint tint)
    {
        float budget = 1000f / Math.Max(1, v.DisplayHz), top = budget * 2;
        int n = v.FrameCount;
        float step = g.Width / VrStats.FrameHistory;
        for (int i = 0; i < n; i++)
        {
            int idx = (v.FrameHead - n + i + VrStats.FrameHistory) % VrStats.FrameHistory;
            float ms = v.FrameMs[idx];
            byte flag = v.FrameFlag[idx];
            float h = Math.Clamp(ms / top, 0.03f, 1) * g.Height;
            uint col = flag == 2 ? Bento.Bad : flag == 1 || ms > budget ? Bento.Warn : tint;
            float x = g.Right - (n - i) * step;
            _p.Fill(new RectangleF(x, g.Bottom - h, MathF.Max(1, step - 0.4f), h), col);
        }
        float by = g.Bottom - g.Height * (budget / top);
        _p.Line(g.X, by, g.Right, by, 0x66FFFFFF, 1.5f);
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
