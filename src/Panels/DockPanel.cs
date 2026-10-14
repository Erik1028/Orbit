using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Valve.VR;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Orbit;

// The faces of the dock's six keys as bitmaps, shared by the dock panel and the watch widget (one Painter,
// one device). Rebuilt only for the keys that changed. Bitmaps are the device's own 68 px faces (the 95 window
// and the watch draw them pixel for pixel); Hi the same faces drawn at 2x by an api 2 Hexpad (null when none).
public sealed class HexpadFaces : IDisposable
{
    public readonly ID2D1Bitmap[] Bitmaps = new ID2D1Bitmap[HexpadLink.Keys];
    public readonly ID2D1Bitmap[] Hi = new ID2D1Bitmap[HexpadLink.Keys];
    readonly HexpadLink.Face[] _dev = new HexpadLink.Face[HexpadLink.Keys], _hi = new HexpadLink.Face[HexpadLink.Keys];
    public bool Any { get; private set; }

    // the sharpest picture of key i
    public ID2D1Bitmap Best(int i) => Hi[i] ?? Bitmaps[i];

    public void Update(Painter p, HexpadLink.Snapshot s)
    {
        Any = false;
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            Rebuild(p, s.Faces[i], _dev, Bitmaps, i);
            Rebuild(p, s.HiFaces[i], _hi, Hi, i);
            Any |= Bitmaps[i] != null || Hi[i] != null;
        }
    }

    static void Rebuild(Painter p, HexpadLink.Face f, HexpadLink.Face[] made, ID2D1Bitmap[] bmps, int i)
    {
        if (ReferenceEquals(f, made[i])) return;
        made[i] = f;
        bmps[i]?.Dispose();
        bmps[i] = null;
        if (f != null && f.W > 0 && f.H > 0 && f.Px.Length == f.W * f.H) bmps[i] = p.CreateBitmap(f.Px, f.W, f.H);
    }

    public void Dispose()
    {
        for (int i = 0; i < Bitmaps.Length; i++)
        {
            Bitmaps[i]?.Dispose(); Bitmaps[i] = null; _dev[i] = null;
            Hi[i]?.Dispose(); Hi[i] = null; _hi[i] = null;
        }
    }
}

// The stream dock in VR: Hexpad's AKP03 laid out like the real one - six keys (what they show right now) and
// a round button under each column - with its three knobs as sliders on the right (a knob is awkward to turn
// with a laser): the big knob first, then the two small ones. A slider Hexpad can read and set ("abs": volume,
// key brightness, monitors) shows the value and sets it as you drag; any other turn is a "jog" slider that springs
// back to the middle and turns the knob a detent per bit of drag. The stick over a slider moves it too. The button
// beside each slider pushes the knob. The laser presses keys and buttons (press and release, so Hexpad's hold and
// double tap work); what could end the VR session (lock, sleep, empty the bin) asks first. Grab it once onto the
// real dock on your desk; it stays there.
public sealed class DockPanel : IPointerTarget, IDisposable
{
    public const int TW = 800, TH = 480, W95 = TW / 2, H95 = TH / 2;

    readonly Painter _p;
    readonly Ui _ui;
    readonly ConfigStore _cfg;
    readonly HexpadLink _link;
    readonly HexpadFaces _faces;
    readonly PointerState _ptr = new();
    public readonly Surface Surf;
    Surface _canvas95;
    public VrOverlay Ov { get; private set; }
    VrSession _vr;
    float _vis;
    bool _dirty = true;
    double _nextDraw;
    int _seen = -1;
    float _twoStartWidth;
    HexpadLink.Snapshot _s = new();

    // input
    Hand? _owner;
    string _hover, _pressed, _confirm, _scrollId;
    float _scrollAcc;
    double _wokeAt = -10;
    readonly Dictionary<string, RectangleF> _hit = new();     // control -> rect in texture pixels ("enc1:s" = its slider)
    readonly Dictionary<string, RectangleF> _cards = new();   // knob -> its whole row (the stick over it moves the slider)
    readonly Dictionary<string, RectangleF> _track = new();   // knob -> where its thumb's centre runs (texture pixels)

    // the slider being dragged
    string _drag, _dragKind;
    bool _dragAbs;
    float _dragGrab;            // abs: where on the thumb it was taken (px from its centre); jog: where the drag began
    float _jogOff;              // jog: how far it is dragged (px)
    int _jogSent;               // jog: detents sent in this drag
    // abs: the value we set is shown instead of Hexpad's while dragging and for a moment after (its read-back lags)
    readonly Dictionary<string, float> _local = new();
    readonly Dictionary<string, double> _holdUntil = new();
    readonly Dictionary<string, (double at, string kind)> _stickEnd = new();   // the stick moved it: final then

    const double Hold = 0.6, StickFinal = 0.4;
    const float StickStep = 0.02f;

    enum Slide { None, Abs, Jog }

    DockConfig C => _cfg.Data.Dock;
    public bool Shown => C.Enabled && C.Visible;

    public DockPanel(Gpu g, Painter p, ConfigStore cfg, HexpadLink link, HexpadFaces faces)
    {
        _p = p; _cfg = cfg; _link = link; _faces = faces;
        _ui = new Ui(p);
        Surf = new Surface(g, TW, TH);
    }

    public void CreateOverlay(VrSession vr)
    {
        _vr = vr;
        Ov = new VrOverlay("erik.orbit.dock", "OrbitOverlay Hexpad");
        Ov.SetSortOrder(135);
        Ov.SetFlag(VROverlayFlags.IsPremultiplied, true);
        Ov.SetMouseScale(TW, TH);
        _vis = 0;
        _dirty = true;
    }

    public void DestroyOverlay()
    {
        ReleasePress();
        _ptr.Leave();
        Ov?.Dispose();
        Ov = null;
        _vr = null;
        _vis = 0;
    }

