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
    bool HomeInWorld { get; }             // its own place is in the room (not on a hand / the head)
    string TheaterState { get; }          // for the log: is it showing, has it a picture
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

    // curved around you at whatever distance it is: c = w / (2 pi r); r is the straight distance, as the screen faces
    // your head (also when it is up above you, lying back)
    public void Refit() => Bend = DesktopLayout.CurvatureFor(Size, Math.Max(0.5f, Vector3.Distance(Place.Translation, Head)));
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

    // The world dims behind it: a wide black wall just behind the screen, bent around you (half a circle). It is
    // under the screen both by sort order and by distance, so it can't end up in front of it whichever one SteamVR
    // goes by (1.8.0 used a sheet on the headset, 0.5 m away: in front of everything by distance).
    public void CreateOverlay()
    {
        _black ??= new Surface(_g, 8, 8);
        _p.Begin(_black.Bitmap, 0xFF000000);
        _p.End();
        _dim = new VrOverlay("erik.orbit.theater.dim", "OrbitOverlay theater dim");
        _dim.SetSortOrder(1);
        _dim.SetTexture(_black.Tex);
        _dim.SetCurvature(0.5f);
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
        _view.Move(Where(screen, vr, dist));
        if (_screen != null && _screen != screen) _screen.Theater = null;     // still gliding back from the last time
        _screen = screen;
        _t = _screen.Theater != null ? _t : 0;
        Blend(vr);
        _screen.Theater = _view;
        On = true;
        _checked = false;
        Log.Info($"theater: on - {screen.TheaterLabel} ({screen.TheaterState})");
    }

    // Where the big screen goes: out along the line from your head through the monitor / window it plays, tilted like
    // it, at the theater distance - where your screens are. NOT where you look: theater mode is started from the
    // watch, so at that moment you look at your wrist (lying back with the arm up, that put the screen on the ceiling).
    // A screen on a hand or the head has no such place: then level in front of you.
    public static Matrix4x4 Where(ITheaterScreen screen, VrSession vr, float dist)
    {
        var head = vr.Hmd.Translation;
        if (screen.HomeInWorld && screen.Home(out var home, out _, out _, out _))
        {
            var dir = home.Translation - head;
            if (dir.Length() > 0.2f)
            {
                var z = -Vector3.Normalize(dir);
                var up = MathUtil.Up(home);
                up -= Vector3.Dot(up, z) * z;
                if (up.Length() < 0.3f) { up = Vector3.UnitY - Vector3.Dot(Vector3.UnitY, z) * z; }
                if (up.Length() > 0.1f)
                {
                    up = Vector3.Normalize(up);
                    var x = Vector3.Normalize(Vector3.Cross(up, z));
                    up = Vector3.Cross(z, x);
                    return MathUtil.Basis(x, up, z, head - z * dist);
                }
            }
        }
        return Matrix4x4.CreateTranslation(0, 0, -dist) * MathUtil.YawFrame(vr.Hmd);
    }

    // Recenter while it plays: the layout has just been brought in front of you, the screen follows it
    public void Recenter(VrSession vr)
    {
        if (!On || !vr.HmdValid || _screen == null) return;
        float dist = Math.Clamp(Vector3.Distance(_view.Place.Translation, _view.Head), 0.6f, 10f);
        _view.Head = vr.Hmd.Translation;
        _view.Move(Where(_screen, vr, dist));
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
            if (a <= 0.004f || _screen == null) _dim.Hide();
            else
            {
                const float Behind = 0.6f;
                float r = Vector3.Distance(_view.World.Translation, _view.Head) + Behind;
                _dim.SetAbsolute(Matrix4x4.CreateTranslation(0, 0, -Behind) * _view.World);
                _dim.SetWidth(MathF.PI * r);        // half a circle around you at that radius (curvature 0.5)
                _dim.SetAlpha(a);
                _dim.Show();
            }
        }
        // once it has glided in: how the screen is doing (if it does not show up, the log says why)
        if (On && _screen != null && _t >= 1 && !_checked)
        {
            _checked = true;
            Log.Info($"theater: playing {_screen.TheaterLabel} - {_screen.TheaterState}, " +
                     $"{Vector3.Distance(_view.World.Translation, _view.Head):0.00} m away, {_view.Width:0.00} m wide");
        }
    }

    bool _checked;

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
