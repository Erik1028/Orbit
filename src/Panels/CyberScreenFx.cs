using System;
using System.Drawing;
using System.Numerics;
using Valve.VR;

namespace Orbit;

// A monitor or window panel in the Cyber style coming into view, going away and taking the theater (DesktopPanel, WindowPanel own
// one each). The captured picture is never touched - everything is done on the overlay:
//  - unfold (Reduced too): the screen opens from a line across its middle and folds back into it - its texture bounds show the
//    middle k of the picture, so the overlay itself grows out of the line (k = 1 by 80 % of the fade, when it takes the laser);
//  - a HUD frame (Full): a second, see-through overlay over the screen (the same place, bend and size, a margin round it) for the
//    0.25-0.5 s it lasts - yellow corner brackets locking on from outside, a bright line with a red / cyan split riding each edge of
//    the unfold, a faint grid over what was just revealed; going away the brackets close in on the line. Taking the theater the
//    brackets lock on round the growing screen and a scan line runs down it; leaving it they let go.
// At rest none of it is there: the bounds are the whole texture again, the frame overlay hidden, nothing drawn. Every other style:
// nothing at all (this is not even made).
public sealed class CyberScreenFx : IDisposable
{
    public static Painter Painter;                   // the app's (OrbitApp); null in the tools: no frame overlay

    readonly Gpu _g;
    readonly string _key;
    VrOverlay _hud;
    Surface _surf;
    bool _bounded;
    float _prevVis;
    bool _prevTheater;
    double _appearAt = -10, _lockAt = -10, _releaseAt = -10;
    const float M = 28;                              // the frame's margin round the screen (texture px at 1024 wide)
    const int HudW = 1024;

    public CyberScreenFx(Gpu g, string key) { _g = g; _key = key; }

    // after the panel has placed, sized and shown / hidden its overlay this frame. vis: its fade 0..1; want: it is meant to show;
    // theater: it is the theater screen; tex: its picture's size
    public void Update(VrOverlay panel, float vis, bool want, bool theater, Vector2 tex, double now)
    {
        bool on = Theme.Cyber && CyberFx.Any && panel != null;
        // ---- the unfold: the texture bounds ----
        float k = on && vis < 1 ? MathF.Max(0.012f, CyberFx.EaseOut(CyberFx.Clamp01(vis / 0.8f))) : 1;
        if (k < 1 || _bounded)
        {
            SetBounds(panel, k);
            _bounded = k < 1;
        }
        // ---- what starts ----
        if (_prevVis <= 0 && vis > 0) _appearAt = now;
        if (theater && !_prevTheater) _lockAt = now;
        if (!theater && _prevTheater) _releaseAt = now;
        bool hiding = vis < _prevVis && !want;
        _prevVis = vis;
        _prevTheater = theater;
        // ---- the HUD frame (Full) ----
        double ta = now - _appearAt, tl = now - _lockAt, tr = now - _releaseAt;
        bool appear = ta < 0.42, lockOn = tl < 0.6, release = tr < 0.35;
        bool frame = on && CyberFx.Full && Painter != null && panel.Visible && (appear || lockOn || release || hiding || k < 1);
        if (!frame)
        {
            _hud?.Hide();
            return;
        }
        try
        {
            if (!EnsureHud(tex)) return;
            panel.CopyTransformTo(_hud);
            float wIn = HudW - 2 * M, scale = HudW / wIn;
            _hud.SetWidth(panel.Width * scale);
            _hud.SetCurvature(Math.Clamp(panel.Curvature * scale, 0, 1));
            _hud.SetSortOrder(theater ? 109u : 108u);
            Draw(k, hiding, appear ? ta : -1, lockOn ? tl : -1, release ? tr : -1);
            _hud.SetTexture(_surf.Tex);
            _hud.Show();
            AnimWake.At(now);
        }
        catch (Exception e)
        {
            Log.Warn($"cyber screen fx ({_key}): {e.Message}");
            _hud?.Dispose();
            _hud = null;
        }
    }

    static void SetBounds(VrOverlay ov, float k)
    {
        float v0 = 0.5f - k / 2, v1 = 0.5f + k / 2;
        if (k >= 1) { v0 = 0; v1 = 1; }
        ov.SetTextureBounds(0, v0, 1, v1);
    }

    bool EnsureHud(Vector2 tex)
    {
        EnsureSurf(tex);
        if (_hud == null)
        {
            _hud = new VrOverlay(_key + ".hud", "OrbitOverlay screen frame");
            _hud.SetFlag(VROverlayFlags.IsPremultiplied, true);
        }
        return true;
    }

