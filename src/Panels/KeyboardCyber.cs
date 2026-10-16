using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// The keyboard in the Cyber style: a HUD input console, not the modern keyboard with cut corners (Erik, 1.13.2: "only the key shapes
// changed"). The same keys in the same places, drawn the watch's way:
//  - the frame: near black, olive, corners cut (the cuts yellow); a header strip - the yellow INPUT // KEYBOARD tag, the layout and
//    the mode as monospace readouts, a lamp for each modifier (lit cyan while it is on), a hazard chip at the end;
//  - the top bar: HUD buttons (a cyan-edged plate, the icon cyan, the name in condensed capitals; Chatbox lit cyan while on, Send
//    the yellow one);
//  - a key: a dark plate, its bottom-right corner cut, a thin dark outline, a small olive tick in its top-left corner; the letter in
//    Bahnschrift's condensed bold, the shifted symbol and the AltGr one as small monospace legends (grey / cyan); the keys that do
//    something rather than type (Esc, Tab, Caps, Shift, Ctrl, Win, Alt, AltGr, Del, Enter, Backspace, the arrows) in cyan
//    monospace capitals, the F keys grey; the space bar a row of segments;
//  - a modifier once: a cyan edge and brackets; locked (and Caps on): a cyan block with a LOCK code.
// Motion (CyberFx; Look > Motion): a key under the laser - a cyan edge, brackets closing in; pressed - a cyan block; let go - the
// cyan fades (0.25 s) while its brackets fly out and the legend splits red / cyan for a moment (the glitch; Full); a modifier
// latching - its brackets snap in from 10 px out (once) or the cyan wipes in from the left (locked), 0.22 s (Reduced too); the
// keyboard opening - the corner brackets snap out from the middle, a scan line sweeps down and every row it passes flashes cyan
// (Full; Reduced: the brackets); closing - it folds into a line (Full). Nothing moves at rest: no frame of its own then.
public sealed partial class KeyboardPanel
{
    // the Cyber rows: a header strip on top (46 px), the top bar and the key rows a little lower (676 px tall in every style)
    const float CyHead = 46, CyBar = 74, CyU = 89.6f;
    bool _builtCyber;

    void RebuildLayout()
    {
        ReleaseAll();
        _keys.Clear(); _barNormal.Clear(); _barChat.Clear();
        Array.Clear(_hover); Array.Clear(_down); Array.Clear(_held95);
        _flash.Clear();
        _cyLatch.Clear();
        BuildLayout();
        _hkl = IntPtr.Zero;            // the key caps again (Draw)
        _dirty = true;
    }

    // ---- the latch animation: when a key's modifier state last changed ----
    readonly Dictionary<Key, (int state, double at)> _cyLatch = new();
    const double CyLatchTime = 0.22;

    float CyLatch(Key k, int state, double now, out int prev)
    {
        prev = state;
        if (!_cyLatch.TryGetValue(k, out var l)) { _cyLatch[k] = (state, -10); return 1; }
        if (l.state != state) { prev = l.state; _cyLatch[k] = l = (state, now); }
        double t = now - l.at;
        if (!CyberFx.Any || t >= CyLatchTime) return 1;
        AnimWake.At(now);
        return CyberFx.EaseOut((float)(t / CyLatchTime));
    }

    static readonly string CyGlobe = ((char)0xE774).ToString();
    static readonly string[] CyMods = { "shift", "ctrl", "alt", "altgr", "win" };
    static readonly string[] CyModNames = { "SHIFT", "CTRL", "ALT", "ALTGR", "WIN" };

    // a key that does something rather than type: its legend in cyan
    static bool CyCommand(Key k) => k.Kind is Kind.Modifier or Kind.Caps || k.Icon != null ||
                                    k.Fixed is "Esc" or "Tab" or "Del";

