using System;
using System.Numerics;
using Valve.VR;
using Windows.Graphics.DirectX.Direct3D11;

namespace Orbit;

// Shared mouse ownership: only one hand drives the real cursor at a time.
public sealed class MouseOwner
{
    public Hand? Hand;
    public IPointerTarget Panel;      // monitor or window panel
    public double LastMove;
    public double LastClick = -10;    // a click from VR went into Windows (keyboard pop-up uses it)
    public Action Clicked;

    public void NoteClick() { LastClick = Clock.Now; Clicked?.Invoke(); }
}

// One monitor mirrored into VR. Pointing moves the real Windows cursor there,
// trigger = left click, B/Y = right click, stick = wheel, grip = grab and move.
public sealed class DesktopPanel : ITheaterScreen, IDisposable
{
    readonly Gpu _g;
    readonly ConfigStore _cfg;
    readonly MouseOwner _mouse;
    public readonly MonitorInfo Mon;
    public readonly DesktopConfig Cfg;
    MonitorCapture _cap;
    public VrOverlay Ov { get; private set; }
    VrSession _vr;
    double _nextFrame;
    float _wheelAcc;
    readonly bool[] _hovering = new bool[2];
    Vector2 _lastPx;
    public string Error;

    public DesktopLayout Layout;

    public DesktopPanel(Gpu g, ConfigStore cfg, MouseOwner mouse, MonitorInfo mon, DesktopConfig dc)
    {
        _g = g; _cfg = cfg; _mouse = mouse; Mon = mon; Cfg = dc;
    }

    public void CreateOverlay(VrSession vr, IDirect3DDevice winrtDevice, int n)
    {
        _vr = vr;
        Ov = new VrOverlay($"erik.orbit.desktop.{n}", $"OrbitOverlay Desktop {Mon.Name}");
        Ov.SetFlag(VROverlayFlags.IgnoreTextureAlpha, true);
        Ov.SetSortOrder(100);
        try { _cap = new MonitorCapture(_g, winrtDevice, Mon); }
        catch (Exception e) { Error = e.Message; Log.Warn($"capture {Mon}: {e}"); }
    }

    public void DestroyOverlay()
    {
        ReleaseButtons();
        _cap?.Dispose(); _cap = null;
        _hasTexture = false;
        _vis = 0;
        Ov?.Dispose(); Ov = null;
        _vr = null;
    }

    public void Update(VrSession vr, double now, bool globallyVisible, int fps)
    {
        if (Ov == null) return;
        float dt = (float)Math.Clamp(now - _lastNow, 0, 0.1);
        _lastNow = now;
        var th = Theater;
        bool want = (globallyVisible || th != null) && !TheaterHidden && Cfg.Enabled && (Cfg.Place.M != null || th != null) && _cap != null && !_cap.Closed;
        if (want)
        {
            if (th != null) Ov.SetAbsolute(th.World);
            else if (!Placement.Apply(Cfg.Place, vr, Ov)) want = false;     // attached to a device that is gone
        }
        // fade in once there is a picture, fade out when hidden
        _vis = Math.Clamp(_vis + (want && _hasTexture ? dt : -dt) / 0.2f, 0, 1);
        if (!want && Ov.Visible) ReleaseButtons();
        if (_vis <= 0) Ov.Hide();
        else
        {
            Ov.SetWidth(th?.Width ?? Cfg.Width);
            Ov.SetCurvature(th?.Curvature ?? Cfg.Curvature);
            Ov.SetAlpha((th?.Opacity ?? Cfg.Opacity) * (1 - MathF.Pow(1 - _vis, 3)));
            Ov.Show();
        }
        _cap?.SetCursorCapture(!(want && _mouse.Panel == this && _mouse.Hand != null && Cfg.Mouse));
        if (!want || !CaptureDue(ref _nextFrame, now, EffectiveFps(vr, fps))) return;
        if (_cap.Poll())
        {
            Ov.SetMouseScale(_cap.Target.W, _cap.Target.H);
            Ov.SetTexture(_cap.Target.Tex);
            _hasTexture = true;
        }
    }

    public bool SmartCapture = true;

    // the capture ended (monitor dropped out for a moment, or a capture error): the app rebuilds it
    public bool CaptureDead => _cap != null && _cap.Closed;

    float EffectiveFps(VrSession vr, int fps) =>
        !SmartCapture ? fps
        : Theater is { } th ? SmartFps(vr, th.World, th.Width, _hovering[0] || _hovering[1], fps)
        : SmartFps(vr, Cfg.Place, Cfg.Width, _hovering[0] || _hovering[1], fps);