    void Draw(float k, bool hiding, double ta, double tl, double tr)
    {
        var p = Painter;
        p.Begin(_surf.Bitmap, 0x00000000);
        var inner = new RectangleF(M, M, HudW - 2 * M, _surf.H - 2 * M);
        float cy = inner.Y + inner.Height / 2, half = inner.Height * k / 2;
        float arm = MathF.Min(inner.Width, inner.Height) * 0.09f;
        // the unfold's two edges: a bright line each, red above / cyan below, the grid between them
        if (k < 1)
        {
            var open = new RectangleF(inner.X, cy - half, inner.Width, 2 * half);
            uint g = Theme.WithAlpha(CyberHud.Cyan, 0.08f);
            for (float x = inner.X + 48; x < inner.Right; x += 48) p.Fill(new RectangleF(x, open.Y, 1.5f, open.Height), g);
            for (float y = cy - MathF.Floor(half / 48) * 48; y < open.Bottom; y += 48) p.Fill(new RectangleF(inner.X, y, inner.Width, 1.5f), g);
            foreach (float y in new[] { cy - half, cy + half })
            {
                p.Fill(new RectangleF(inner.X, y - 2, inner.Width, 4), CyberHud.Cyan);
                p.Fill(new RectangleF(inner.X, y - 8, inner.Width, 2), Theme.WithAlpha(CyberHud.Red, 0.7f));
                p.Fill(new RectangleF(inner.X, y + 6, inner.Width, 2), Theme.WithAlpha(CyberHud.Cyan, 0.5f));
            }
            // the brackets ride the edges in, closing on the line when it goes
            var b = new RectangleF(inner.X, cy - half - 6, inner.Width, 2 * half + 12);
            CyberFx.Corners(p, b, hiding ? CyberHud.Cyan : CyberHud.Yellow, arm, 5);
        }
        else if (ta >= 0)
        {
            // in: the brackets lock on from outside, then fade
            float lk = CyberFx.EaseOut(CyberFx.Ramp(ta, 0, 0.18));
            float fade = 1 - CyberFx.Ramp(ta, 0.24, 0.18);
            CyberFx.Corners(p, inner, Theme.WithAlpha(CyberHud.Yellow, fade), arm, 5, (1 - lk) * (M - 4) + 2);
        }
        if (tl >= 0)
        {
            // the theater: brackets lock on round the growing screen, a scan line runs down it
            float lk = CyberFx.EaseOut(CyberFx.Ramp(tl, 0, 0.2));
            float fade = 1 - CyberFx.Ramp(tl, 0.42, 0.18);
            CyberFx.Corners(p, inner, Theme.WithAlpha(CyberHud.Yellow, fade), arm * 1.2f, 6, (1 - lk) * (M - 4) + 2);
            float s = CyberFx.EaseInOut(CyberFx.Ramp(tl, 0.05, 0.45));
            if (s > 0 && s < 1) CyberFx.ScanH(p, inner.X, inner.Right, inner.Y + inner.Height * s, CyberHud.Cyan, 1.5f * (1 - s * 0.5f));
        }
        if (tr >= 0)
        {
            float fade = 1 - CyberFx.Ramp(tr, 0.05, 0.3);
            CyberFx.Corners(p, inner, Theme.WithAlpha(CyberHud.Cyan, fade), arm, 4, 2 + 16 * CyberFx.EaseOut(CyberFx.Ramp(tr, 0, 0.3)));
        }
        p.End();
    }

    // --snapshot: the frame as it is drawn at these moments (a texture of the given aspect), without SteamVR
    public Surface DrawForTest(Painter p, Vector2 tex, float k, bool hiding, double ta, double tl, double tr)
    {
        var keep = Painter;
        Painter = p;
        EnsureSurf(tex);
        Draw(k, hiding, ta, tl, tr);
        Painter = keep;
        return _surf;
    }

    void EnsureSurf(Vector2 tex)
    {
        float aspect = tex.X > 1 && tex.Y > 1 ? tex.Y / tex.X : 9 / 16f;
        int h = (int)MathF.Round((HudW - 2 * M) * Math.Clamp(aspect, 0.1f, 4f) + 2 * M);
        if (_surf == null || _surf.H != h) { _surf?.Dispose(); _surf = new Surface(_g, HudW, h); }
    }

    // the panel's overlay goes (monitors rebuilt, window closed): the frame with it; the bounds are the new overlay's own
    public void Dispose()
    {
        _hud?.Dispose();
        _hud = null;
        _surf?.Dispose();
        _surf = null;
        _bounded = false;
    }
}