    void DrawCyber()
    {
        if (_builtCyber != Theme.Cyber) RebuildLayout();
        if (_hkl == IntPtr.Zero)
        {
            if (BuiltIn) { _hkl = (IntPtr)(-1); ApplyBuiltInLabels(); }
            else RefreshLabels(Native.ForegroundLayout());
        }
        double now = Clock.Now;
        var all = new RectangleF(0, 0, TW, TH);
        _p.Begin(Surf.Bitmap, 0x00000000);
        var rev = CyberReveal.Begin(_p, all, Ov != null ? Ov.ShownAt : OpenAtForTest, Ov != null ? _vis : 1, Ov != null && !(C.Visible && C.Place.M != null));
        CyberHud.ConsoleFrame(_p, all, Theme.OverlayBg, 22);
        bool shift = _mods["shift"] > 0, altgr = _mods["altgr"] > 0;
        foreach (var key in new List<Key>(_flash.Keys))
        {
            if (Array.IndexOf(_down, key) >= 0) continue;
            float f = Motion.Any ? _flash[key] - _lastDt / 0.25f : 0;
            if (f <= 0) _flash.Remove(key); else _flash[key] = f;
        }
        CyHeader();
        if (_chat) CyChatField(now);
        // the opening's scan line: the rows it has just passed flash (Full)
        float scanY = rev.Kind == 1 && CyberFx.Full ? CyberReveal.ScanY(rev) : -1;
        var drawList = new List<Key>(_keys);
        drawList.AddRange(_chat ? _barChat : _barNormal);
        foreach (var k in drawList) CyKey(k, shift, altgr, now, scanY);
        CyberReveal.End(_p, rev);
        _p.End();
    }

    // ---- the header strip: the tag, the readouts, the modifier lamps, a hazard chip ----
    void CyHeader()
    {
        float y = Pad + 2, h = CyHead - 12, x = Pad + 6;
        float tw = CyberHud.Tag(_p, x, y + (h - 28) / 2, "INPUT // KEYBOARD", size: 19, h: 28);
        x += tw + 18;
        float by = y + h / 2 + 7;
        string lang = "--";
        foreach (var k in _keys) if (k.Action == "lang") { lang = k.Fixed ?? "--"; break; }
        x = CyReadout(x, by, "LAYOUT", lang, CyberHud.White);
        x = CyReadout(x, by, "MODE", _chat ? "CHATBOX" : "TYPE", _chat ? CyberHud.Cyan : CyberHud.White);
        // the lamps: right-aligned, before the hazard chip
        float hx = TW - Pad - 6 - 96;
        var hz = new RectangleF(hx, y + h / 2 - 6, 96, 12);
        _p.Fill(hz, CyberHud.Yellow);
        CyberHud.Hazard(_p, hz, CyberHud.Ink, 12);
        float lx = hx - 14;
        bool caps = Caps;
        for (int i = CyMods.Length; i >= 0; i--)
        {
            string name = i == CyMods.Length ? "CAPS" : CyModNames[i];
            int st = i == CyMods.Length ? (caps ? 2 : 0) : _mods[CyMods[i]];
            float w = CyberHud.MonoWidth(_p, name, 16, FontWeight.Bold) + 20;
            var r = new RectangleF(lx - w, y + (h - 28) / 2, w, 28);
            if (st == 2) _p.FillCut(r, CyberHud.Cyan, 0, 0, 6, 0);
            else if (st == 1) _p.FillCut(r, Theme.WithAlpha(CyberHud.Cyan, 0.16f), 0, 0, 6, 0);
            _p.StrokeCut(RectangleF.Inflate(r, -0.6f, -0.6f), st > 0 ? CyberHud.Cyan : CyberHud.Line, 1.25f, 0, 0, 5.7f, 0);
            _p.Text(name, r, st == 2 ? CyberHud.Ink : st == 1 ? CyberHud.Cyan : CyberHud.Faint, 16, FontWeight.Bold, TextAlignment.Center,
                ParagraphAlignment.Center, CyberHud.Mono);
            lx -= w + 6;
        }
        _p.Fill(new RectangleF(Pad, Pad + CyHead - 4, TW - 2 * Pad, 1), CyberHud.Line);
        for (int i = 0; i < 3; i++) _p.Fill(new RectangleF(Pad + 2 + i * 6, Pad + CyHead - 7, 4, 4), i == 0 ? CyberHud.Yellow : CyberHud.Edge);
    }

    float CyReadout(float x, float by, string key, string value, uint c)
    {
        CyberHud.MonoAt(_p, key, x, by, 140, CyberHud.Grey, 18, FontWeight.SemiBold);
        x += CyberHud.MonoWidth(_p, key, 18, FontWeight.SemiBold) + 10;
        CyberHud.MonoAt(_p, value, x, by, 220, c, 18, FontWeight.Bold);
        return x + CyberHud.MonoWidth(_p, value, 18, FontWeight.Bold) + 30;
    }

