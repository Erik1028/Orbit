using System;
using System.Numerics;

namespace Orbit;

// Shadow-mode comparison of LocalRaycast with SteamVR's answer, per 10 s window. Panels attached
// to a device are kept apart: there SteamVR and we may use slightly different poses of the device.
public static class RayStats
{
    public struct Group
    {
        public int Compared, Disagree, SteamOnly, LocalOnly;
        public float MaxUv, MaxDist, MaxPoint, MinNormalDot;

        public void Add(bool hs, bool hl, float ds, Vector2 uvs, Vector3 ps, Vector3 ns, float dl, Vector2 uvl, Vector3 pl, Vector3 nl)
        {
            if (Compared == 0) MinNormalDot = 1;
            Compared++;
            if (hs != hl) { Disagree++; if (hs) SteamOnly++; else LocalOnly++; return; }
            if (!hs) return;
            MaxUv = MathF.Max(MaxUv, Vector2.Distance(uvs, uvl));
            MaxDist = MathF.Max(MaxDist, MathF.Abs(ds - dl));
            MaxPoint = MathF.Max(MaxPoint, Vector3.Distance(ps, pl));
            if (ns.LengthSquared() > 0.5f && nl.LengthSquared() > 0.5f)
                MinNormalDot = MathF.Min(MinNormalDot, Vector3.Dot(Vector3.Normalize(ns), Vector3.Normalize(nl)));
        }

        public override string ToString() => Compared == 0 ? "none" :
            $"{Compared} rays, disagree {Disagree} (steam-only {SteamOnly}, local-only {LocalOnly}), max uv {MaxUv:0.00000}, " +
            $"dist {MaxDist * 1000:0.00} mm, point {MaxPoint * 1000:0.00} mm, normal dot {MinNormalDot:0.0000}";
    }

    public static Group World, Device;
    public static long SteamTicks, LocalTicks, SteamMax, LocalMax;     // the max: does a single call ever stall?

    public static void Steam(long ticks) { SteamTicks += ticks; SteamCalls++; if (ticks > SteamMax) SteamMax = ticks; }
    public static void Local(long ticks) { LocalTicks += ticks; LocalCalls++; if (ticks > LocalMax) LocalMax = ticks; }
    public static int SteamCalls, LocalCalls;

    public static void Add(bool relative, bool hs, bool hl, float ds, Vector2 uvs, Vector3 ps, Vector3 ns, float dl, Vector2 uvl, Vector3 pl, Vector3 nl)
    {
        if (relative) Device.Add(hs, hl, ds, uvs, ps, ns, dl, uvl, pl, nl);
        else World.Add(hs, hl, ds, uvs, ps, ns, dl, uvl, pl, nl);
    }

    // one log line per window, then reset
    public static string TakeLine()
    {
        if (SteamCalls == 0 && LocalCalls == 0) return null;
        double us(long ticks, int calls) => calls == 0 ? 0 : ticks * 1e6 / System.Diagnostics.Stopwatch.Frequency / calls;
        string line = $"raycast: steamvr {SteamCalls} calls {us(SteamTicks, SteamCalls):0.0} us/call (max {us(SteamMax, 1):0} us), " +
                      $"local {LocalCalls} calls {us(LocalTicks, LocalCalls):0.00} us/call (max {us(LocalMax, 1):0} us)" +
                      (World.Compared + Device.Compared > 0 ? $" | world panels: {World} | device panels: {Device}" : "");
        World = default; Device = default;
        SteamTicks = LocalTicks = SteamMax = LocalMax = 0; SteamCalls = LocalCalls = 0;
        return line;
    }
}
