using System;
using System.Numerics;
using Valve.VR;
using Vortice.Direct3D11;

namespace Orbit;

// Wrapper around one SteamVR overlay handle. Setters skip IPC when nothing changed.
public sealed class VrOverlay : IDisposable
{
    public readonly ulong Handle;
    public readonly string Key;
    static CVROverlay Ov => OpenVR.Overlay;

    bool _visible;
    float _width = -1, _alpha = -1, _curv = -1, _aspect = -1;
    Matrix4x4 _lastM;
    uint _lastDevice = uint.MaxValue - 1;
    bool _hasTransform;
    double _nextWarn;

    public bool Visible => _visible;

    // Night mode: every overlay Orbit shows in VR is multiplied by this (1 = normal brightness).
    // The SteamVR dashboard tab is left alone (it is not created through the public constructor).
    static readonly System.Collections.Generic.List<VrOverlay> _dimmed = new();
    static float _dim = 1;

    public static void SetDim(float d)
    {
        d = Math.Clamp(d, 0.05f, 1f);
        if (d == _dim) return;
        _dim = d;
        foreach (var o in _dimmed) o.PushColor();
    }

    public VrOverlay(string key, string name)
    {
        Key = key;
        ulong h = 0;
        var e = Ov.CreateOverlay(key, name, ref h);
        if (e != EVROverlayError.None) throw new InvalidOperationException($"CreateOverlay({key}) failed: {e}");
        Handle = h;
        _dimmed.Add(this);
        PushColor();
    }

    // Dashboard overlays come as a pair; the thumbnail handle is exposed separately.
    VrOverlay(ulong handle, string key) { Handle = handle; Key = key; }

    public static (VrOverlay main, VrOverlay thumb) CreateDashboard(string key, string name)
    {
        ulong main = 0, thumb = 0;
        var e = Ov.CreateDashboardOverlay(key, name, ref main, ref thumb);
        if (e != EVROverlayError.None) throw new InvalidOperationException($"CreateDashboardOverlay failed: {e}");
        return (new VrOverlay(main, key), new VrOverlay(thumb, key + ".thumb"));
    }

    public void SetTexture(ID3D11Texture2D tex)
    {
        var t = new Texture_t { handle = tex.NativePointer, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Auto };
        var e = Ov.SetOverlayTexture(Handle, ref t);
        if (e != EVROverlayError.None && Clock.Now >= _nextWarn)
        {
            _nextWarn = Clock.Now + 10;
            Log.Warn($"SetOverlayTexture({Key}): {e}");
        }
    }

    // ComputeOverlayIntersection takes the overlay's aspect from the mouse scale, so keep it = texture size.
    float _msW, _msH;
    public void SetMouseScale(float w, float h)
    {
        if (w == _msW && h == _msH) return;
        _msW = w; _msH = h;
        var v = new HmdVector2_t { v0 = w, v1 = h };
        Ov.SetOverlayMouseScale(Handle, ref v);
    }

    Vector3 _color = Vector3.One, _tint = Vector3.One;

    // multiplies the texture colour (grab / snap feedback); night mode dims on top of it
    public void SetColor(float r, float g, float b)
    {
        _tint = new Vector3(r, g, b);
        PushColor();
    }

    void PushColor()
    {
        var c = _dimmed.Contains(this) ? _tint * _dim : _tint;
        if (c == _color) return;
        _color = c;
        Ov.SetOverlayColor(Handle, c.X, c.Y, c.Z);
    }

    public void SetWidth(float w) { if (w != _width) { _width = w; Ov.SetOverlayWidthInMeters(Handle, w); } }
    public float Width => _width > 0 ? _width : 1f;            // as last set (SteamVR's default is 1 m)
    public float Curvature => Math.Max(0, _curv);
    public void SetAlpha(float a) { if (a != _alpha) { _alpha = a; Ov.SetOverlayAlpha(Handle, a); } }
    public void SetCurvature(float c) { if (c != _curv) { _curv = c; Ov.SetOverlayCurvature(Handle, c); } }
    public void SetTexelAspect(float a) { if (a != _aspect) { _aspect = a; Ov.SetOverlayTexelAspect(Handle, a); } }
    uint _sort;
    public void SetSortOrder(uint o) { if (o != _sort) { _sort = o; Ov.SetOverlaySortOrder(Handle, o); } }
    public void SetFlag(VROverlayFlags f, bool on) => Ov.SetOverlayFlag(Handle, f, on);

