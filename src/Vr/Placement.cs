using System.Numerics;

namespace Orbit;

// Applies a PlacementConfig to an overlay and converts between world and device-relative transforms.
public static class Placement
{
    public static bool IsWorld(PlacementConfig p) => p.Attach is null or "world";

    // World transform of the overlay right now (false if its device is not tracked).
    public static bool TryWorld(PlacementConfig p, VrSession vr, out Matrix4x4 world)
    {
        var m = MathUtil.FromArray(p.M);
        if (IsWorld(p)) { world = m; return true; }
        if (vr.TryDeviceWorld(vr.IndexFor(p.Attach), out var dev)) { world = m * dev; return true; }
        world = m;
        return false;
    }

    public static void SetWorld(PlacementConfig p, VrSession vr, in Matrix4x4 world)
    {
        if (IsWorld(p)) { p.M = MathUtil.ToArray(world); return; }
        if (vr.TryDeviceWorld(vr.IndexFor(p.Attach), out var dev))
            p.M = MathUtil.ToArray(world * MathUtil.Invert(dev));
    }

    // Changes what the overlay is attached to while keeping it where it is now.
    public static void Reattach(PlacementConfig p, VrSession vr, string attach)
    {
        Matrix4x4 world = default;
        bool had = p.M != null && TryWorld(p, vr, out world);
        p.Attach = attach;
        if (had && (IsWorld(p) || vr.TryDeviceWorld(vr.IndexFor(attach), out _))) SetWorld(p, vr, world);
        else p.M = null;   // re-placed in front of the user
    }

    // Pushes the transform to SteamVR. Returns false when the overlay should be hidden (device missing).
    public static bool Apply(PlacementConfig p, VrSession vr, VrOverlay ov)
    {
        if (p.M == null) return false;
        var m = MathUtil.FromArray(p.M);
        if (IsWorld(p)) { ov.SetAbsolute(m); return true; }
        uint idx = vr.IndexFor(p.Attach);
        if (idx == VrSession.InvalidIndex || idx >= vr.DeviceValid.Length) return false;
        ov.SetRelative(idx, m);
        return true;
    }
}
