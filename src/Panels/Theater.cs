using System;
using System.Collections.Generic;
using System.Numerics;
using Valve.VR;

namespace Orbit;

// A monitor or window panel that can play the theater screen.
public interface ITheaterScreen : IPointerTarget
{
    TheaterView Theater { get; set; }     // non-null: this panel is the theater screen, drawn where it says
    bool TheaterHidden { get; set; }      // another panel is: step aside
    string TheaterLabel { get; }          // its name on the switcher under the screen
    bool Playable { get; }                // has an overlay and a live capture
    // its own (saved) place, size, bend and opacity, where the screen glides from and back to
    bool Home(out Matrix4x4 world, out float width, out float curvature, out float opacity);
}

// Where the theater screen is. Place / Size / Bend are the theater's own (grabbing moves and sizes them, in memory
// only); World / Width / Curvature / Opacity are what the panel draws this frame, gliding between its home and them.
public sealed class TheaterView
{
    public Matrix4x4 Place;
    public float Size, Bend;
    public Vector3 Head;                  // the headset now: the bend is centred on it
    public Matrix4x4 World;
    public float Width, Curvature, Opacity = 1;
    float _scaleFrom;

    public void Move(in Matrix4x4 world) { Place = world; Refit(); }

    // stick left / right while holding it: smaller / bigger
    public void Resize(float v, float dt) { Size = Math.Clamp(Size * (1 + v * dt * 0.8f), Theater.MinWidth, Theater.MaxWidth); Refit(); }

    public void ScaleBegin() => _scaleFrom = Size;
    public void Scale(float ratio) { Size = Math.Clamp(_scaleFrom * ratio, Theater.MinWidth, Theater.MaxWidth); Refit(); }

    // curved around you at whatever distance it is: c = w / (2 pi r)
    public void Refit()
    {
        var d = Place.Translation - Head;
        d.Y = 0;
        Bend = DesktopLayout.CurvatureFor(Size, Math.Max(0.5f, d.Length()));
    }
}

// Theater mode: one monitor or window becomes a big curved screen in front of you, the other panels and the keyboard
// step aside and the world dims behind it. The saved layout is not touched: the screen's place is an override for as
// long as it lasts, and the next tap glides it back. The size and distance you leave it at are kept for next time.
public sealed class Theater : IDisposable
{
    public const float MinWidth = 0.8f, MaxWidth = 8f;
    const float Glide = 0.45f;            // seconds to glide in / out

    readonly Gpu _g;
    readonly Painter _p;
    Surface _black;
    VrOverlay _dim;
    TheaterView _view;
    ITheaterScreen _screen;
    float _t;                             // 0 = the screen at its home, 1 = the theater screen
    public bool On { get; private set; }

    public Theater(Gpu g, Painter p) { _g = g; _p = p; }

    // The world dims behind it: a black overlay locked to the headset, under every other Orbit overlay.
    public void CreateOverlay()
    {
        _black ??= new Surface(_g, 8, 8);
        _p.Begin(_black.Bitmap, 0xFF000000);
        _p.End();
        _dim = new VrOverlay("erik.orbit.theater.dim", "OrbitOverlay theater dim");
        _dim.SetSortOrder(1);
        _dim.SetWidth(4);
        _dim.SetTexture(_black.Tex);
        _dim.SetRelative(OpenVR.k_unTrackedDeviceIndex_Hmd, Matrix4x4.CreateTranslation(0, 0, -0.5f));
    }

    public void DestroyOverlay()
    {
        Stop(null, null);
        _dim?.Dispose();
        _dim = null;
    }

    public ITheaterScreen Screen => On ? _screen : null;
    public TheaterView View => On ? _view : null;

    // the switcher under the screen: another monitor / window takes the same place and size, the two cross-fade there
    // (a panel that stops showing stays where it was while it fades)
    public void Switch(ITheaterScreen to)
    {
        if (!On || to == null || to == _screen || !to.Playable) return;
        _screen.Theater = null;
        _screen.TheaterHidden = true;
        to.TheaterHidden = false;
        _screen = to;
        _screen.Theater = _view;
        Log.Info("theater: switched to " + to.TheaterLabel);
    }

