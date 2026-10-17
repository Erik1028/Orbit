using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The shared controls in the Cyber style: not the modern ones recoloured, the HUD's own (Erik's watch concept, 10-16): a button is a
// plate with its bottom-right corner cut (yellow when it is the main thing, red-edged when it deletes), a switch a housing that
// fills cyan and says ON / OFF, a slider a segmented bar with a notch and a monospace readout, a choice a tab strip with a cyan
// underline sliding to the pick, a card a dark-red-edged plate, a colour a cut square with brackets, a section a yellow tag on a
// rule, a caption monospace capitals. Each one keeps the modern control's id, hit area and behaviour: every Ui call - also the ones
// added later - draws as part of the HUD with nothing to do for it. Ui.Hud off (the watch's own Ui: its Cyber designs are
// WristCyber's) keeps the 1.13 look.
public sealed partial class Ui
{
    // the HUD look for this Ui's controls in the Cyber style (the watch turns it off: it has its own designs)
    public bool Hud = true;

    // a section's rule ends here at the latest (the settings: inside the page's card)
    public float HudRight = float.MaxValue;

    bool CyberHud_ => Theme.Cyber && Hud;

    // ---- text that fits: UPPERCASE where it fits (shrinking at most to 88 %), else as written, shrunk to fit ----
    readonly Dictionary<(string, float, float, FontWeight, string), (string s, float size)> _hudFit = new();
    readonly Dictionary<string, string> _upper = new();
    int _hudFitLook = -1;

    public string Upper(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (!_upper.TryGetValue(s, out var u))
        {
            if (_upper.Count > 800) _upper.Clear();
            _upper[s] = u = s.ToUpperInvariant();
        }
        return u;
    }

    public (string s, float size) HudFit(string label, float maxW, float size, FontWeight w = FontWeight.SemiBold, string family = null, bool upper = true)
    {
        if (string.IsNullOrEmpty(label)) return (label, size);
        if (_hudFitLook != Theme.Version) { _hudFit.Clear(); _hudFitLook = Theme.Version; }
        var key = (label, maxW, size, w, family);
        if (_hudFit.TryGetValue(key, out var r)) return r;
        if (upper)
        {
            string up = Upper(label);
            float uw = P.MeasureWidth(up, size, w, family);
            if (uw <= maxW) r = (up, size);
            else if (uw * 0.88f <= maxW) r = (up, MathF.Floor(size * maxW / uw * 2) / 2);
        }
        if (r.s == null)
        {
            float lw = P.MeasureWidth(label, size, w, family);
            r = lw <= maxW ? (label, size) : (label, MathF.Max(size * 0.72f, MathF.Floor(size * maxW / lw * 2) / 2));
        }
        if (_hudFit.Count > 600) _hudFit.Clear();
        _hudFit[key] = r;
        return r;
    }

    // a hint / note: monospace where it fits on its line (11, then 10 px), else the condensed text face
    public void HudHint(string s, RectangleF r, uint c, float size = 12, TextAlignment al = TextAlignment.Leading,
        ParagraphAlignment v = ParagraphAlignment.Near)
    {
        if (string.IsNullOrEmpty(s)) return;
        string mono = CyberHud.Mono;
        if (P.MeasureWidth(s, size, FontWeight.Normal, mono) <= r.Width) P.Text(s, r, c, size, FontWeight.Normal, al, v, mono);
        else if (P.MeasureWidth(s, size - 1, FontWeight.Normal, mono) <= r.Width) P.Text(s, r, c, size - 1, FontWeight.Normal, al, v, mono);
        else
        {
            // the condensed face at the modern hint's size, smaller if even that is too long
            float w12 = P.MeasureWidth(s, 12, FontWeight.Normal);
            float fs = w12 <= r.Width ? 12 : MathF.Max(9.5f, MathF.Floor(12 * r.Width / w12 * 2) / 2);
            P.Text(s, r, c, fs, FontWeight.Normal, al, v);
        }
    }