    // Paces captures to an exact average rate on a loop that runs at the compositor's rate:
    // "next = now + 1/fps" skipped every other loop when the two were close (60 fps on an
    // 80-90 Hz loop came out at ~40). The next slot is kept on the grid instead (2 ms early is fine).
    public static bool CaptureDue(ref double next, double now, float fps)
    {
        double period = 1.0 / Math.Clamp(fps, 1, 240);
        if (next > now + period) next = now + period;      // the rate just went up (you turned to it)
        if (now + 0.002 < next) return false;
        next = Math.Max(next + period, now - period * 0.5);
        return true;
    }

    // Smart rate: the monitor you point at or look at runs at the headset's refresh rate (it is
    // limited by the monitor's own rate and by the loop), 15 fps at the edge of view, 4 behind you.
    public static float SmartFps(VrSession vr, PlacementConfig place, float width, bool hovered, int fps)
    {
        if (hovered || !vr.HmdValid) return Math.Max(fps, vr.DisplayHz);
        if (!Placement.TryWorld(place, vr, out var w)) return fps;
        return SmartFps(vr, w, width, false, fps);
    }

    public static float SmartFps(VrSession vr, in Matrix4x4 w, float width, bool hovered, int fps)
    {
        float focus = Math.Max(fps, vr.DisplayHz);
        if (hovered || !vr.HmdValid) return focus;
        var to = w.Translation - vr.Hmd.Translation;
        if (to.LengthSquared() < 1e-6f) return fps;
        float cos = Vector3.Dot(Vector3.Normalize(to), Vector3.Normalize(MathUtil.Forward(vr.Hmd)));
        // the panel's half-width widens the "looked at" cone for big / close monitors
        float half = MathF.Atan2(width / 2, to.Length());
        float ang = MathF.Acos(Math.Clamp(cos, -1f, 1f)) - half;
        if (ang < 30 * MathF.PI / 180) return focus;
        if (ang < 70 * MathF.PI / 180) return Math.Min(fps, 15);
        return Math.Min(fps, 4);
    }

    bool _hasTexture;
    float _vis;
    double _lastNow;

    // ---- IPointerTarget ----
    public VrOverlay Overlay => Ov;
    public bool Interactive => Ov != null && Ov.Visible && _vis > 0.8f;
    public bool AcceptsFrom(Hand h) => true;
    public Vector2 TextureSize => new(_cap?.Target?.W ?? Mon.W, _cap?.Target?.H ?? Mon.H);

    bool OwnsMouse(Hand h)
    {
        double now = Clock.Now;
        // take over when nobody owns it, the owner stopped pointing at a desktop, or went quiet for a moment
        if (_mouse.Hand == null || _mouse.Hand == h || _mouse.Panel == null || now - _mouse.LastMove > 0.4)
        {
            _mouse.Hand = h; _mouse.Panel = this; _mouse.LastMove = now;
            return true;
        }
        return false;
    }

    (int x, int y) ScreenPoint(Vector2 px)
    {
        var size = TextureSize;
        int x = Mon.X + (int)Math.Clamp(px.X * Mon.W / size.X, 0, Mon.W - 1);
        int y = Mon.Y + (int)Math.Clamp(px.Y * Mon.H / size.Y, 0, Mon.H - 1);
        return (x, y);
    }

    public void PointerMove(Hand h, Vector2 px)
    {
        _hovering[(int)h] = true;
        if (!Cfg.Mouse || !OwnsMouse(h)) return;
        if (Vector2.DistanceSquared(px, _lastPx) < 0.25f) return;
        _lastPx = px;
        var (x, y) = ScreenPoint(px);
        Native.SetCursorPos(x, y);
    }

    public void PointerLeave(Hand h)
    {
        _hovering[(int)h] = false;
        if (_mouse.Hand == h && _mouse.Panel == this) { _mouse.Hand = null; _mouse.Panel = null; }
    }

    bool _leftDown, _rightDown;

    public void PointerButton(Hand h, PtrButton b, bool down, Vector2 px)
    {
        if (!Cfg.Mouse && down) return;      // ups still go out, or a button would stay held
        if (down)
        {
            _mouse.Hand = h; _mouse.Panel = this; _mouse.LastMove = Clock.Now;
            _mouse.NoteClick();
            var (x, y) = ScreenPoint(px);
            Native.SetCursorPos(x, y);
        }
        if (b == PtrButton.Primary && down != _leftDown)
        {
            _leftDown = down;
            Native.SendMouse(down ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP);
        }
        else if (b == PtrButton.Secondary && down != _rightDown)
        {
            _rightDown = down;
            Native.SendMouse(down ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP);
        }
    }

    void ReleaseButtons()
    {
        if (_leftDown) { _leftDown = false; Native.SendMouse(Native.MOUSEEVENTF_LEFTUP); }
        if (_rightDown) { _rightDown = false; Native.SendMouse(Native.MOUSEEVENTF_RIGHTUP); }
    }