    public void Toggle()
    {
        if (C.Visible) { C.Visible = false; _confirm = null; _cfg.MarkDirty(); return; }
        Open();
    }

    public void Open()
    {
        C.Visible = true;
        _cfg.MarkDirty();
        _dirty = true;
        if (C.Place.M == null) BringHere();             // else where it was put last (over the real dock)
    }

    // a desk console in front of you: 0.45 m ahead, 0.3 m below the eyes, facing up at you
    public void BringHere()
    {
        if (_vr == null || !_vr.HmdValid) return;
        var local = Matrix4x4.CreateRotationX(-35 * MathF.PI / 180) * Matrix4x4.CreateTranslation(0, -0.30f, -0.45f);
        C.Place.Attach = "world";
        C.Place.M = MathUtil.ToArray(local * MathUtil.GazeFrame(_vr.Hmd));
        _cfg.MarkDirty();
    }

    public void Update(VrSession vr, double now, float dt, HexpadLink.Snapshot s)
    {
        if (Ov == null) return;
        float target = Shown ? 1 : 0;
        _vis = Math.Clamp(_vis + Math.Sign(target - _vis) * dt / 0.18f, 0, 1);
        if (_vis <= 0 || C.Place.M == null || !Placement.Apply(C.Place, vr, Ov))
        {
            if (_vis <= 0) { ReleasePress(); _ptr.Leave(); _hover = null; }
            Ov.Hide();
            return;
        }
        float e = 1 - MathF.Pow(1 - _vis, 3);
        Ov.SetAlpha(e);
        Ov.SetWidth(Math.Clamp(C.Width, 0.15f, 0.9f) * (0.92f + 0.08f * e));
        if (s.Version != _seen) { _seen = s.Version; _s = s; _dirty = true; }
        Timers(now);
        bool flashing = now - _s.EchoAt < 0.4;
        if (_dirty || flashing || now >= _nextDraw || _ui.Animating)
        {
            Draw(now);
            Ov.SetTexture(Surf.Tex);
            _dirty = _ui.Interacting || _ui.Changed;
            _nextDraw = now + 1;
        }
        Ov.Show();
    }

    // the stick's final value once it stops; Hexpad's own value again once a hold runs out
    void Timers(double now)
    {
        if (_stickEnd.Count > 0)
            foreach (var id in new List<string>(_stickEnd.Keys))
            {
                var (at, kind) = _stickEnd[id];
                if (now < at) continue;
                _stickEnd.Remove(id);
                if (_local.TryGetValue(id, out float v)) _link.Set(id, kind, v, true);
                _holdUntil[id] = now + Hold;
            }
        if (_holdUntil.Count > 0)
            foreach (var id in new List<string>(_holdUntil.Keys))
                if (now >= _holdUntil[id] && _drag != id && !_stickEnd.ContainsKey(id))
                {
                    _holdUntil.Remove(id);
                    _local.Remove(id);
                    _dirty = true;
                }
    }

    // ---------------- input ----------------

    bool Live => _s.State == HexpadLink.LinkState.Ok;
    bool CanPress => Live && C.Input && _confirm == null;

    string HitTest(Vector2 px)
    {
        foreach (var (id, r) in _hit)
            if (r.Contains(px.X, px.Y)) return id;
        return null;
    }

    string CardAt(Vector2 px)
    {
        foreach (var (id, r) in _cards)
            if (r.Contains(px.X, px.Y)) return id;
        return null;
    }

    static string Base(string id) => id != null && id.Contains(':') ? id[..id.IndexOf(':')] : id;
    static string S(string id) => Ui.Id(id, ":s");          // a knob's slider (the push is the knob's own id)

    // what a knob's slider does now
    Slide TypeOf(string id)
    {
        var c = Ctl(id);
        if (c == null || string.IsNullOrEmpty(c.Slider)) return Slide.None;
        if (c.Slider == "abs") return _s.Api >= 2 && !string.IsNullOrEmpty(c.Kind) ? Slide.Abs : Slide.Jog;
        return Slide.Jog;
    }

    bool Off(string id) => _s.Values.TryGetValue(id, out var v) && v.Off;

    // an abs slider's value: ours while dragging and just after, else Hexpad's (null = it does not know)
    float? AbsValue(string id, double now)
    {
        if ((_drag == id || _holdUntil.TryGetValue(id, out var t) && now < t) && _local.TryGetValue(id, out float mine)) return mine;
        return _s.Values.TryGetValue(id, out var v) ? v.V : null;
    }

    // drag this far for one detent; pages and tracks are whole steps, so they want a longer pull
    float Detent(string id) => Ctl(id)?.Kind is "page" or "media" ? 60 : 24;

    void Press(string id)
    {
        if (_s.Controls.TryGetValue(id, out var c) && c.Danger)
        {
            _confirm = id;               // nothing is sent until it is confirmed
            return;
        }
        _link.Input(id, true);
        _pressed = id;
    }

    // the laser lets go (or leaves, or the panel goes): the key comes up, a drag ends, the stick's value is final
    void ReleasePress()
    {
        if (_pressed != null) _link.Input(_pressed, false);
        _pressed = null;
        EndDrag();
        if (_stickEnd.Count > 0)
        {
            foreach (var (id, (_, kind)) in _stickEnd)
                if (_local.TryGetValue(id, out float v)) _link.Set(id, kind, v, true);
            foreach (var id in _stickEnd.Keys) _holdUntil[id] = Clock.Now + Hold;
            _stickEnd.Clear();
        }
    }