    // the corners that close in on what is under the pointer (top-right and bottom-left, as on the watch's buttons)
    void HudBrackets(RectangleF r, float h, uint c, float arm = 7)
    {
        if (h <= 0.01f) return;
        CyberHud.Brackets(P, r, Theme.WithAlpha(c, h), arm, 4 - 6 * (1 - h), 2);
    }

    // ---------------- button: a plate, its bottom-right corner cut ----------------
    // accent: the main thing / a pick that is on - a yellow plate (onCyan: cyan, a state like "shown"); an icon that deletes: red edge.
    // Full: the pointer coming onto it sends a light across the plate (0.24 s); held, its label splits red / cyan; a click kicks its
    // corners out and the split dies away (0.22 s)
    const double HudSweepTime = 0.24, HudKickTime = 0.22;

    public bool HudButton(string id, RectangleF r, string label, string icon, bool accent, bool enabled, float fontSize, bool onCyan = false)
    {
        bool click = Behave(id, r, out bool hover, out bool held) && enabled;
        float h = Anim(K(id, "#h"), hover && enabled ? 1 : 0, 18);
        float pr = Anim(K(id, "#p"), held && enabled ? 1 : 0, 30);
        float a = Anim(K(id, "#a"), accent ? 1 : 0, 14);
        float sweep = CyFx(K(id, "#hs"), hover && enabled, HudSweepTime), kick = CyFx(K(id, "#ck"), click, HudKickTime);
        float split = held && enabled && CyberFx.Full ? 1.5f : kick < 1 ? 2.5f * (1 - kick) : 0;
        bool danger = icon == Theme.IcoDelete;
        uint on = onCyan ? CyberHud.Cyan : CyberHud.Yellow;
        var b = Shrink(r, 0.03f * pr);
        float cut = MathF.Round(Math.Clamp(MathF.Min(b.Width, b.Height) * 0.28f, 4, 11));
        uint bg = !enabled ? Theme.WithAlpha(CyberHud.Card2, 0.6f) : Theme.Mix(Theme.Mix(CyberHud.Card2, CyberHud.Card3, h), on, a);
        if (!enabled && accent) bg = Theme.Mix(CyberHud.Card2, on, 0.18f);
        bg = Theme.Mix(bg, 0xFF000000, 0.25f * pr);
        P.FillCut(b, bg, 0, 0, cut, 0);
        if (sweep < 1) HudSweep(b, cut, sweep, a > 0.5f ? CyberHud.White : CyberHud.Cyan);
        if (a < 0.99f)
        {
            uint edge = !enabled ? CyberHud.Line : danger ? Theme.Mix(CyberHud.DangerEdge, CyberHud.Red, h) : Theme.Mix(CyberHud.CyanDim, CyberHud.Cyan, h);
            P.StrokeCut(RectangleF.Inflate(b, -0.6f, -0.6f), Theme.WithAlpha(edge, 1 - a), 1.25f, 0, 0, MathF.Max(0, cut - 0.3f), 0);
        }
        if (enabled && b.Height >= 26) HudBrackets(b, h * (1 - 0.6f * a), a > 0.5f ? CyberHud.White : CyberHud.Cyan);
        if (kick < 1) CyberFx.Corners(P, b, Theme.WithAlpha(a > 0.5f ? on : CyberHud.Cyan, 1 - kick), 7, 2, 1 + 7 * CyberFx.EaseOut(kick));
        uint fg = !enabled ? CyberHud.Faint : Theme.Mix(danger ? CyberHud.Red : CyberHud.White, CyberHud.Ink, a);
        uint ic = !enabled ? CyberHud.Faint : Theme.Mix(danger ? CyberHud.Red : CyberHud.Cyan, CyberHud.Ink, a);
        if (icon != null && !string.IsNullOrEmpty(label))
        {
            var (s, fs) = HudFit(label, b.Width - fontSize - 8 - 18, fontSize);
            float tw = P.MeasureWidth(s, fs, FontWeight.SemiBold);
            float total = fontSize + 8 + tw, x = b.X + (b.Width - total) / 2;
            HudSplit(icon, new RectangleF(x, b.Y, fontSize, b.Height), ic, fontSize, FontWeight.Normal, TextAlignment.Center, Theme.IconFont, split);
            HudSplit(s, new RectangleF(x + fontSize + 8, b.Y, tw + 4, b.Height), fg, fs, FontWeight.SemiBold, TextAlignment.Leading, null, split);
        }
        else if (icon != null) HudSplit(icon, b, ic, fontSize + 3, FontWeight.Normal, TextAlignment.Center, Theme.IconFont, split);
        else
        {
            var (s, fs) = HudFit(label, b.Width - 12, fontSize);
            HudSplit(s, b, fg, fs, FontWeight.SemiBold, TextAlignment.Center, null, split);
        }
        return click;
    }

