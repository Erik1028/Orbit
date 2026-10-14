using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Valve.VR;
using Vortice.DirectWrite;

namespace Orbit;

// The switcher under the theater screen: a button for every monitor and mirrored window (left to right as they are on
// your desk, then the windows), the one playing lit, and Exit at the end. It shows up while you point at the screen (and
// for a moment when theater mode starts), then fades away so nothing sits under the picture.
public sealed class TheaterBar : IPointerTarget, IDisposable
{
    // modern: pills on a rounded bar; 95: a strip of taskbar buttons on a half-size canvas, drawn 2x
    const int H = 84, Pad = 10, ChipW = 230, Gap = 8;
    const int H95 = 28, Btn95W = 150, Exit95W = 46;

    readonly Gpu _g;
    readonly Painter _p;
    readonly Ui _ui;
    readonly PointerState _ptr = new();
    Surface _surf, _canvas95, _retired;     // _retired: the last size's texture, freed once the overlay has the new one
    int _tw, _th, _w95, _layoutKey = -1;
    public VrOverlay Ov { get; private set; }
    float _vis;
    bool _dirty = true, _wasOn;
    double _showUntil;
    Hand? _owner;
    Matrix4x4 _world = Matrix4x4.Identity;
    readonly List<ITheaterScreen> _choices = new();
    ITheaterScreen _current;

    public Action<ITheaterScreen> OnSwitch;
    public Action OnExit;

    public TheaterBar(Gpu g, Painter p) { _g = g; _p = p; _ui = new Ui(p); }

    public void CreateOverlay()
    {
        Ov = new VrOverlay("erik.orbit.theater.bar", "OrbitOverlay theater switcher");
        Ov.SetSortOrder(110);           // over the screens, under the keyboard and the panels
        Ov.SetFlag(VROverlayFlags.IsPremultiplied, true);
        _layoutKey = -1;
        _vis = 0;
    }

    public void DestroyOverlay() { _ptr.Leave(); _owner = null; Ov?.Dispose(); Ov = null; _vis = 0; }

    // choices: the screens that can play, in bar order; pointing: a laser is on the theater screen or on the bar
    public void Update(VrSession vr, double now, float dt, Theater theater, List<ITheaterScreen> choices, bool pointing)
    {
        if (Ov == null) return;
        var view = theater.View;
        bool on = theater.On && view != null && theater.Screen != null;
        if (on && !_wasOn) _showUntil = now + 4;        // theater just started: show where the switcher is
        _wasOn = on;
        if (on && (pointing || _owner != null)) _showUntil = Math.Max(_showUntil, now + 2.5);
        _vis = Math.Clamp(_vis + ((on && now < _showUntil) ? dt : -dt) / 0.18f, 0, 1);
        if (!on || _vis <= 0)
        {
            _ptr.Leave();
            _owner = null;
            Ov.Hide();
            return;
        }

        if (!Same(choices) || theater.Screen != _current)
        {
            _choices.Clear();
            _choices.AddRange(choices);
            _current = theater.Screen;
            _dirty = true;
        }
        int key = _choices.Count * 2 + (Theme.Classic ? 1 : 0);
        if (key != _layoutKey) Relayout(key);

        // under the middle of the screen, the same height on your view whatever the distance, bent like the screen;
        // it rises into place a little as it fades in
        var ts = theater.Screen.TextureSize;
        float screenH = view.Width * ts.Y / Math.Max(1, ts.X);
        float dist = vr.HmdValid ? Vector3.Distance(vr.Hmd.Translation, view.World.Translation) : 2.5f;
        float hM = Math.Clamp(0.05f * dist, 0.05f, 0.4f), wM = hM * _tw / _th;
        float e = 1 - MathF.Pow(1 - _vis, 3);
        _world = Matrix4x4.CreateTranslation(0, -(screenH / 2 + hM * 0.35f + hM / 2) - (1 - e) * hM * 0.4f, 0) * view.World;
        Ov.SetAbsolute(_world);
        Ov.SetWidth(wM);
        Ov.SetCurvature(view.Width > 0 ? Math.Clamp(wM * view.Curvature / view.Width, 0, 1) : 0);
        Ov.SetAlpha(e);
        if (_dirty || _ui.Animating)
        {
            Draw();
            Ov.SetTexture(_surf.Tex);
            _retired?.Dispose();
            _retired = null;
            _dirty = _ui.Interacting || _ui.Changed;
        }
        Ov.Show();
    }