    void BeginDrag(string id, Vector2 px)
    {
        var t = TypeOf(id);
        if (t == Slide.None || t == Slide.Abs && Off(id) || !_track.TryGetValue(id, out var tr)) return;
        _drag = id;
        _dragKind = Ctl(id)?.Kind;          // a page change mid-drag must not turn a volume drag into something else
        _dragAbs = t == Slide.Abs;
        _stickEnd.Remove(id);
        if (_dragAbs)
        {
            // taken by the thumb it keeps where it was held; anywhere else on the track it jumps there
            float? cur = AbsValue(id, Clock.Now);
            float grab = Theme.Classic ? 14 : 24;
            _dragGrab = 0;
            if (cur is float v && MathF.Abs(px.X - (tr.X + tr.Width * v)) <= grab) _dragGrab = px.X - (tr.X + tr.Width * v);
        }
        else
        {
            _dragGrab = px.X;
            _jogOff = 0;
            _jogSent = 0;
        }
        DragTo(px);
    }

    void DragTo(Vector2 px)
    {
        if (_drag == null || !_track.TryGetValue(_drag, out var tr)) return;
        if (_dragAbs)
        {
            float v = Math.Clamp((px.X - _dragGrab - tr.X) / tr.Width, 0, 1);
            _local[_drag] = v;
            _holdUntil[_drag] = double.MaxValue;
            _link.Set(_drag, _dragKind, v, false);
        }
        else
        {
            _jogOff = px.X - _dragGrab;
            int n = (int)(_jogOff / Detent(_drag));         // towards zero: a detent per full step of drag
            if (n != _jogSent) { _link.Jog(_drag, n - _jogSent); _jogSent = n; }
        }
        _dirty = true;
    }

    void EndDrag()
    {
        if (_drag == null) return;
        if (_dragAbs)
        {
            if (_local.TryGetValue(_drag, out float v)) _link.Set(_drag, _dragKind, v, true);
            _holdUntil[_drag] = Clock.Now + Hold;
        }
        _drag = null;
        _jogOff = 0;
        _dirty = true;
    }

    public void PointerMove(Hand h, Vector2 px)
    {
        _owner ??= h;
        if (_owner != h) return;
        _ptr.Move(px.X, px.Y);
        if (_drag != null) DragTo(px);
        string hov = _drag != null ? S(_drag) : CanPress ? HitTest(px) : null;
        if (hov != _hover) { _hover = hov; _dirty = true; }
        if (_ui.NeedsRedrawForMove(_ptr, Theme.Classic ? 2 : 1)) _dirty = true;
        // the dock's idle screen swallows the first press: wake it as soon as the laser arrives
        if (_s.Ambient && C.Input && Clock.Now - _wokeAt > 2)
        {
            _wokeAt = Clock.Now;
            _link.Wake();
        }
    }

    public void PointerLeave(Hand h)
    {
        if (_owner != h) return;
        _owner = null;
        ReleasePress();
        _ptr.Leave();
        _hover = null;
        _dirty = true;
    }

    public void PointerButton(Hand h, PtrButton b, bool down, Vector2 px)
    {
        if (b != PtrButton.Primary) return;
        if (down)
        {
            if (_owner != h && (_ptr.Down || _pressed != null || _drag != null)) return;
            _owner = h;
            _ptr.Move(px.X, px.Y);
            _ptr.Press();
            if (CanPress && HitTest(px) is { } id)
            {
                if (id.EndsWith(":s")) BeginDrag(Base(id), px);
                else Press(id);
            }
        }
        else
        {
            if (_owner != h) return;
            _ptr.Move(px.X, px.Y);
            _ptr.Release();
            ReleasePress();
        }
        _dirty = true;
    }

    // the stick over a knob's row: an abs slider moves 2 % per notch (its final value once the stick rests),
    // a jog one turns a detent per notch
    public void PointerScroll(Hand h, float notches, Vector2 px)
    {
        if (_owner != h || !CanPress || _drag != null) return;
        string id = CardAt(px);
        if (id == null) return;
        var t = TypeOf(id);
        if (t == Slide.Abs)
        {
            if (Off(id) || AbsValue(id, Clock.Now) is not float cur) return;
            string kind = Ctl(id).Kind;
            float v = Math.Clamp(cur + StickStep * notches, 0, 1);
            _local[id] = v;
            _holdUntil[id] = double.MaxValue;
            _stickEnd[id] = (Clock.Now + StickFinal, kind);
            _link.Set(id, kind, v, false);
            _dirty = true;
            return;
        }
        if (t != Slide.Jog) return;
        if (_scrollId != id) { _scrollId = id; _scrollAcc = 0; }
        _scrollAcc += notches;
        int n = 0;
        while (_scrollAcc >= 1) { n++; _scrollAcc -= 1; }
        while (_scrollAcc <= -1) { n--; _scrollAcc += 1; }
        if (n != 0) _link.Jog(id, n);
    }

    public VrOverlay Overlay => Ov;
    public bool Interactive => Ov != null && Ov.Visible && Shown && _vis > 0.8f;
    public bool AcceptsFrom(Hand h) => true;
    public Vector2 TextureSize => new(TW, TH);
    public bool Grabbable => true;
    public Matrix4x4 WorldTransform =>
        _vr != null && Placement.TryWorld(C.Place, _vr, out var w) ? w : MathUtil.FromArray(C.Place.M);
    public void GrabBegin(bool detach) { ReleasePress(); }
    public void GrabMove(Matrix4x4 world) { if (_vr != null) { Placement.SetWorld(C.Place, _vr, world); _cfg.MarkDirty(); } }
    public void GrabStickX(float v, float dt) { C.Width = Math.Clamp(C.Width * (1 + v * 0.9f * dt), 0.15f, 0.9f); _cfg.MarkDirty(); }
    public void GrabTwoHandBegin() => _twoStartWidth = C.Width;
    public void GrabTwoHandScale(float r) { C.Width = Math.Clamp(_twoStartWidth * r, 0.15f, 0.9f); _cfg.MarkDirty(); }
    public bool GrabSnapPreview => false;
    public void GrabEnd() { }