    public void PointerScroll(Hand h, float notches, Vector2 px)
    {
        if (!Cfg.Mouse || !OwnsMouse(h)) return;
        _wheelAcc += notches * 120;
        int delta = (int)_wheelAcc;
        if (delta == 0) return;
        _wheelAcc -= delta;
        Native.SendMouse(Native.MOUSEEVENTF_WHEEL, delta);
    }

    public bool Grabbable => true;

    bool DrivesCursor(Hand h) => Cfg.Mouse && _mouse.Hand == h && _mouse.Panel == this;
    public float LaserCursorScale(Hand h) => DrivesCursor(h) ? (Theater?.Width ?? Cfg.Width) / Math.Max(1, Mon.W) : 0;

    public Matrix4x4 WorldTransform =>
        Theater is { } th ? th.World
        : _vr != null && Placement.TryWorld(Cfg.Place, _vr, out var w) ? w : MathUtil.FromArray(Cfg.Place.M);

    // ---- theater mode (Panels/Theater.cs): the big screen is an override, nothing here is saved ----
    public TheaterView Theater { get; set; }
    public bool TheaterHidden { get; set; }
    public string TheaterLabel => string.IsNullOrWhiteSpace(Mon.Name) ? Mon.Device.TrimStart('\\', '.') : Mon.Name;
    public bool Playable => Ov != null && _cap != null && !_cap.Closed && Cfg.Enabled;

    public bool Home(out Matrix4x4 world, out float width, out float curvature, out float opacity)
    {
        width = Cfg.Width; curvature = Cfg.Curvature; opacity = Cfg.Opacity;
        world = default;
        return _vr != null && Cfg.Place.M != null && Placement.TryWorld(Cfg.Place, _vr, out world);
    }

    DesktopLayout.Snap? _snap;
    float[] _twoStartWidths;
    System.Collections.Generic.List<DesktopPanel> _twoRow;
    TheaterView _grabView;      // the grab started on the theater screen; theater starting / ending mid-grab drops the rest of it

    bool GrabStale => _grabView != Theater;

    public void GrabBegin(bool detach)
    {
        _snap = null;
        _grabView = Theater;
        if (Theater != null) return;
        if (detach && Cfg.Group != null) Layout?.Detach(this);
    }

    public void GrabMove(Matrix4x4 world)
    {
        if (_vr == null || GrabStale) return;
        if (Theater is { } th) { th.Move(world); return; }
        Placement.SetWorld(Cfg.Place, _vr, world);
        if (Cfg.Group != null) Layout?.Reflow(this);       // the whole row follows
        _snap = Layout?.FindSnap(this, world);
        _cfg.MarkDirty();
    }

    // stick left/right while holding: bend (the whole row shares the new radius)
    public void GrabStickX(float v, float dt)
    {
        if (GrabStale) return;
        if (Theater is { } th) { th.Resize(v, dt); return; }       // it bends around you by itself: the stick sizes it
        Cfg.Curvature = DesktopLayout.Flat(Math.Clamp(Cfg.Curvature + v * dt * 0.25f, 0f, 1f));
        if (Cfg.Group != null) Layout?.Reflow(this);
        _cfg.MarkDirty();
    }

    public void GrabTwoHandBegin()
    {
        if (Theater is { } th) { th.ScaleBegin(); return; }
        _twoRow = Cfg.Group != null && Layout != null ? Layout.Members(Cfg.Group) : new() { this };
        if (!_twoRow.Contains(this)) _twoRow.Add(this);
        _twoStartWidths = _twoRow.ConvertAll(p => p.Cfg.Width).ToArray();
    }

    // uniform scale: widths grow, curvature stays, so the row keeps its shape
    public void GrabTwoHandScale(float ratio)
    {
        if (GrabStale) return;
        if (Theater is { } th) { th.Scale(ratio); return; }
        if (_twoRow == null) return;
        for (int i = 0; i < _twoRow.Count; i++)
            _twoRow[i].Cfg.Width = Math.Clamp(_twoStartWidths[i] * ratio, 0.25f, 4f);
        if (Cfg.Group != null) Layout?.Reflow(this);
        _cfg.MarkDirty();
    }

    public bool GrabSnapPreview => _snap != null;

    public void GrabEnd()
    {
        bool layout = _grabView == null && Theater == null;
        _grabView = null;
        if (layout && _snap is { } s && Layout != null) Layout.Join(this, s);
        _snap = null;
        _twoRow = null;
        if (layout) _cfg.MarkDirty();
    }

    // used by --snapshot
    public MonitorCapture StartCaptureOnly(IDirect3DDevice dev) => _cap = new MonitorCapture(_g, dev, Mon);

    public void Dispose() => DestroyOverlay();
}