    public void Show() { if (!_visible) { _visible = true; Ov.ShowOverlay(Handle); } }
    public void Hide() { if (_visible) { _visible = false; Ov.HideOverlay(Handle); } }
    public void SetVisible(bool v) { if (v) Show(); else Hide(); }

    public void SetAbsolute(in Matrix4x4 world)
    {
        if (_hasTransform && _lastDevice == uint.MaxValue && world == _lastM) return;
        var m = MathUtil.ToHmd(world);
        Ov.SetOverlayTransformAbsolute(Handle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref m);
        _lastM = world; _lastDevice = uint.MaxValue; _hasTransform = true;
    }

    public void SetRelative(uint device, in Matrix4x4 rel)
    {
        if (_hasTransform && _lastDevice == device && rel == _lastM) return;
        var m = MathUtil.ToHmd(rel);
        Ov.SetOverlayTransformTrackedDeviceRelative(Handle, device, ref m);
        _lastM = rel; _lastDevice = device; _hasTransform = true;
    }

    // Ray test in standing space. uv is returned with a TOP-left origin (OpenVR reports GL-style, bottom-left).
    public bool Raycast(Vector3 origin, Vector3 dir, out float dist, out Vector2 uv) =>
        Raycast(origin, dir, out dist, out uv, out _, out _);

    public bool Raycast(Vector3 origin, Vector3 dir, out float dist, out Vector2 uv, out Vector3 point, out Vector3 normal)
    {
        var p = new VROverlayIntersectionParams_t
        {
            vSource = new HmdVector3_t { v0 = origin.X, v1 = origin.Y, v2 = origin.Z },
            vDirection = new HmdVector3_t { v0 = dir.X, v1 = dir.Y, v2 = dir.Z },
            eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding,
        };
        var r = new VROverlayIntersectionResults_t();
        bool hit = Ov.ComputeOverlayIntersection(Handle, ref p, ref r);
        dist = r.fDistance;
        uv = new Vector2(r.vUVs.v0, UvFlipV ? 1 - r.vUVs.v1 : r.vUVs.v1);
        point = new Vector3(r.vPoint.v0, r.vPoint.v1, r.vPoint.v2);
        normal = new Vector3(r.vNormal.v0, r.vNormal.v1, r.vNormal.v2);
        return hit && dist > 0;
    }

    // ---- the same test in-process (LocalRaycast), from what we last told SteamVR about this overlay ----
    public bool IsRelative => _hasTransform && _lastDevice != uint.MaxValue;

    public bool TryWorld(VrSession vr, out Matrix4x4 world)
    {
        world = _lastM;
        if (!_hasTransform) return false;
        if (_lastDevice == uint.MaxValue) return true;
        if (_lastDevice >= vr.DeviceValid.Length || !vr.DeviceValid[_lastDevice]) return false;
        world = _lastM * vr.Device[_lastDevice];
        return true;
    }

    public bool RaycastLocal(VrSession vr, Vector3 origin, Vector3 dir, out float dist, out Vector2 uv, out Vector3 point, out Vector3 normal)
    {
        dist = 0; uv = default; point = default; normal = default;
        if (_msW <= 0 || _msH <= 0 || !TryWorld(vr, out var world)) return false;
        float w = _width > 0 ? _width : 1f;          // SteamVR's default width
        float c = _curv > 0 ? _curv : 0f;
        return LocalRaycast.Intersect(world, w, c, _msW, _msH, origin, dir, out dist, out uv, out point, out normal);
    }

    // Verified by --selftest; kept switchable in case a runtime reports top-left UVs.
    public static bool UvFlipV = true;

    public void Dispose()
    {
        _dimmed.Remove(this);
        try { Ov?.DestroyOverlay(Handle); } catch { }
    }
}