    // ---------------- drawing ----------------

    // without VR (--docktest): this snapshot, a pointer over a control, a pending confirmation, a slider being
    // dragged (drag = the knob; dragAt = the value for an abs slider, the pull in texture px for a jog one).
    // Only draws: nothing is sent.
    public void DrawFor(HexpadLink.Snapshot s, double now, string hover = null, string pressed = null, string confirm = null,
        string drag = null, float dragAt = 0)
    {
        _s = s; _hover = hover; _pressed = pressed; _confirm = confirm;
        _local.Clear(); _holdUntil.Clear(); _stickEnd.Clear();
        _drag = null; _jogOff = 0; _jogSent = 0;
        _ui.ResetAnimations();
        if (drag != null)
        {
            _drag = drag;
            _dragAbs = TypeOf(drag) == Slide.Abs;
            _dragKind = Ctl(drag)?.Kind;
            if (_dragAbs) { _local[drag] = Math.Clamp(dragAt, 0, 1); _holdUntil[drag] = double.MaxValue; }
            else { _jogOff = dragAt; _jogSent = (int)(dragAt / Detent(drag)); }
            _hover ??= S(drag);
        }
        Draw(now);
        _drag = null; _jogOff = 0; _jogSent = 0;
        _local.Clear(); _holdUntil.Clear();
    }

    public void Draw(double now)
    {
        _hit.Clear();
        _cards.Clear();
        _track.Clear();
        if (Theme.Classic) Draw95(now);
        else DrawModern(now);
    }

    string Where()
    {
        if (_s.InFolder)
            return _s.FolderPages > 1 ? $"{_s.FolderName} · {_s.FolderPage + 1}/{_s.FolderPages}" : _s.FolderName;
        return $"{_s.PageName} · {_s.Page + 1}/{_s.Pages}";
    }

    string StateLine() => _s.State switch
    {
        HexpadLink.LinkState.NotRunning => "Hexpad is not running",
        HexpadLink.LinkState.OldHexpad => "Update Hexpad to see the dock here",
        HexpadLink.LinkState.Unplugged => "The dock is unplugged",
        HexpadLink.LinkState.ScreenOff => "The dock's screen is off",
        _ => "Connecting to Hexpad...",
    };

    HexpadLink.Control Ctl(string id) => id != null && _s.Controls.TryGetValue(id, out var c) ? c : null;

    // "Volume: mute" -> "Mute": a knob's group / caption already says what it is
    static string Short(string summary)
    {
        if (string.IsNullOrEmpty(summary)) return "";
        int i = summary.IndexOf(": ", StringComparison.Ordinal);
        string s = i >= 0 ? summary[(i + 2)..] : summary;
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
    }
    bool Flash(string id, double now) => now - _s.EchoAt < 0.4 && _s.Echo == id;

    // a knob's name: what it turns ("Volume"), else where it sits on the dock
    static string KnobName(string id, HexpadLink.Control c) =>
        !string.IsNullOrEmpty(c?.Label) ? c.Label : id switch { "enc1" => "Big knob", "enc0" => "Left knob", _ => "Right knob" };

    static string PushLabel(HexpadLink.Control c) => string.IsNullOrEmpty(c?.Push) ? "" : Short(c.Push);

    // the value beside a slider's name: "42%" (abs), the detents of this pull (jog), or why the track rests
    string ValueText(string id, Slide t, double now)
    {
        var c = Ctl(id);
        if (t == Slide.Abs)
        {
            _s.Values.TryGetValue(id, out var x);
            if (x?.Off == true) return "Off";
            float? v = AbsValue(id, now);
            if (v == null) return "--";
            bool mine = _drag == id || _holdUntil.ContainsKey(id);
            if (x?.Muted == true && !mine) return "Muted";
            return $"{MathF.Round(v.Value * Math.Max(1, c?.Max ?? 100))}%";
        }
        if (t == Slide.Jog)
            return _drag != id ? "" : _jogSent > 0 ? $"+{_jogSent}" : _jogSent < 0 ? $"−{-_jogSent}" : "0";
        return "";
    }

    // ---- modern: a dark slab like the device ----
    const float Pad = 20, Tile = 136, Gap = 12, KeysY = 66;
    static float KeyX(int col) => Pad + col * (Tile + Gap);
    static float KeyY(int row) => KeysY + row * (Tile + Gap);
    // the knobs' column: the big one first, then the small left and right ones
    const float ColX = 472, RowH = 116, RowGap = 12;
    static readonly string[] Knobs = { "enc1", "enc0", "enc2" };

