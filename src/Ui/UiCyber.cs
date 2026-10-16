using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orbit;

// The settings' Cyber motion (CyberFx): a section's yellow tag slides in and types its letters when its page comes into view (Full);
// a button under the pointer gets a cyan outline and its corner marks close in (with the button's own hover ease).
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

    void CyberSection(RectangleF r, string title)
    {
        string s = title.ToUpperInvariant();
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

    // ---- a button's hover: a cyan outline (its cut corners) and the two corner marks closing in ----
    void CyberHover(RectangleF r, float h)
    {
        if (h <= 0.01f) return;
        var (tl, br) = CyberHud.Cuts(r, 8);
        P.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), Theme.WithAlpha(CyberHud.Cyan, 0.85f * h), 1.25f, MathF.Max(0, tl - 0.3f), 0, MathF.Max(0, br - 0.3f), 0);
        if (r.Height >= 26) CyberHud.Brackets(P, r, Theme.WithAlpha(CyberHud.Cyan, h), 7, 4 - 5 * (1 - h), 2);
    }
}