    public void Start(ITheaterScreen screen, VrSession vr, TheaterConfig cfg)
    {
        if (On || screen == null || !vr.HmdValid) return;
        float size = Math.Clamp(cfg.Width, MinWidth, MaxWidth), dist = Math.Clamp(cfg.Distance, 0.6f, 10f);
        _view = new TheaterView { Size = size, Head = vr.Hmd.Translation };
        _view.Move(Matrix4x4.CreateTranslation(0, 0, -dist) * MathUtil.GazeFrame(vr.Hmd));
        if (_screen != null && _screen != screen) _screen.Theater = null;     // still gliding back from the last time
        _screen = screen;
        _t = _screen.Theater != null ? _t : 0;
        Blend(vr);
        _screen.Theater = _view;
        On = true;
        Log.Info("theater: on");
    }

    // in front of you again (Recenter while it plays)
    public void Recenter(VrSession vr)
    {
        if (!On || !vr.HmdValid) return;
        var d = _view.Place.Translation - vr.Hmd.Translation;
        _view.Head = vr.Hmd.Translation;
        _view.Move(Matrix4x4.CreateTranslation(0, 0, -Math.Clamp(d.Length(), 0.6f, 10f)) * MathUtil.GazeFrame(vr.Hmd));
    }

    // glides back (vr != null), or snaps back at once; keeps the size and distance for next time
    public void Stop(VrSession vr, TheaterConfig cfg)
    {
        if (On && cfg != null && _view != null)
        {
            cfg.Width = _view.Size;
            cfg.Distance = Math.Clamp(Vector3.Distance(_view.Place.Translation, _view.Head), 0.6f, 10f);
            Log.Info($"theater: off ({cfg.Width:0.00} m wide at {cfg.Distance:0.00} m)");
        }
        On = false;
        if (vr == null && _screen != null) { _screen.Theater = null; _screen = null; _t = 0; }
    }

    // before the panels update: where the screen is this frame, who steps aside, how dark the world is
    public void Update(VrSession vr, float dt, float dim, IReadOnlyCollection<ITheaterScreen> live)
    {
        if (_screen != null && (!Contains(live, _screen) || !_screen.Playable))
        {
            // its panel was rebuilt (monitors changed) or removed, or its window closed: theater is over
            _screen.Theater = null;
            _screen = null;
            if (On) { On = false; Log.Info("theater: its screen went away"); }
        }
        // the others step aside while it plays (a window panel added meanwhile too), and come back near the end of
        // the glide home, so the screen does not pass through them on its way
        bool aside = On || (_screen != null && _t > 0.3f);
        foreach (var s in live) s.TheaterHidden = aside && s != _screen;
        _t = Math.Clamp(_t + (On ? dt : -dt) / Glide, 0, 1);
        if (_screen != null)
        {
            if (vr.HmdValid) _view.Head = vr.Hmd.Translation;
            if (!On && _t <= 0) { _screen.Theater = null; _screen = null; }
            else Blend(vr);
        }
        if (_dim != null)
        {
            float a = Math.Clamp(dim, 0, 0.95f) * Ease(_t);
            if (a <= 0.004f) _dim.Hide();
            else { _dim.SetAlpha(a); _dim.Show(); }
        }
    }

    static bool Contains(IReadOnlyCollection<ITheaterScreen> live, ITheaterScreen s)
    {
        foreach (var x in live) if (x == s) return true;
        return false;
    }

    static float Ease(float t) => t * t * (3 - 2 * t);

    void Blend(VrSession vr)
    {
        float e = Ease(_t);
        var v = _view;
        if (!_screen.Home(out var home, out float hw, out float hc, out float ho)) { home = v.Place; hw = v.Size; hc = v.Bend; ho = 1; }
        Matrix4x4.Decompose(home, out _, out var qa, out var ta);
        Matrix4x4.Decompose(v.Place, out _, out var qb, out var tb);
        v.World = Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(qa, qb, e)) * Matrix4x4.CreateTranslation(Vector3.Lerp(ta, tb, e));
        v.Width = hw + (v.Size - hw) * e;
        v.Curvature = DesktopLayout.Flat(hc + (v.Bend - hc) * e);
        v.Opacity = ho + (1 - ho) * e;
    }

    public void Dispose()
    {
        _dim?.Dispose();
        _black?.Dispose();
    }
}