    void DrawModern(double now)
    {
        _ui.Begin(_ptr, 1);
        _p.Begin(Surf.Bitmap, 0x00000000);
        var all = new RectangleF(0, 0, TW, TH);
        _p.Fill(all, Theme.OverlayBg, 30);
        _p.Stroke(new RectangleF(1, 1, TW - 2, TH - 2), 0x40FFFFFF, 2, 29);
        uint sub = Theme.Mix(Theme.Text, Theme.OverlayBg, 0.42f);

        // header: name, where on the dock, the idle chip; bring here / close on the right
        _p.Text("Hexpad", new RectangleF(Pad + 4, 14, 140, 38), Theme.Text, 22, FontWeight.SemiBold);
        float hx = Pad + 4 + _p.MeasureWidth("Hexpad", 22, FontWeight.SemiBold) + 14;
        if (Live || _s.State == HexpadLink.LinkState.ScreenOff)
        {
            string where = Where();
            float pw = Math.Min(_p.MeasureWidth(where, 15, FontWeight.SemiBold) + 26, 360);
            _p.Fill(new RectangleF(hx, 19, pw, 30), Theme.Panel3, 15);
            _p.Text(where, new RectangleF(hx + 13, 19, pw - 22, 30), sub, 15, FontWeight.SemiBold);
            hx += pw + 10;
            if (_s.Ambient)
            {
                const string idle = "idle · point to wake";
                float iw = _p.MeasureWidth(idle, 14) + 24;
                _p.Fill(new RectangleF(hx, 19, iw, 30), Theme.WithAlpha(Theme.Accent, 0.18f), 15);
                _p.Text(idle, new RectangleF(hx + 12, 19, iw - 18, 30), Theme.Accent, 14);
            }
        }
        if (HeaderButton("dk.x", new RectangleF(TW - Pad - 40, 14, 40, 40), "")) { Toggle(); }
        if (HeaderButton("dk.here", new RectangleF(TW - Pad - 90, 14, 40, 40), Theme.IcoRecenter)) BringHere();

        if (!Live && _s.State != HexpadLink.LinkState.ScreenOff || _s.State == HexpadLink.LinkState.ScreenOff && !_faces.Any)
        {
            _p.Text(StateLine(), new RectangleF(0, 150, TW, 60), Theme.Text, 24, FontWeight.SemiBold, TextAlignment.Center);
            string hint = _s.State switch
            {
                HexpadLink.LinkState.NotRunning => "start it, and the dock shows up here",
                HexpadLink.LinkState.ScreenOff => "hold its middle round button to turn it on",
                _ => "",
            };
            _p.Text(hint, new RectangleF(0, 206, TW, 30), sub, 16, FontWeight.Normal, TextAlignment.Center);
            _p.End();
            _ui.End();
            return;
        }

        // the six keys: the sharp faces when Hexpad sends them, else the device's
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            string id = $"key{i + 1}";
            var r = new RectangleF(KeyX(i % 3), KeyY(i / 3), Tile, Tile);
            _hit[id] = r;
            bool down = _pressed == id, hov = _hover == id, flash = Flash(id, now);
            var rr = down ? RectangleF.Inflate(r, -5, -5) : r;
            _p.Fill(rr, 0xFF000000, 18);
            var bmp = _faces.Best(i);
            if (bmp != null && !_s.Dark)
            {
                _p.PushRoundedClip(rr, 18);
                _p.Image(bmp, rr);
                _p.PopRoundedClip();
            }
            if (Ctl(id) is { Danger: true }) _p.Circle(rr.Right - 14, rr.Y + 14, 5, Theme.Bad);
            if (hov || flash) _p.Stroke(RectangleF.Inflate(rr, 3, 3), flash ? Theme.Accent : Theme.WithAlpha(Theme.Accent, 0.85f), 3, 21);
        }

        // a round button under each column
        for (int col = 0; col < 3; col++)
        {
            string id = $"btn{col + 7}";
            float cx = KeyX(col) + Tile / 2, cy = KeyY(2) + 18;
            var r = new RectangleF(cx - 24, cy - 24, 48, 48);
            _hit[id] = RectangleF.Inflate(r, 10, 6);
            bool down = _pressed == id, hov = _hover == id, flash = Flash(id, now);
            _p.Circle(cx, cy, down ? 21 : 24, down || flash ? Theme.Accent : Theme.Panel3);
            if (hov) _p.Stroke(RectangleF.Inflate(r, 3, 3), Theme.Accent, 2.5f, 27);
            string label = Ctl(id)?.Label ?? "";
            _p.Text(label, new RectangleF(cx - Tile / 2, cy + 28, Tile, 22), sub,
                FitSize(label, Tile - 4, 14), FontWeight.SemiBold, TextAlignment.Center);
        }

        // the knobs as sliders
        for (int k = 0; k < Knobs.Length; k++)
            SliderRow(Knobs[k], new RectangleF(ColX, KeysY + k * (RowH + RowGap), TW - Pad - ColX, RowH), now, sub);