    // ---- the Chatbox line: a dark field with a cyan edge and brackets, a block cursor ----
    void CyChatField(double now)
    {
        var f = _chatField;
        _p.FillCut(f, CyberHud.Page, 0, 0, 10, 0);
        _p.StrokeCut(RectangleF.Inflate(f, -0.75f, -0.75f), CyberHud.Cyan, 1.5f, 0, 0, 9.6f, 0);
        CyberHud.Brackets(_p, f, CyberHud.Cyan, 10, 4, 2);
        string text = _chatText.ToString();
        var tr = new RectangleF(f.X + 18, f.Y, f.Width - 130, f.Height);
        if (text.Length == 0)
            _p.Text("Type a message for the VRChat chatbox, Enter sends", tr, CyberHud.Faint, 18, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, CyberHud.Mono);
        else
        {
            string shown = text;
            while (shown.Length > 1 && _p.MeasureWidth(shown, 26) > tr.Width - 20) shown = shown[1..];
            if (shown.Length < text.Length) shown = "…" + shown;
            _p.Text(shown, tr, CyberHud.White, 26);
            if (_blink == 0)
            {
                float cx = tr.X + _p.MeasureWidth(shown, 26) + 4;
                _p.Fill(new RectangleF(cx, f.Y + 15, 12, f.Height - 30), CyberHud.Cyan);
            }
        }
        _p.Text($"{text.Length}/{ChatMax}", new RectangleF(f.Right - 120, f.Y, 104, f.Height), CyberHud.Grey, 15, FontWeight.SemiBold,
            TextAlignment.Trailing, ParagraphAlignment.Center, CyberHud.Mono);
    }

    static readonly Vector2[] _cyTick = new Vector2[4];

    // ---- one key ----
    void CyKey(Key k, bool shift, bool altgr, double now, float scanY)
    {
        bool hover = Array.IndexOf(_hover, k) >= 0;
        bool down = Array.IndexOf(_down, k) >= 0;
        _flash.TryGetValue(k, out float glow);
        int modState = k.Kind == Kind.Modifier ? _mods[k.Mod] : k.Kind == Kind.Caps && Caps ? 2 : k.Action == "chat" && _chat ? 2 : 0;
        float latch = k.Kind is Kind.Modifier or Kind.Caps || k.Action == "chat" ? CyLatch(k, modState, now, out int prevState) : 1;
        bool action = k.Kind == Kind.Action && k.Action != "lang";
        bool primary = k.Action == "send";
        var kr = down ? RectangleF.Inflate(k.R, -3, -3) : k.R;
        float cut = action ? 12 : 10;
        bool lit = down || modState == 2;
        // the plate
        uint bg = primary ? CyberHud.Yellow : hover ? CyberHud.Card3 : CyberHud.Card2;
        if (!down && glow > 0) bg = Theme.Mix(bg, CyberHud.Cyan, glow * 0.6f);
        _p.FillCut(kr, bg, 0, 0, cut, 0);
        if (lit)
        {
            // pressed / locked: the cyan block (locking: it wipes in from the left)
            float wk = down ? 1 : latch;
            if (wk < 1) _p.PushClip(new RectangleF(kr.X, kr.Y, kr.Width * wk, kr.Height));
            _p.FillCut(kr, CyberHud.Cyan, 0, 0, cut, 0);
            if (wk < 1) _p.PopClip();
        }
        // the row the opening's scan line just passed: a cyan flash
        if (scanY >= 0 && kr.Y < scanY)
        {
            float a = 1 - CyberFx.Clamp01((scanY - kr.Y) / 110f);
            if (a > 0.01f) _p.FillCut(kr, Theme.WithAlpha(CyberHud.Cyan, 0.4f * a), 0, 0, cut, 0);
        }
        // the edge
        if (!lit)
        {
            uint edge = hover ? CyberHud.Cyan : modState == 1 ? CyberHud.Cyan : action ? CyberHud.CyanDim : primary ? CyberHud.Yellow : CyberHud.Line;
            float ew = hover || modState == 1 ? 1.75f : 1.25f;
            _p.StrokeCut(RectangleF.Inflate(kr, -ew / 2, -ew / 2), edge, ew, 0, 0, cut - ew * 0.3f, 0);
        }
        // the tick in its top-left corner
        if (!lit && !primary)
        {
            uint tc = action ? CyberHud.CyanDim : CyberHud.Frame;
            _p.Fill(new RectangleF(kr.X + 5, kr.Y + 5, 7, 1.5f), Theme.WithAlpha(tc, 0.8f));
            _p.Fill(new RectangleF(kr.X + 5, kr.Y + 5, 1.5f, 7), Theme.WithAlpha(tc, 0.8f));
        }
        // a modifier on once: its brackets (snapping in when it latches); hover: brackets closing in
        if (modState == 1) CyberHud.Brackets(_p, kr, CyberHud.Cyan, 10, 5 - 10 * (1 - latch), 2);
        else if (hover && !down) CyberHud.Brackets(_p, kr, CyberHud.Cyan, 9, 5, 2);
        if (modState == 2 && !down && k.Kind != Kind.Action)
            CyberHud.MonoAt(_p, "LOCK", kr.Right - 50, kr.Y + 20, 46, Theme.WithAlpha(CyberHud.Ink, 0.7f), 13, FontWeight.Bold);
        // let go: the brackets fly out as the glow fades
        if (glow > 0.01f && !down && Motion.Any)
            CyberFx.Corners(_p, kr, Theme.WithAlpha(CyberHud.Cyan, glow), 10, 2, 1 + 8 * (1 - glow));
        // the glitch: for the first moments after it is let go the legend splits red / cyan (Full)
        float split = glow > 0.55f && !down && CyberFx.Full ? (glow - 0.55f) / 0.45f * 3f : 0;
        CyLegend(k, kr, lit, primary, shift, altgr, split);
    }

