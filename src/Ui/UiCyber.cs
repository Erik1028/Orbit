using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The settings' Cyber motion (CyberFx): a section's yellow tag slides in and types its letters when its page comes into view (Full);
// a button under the pointer gets a cyan outline and its corner marks close in (with the button's own hover ease); the HUD controls'
// own moments (a light across a plate the pointer comes onto, a click's corner kick and red / cyan split, a switch's flip) - Full.
public sealed partial class Ui
{
    // the frame being drawn (a new one with every Begin) - the watch's Cyber effects keep their flags per frame
    public long BeginStamp => _lastBegin;

    // an eased value set at once (the transition is not wanted: Motion, a --snapshot)
    public float AnimSnap(string id, float target)
    {
        _anim[id] = target;
        return target;
    }

    // ---- a section's tag ----
    // It slides in when it was not drawn in the frame before (another tab was), lower ones a little later: the tag's body wipes in
    // from the left (0.1 s), then its letters type (25 ms each)
    readonly Dictionary<string, (long seen, double start)> _cyTags = new();
    long _cyPrevFrame, _cyThisFrame;

    readonly Dictionary<(string, string), string> _cyTitles = new();

    void CyberSection(RectangleF r, string title)
    {
        // translated before its letters type (each made once per language, not in every frame)
        var tk = (title, Lang.Code);
        if (!_cyTitles.TryGetValue(tk, out var s)) { if (_cyTitles.Count > 300) _cyTitles.Clear(); _cyTitles[tk] = s = Lang.Upper(title); }
        float wipe = 1, type = 1;
        if (CyberFx.Full)
        {
            if (_lastBegin != _cyThisFrame) { _cyPrevFrame = _cyThisFrame; _cyThisFrame = _lastBegin; }
            double now = Clock.Now;
            if (!_cyTags.TryGetValue(title, out var st) || (st.seen != _cyThisFrame && st.seen != _cyPrevFrame))
                st.start = now + Math.Clamp(r.Y / 900.0, 0, 1) * 0.12;
            st.seen = _cyThisFrame;
            _cyTags[title] = st;
            double t = now - st.start;
            wipe = CyberFx.EaseOut(CyberFx.Ramp(t, 0, 0.1));
            type = CyberFx.Ramp(t, 0.06, 0.025 * s.Length);
            if (type < 1) Animating = true;
        }
        float ty = r.Y + (r.Height - 18) / 2;
        float tw = CyberHud.Tag(P, r.X, ty, s, wipe, type, size: 12, h: 18);
        if (Hud && wipe > 0) HudSectionRule(new RectangleF(r.X, r.Y, r.Width * wipe, r.Height), tw, ty + 18);   // the rule it sits on (UiHud.cs)
    }

    // ---- the HUD controls' moments (UiHud.cs): the pointer coming onto one, a click, a switch flipped ----
    // Each is timed from the frame its flag turned on; a control first seen with it on starts at rest (no effect for a page opening
    // under the pointer). Full only: Reduced and Off keep the controls' own short eases.
    readonly Dictionary<string, (bool on, double at)> _cyAt = new();

    // how far (0..1) an effect of 'len' seconds is that starts when 'on' turns true; 1: not running. While it runs the next frame
    // is asked for (Animating).
    float CyFx(string key, bool on, double len)
    {
        if (!CyberFx.Full) return 1;
        double now = Clock.Now;
        if (!_cyAt.TryGetValue(key, out var e)) { _cyAt[key] = (on, -100); return 1; }
        if (on && !e.on) e.at = now;
        if (on != e.on) { e.on = on; _cyAt[key] = e; }
        double t = now - e.at;
        if (t >= len) return 1;
        Animating = true;
        return (float)(t / len);
    }

    // text with a red ghost to its left and a cyan one to its right, 'split' px apart (a click's glitch); 0: the text alone
    void HudSplit(string s, RectangleF r, uint c, float size, FontWeight w, TextAlignment al, string family, float split)
    {
        if (split > 0.3f)
        {
            P.Text(s, new RectangleF(r.X - split, r.Y, r.Width, r.Height), Theme.WithAlpha(CyberHud.Red, 0.8f), size, w, al, ParagraphAlignment.Center, family);
            P.Text(s, new RectangleF(r.X + split, r.Y, r.Width, r.Height), Theme.WithAlpha(CyberHud.Cyan, 0.8f), size, w, al, ParagraphAlignment.Center, family);
        }
        P.Text(s, r, c, size, w, al, ParagraphAlignment.Center, family);
    }

    static readonly System.Numerics.Vector2[] _sweep = new System.Numerics.Vector2[4];

    // a light sweeping across a plate (cut bottom-right by 'cut'), t 0..1: a band leaning like the cut, so it never shows past it
    void HudSweep(RectangleF b, float cut, float t, uint c)
    {
        float e = CyberFx.EaseInOut(t), w = MathF.Min(22, b.Width * 0.25f), sl = b.Height;
        float x0 = b.X - w - sl + (b.Width + w + sl) * e, x1 = MathF.Min(x0 + w, b.Right - cut);
        if (x1 <= x0) return;
        P.PushClip(b);
        _sweep[0] = new(x0 + sl, b.Y); _sweep[1] = new(x1 + sl, b.Y); _sweep[2] = new(x1, b.Bottom); _sweep[3] = new(x0, b.Bottom);
        P.Polygon(_sweep, 4, Theme.WithAlpha(c, 0.16f * (1 - 0.5f * e)));
        P.PopClip();
    }

    // ---- a button's hover: a cyan outline (its cut corners) and the two corner marks closing in ----
    void CyberHover(RectangleF r, float h)
    {
        if (h <= 0.01f) return;
        var (tl, br) = CyberHud.Cuts(r, 8);
        P.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.WithAlpha(CyberHud.Cyan, 0.85f * h), 1.25f, MathF.Max(0, tl - 0.3f), 0, MathF.Max(0, br - 0.3f), 0);
        if (r.Height >= 26) CyberHud.Brackets(P, r, Theme.WithAlpha(CyberHud.Cyan, h), 7, 4 - 5 * (1 - h), 2);
    }
}