    // ---------------- switch: a housing that fills cyan from the left and says ON / OFF ----------------
    public const float HudSwitchW = 62, HudSwitchH = 26;

    bool HudToggle(string id, RectangleF r, string label, ref bool value, string hint, bool enabled)
    {
        bool click = Behave(id, r, out bool hover, out _) && enabled;
        if (click) { value = !value; Changed = true; }
        float h = Anim(K(id, "#h"), hover && enabled ? 1 : 0, 18);
        float k = Anim(K(id, "#k"), value ? 1 : 0, 16);
        float flip = MathF.Min(CyFx(K(id, "#f1"), value, HudFlipTime), CyFx(K(id, "#f0"), !value, HudFlipTime));
        if (h > 0.01f)
        {
            P.FillCut(r, Theme.WithAlpha(CyberHud.Card3, 0.45f * h), 0, 0, 8, 0);
            HudBrackets(RectangleF.Inflate(r, -1, -1), h, CyberHud.CyanDim, 6);
        }
        HudSwitch(new RectangleF(r.Right - HudSwitchW - 10, r.Y + (r.Height - HudSwitchH) / 2, HudSwitchW, HudSwitchH), k, enabled, h, flip);
        uint fg = enabled ? CyberHud.White : CyberHud.Faint;
        float tw = r.Width - HudSwitchW - 30;
        if (hint == null)
            P.Text(label, new RectangleF(r.X + 10, r.Y, tw, r.Height), fg, 15);
        else
        {
            P.Text(label, new RectangleF(r.X + 10, r.Y + 2, tw, r.Height / 2), fg, 15, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Far);
            HudHint(hint, new RectangleF(r.X + 10, r.Y + r.Height / 2 + 3, tw, r.Height / 2 - 3), enabled ? CyberHud.Grey : CyberHud.Faint);
        }
        return click;
    }

    static readonly Vector2[] _sw = new Vector2[4];

    // k: 0 off .. 1 on (eased); the fill wipes in from the left, the block rides to the right, the word turns over half-way.
    // Full: the wipe has a bright front; a flip (flip 0..1 of HudFlipTime after it, 1: none) kicks the housing's corners out and
    // splits the word red / cyan for a moment
    const double HudFlipTime = 0.2;