        if (_confirm != null) ConfirmModern(sub);
        _p.End();
        _ui.End();
    }

    float FitSize(string s, float w, float max)
    {
        if (string.IsNullOrEmpty(s)) return max;
        float w100 = _p.MeasureWidth(s, 100, FontWeight.SemiBold);
        return w100 <= 0 ? max : MathF.Max(9, MathF.Min(max, MathF.Floor(100 * w / w100 * 2) / 2));
    }

    // what does not fit even at the smallest size it may have ends in "…"
    string Ellipsize(string s, float w, float size)
    {
        if (string.IsNullOrEmpty(s) || _p.MeasureWidth(s, size, FontWeight.SemiBold) <= w) return s;
        int n = s.Length;
        while (n > 1 && _p.MeasureWidth(s[..n].TrimEnd() + "…", size, FontWeight.SemiBold) > w) n--;
        return s[..n].TrimEnd() + "…";
    }

    bool HeaderButton(string id, RectangleF r, string icon)
    {
        bool click = _ui.Clickable(id, r, out bool hover, out bool held);
        _p.Fill(r, held ? Theme.Accent : hover ? Theme.Mix(Theme.Panel3, 0xFFFFFFFF, 0.12f) : Theme.Panel3, r.Height / 2);
        _p.Icon(icon, r, held ? Theme.OnAccent : Theme.Text, 16);
        return click;
    }

    // one knob: a card with its name and value, the push as a pill on the right, and a wide track for the laser
    void SliderRow(string id, RectangleF card, double now, uint sub)
    {
        var c = Ctl(id);
        var t = TypeOf(id);
        bool off = t == Slide.Abs && Off(id), on = t != Slide.None && !off;
        bool dragging = _drag == id, flash = Flash(id, now);
        bool hovS = on && (dragging || _hover == S(id)), hovP = _hover == id;
        _cards[id] = card;
        _p.Fill(card, Theme.Panel2, 18);
        if (flash) _p.Stroke(RectangleF.Inflate(card, -1, -1), Theme.Accent, 2, 17);

        // the push: a pill at the right of the name
        string push = PushLabel(c);
        float right = card.Right - 16;
        if (push.Length > 0)
        {
            float bw = Math.Clamp(_p.MeasureWidth(push, 15, FontWeight.SemiBold) + 36, 84, 132);
            var b = new RectangleF(card.Right - 12 - bw, card.Y + 12, bw, 38);
            _hit[id] = RectangleF.Inflate(b, 6, 4);
            bool down = _pressed == id;
            var bb = down ? RectangleF.Inflate(b, -2, -2) : b;
            _p.Fill(bb, down ? Theme.Accent : hovP ? Theme.Mix(Theme.Panel3, 0xFFFFFFFF, 0.12f) : Theme.Panel3, bb.Height / 2);
            if (hovP && !down) _p.Stroke(RectangleF.Inflate(b, 2, 2), Theme.Accent, 2, b.Height / 2 + 2);
            // what could end the session (it asks first) has the keys' red dot, beside the label
            float dr = c.Danger ? 10 : 0, pw = b.Width - 24 - dr, ps = MathF.Max(12, FitSize(push, pw - 2, 15));
            _p.Text(Ellipsize(push, pw, ps), new RectangleF(b.X + 12, b.Y, pw, b.Height), down ? Theme.OnAccent : Theme.Text,
                ps, FontWeight.SemiBold, TextAlignment.Center);
            if (c.Danger) _p.Circle(b.Right - 15, b.Y + b.Height / 2, 4, Theme.Bad);
            right = b.X - 12;
        }

        // name, then the value
        string name = KnobName(id, c), value = ValueText(id, t, now);
        if (t == Slide.None) value = push.Length > 0 ? "push only" : "";
        float x0 = card.X + 18;
        float vw = value.Length > 0 ? _p.MeasureWidth(value, 16, FontWeight.SemiBold) + 12 : 0;
        float nmax = MathF.Max(40, right - x0 - vw);
        float ns = MathF.Max(15, FitSize(name, nmax, 20));
        name = Ellipsize(name, nmax, ns);
        float nw = MathF.Min(_p.MeasureWidth(name, ns, FontWeight.SemiBold), nmax);
        _p.Text(name, new RectangleF(x0, card.Y + 12, nmax + 2, 38), Theme.Text, ns, FontWeight.SemiBold);
        if (value.Length > 0)
        {
            uint vc = t == Slide.None || off ? Theme.Faint : dragging && t == Slide.Jog ? Theme.Accent : value == "Muted" ? Theme.Warn : sub;
            _p.Text(value, new RectangleF(x0 + nw + 12, card.Y + 12, MathF.Max(0, right - x0 - nw - 10), 38), vc, 16, FontWeight.SemiBold);
        }

        // the track: 14 px, a 28 px thumb; the whole lower part of the card takes the laser
        float cy = card.Y + 84;
        var tr = new RectangleF(card.X + 30, cy - 7, card.Width - 60, 14);
        _track[id] = tr;
        if (on) _hit[S(id)] = new RectangleF(card.X, card.Y + 56, card.Width, card.Height - 56 + 5);
        float hl = _ui.Anim(Ui.Id("dkh", id), hovS ? 1 : 0, 18);
        if (!on)
        {
            _p.Fill(tr, Theme.WithAlpha(Theme.Panel3, 0.55f), 7);
            return;
        }
        _p.Fill(tr, Theme.Panel3, 7);
        if (t == Slide.Abs)
        {
            if (AbsValue(id, now) is not float v)
            {
                _p.Ring(tr.X + tr.Width / 2, cy, 11, Theme.Faint, 2);     // Hexpad does not know it yet
                return;
            }
            float kx = tr.X + tr.Width * v;
            bool muted = _s.Values.TryGetValue(id, out var x) && x.Muted && !dragging && !_holdUntil.ContainsKey(id);
            if (kx - tr.X > 1) _p.Fill(new RectangleF(tr.X, tr.Y, MathF.Max(14, kx - tr.X + 7), tr.Height), muted ? Theme.Faint : Theme.Accent, 7);
            Thumb(kx, cy, hl, dragging);
            return;
        }
        // jog: detent marks either side of the middle; the thumb follows the pull and springs back
        float mid = tr.X + tr.Width / 2, d = Detent(id), half = tr.Width / 2;
        for (float k = d; k < half - 6; k += d)
        {
            _p.Circle(mid - k, cy, 2, Theme.WithAlpha(Theme.Text, 0.3f));
            _p.Circle(mid + k, cy, 2, Theme.WithAlpha(Theme.Text, 0.3f));
        }
        _p.Icon(Theme.IcoLeft, new RectangleF(tr.X + 1, tr.Y, 14, tr.Height), sub, 9);
        _p.Icon(Theme.IcoRight, new RectangleF(tr.Right - 15, tr.Y, 14, tr.Height), sub, 9);
        float off2 = _ui.Anim(Ui.Id("dkj", id), dragging ? Math.Clamp(_jogOff, -half, half) : 0, dragging ? 1000 : 14);
        if (MathF.Abs(off2) > 1)
        {
            float a = MathF.Min(mid, mid + off2), w = MathF.Abs(off2);
            _p.Fill(new RectangleF(a - 7, tr.Y, w + 14, tr.Height), Theme.Accent, 7);
        }
        Thumb(mid + off2, cy, hl, dragging);
    }

    void Thumb(float x, float y, float hl, bool held)
    {
        if (hl > 0.01f) _p.Circle(x, y, 14 + 9 * hl, Theme.WithAlpha(Theme.Accent, 0.22f * hl));
        _p.Circle(x, y, held ? 15 : 14, Theme.Text);
    }

    void ConfirmModern(uint sub)
    {
        string label = ConfirmLabel();
        _p.Fill(new RectangleF(0, 0, TW, TH), Theme.WithAlpha(0xFF000000, 0.55f), 30);
        var card = new RectangleF(TW / 2 - 230, TH / 2 - 100, 460, 200);
        _p.Fill(card, Theme.Panel, 22);
        _p.Stroke(RectangleF.Inflate(card, -0.6f, -0.6f), Theme.Border, 1.2f, 22);
        _p.Text($"Run “{label}”?", new RectangleF(card.X + 24, card.Y + 20, card.Width - 48, 40), Theme.Text,
            FitSize($"Run “{label}”?", card.Width - 48, 22), FontWeight.SemiBold);
        _p.Text("It could end your VR session, so it asks first.", new RectangleF(card.X + 24, card.Y + 62, card.Width - 48, 26), sub, 15);
        if (_ui.Button("dk.no", new RectangleF(card.X + 24, card.Bottom - 68, 196, 46), "Cancel")) _confirm = null;
        if (_ui.Button("dk.yes", new RectangleF(card.Right - 220, card.Bottom - 68, 196, 46), "Do it", null, true))
        {
            _link.Input(_confirm, true);
            _link.Input(_confirm, false);
            _confirm = null;
        }
    }

    // a key says what it does; a knob's push is its own action
    string ConfirmLabel()
    {
        var c = Ctl(_confirm);
        string label = _confirm.StartsWith("enc") && !string.IsNullOrEmpty(c?.Push) ? c.Push : c?.Label;
        return string.IsNullOrEmpty(label) ? _confirm : label;
    }

    // ---- Windows 95: a "Hexpad" window, the key faces 1:1 in picture boxes, a trackbar group per knob ----
    const uint Blk95 = 0xFF000000, Wht95 = 0xFFFFFFFF, Navy95 = 0xFF000080;

    void Draw95(double now)
    {
        _canvas95 ??= new Surface(_p.Gpu, W95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(W95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var win = new RectangleF(0, 0, W95, H95);
        _p.Fill(win, Theme.W95Face);
        _p.Bevel(win, Painter.Edge.Window);
        string title = string.IsNullOrEmpty(_s.Profile) ? "Hexpad" : $"Hexpad - {_s.Profile}";
        _ui.Caption95(new RectangleF(3, 3, W95 - 6, 18), title);
        if (_ui.CaptionClose95("dk95.x", W95 - 5 - 16, 5, true, 6)) Toggle();

        bool show = Live || _s.State == HexpadLink.LinkState.ScreenOff && _faces.Any;
        if (!show)
        {
            // nothing to show: the classic message in one white box where the keys would be
            var msg = new RectangleF(8, 26, 228, 150);
            _p.Fill(msg, Wht95);
            _p.Bevel(msg, Painter.Edge.Sunken);
            W95Icons.Draw(_p, W95Icons.Id.Info32, msg.X + 14, msg.Y + 16);
            _ui.Text95(StateLine() + ".", new RectangleF(msg.X + 54, msg.Y + 18, msg.Width - 64, msg.Height - 30), Blk95, false,
                TextAlignment.Leading, true, true);
        }
        // the six keys: 68 x 68 picture boxes, the dock's own pixels
        for (int i = 0; i < HexpadLink.Keys && show; i++)
        {
            string id = $"key{i + 1}";
            var box = new RectangleF(8 + i % 3 * 78, 26 + i / 3 * 78, 72, 72);
            _hit[id] = Tex(box);
            bool down = _pressed == id;
            _p.Fill(box, show ? Blk95 : Wht95);
            var bmp = _faces.Bitmaps[i];
            if (show && bmp != null && !_s.Dark)
                _p.ImageNearest(bmp, new RectangleF(box.X + 2 + (down ? 1 : 0), box.Y + 2 + (down ? 1 : 0), 68, 68));
            _p.Bevel(box, down ? Painter.Edge.Pressed : Painter.Edge.Sunken);
            if (show && (_hover == id || Flash(id, now))) Dotted(RectangleF.Inflate(box, -4, -4), Flash(id, now) ? 0xFFFFFF00 : Wht95);
        }
        // the round buttons under the columns
        for (int col = 0; col < 3; col++)
        {
            string id = $"btn{col + 7}";
            var r = new RectangleF(8 + col * 78, 182, 72, 22);
            _hit[id] = Tex(r);
            var inner = _p.ButtonFace95(r, false, _pressed == id || Flash(id, now), false);
            string label = Ctl(id)?.Label ?? "";
            _ui.Text95(Fit95(label, inner.Width - 4), inner, Blk95, false, TextAlignment.Center, show);
        }
        // the knobs: a trackbar group each, the big one first
        for (int k = 0; k < Knobs.Length; k++)
            SliderGroup95(Knobs[k], new RectangleF(243, 23 + k * 63, 152, 61), show, now);
        // status bar
        string pages = _s.InFolder ? (_s.FolderPages > 1 ? $"Folder {_s.FolderPage + 1} of {_s.FolderPages}" : "Folder")
            : $"Page {_s.Page + 1} of {_s.Pages}";
        string name = _s.InFolder ? _s.FolderName : _s.PageName;
        string state = Live ? (_s.Ambient ? "Idle" : "Connected") : StateLine();
        if (show)
        {
            _ui.Pane95(new RectangleF(4, 214, 96, 20), pages);
            _ui.Pane95(new RectangleF(102, 214, 140, 20), Fit95(name, 130));
            _ui.Pane95(new RectangleF(244, 214, W95 - 248, 20), Fit95(state, W95 - 262), Live ? 0xFF008000 : 0xFF808080);
        }
        else _ui.Pane95(new RectangleF(4, 214, W95 - 8, 20), Fit95(StateLine(), W95 - 20));
        if (_confirm != null) Confirm95();
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }

    static RectangleF Tex(RectangleF r) => new(r.X * 2, r.Y * 2, r.Width * 2, r.Height * 2);

    string Fit95(string s, float w)
    {
        if (string.IsNullOrEmpty(s) || _ui.Width95(s) <= w) return s;
        int n = s.Length;
        while (n > 1 && _ui.Width95(s[..n] + "...") > w) n--;
        return s[..n].TrimEnd() + "...";
    }

    void Dotted(RectangleF r, uint c)
    {
        for (float x = r.X; x < r.Right; x += 2) { _p.Fill(new RectangleF(x, r.Y, 1, 1), c); _p.Fill(new RectangleF(x, r.Bottom - 1, 1, 1), c); }
        for (float y = r.Y; y < r.Bottom; y += 2) { _p.Fill(new RectangleF(r.X, y, 1, 1), c); _p.Fill(new RectangleF(r.Right - 1, y, 1, 1), c); }
    }

    // a group box titled with what the knob does: a trackbar (no ticks, so its thumb is the plain raised block),
    // under it the value and a push button. Focus dots round the trackbar while the laser is on it.
    void SliderGroup95(string id, RectangleF g, bool show, double now)
    {
        var c = Ctl(id);
        var t = show ? TypeOf(id) : Slide.None;
        bool off = t == Slide.Abs && Off(id), on = t != Slide.None && !off;
        bool dragging = _drag == id;
        _cards[id] = Tex(g);
        _ui.Group95(g, Fit95(KnobName(id, c), g.Width - 20));

        // the trackbar: a 4 px channel, an 11 x 18 thumb whose centre runs from end to end of it
        var bar = new RectangleF(g.X + 7, g.Y + 15, g.Width - 14, 18);
        var ch = new RectangleF(bar.X + 5, bar.Y + 7, bar.Width - 10, 4);
        _track[id] = Tex(new RectangleF(ch.X, ch.Y, ch.Width - 1, ch.Height));
        if (on) _hit[S(id)] = Tex(new RectangleF(g.X + 2, g.Y + 8, g.Width - 4, 28));
        _p.Fill(ch, Wht95);
        _p.Bevel(ch, Painter.Edge.Sunken);
        float mid = ch.X + MathF.Floor((ch.Width - 1) / 2), tx = mid;
        bool known = true;
        if (t == Slide.Abs && !off)
        {
            if (AbsValue(id, now) is float v) tx = ch.X + MathF.Round((ch.Width - 1) * v);
            else known = false;
        }
        else if (t == Slide.Jog && dragging)
            tx = mid + Math.Clamp(MathF.Round(_jogOff / 2), -(ch.Width - 1) / 2, (ch.Width - 1) / 2);
        var thumb = new RectangleF(tx - 5, bar.Y, 11, bar.Height);
        _p.Fill(thumb, Theme.W95Face);
        _p.Bevel(thumb, Painter.Edge.Raised);
        if (!on || !known) _p.FillChecker(RectangleF.Inflate(thumb, -2, -2), Wht95, Theme.W95Face);   // greyed: nothing to show / turn
        if (on && (_hover == S(id) || dragging) || Flash(id, now)) Dotted(new RectangleF(g.X + 4, g.Y + 13, g.Width - 8, 22), Blk95);

        // the value, and the push button
        float by = g.Y + 37;
        string push = PushLabel(c);
        bool canPush = show && push.Length > 0;
        if (!canPush) push = "Push";
        float bw = Math.Clamp(_ui.Width95(push) + 18, 52, 75);
        var b = new RectangleF(g.Right - 8 - bw, by, bw, 20);
        if (canPush) _hit[id] = Tex(RectangleF.Inflate(b, 1, 1));
        bool down = canPush && _pressed == id;
        var inner = _p.ButtonFace95(b, false, down, false);
        float o = down ? 1 : 0;
        _ui.Text95(Fit95(push, inner.Width - 6), new RectangleF(inner.X + o, inner.Y + o, inner.Width, inner.Height), Blk95, false,
            TextAlignment.Center, canPush);
        string value = show ? ValueText(id, t, now).Replace('−', '-') : "";     // 1995 typed a hyphen
        var vr = new RectangleF(g.X + 9, by, b.X - g.X - 13, 20);
        _ui.Text95(Fit95(value, vr.Width), vr, Blk95, false, TextAlignment.Leading, on);
    }

    void Confirm95()
    {
        string label = ConfirmLabel();
        var box = new RectangleF(W95 / 2 - 130, H95 / 2 - 56, 260, 112);
        _p.Fill(box, Theme.W95Face);
        _p.Bevel(box, Painter.Edge.Window);
        _ui.Caption95(new RectangleF(box.X + 3, box.Y + 3, box.Width - 6, 18), "Hexpad");
        W95Icons.Draw(_p, W95Icons.Id.Info32, box.X + 12, box.Y + 32);
        _ui.Text95(Fit95($"Run \"{label}\"?", box.Width - 64), new RectangleF(box.X + 52, box.Y + 30, box.Width - 60, 16), Blk95, true);
        _ui.Text95("It could end your VR session.", new RectangleF(box.X + 52, box.Y + 48, box.Width - 60, 16));
        if (_ui.Button95("dk95.yes", new RectangleF(box.X + box.Width / 2 - 80, box.Bottom - 32, 75, 23), "&Yes", true, true))
        {
            _link.Input(_confirm, true);
            _link.Input(_confirm, false);
            _confirm = null;
        }
        if (_ui.Button95("dk95.no", new RectangleF(box.X + box.Width / 2 + 5, box.Bottom - 32, 75, 23), "&No")) _confirm = null;
    }

    public void Dispose()
    {
        DestroyOverlay();
        Surf.Dispose();
        _canvas95?.Dispose();
    }
}