    void CyLegend(Key k, RectangleF kr, bool lit, bool primary, bool shift, bool altgr, float split)
    {
        uint ink = lit || primary ? CyberHud.Ink : 0;
        if (k.Kind == Kind.Action)
        {
            if (k.Action == "lang")
            {
                // the globe and the layout's code
                float lw = CyberHud.MonoWidth(_p, k.Fixed ?? "", 21, FontWeight.Bold);
                float x0 = kr.X + (kr.Width - (26 + 6 + lw)) / 2;
                _p.Icon(CyGlobe, new RectangleF(x0, kr.Y, 26, kr.Height), ink != 0 ? ink : CyberHud.Cyan, 22);
                _p.Text(k.Fixed, new RectangleF(x0 + 32, kr.Y, lw + 6, kr.Height), ink != 0 ? ink : CyberHud.White, 21, FontWeight.Bold,
                    TextAlignment.Leading, ParagraphAlignment.Center, CyberHud.Mono);
                return;
            }
            string label = k.Fixed.ToUpperInvariant();
            float fs = 21;
            float tw = _p.MeasureWidth(label, fs, FontWeight.SemiBold);
            if (28 + 10 + tw > kr.Width - 20) { fs = 18; tw = _p.MeasureWidth(label, fs, FontWeight.SemiBold); }
            float x = kr.X + (kr.Width - (28 + 10 + tw)) / 2;
            _p.Icon(k.Icon, new RectangleF(x, kr.Y, 28, kr.Height), ink != 0 ? ink : CyberHud.Cyan, 23);
            _p.Text(label, new RectangleF(x + 38, kr.Y, tw + 6, kr.Height), ink != 0 ? ink : CyberHud.White, fs, FontWeight.SemiBold);
            return;
        }
        if (k.Icon != null)
        {
            Split(() => _p.Icon(k.Icon, kr, ink != 0 ? ink : CyberHud.Cyan, 28), split, () => _p.Icon(k.Icon, Off(kr, -split), CyberHud.Red, 28),
                () => _p.Icon(k.Icon, Off(kr, split), CyberHud.Cyan, 28));
            return;
        }
        if (k.Fixed == " ")
        {
            // the space bar: a row of segments and its name
            var bar = new RectangleF(kr.X + kr.Width * 0.2f, kr.Y + kr.Height / 2 - 4, kr.Width * 0.6f, 8);
            CyberHud.Segments(_p, bar, lit ? 1 : 0, ink != 0 ? ink : CyberHud.Cyan, lit ? Theme.WithAlpha(CyberHud.Ink, 0.4f) : CyberHud.Track, 14, 4);
            _p.Text("SPACE", new RectangleF(kr.X, kr.Bottom - 28, kr.Width, 22), ink != 0 ? ink : CyberHud.Faint, 14, FontWeight.Bold, TextAlignment.Center,
                ParagraphAlignment.Center, CyberHud.Mono);
            return;
        }
        if (k.Fixed != null)
        {
            string s = k.Fixed.ToUpperInvariant();
            bool fkey = s.Length >= 2 && s[0] == 'F' && char.IsDigit(s[1]);
            uint c = ink != 0 ? ink : CyCommand(k) ? CyberHud.Cyan : fkey ? CyberHud.Grey : CyberHud.White;
            float fs = s.Length > 4 ? 19 : 22;
            var tr = new RectangleF(kr.X, kr.Y + 1, kr.Width, kr.Height);
            Split(() => _p.Text(s, tr, c, fs, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono), split,
                () => _p.Text(s, Off(tr, -split), CyberHud.Red, fs, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono),
                () => _p.Text(s, Off(tr, split), CyberHud.Cyan, fs, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono));
            return;
        }
        string main = altgr && k.AltGr != null ? k.AltGr : shift ? k.Shifted ?? k.Normal : k.Normal;
        if (main != null && main.Length == 1 && char.IsLetter(main[0]) && Caps && !altgr)
            main = shift ? main.ToLowerInvariant() : main.ToUpperInvariant();
        uint mc = ink != 0 ? ink : CyberHud.White;
        var mr = new RectangleF(kr.X, kr.Y + 2, kr.Width, kr.Height - 2);
        Split(() => _p.Text(main, mr, mc, 40, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display), split,
            () => _p.Text(main, Off(mr, -split), CyberHud.Red, 40, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display),
            () => _p.Text(main, Off(mr, split), CyberHud.Cyan, 40, FontWeight.SemiBold, TextAlignment.Center, ParagraphAlignment.Center, Theme.Display));
        // the small legends: the shifted symbol top-left (grey), the AltGr one bottom-right (cyan)
        if (!shift && k.Shifted != null && k.Normal != null && !char.IsLetter(k.Normal[0]))
            _p.Text(k.Shifted, new RectangleF(kr.X + 14, kr.Y + 6, 34, 28), ink != 0 ? ink : CyberHud.Grey, 20, FontWeight.SemiBold, TextAlignment.Leading,
                ParagraphAlignment.Center, CyberHud.Mono);
        if (!altgr && k.AltGr != null)
            _p.Text(k.AltGr, new RectangleF(kr.Right - 40, kr.Bottom - 34, 30, 28), ink != 0 ? ink : Theme.WithAlpha(CyberHud.Cyan, 0.9f), 20,
                FontWeight.SemiBold, TextAlignment.Trailing, ParagraphAlignment.Center, CyberHud.Mono);
    }