    public void HudSwitch(RectangleF t, float k, bool enabled, float hover = 0, float flip = 1)
    {
        const float cut = 7;
        uint fill = enabled ? CyberHud.Cyan : Theme.Mix(CyberHud.Card3, CyberHud.CyanDim, 0.5f);
        bool full = k >= 0.999f;
        // on: the fill alone (its edge is its own colour); a wipe: the dark housing, the fill clipped to how far it has come
        if (full) P.FillCut(t, fill, 0, 0, cut, 0);
        else
        {
            P.FillCut(t, CyberHud.Page, 0, 0, cut, 0);
            if (k > 0.01f)
            {
                P.PushClip(new RectangleF(t.X, t.Y, t.Width * k, t.Height));
                P.FillCut(t, fill, 0, 0, cut, 0);
                P.PopClip();
                if (CyberFx.Full && enabled)
                {
                    float fx = t.X + t.Width * k, fb = MathF.Min(t.Bottom - 2, t.Bottom - (fx - (t.Right - cut)));
                    if (fb > t.Y + 4) P.Fill(new RectangleF(fx - 1.5f, t.Y + 2, 2, fb - t.Y - 2), Theme.WithAlpha(CyberHud.White, 0.9f));
                }
            }
        }
        uint edge = !enabled ? CyberHud.Line : k > 0.5f ? CyberHud.Cyan : Theme.Mix(CyberHud.Line, CyberHud.CyanDim, 0.4f + 0.6f * hover);
        if (!full || !enabled) P.StrokeCut(RectangleF.Inflate(t, -0.6f, -0.6f), edge, 1.25f, 0, 0, cut - 0.3f, 0);
        if (flip < 1) CyberFx.Corners(P, t, Theme.WithAlpha(CyberHud.Cyan, 1 - flip), 6, 1.5f, 1 + 6 * CyberFx.EaseOut(flip));
        // the block: a slanted bar that rides from the left end to the right
        float bw = 9, bx = t.X + 5 + (t.Width - 10 - bw - 3) * k, by = t.Y + 5, bh = t.Height - 10;
        _sw[0] = new Vector2(bx + 3, by); _sw[1] = new Vector2(bx + 3 + bw, by); _sw[2] = new Vector2(bx + bw, by + bh); _sw[3] = new Vector2(bx, by + bh);
        P.Polygon(_sw, 4, !enabled ? CyberHud.Faint : k > 0.5f ? CyberHud.Ink : CyberHud.Grey);
        // the word, on the side the block is not
        bool onWord = k > 0.5f;
        string word = onWord ? "ON" : "OFF";
        var tr = onWord ? new RectangleF(t.X + 4, t.Y, t.Width - bw - 18, t.Height) : new RectangleF(t.X + bw + 12, t.Y, t.Width - bw - 18, t.Height);
        uint wc = !enabled ? CyberHud.Faint : onWord ? CyberHud.Ink : CyberHud.Grey;
        HudSplit(word, tr, wc, 11.5f, FontWeight.Bold, TextAlignment.Center, CyberHud.Mono, flip < 1 ? 2 * (1 - flip) : 0);
    }