    bool Same(List<ITheaterScreen> l)
    {
        if (l.Count != _choices.Count) return false;
        for (int i = 0; i < l.Count; i++) if (l[i] != _choices[i]) return false;
        return true;
    }

    void Relayout(int key)
    {
        _layoutKey = key;
        int n = _choices.Count;
        if (Theme.Classic)
        {
            _w95 = 4 + n * (Btn95W + 3) + Exit95W + 4;
            _tw = _w95 * 2; _th = H95 * 2;
        }
        else
        {
            _tw = Pad + n * (ChipW + Gap) + (H - 2 * Pad) + Pad;
            _th = H;
        }
        _retired?.Dispose();
        _retired = Ov != null ? _surf : null;           // the overlay still shows it until the next SetTexture
        if (Ov == null) _surf?.Dispose();
        _surf = new Surface(_g, _tw, _th);
        _canvas95?.Dispose();
        _canvas95 = null;
        Ov?.SetMouseScale(_tw, _th);
        _dirty = true;
    }

    // ---------------- drawing ----------------

    // the names on the buttons; two monitors of the same model get a number (left to right)
    string Label(int i)
    {
        string name = _choices[i].TheaterLabel;
        int same = 0, nth = 0;
        for (int j = 0; j < _choices.Count; j++)
            if (_choices[j].TheaterLabel == name) { same++; if (j <= i) nth++; }
        return same > 1 ? $"{name} {nth}" : name;
    }

    void Draw()
    {
        if (Theme.Classic) Draw95();
        else DrawModern();
    }

    void DrawModern()
    {
        _ui.Begin(_ptr, 1);
        _p.Begin(_surf.Bitmap, 0x00000000);
        _p.Fill(new RectangleF(0, 0, _tw, _th), Theme.OverlayBg, _th / 2f);
        _p.Stroke(new RectangleF(1, 1, _tw - 2, _th - 2), 0x40FFFFFF, 2, _th / 2f - 1);
        float x = Pad, ch = _th - 2 * Pad;
        ITheaterScreen pick = null;
        for (int i = 0; i < _choices.Count; i++, x += ChipW + Gap)
        {
            var r = new RectangleF(x, Pad, ChipW, ch);
            var s = _choices[i];
            bool on = s == _current;
            string id = Ui.Id("thb", i);
            if (_ui.Clickable(id, r, out bool hover, out bool held) && !on) pick = s;
            // the lit one moves over with a short fade; hover lightens, a press dips
            float a = _ui.Anim(id + "#a", on ? 1 : 0, 14), hv = _ui.Anim(id + "#h", hover ? 1 : 0, 18), pr = _ui.Anim(id + "#p", held ? 1 : 0, 30);
            uint bg = Theme.Mix(Theme.Mix(Theme.Panel3, Theme.Accent, a), 0xFFFFFFFF, 0.12f * hv * (1 - a));
            bg = Theme.Mix(bg, 0xFF000000, 0.2f * pr);
            uint fg = Theme.Mix(Theme.Text, Theme.OnAccent, a);
            r = RectangleF.Inflate(r, -ChipW * 0.02f * pr, -ch * 0.04f * pr);
            _p.Fill(r, bg, r.Height / 2);
            _p.Icon(s is WindowPanel ? Theme.IcoWindow : Theme.IcoMonitor, new RectangleF(r.X + 14, r.Y, 30, r.Height), fg, 19);
            _p.Text(Ellipsize(Label(i), r.Width - 66, 17, FontWeight.SemiBold), new RectangleF(r.X + 50, r.Y, r.Width - 62, r.Height), fg, 17, FontWeight.SemiBold);
        }
        // Exit: everything glides back
        var xr = new RectangleF(x, Pad, ch, ch);
        bool exit = _ui.Clickable("thb.x", xr, out bool xh, out bool xheld);
        float xa = _ui.Anim("thb.x#h", xh ? 1 : 0, 18), xp = _ui.Anim("thb.x#p", xheld ? 1 : 0, 30);
        _p.Fill(RectangleF.Inflate(xr, -ch * 0.04f * xp, -ch * 0.04f * xp), Theme.Mix(Theme.Mix(Theme.Panel3, 0xFFFFFFFF, 0.12f * xa), Theme.Accent, xp), ch / 2);
        _p.Icon("\uE711", xr, Theme.Mix(Theme.Text, Theme.OnAccent, xp), 18);
        _p.End();
        _ui.End();
        Act(pick, exit);
    }