    static RectangleF Off(RectangleF r, float dx) => new(r.X + dx, r.Y, r.Width, r.Height);

    // the legend, with a red ghost to the left and a cyan one to the right while it glitches
    static void Split(Action main, float split, Action red, Action cyan)
    {
        if (split > 0.3f) { red(); cyan(); }
        main();
    }

    // the modern keyboard's Cyber marks (no longer drawn: Cyber has its own keyboard above; kept for the modern draw's call)
    void CyberKey(RectangleF kr, bool hover, bool down, float glow)
    {
        if (hover && !down)
        {
            _p.Stroke(RectangleF.Inflate(kr, -0.6f, -0.6f), CyberHud.Cyan, 1.5f, 11.4f);
            CyberHud.Brackets(_p, kr, CyberHud.Cyan, 9, 6, 2);
        }
        if (glow > 0.01f && !down && Motion.Any)
            CyberFx.Corners(_p, kr, Theme.WithAlpha(CyberHud.Cyan, glow), 10, 2, 1 + 7 * (1 - glow));
    }

    // --animshot: key i let go just now (its glow)
    public void CyberFlashForTest(int i)
    {
        if (i >= 0 && i < _keys.Count) _flash[_keys[i]] = 1;
        _lastDt = 1 / 120f;                         // Draw fades the glow by the last Update's step: the tool's
    }

    // --animshot: where key i is (texture px)
    public RectangleF KeyRectForTest(int i) => i >= 0 && i < _keys.Count ? _keys[i].R : RectangleF.Empty;

    // --animshot: key i held down / let go - only its look (nothing is sent to Windows: the tool must not type)
    public void HoldForTest(int i, bool down)
    {
        if (i < 0 || i >= _keys.Count) return;
        if (down) { _down[2] = _keys[i]; _flash[_keys[i]] = 1; }
        else _down[2] = null;
        _lastDt = 1 / 120f;
        _dirty = true;
    }

    // --animshot: a modifier's state (0 off, 1 once, 2 locked), as a click on it sets it
    public void ModForTest(string mod, int state) { _mods[mod] = state; _dirty = true; }

    public int KeyIndexForTest(string fixedOrMod)
    {
        for (int i = 0; i < _keys.Count; i++) if (_keys[i].Mod == fixedOrMod || _keys[i].Fixed == fixedOrMod) return i;
        return -1;
    }

    public double OpenAtForTest = -10;
}
