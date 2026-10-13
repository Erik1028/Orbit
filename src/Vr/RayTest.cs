using System;
using System.Collections.Generic;
using System.Numerics;
using Valve.VR;

namespace Orbit;

// Ground truth for LocalRaycast (--raytest, needs SteamVR running): one fully transparent overlay
// (alpha 0, no texture - nothing to see; SteamVR ignores hidden overlays for intersections) put into
// many shapes and places for a few seconds, thousands of random rays tested with SteamVR's
// ComputeOverlayIntersection and with ours, then removed. SteamVR applies overlay changes on a later
// compositor frame, so every shape waits two frames before its rays.
public static class RayTest
{
    sealed class Bucket
    {
        public int Rays, HitBoth, MissBoth, SteamOnly, LocalOnly;
        public float MaxUv, MaxDist, MinDot = 1;
        public string Worst;
    }

    public static int Run(int raysPerConfig = 400)
    {
        var err = EVRInitError.None;
        OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
        if (err != EVRInitError.None) { Log.Error("raytest: SteamVR is not running (" + err + ")"); return 1; }
        VrOverlay ov = null;
        try
        {
            ov = new VrOverlay("erik.orbit.raytest", "Orbit raytest");
            ov.SetAlpha(0);
            ov.Show();
            // SteamVR reports uv 0 for an overlay without a texture: give it one of each size (never visible, alpha 0)
            using var gpu = new Gpu(-1);
            var surfaces = new Dictionary<(int, int), Surface>();
            var rng = new Random(7);
            float[] widths = { 0.3f, 0.9f, 2.0f };
            float[] curvs = { 0f, 0.000001f, 0.00001f, 0.0001f, 0.0005f, 0.005f, 0.02f, 0.15f, 0.35f, 0.5f, 0.6f, 0.85f, 1f };
            (int w, int h)[] texs = { (1920, 1080), (640, 440), (1480, 676), (1500, 960) };
            var buckets = new SortedDictionary<string, Bucket>();
            int bad = 0;
            foreach (float width in widths)
            foreach (float curv in curvs)
            foreach (var tex in texs)
            {
                var rot = Quaternion.Normalize(new Quaternion((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f,
                    (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f + 1.2f));
                var world = Matrix4x4.CreateFromQuaternion(rot) *
                            Matrix4x4.CreateTranslation((float)rng.NextDouble() * 3 - 1.5f, 1 + (float)rng.NextDouble(), (float)rng.NextDouble() * 3 - 1.5f);
                ov.SetWidth(width);
                ov.SetCurvature(curv);
                ov.SetMouseScale(tex.w, tex.h);
                if (!surfaces.TryGetValue(tex, out var surf)) surfaces[tex] = surf = new Surface(gpu, tex.w, tex.h);
                ov.SetTexture(surf.Tex);
                ov.SetAbsolute(world);
                OpenVR.Overlay.WaitFrameSync(100);
                OpenVR.Overlay.WaitFrameSync(100);
                float height = width * tex.h / tex.w;
                for (int i = 0; i < raysPerConfig; i++)
                {
                    // aim at a point on (or a bit beyond) the panel, from in front or (1 in 8) from behind
                    var target = new Vector3(((float)rng.NextDouble() - .5f) * width * 1.3f, ((float)rng.NextDouble() - .5f) * height * 1.3f, 0);
                    bool behind = i % 8 == 0;
                    var from = new Vector3(((float)rng.NextDouble() - .5f) * 2, ((float)rng.NextDouble() - .5f) * 2,
                        (behind ? -1 : 1) * (0.2f + (float)rng.NextDouble() * 2));
                    var o = Vector3.Transform(from, world);
                    var d = Vector3.Normalize(Vector3.Transform(target, world) - o);
                    bool hs = ov.Raycast(o, d, out float ds, out Vector2 uvs, out Vector3 ps, out Vector3 ns);
                    bool hl = LocalRaycast.Intersect(world, width, curv, tex.w, tex.h, o, d, out float dl, out Vector2 uvl, out Vector3 pl, out Vector3 nl);
                    bool imprecise = curv > 0 && curv < DesktopLayout.MinCurvature;   // SteamVR's float zone; Orbit never uses it
                    string key = $"c {curv,-6} {(behind ? "behind" : "front ")}{(imprecise ? " (not used by Orbit)" : "")}";
                    if (!buckets.TryGetValue(key, out var b)) buckets[key] = b = new Bucket();
                    b.Rays++;
                    if (hs && hl)
                    {
                        b.HitBoth++;
                        float e = Vector2.Distance(uvs, uvl);
                        if (e > b.MaxUv)
                        {
                            b.MaxUv = e;
                            b.Worst = $"w {width} tex {tex.w}x{tex.h}: steam uv {uvs} d {ds:0.000} n {ns} | local uv {uvl} d {dl:0.000} n {nl}";
                        }
                        b.MaxDist = Math.Max(b.MaxDist, Math.Abs(ds - dl));
                        if (ns.LengthSquared() > 0.5f) b.MinDot = Math.Min(b.MinDot, Vector3.Dot(Vector3.Normalize(ns), Vector3.Normalize(nl)));
                        if (!imprecise && (e > 1e-3f || Math.Abs(ds - dl) > 1e-3f)) bad++;
                    }
                    else if (!hs && !hl) b.MissBoth++;
                    else
                    {
                        if (!imprecise) bad++;
                        if (hs) b.SteamOnly++; else b.LocalOnly++;
                        b.Worst ??= $"w {width} tex {tex.w}x{tex.h}: steam={hs} uv {uvs} d {ds:0.000} | local={hl} uv {uvl} d {dl:0.000}";
                    }
                }
            }
            foreach (var (k, b) in buckets)
                Log.Info($"raytest {k}: {b.Rays} rays, both hit {b.HitBoth}, both miss {b.MissBoth}, steam-only {b.SteamOnly}, local-only {b.LocalOnly}, " +
                         $"max uv {b.MaxUv:0.000000}, max dist {b.MaxDist * 1000:0.000} mm, min normal dot {b.MinDot:0.0000}" +
                         (b.Worst != null && (b.MaxUv > 1e-3f || b.SteamOnly + b.LocalOnly > 0) ? $"\n      worst: {b.Worst}" : ""));
            Log.Info($"raytest: {bad} rays disagree (curvatures Orbit uses: 0 and >= {DesktopLayout.MinCurvature})");
            foreach (var s in surfaces.Values) s.Dispose();
            return bad == 0 ? 0 : 2;
        }
        finally
        {
            ov?.Dispose();
            OpenVR.Shutdown();
        }
    }
}