    void Draw95()
    {
        _canvas95 ??= new Surface(_p.Gpu, _w95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(_w95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var strip = new RectangleF(0, 0, _w95, H95);
        _p.Fill(strip, Theme.W95Face);
        _p.Bevel(strip, Painter.Edge.Raised);
        float x = 4;
        ITheaterScreen pick = null;
        for (int i = 0; i < _choices.Count; i++, x += Btn95W + 3)
        {
            var s = _choices[i];
            bool on = s == _current;
            if (_ui.Button95(Ui.Id("thb95", i), new RectangleF(x, 3, Btn95W, 22), Fit95(Label(i), Btn95W - 30), true, false, on,
                    s is WindowPanel ? W95Icons.Id.Window : W95Icons.Id.Computer) && !on)
                pick = s;
        }
        bool exit = _ui.Button95("thb95.x", new RectangleF(x, 3, Exit95W, 22), "Exit");
        _p.End();
        _p.Begin(_surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, _tw, _th));
        _p.End();
        _ui.End();
        Act(pick, exit);
    }

    void Act(ITheaterScreen pick, bool exit)
    {
        if (pick != null) { OnSwitch?.Invoke(pick); _dirty = true; }
        else if (exit) OnExit?.Invoke();
    }

    string Ellipsize(string s, float maxW, float size, FontWeight wt)
    {
        if (string.IsNullOrEmpty(s) || _p.MeasureWidth(s, size, wt) <= maxW) return s;
        int n = s.Length;
        while (n > 1 && _p.MeasureWidth(s[..n] + "…", size, wt) > maxW) n--;
        if (n > 1 && char.IsHighSurrogate(s[n - 1])) n--;
        return s[..n].TrimEnd() + "…";
    }

    string Fit95(string s, float w)
    {
        if (string.IsNullOrEmpty(s) || _ui.Width95(s) <= w) return s;
        int n = s.Length;
        while (n > 1 && _ui.Width95(s[..n] + "...") > w) n--;
        return s[..n].TrimEnd() + "...";
    }

    // ---------------- input: one hand at a time ----------------

    public VrOverlay Overlay => Ov;
    public bool Interactive => Ov != null && Ov.Visible && _vis > 0.8f;
    public bool AcceptsFrom(Hand h) => true;
    public Vector2 TextureSize => new(Math.Max(1, _tw), Math.Max(1, _th));

    public void PointerMove(Hand h, Vector2 px)
    {
        _owner ??= h;
        if (_owner != h) return;
        _ptr.Move(px.X, px.Y);
        if (_ui.NeedsRedrawForMove(_ptr, Theme.Classic ? 2 : 1)) _dirty = true;
    }

    public void PointerLeave(Hand h)
    {
        if (_owner != h) return;
        _owner = null;
        _ptr.Leave();
        _dirty = true;
    }

    public void PointerButton(Hand h, PtrButton b, bool down, Vector2 px)
    {
        if (b != PtrButton.Primary) return;
        if (down)
        {
            if (_owner != h && _ptr.Down) return;
            _owner = h;
            _ptr.Move(px.X, px.Y);
            _ptr.Press();
        }
        else
        {
            if (_owner != h) return;
            _ptr.Move(px.X, px.Y);
            _ptr.Release();
        }
        _dirty = true;
    }

    public void PointerScroll(Hand h, float notches, Vector2 px) { }

    // it rides under the screen: grab the screen to move both
    public bool Grabbable => false;
    public Matrix4x4 WorldTransform => _world;
    public void GrabBegin(bool detach) { }
    public void GrabMove(Matrix4x4 world) { }
    public void GrabStickX(float v, float dt) { }
    public void GrabTwoHandBegin() { }
    public void GrabTwoHandScale(float ratio) { }
    public bool GrabSnapPreview => false;
    public void GrabEnd() { }

    // used by --snapshot: the bar for these screens, without VR
    public Surface DrawFor(List<ITheaterScreen> choices, ITheaterScreen current)
    {
        _choices.Clear();
        _choices.AddRange(choices);
        _current = current;
        int key = _choices.Count * 2 + (Theme.Classic ? 1 : 0);
        if (key != _layoutKey) Relayout(key);
        Draw();
        return _surf;
    }

    public void Dispose()
    {
        DestroyOverlay();
        _surf?.Dispose();
        _retired?.Dispose();
        _canvas95?.Dispose();
    }
}
