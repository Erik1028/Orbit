using System;
using System.Numerics;

namespace Orbit;

// --capturetest: pointer capture geometry without VR (it moves no real mouse). A ray past a panel's edge must land
// on the edge with clampToEdge (a drag reaches the very top of a monitor) and miss without it; on flat and curved
// panels, past every edge.
static class CaptureTest
{
    public static int Run()
    {
        int fails = 0;
        void Ck(string name, bool ok) { Log.Info($"capturetest: {(ok ? "ok  " : "FAIL")} {name}"); if (!ok) fails++; }
        static bool Near(float a, float b, float tol = 0.002f) => MathF.Abs(a - b) <= tol;

        // a 1.07 m, 16:9 monitor 1.2 m in front, facing the viewer (+Z towards the eye)
        var world = Matrix4x4.CreateTranslation(0, 1.2f, -1.2f);
        var eye = new Vector3(0, 1.2f, 0);
        foreach (float curv in new[] { 0f, 0.145f })
        {
            string kind = curv > 0 ? "curved" : "flat";
            Vector3 Dir(float x, float y) => Vector3.Normalize(new Vector3(x, 1.2f + y, -1.2f) - eye);
            bool inside = LocalRaycast.Intersect(world, 1.07f, curv, 1920, 1080, eye, Dir(0.1f, 0.1f), out _, out var uvIn, out _, out _);
            Ck($"{kind}: a ray on the panel hits it", inside && uvIn.X > 0 && uvIn.X < 1 && uvIn.Y > 0 && uvIn.Y < 1);
            bool missAbove = LocalRaycast.Intersect(world, 1.07f, curv, 1920, 1080, eye, Dir(0.1f, 0.6f), out _, out _, out _, out _);
            Ck($"{kind}: past the top edge, no hit without capture", !missAbove);
            bool capAbove = LocalRaycast.Intersect(world, 1.07f, curv, 1920, 1080, eye, Dir(0.1f, 0.6f), out _, out var uvTop, out var pTop, out _, clampToEdge: true);
            Ck($"{kind}: past the top edge, captured on the very top row (uv {uvTop.X:0.000}, {uvTop.Y:0.000})", capAbove && Near(uvTop.Y, 0) && uvTop.X > 0.5f && uvTop.X < 0.7f);
            bool capLeft = LocalRaycast.Intersect(world, 1.07f, curv, 1920, 1080, eye, Dir(-0.9f, -0.1f), out _, out var uvLeft, out _, out _, clampToEdge: true);
            Ck($"{kind}: past the left edge, on the left column (uv {uvLeft.X:0.000}, {uvLeft.Y:0.000})", capLeft && Near(uvLeft.X, 0) && uvLeft.Y > 0.5f);
            bool capCorner = LocalRaycast.Intersect(world, 1.07f, curv, 1920, 1080, eye, Dir(0.9f, -0.6f), out _, out var uvC, out _, out _, clampToEdge: true);
            Ck($"{kind}: past the bottom-right corner, in the corner (uv {uvC.X:0.000}, {uvC.Y:0.000})", capCorner && Near(uvC.X, 1) && Near(uvC.Y, 1));
            Ck($"{kind}: the captured point is on the panel's top edge", MathF.Abs(pTop.Y - (1.2f + 1.07f * 1080 / 1920 / 2)) < 0.003f);
        }
        Log.Info($"capturetest: {(fails == 0 ? "ALL OK" : fails + " FAILED")}");
        return fails == 0 ? 0 : 1;
    }
}