    // ---------------- slider: a segmented bar, a notch on it, the value in monospace ----------------
    bool HudSlider(string id, RectangleF r, string label, float value, float min, float max, Func<float, string> fmt, bool enabled, bool hover, bool held)
    {
        float h = Anim(K(id, "#h"), held ? 2 : hover && enabled ? 1 : 0, 18);
        var track = new RectangleF(r.X + 10, r.Bottom - 18, r.Width - 20, 6);
        uint fg = enabled ? CyberHud.White : CyberHud.Faint;
        P.Text(label, new RectangleF(r.X + 10, r.Y, r.Width / 2, r.Height - 22), fg, 15);
        uint vc = !enabled ? CyberHud.Faint : held ? CyberHud.Cyan : Theme.Mix(CyberHud.Grey, CyberHud.White, MathF.Min(1, h));
        P.Text(ValueText(id, fmt, value), new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2 - 10, r.Height - 22), vc, 13.5f, FontWeight.SemiBold,
            TextAlignment.Trailing, ParagraphAlignment.Center, CyberHud.Mono);
        float t = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0;
        float shown = Anim(K(id, "#v"), t, held ? 60 : 14);           // the segments fill block by block after a jump
        var bar = new RectangleF(track.X, track.Y - 2, track.Width, 10);
        int n = Math.Max(6, (int)MathF.Round((bar.Width + 3) / 13f));
        CyberHud.Segments(P, bar, shown, enabled ? CyberHud.Cyan : CyberHud.Faint, CyberHud.Track, n, 3);
        // the notch: a bar across the track, a yellow pointer on top
        float kx = track.X + track.Width * t;
        float hv = MathF.Min(1, h);
        if (hv > 0.01f && enabled)
            CyberFx.Corners(P, new RectangleF(kx - 9, bar.Y - 9, 18, bar.Height + 18), Theme.WithAlpha(held ? CyberHud.Cyan : CyberHud.CyanDim, hv), 5, 1.5f, 1 - hv * 2);
        P.Fill(new RectangleF(kx - 2, bar.Y - 5, 4, bar.Height + 10), !enabled ? CyberHud.Faint : held ? CyberHud.Cyan : CyberHud.White);
        _sw[0] = new Vector2(kx - 5.5f, bar.Y - 11); _sw[1] = new Vector2(kx + 5.5f, bar.Y - 11); _sw[2] = new Vector2(kx + 1.5f, bar.Y - 5); _sw[3] = new Vector2(kx - 1.5f, bar.Y - 5);
        P.Polygon(_sw, 4, enabled ? CyberHud.Yellow : CyberHud.Faint);
        return false;
    }

    // ---------------- choice: a tab strip, the pick's cyan underline sliding over ----------------
    bool HudSegmented(string id, RectangleF r, string[] options, ref int index, bool enabled)
    {
        bool changed = false;
        int n = options.Length;
        float w = r.Width / n;
        float pos = Anim(K(id, "#sel"), index, 14);
        P.FillCut(r, CyberHud.Page, 0, 0, 7, 0);
        P.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), CyberHud.Line, 1.25f, 0, 0, 6.7f, 0);
        for (int i = 0; i < n; i++)
        {
            var cell = new RectangleF(r.X + i * w + 3, r.Y + 3, w - 6, r.Height - 6);
            string cid = K(id, Seg[Math.Min(i, Seg.Length - 1)]);
            bool click = Behave(cid, cell, out bool hover, out _) && enabled;
            if (click && index != i) { index = i; changed = true; Changed = true; }
            float hv = Anim(K(cid, "#h"), hover && enabled && index != i ? 1 : 0, 18);
            if (hv > 0.01f) P.Fill(cell, Theme.WithAlpha(CyberHud.Card3, hv));
            if (i > 0) P.Fill(new RectangleF(r.X + i * w - 0.5f, r.Y + 9, 1, r.Height - 18), CyberHud.Line);
        }
        // the pick: a faint cyan wash and its underline, both sliding (Full: the underline's front runs ahead and its tail catches up -
        // it stretches across and settles; the new pick's name glitches red / cyan for a moment)
        var hl = new RectangleF(r.X + pos * w + 3, r.Y + 3, w - 6, r.Height - 6);
        P.Fill(hl, Theme.WithAlpha(enabled ? CyberHud.Cyan : CyberHud.Faint, 0.13f));
        float lead = CyberFx.Full ? Anim(K(id, "#lead"), index, 30) : pos, u0 = MathF.Min(pos, lead), u1 = MathF.Max(pos, lead);
        P.Fill(new RectangleF(r.X + u0 * w + 7, hl.Bottom - 3, (u1 - u0) * w + w - 14, 3), enabled ? CyberHud.Cyan : CyberHud.Faint);
        float pick = CyFx(K(id, "#pk"), changed, HudKickTime);
        for (int i = 0; i < n; i++)
        {
            var cell = new RectangleF(r.X + i * w + 3, r.Y + 3, w - 6, r.Height - 6);
            float onSel = Math.Clamp(1 - MathF.Abs(pos - i), 0, 1);
            uint fg = !enabled ? CyberHud.Faint : Theme.Mix(CyberHud.Grey, CyberHud.Cyan, onSel);
            var (s, fs) = HudFit(options[i], cell.Width - 8, 13);
            HudSplit(s, new RectangleF(cell.X, cell.Y, cell.Width, cell.Height - 1), fg, fs, FontWeight.SemiBold, TextAlignment.Center, null,
                i == index && pick < 1 ? 2.5f * (1 - pick) : 0);
        }
        return changed;
    }

    // ---------------- card: a plate with a dark-red edge; picked - a cyan edge and brackets ----------------
    void HudCard(string id, RectangleF r, bool selected, bool hover)
    {
        float h = Anim(K(id, "#h"), hover ? 1 : 0, 18);
        float s = Anim(K(id, "#s"), selected ? 1 : 0, 16);
        var (tl, br) = CyberHud.Cuts(r, 12);
        uint fill = Theme.Mix(Theme.Mix(CyberHud.Card, CyberHud.Card2, h), Theme.Mix(CyberHud.Card2, CyberHud.Cyan, 0.06f), s);
        P.FillCut(r, fill, tl, 0, br, 0);
        uint edge = Theme.Mix(Theme.Mix(CyberHud.Edge, CyberHud.CyanDim, h), CyberHud.Cyan, s);
        float ew = 1.25f + 0.25f * s;
        P.StrokeCut(RectangleF.Inflate(r, -ew / 2, -ew / 2), edge, ew, MathF.Max(0, tl - 0.3f), 0, MathF.Max(0, br - 0.3f), 0);
        if (r.Height >= 30) HudBrackets(r, MathF.Max(s, h * 0.7f), s > 0.5f ? CyberHud.Cyan : CyberHud.CyanDim, 8);
    }

    // ---------------- colour: a cut square; picked - white brackets round it ----------------
    void HudSwatch(RectangleF box, float cx, float cy, float r, uint color, bool selected, bool hover, string inner, string id)
    {
        float s = Anim(K(id, "#s"), selected ? 1 : 0, 14);
        float h = Anim(K(id, "#h"), hover ? 1 : 0, 18);
        var sq = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
        float cut = MathF.Round(r * 0.55f);
        if (s > 0.01f) CyberFx.Corners(P, sq, Theme.WithAlpha(CyberHud.White, s), MathF.Max(5, r * 0.6f), 2, 4 + 2 * (1 - s));
        else if (h > 0.01f) CyberFx.Corners(P, sq, Theme.WithAlpha(CyberHud.Grey, h), MathF.Max(4, r * 0.45f), 1.5f, 3);
        P.FillCut(sq, color, 0, 0, cut, 0);
        P.StrokeCut(RectangleF.Inflate(sq, -0.5f, -0.5f), 0x30FFFFFF, 1, 0, 0, cut - 0.3f, 0);
        if (inner != null)
            P.Text(inner, sq, Theme.Luma(color) > 0.55f ? CyberHud.Ink : CyberHud.White, r * 0.9f, FontWeight.Bold, TextAlignment.Center,
                ParagraphAlignment.Center, CyberHud.Mono);
    }

    // ---------------- caption / paragraph ----------------
    // a caption (the grey, one-line label over a control): monospace capitals; a paragraph (wrapped): monospace grey when it fits its
    // box, else the condensed face; any other text (a coloured message) in the condensed face
    void HudLabel(RectangleF r, string text, uint color, float size, FontWeight w, bool wrap)
    {
        if (string.IsNullOrEmpty(text)) return;
        string mono = CyberHud.Mono;
        if (!wrap && color == Theme.Dim && w == FontWeight.Normal)
        {
            var (s, fs) = HudFit(text, r.Width, MathF.Min(12.5f, size), FontWeight.SemiBold, mono);
            P.Text(s, r, CyberHud.Grey, fs, FontWeight.SemiBold, TextAlignment.Leading, ParagraphAlignment.Center, mono);
            return;
        }
        if (wrap && color == Theme.Dim)
        {
            float ms = MathF.Min(12.5f, size);
            if (P.MeasureHeight(text, ms, FontWeight.Normal, r.Width, mono) <= r.Height)
            {
                P.Text(text, r, CyberHud.Grey, ms, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Near, mono, true);
                return;
            }
        }
        P.Text(text, r, color, size, w, TextAlignment.Leading, wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center, null, wrap);
    }

    // ---------------- section: the yellow tag on a rule, three ticks after it ----------------
    // (the ticks right after the tag, not at the rule's end: a page may put a note at the right of its section's line)
    void HudSectionRule(RectangleF r, float tagW, float tagBottom)
    {
        float x0 = r.X + tagW + 6, x1 = MathF.Min(r.Right, HudRight);
        if (x1 - x0 < 20) return;
        P.Fill(new RectangleF(r.X, tagBottom - 1, x1 - r.X, 1), CyberHud.Line);
        for (int i = 0; i < 3; i++)
            P.Fill(new RectangleF(x0 + i * 5, tagBottom - 5, 3, 3), i == 0 ? CyberHud.Yellow : CyberHud.Edge);
    }
}
