using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// Pointer state fed by whoever shows the UI (desktop window, SteamVR dashboard, our laser).
// Down/up edges are latched so a click that starts and ends between two frames is not lost.
public sealed class PointerState
{
    public Vector2 Pos;
    public bool Inside;
    public bool Down;
    public bool PressedLatch, ReleasedLatch;
    public float Wheel;

    public void Move(float x, float y) { Pos = new(x, y); Inside = true; }
    public void Leave() { Inside = false; if (Down) { Down = false; ReleasedLatch = true; } }
    public void Press() { Down = true; PressedLatch = true; }
    public void Release() { if (Down || PressedLatch) ReleasedLatch = true; Down = false; }
}

// Small immediate-mode widget set drawn with Painter. Layout is in logical units.
// Widgets ease between states (hover glow, toggle knob, segment highlight...); while anything is
// still moving, Animating is set and the owner should draw another frame soon.
public sealed partial class Ui
{
    public readonly Painter P;
    public Vector2 Mouse;
    public bool Inside, Down, Pressed, Released;
    public float Wheel;
    string _active;
    public bool Changed;      // a value changed this frame
    public bool Interacting => _active != null;
    public bool Animating;    // an eased value has not reached its target yet
    float _dt = 1 / 60f;
    readonly Dictionary<string, float> _anim = new();
    long _lastBegin;
    readonly List<RectangleF> _hot = new();     // every widget area of the last drawn frame

    // "id" + "#h" etc. would be a new string for every widget in every frame: build each once
    readonly Dictionary<(string, string), string> _keys = new();
    string K(string id, string part)
    {
        if (!_keys.TryGetValue((id, part), out var k)) _keys[(id, part)] = k = id + part;
        return k;
    }

    static readonly Dictionary<(string, int), string> _ids = new();

    // widget id for item i of a list ("acc" + 3), made once
    public static string Id(string prefix, int i)
    {
        if (!_ids.TryGetValue((prefix, i), out var k)) _ids[(prefix, i)] = k = prefix + i;
        return k;
    }

    static readonly Dictionary<(string, string), string> _names = new();

    public static string Id(string prefix, string name)
    {
        if (!_names.TryGetValue((prefix, name), out var k)) _names[(prefix, name)] = k = prefix + name;
        return k;
    }
    int _lastHot = -2;

    public Ui(Painter p) { P = p; }

    public void Begin(PointerState s, float scale) => Begin(s, scale, 0, 0);

    public void Begin(PointerState s, float scale, float ox, float oy)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        _dt = _lastBegin == 0 ? 1 / 60f : (float)Math.Clamp((now - _lastBegin) / (double)System.Diagnostics.Stopwatch.Frequency, 0.001, 0.1);
        _lastBegin = now;
        Mouse = (s.Pos - new Vector2(ox, oy)) / scale;
        Inside = s.Inside;
        Down = s.Down;
        Pressed = s.PressedLatch;
        Released = s.ReleasedLatch;
        Wheel = s.Wheel;
        s.PressedLatch = s.ReleasedLatch = false;
        s.Wheel = 0;
        Changed = false;
        Animating = false;
        DragHeld = false;
        _hot.Clear();
        Inert = false;
        BeginPopups95();
    }

    // The pointer moved: does the picture change? Only when it gets onto another widget (hover
    // glow) or while something is pressed / dragged - otherwise the last frame is still right.
    public bool NeedsRedrawForMove(PointerState s, float scale) => NeedsRedrawForMove(s, scale, 0, 0);

    public bool NeedsRedrawForMove(PointerState s, float scale, float ox, float oy)
    {
        if (_active != null || s.Down) return true;
        var m = (s.Pos - new Vector2(ox, oy)) / scale;
        int hot = -1;
        if (s.Inside)
            for (int i = _hot.Count - 1; i >= 0; i--)
                if (_hot[i].Contains(m.X, m.Y)) { hot = i; break; }
        if (hot == _lastHot) return false;
        _lastHot = hot;
        return true;
    }

    // forget all eased values: the next frame starts settled (after an off-screen warm-up draw)
    public void ResetAnimations()
    {
        _anim.Clear();
        _springs.Clear();
        _lastBegin = 0;
        _lastHot = -2;
    }

    public void End()
    {
        if (!Down && !Pressed) _active = null;
        // what the pointer is over in the frame just drawn (pointer leave, panel reopen, list refresh)
        _lastHot = -1;
        if (Inside)
            for (int i = _hot.Count - 1; i >= 0; i--)
                if (_hot[i].Contains(Mouse.X, Mouse.Y)) { _lastHot = i; break; }
    }

    // Eases a value towards 'target' (exponential, ~1/speed seconds). The first call snaps; so does every one in the 95 styles and
    // with Look > Motion off.
    public float Anim(string id, float target, float speed = 16f)
    {
        if (Theme.Classic || !Motion.Any) { _anim[id] = target; return target; }
        if (!_anim.TryGetValue(id, out float v)) v = target;
        v += (target - v) * (1 - MathF.Exp(-speed * _dt));
        if (MathF.Abs(v - target) < 0.003f) v = target;
        else Animating = true;
        _anim[id] = v;
        return v;
    }

    // ---- springs (the modern watch): an eased value with velocity, so it can overshoot a little like
    // Apple's animations. stiffness / damping as in SwiftUI's spring; the 95 style snaps.
    readonly Dictionary<string, (float x, float v)> _springs = new();

    // snap: a change smaller than this (and nothing in motion) jumps instead of animating - no endless motion
    // (and redraws) for values that jitter a little, like a frame rate
    public float Spring(string id, float target, float stiffness = 170, float damping = 20, float snap = 0)
    {
        if (Theme.Classic || !Motion.Any || !_springs.TryGetValue(id, out var s)) { _springs[id] = (target, 0); return target; }
        if (snap > 0 && MathF.Abs(target - s.x) < snap && MathF.Abs(s.v) < 0.05f) { _springs[id] = (target, 0); return target; }
        float t = _dt;
        while (t > 0)
        {
            float h = MathF.Min(t, 1 / 240f);         // small steps: stable at any frame rate
            s.v += (stiffness * (target - s.x) - damping * s.v) * h;
            s.x += s.v * h;
            t -= h;
        }
        if (MathF.Abs(target - s.x) < 0.0015f && MathF.Abs(s.v) < 0.01f) s = (target, 0);
        else Animating = true;
        _springs[id] = s;
        return s.x;
    }

    // start a spring from a value (e.g. a widget popping in from 0)
    public void SpringFrom(string id, float from) => _springs[id] = (from, 0);

    // start an eased value from 'from' (a page fading in from 0 after a tab switch)
    public void AnimFrom(string id, float from) => _anim[id] = from;

    // before a widget's own call this frame: is it held (pressed and not let go), is the pointer over r - so what is drawn under it
    // (a card's scale, its highlight) can follow the press; no hot area is added
    public bool HeldNow(string id) => _active == id && (Down || Pressed);

    public bool OverNow(RectangleF r) => !Inert && Inside && r.Contains(Mouse.X, Mouse.Y) &&
                                         (_drop == null || !_dropList.Contains(Mouse.X, Mouse.Y));

    public bool HasSpring(string id) => _springs.ContainsKey(id);

    public void ForgetSprings(Func<string, bool> which)
    {
        foreach (var k in new List<string>(_springs.Keys)) if (which(k)) _springs.Remove(k);
    }

    // a slider / trackbar was held in the frame just drawn (a drag there is the slider's, not a swipe)
    public bool DragHeld;

    // the press belongs to no widget (the watch is being swiped): nothing is held, the release clicks nothing
    public void CancelPress()
    {
        _active = null;
        Pressed = Released = false;
    }

    // a press-and-release area without a look of its own (the caller draws it); held = pressed right now
    public bool Clickable(string id, RectangleF r, out bool hover, out bool held) => Behave(id, r, out hover, out held);

    // everything drawn while Inert is just a picture: no hover, no clicks (the watch behind an opened widget)
    public bool Inert;

    public bool Hover(RectangleF r)
    {
        _hot.Add(r);
        if (Inert) return false;
        if (_drop != null && !_inPopup && _dropList.Contains(Mouse.X, Mouse.Y)) return false;   // under an open list
        return Inside && r.Contains(Mouse.X, Mouse.Y);
    }

    // returns true on click (press and release inside)
    bool Behave(string id, RectangleF r, out bool hover, out bool held)
    {
        hover = Hover(r);
        if (hover && Pressed) _active = id;
        held = _active == id && (Down || Pressed);
        bool click = _active == id && Released && hover;
        if (click) _active = null;
        return click;
    }

    static RectangleF Shrink(RectangleF r, float k) =>
        RectangleF.Inflate(r, -r.Width * k / 2, -r.Height * k / 2);

    public bool Button(string id, RectangleF r, string label, string icon = null, bool accent = false, bool enabled = true, float fontSize = 15)
    {
        if (CyberHud_) return HudButton(id, r, label, icon, accent, enabled, fontSize);     // Cyber: the HUD's plate (UiHud.cs)
        bool click = Behave(id, r, out bool hover, out bool held) && enabled;
        if (Theme.Classic)
        {
            // a 95 push button: raised, pushed in while held; "accent" (a toggle that is on) stays latched in
            bool down = (held && enabled) || accent;
            P.Fill(r, accent && !held ? Theme.W95Light : Theme.W95Face);
            P.Bevel(r, down ? Painter.Edge.Pressed : Painter.Edge.Raised);
            float o = down ? P.Px : 0;
            ClassicContent(new RectangleF(r.X + o, r.Y + o, r.Width, r.Height), label, icon, enabled, fontSize);
            return click;
        }
        float h = Anim(K(id, "#h"), hover && enabled ? 1 : 0, 18);
        float p = Anim(K(id, "#p"), held ? 1 : 0, 30);
        float a = Anim(K(id, "#a"), accent ? 1 : 0, 14);
        uint bg = Theme.Mix(Theme.Panel2, Theme.Accent, a);
        if (!enabled) bg = Theme.Panel;
        bg = Theme.Mix(bg, 0xFFFFFFFF, 0.09f * h);
        bg = Theme.Mix(bg, 0xFF000000, 0.25f * p);
        var rr = Shrink(r, 0.03f * p);
        P.Fill(rr, bg, 8);
        if (a < 0.99f) P.Stroke(rr, Theme.WithAlpha(Theme.Mix(Theme.Border, Theme.Accent, 0.5f * h), 1 - a), 1, 8);
        if (Theme.Cyber && enabled && a < 0.5f) CyberHover(rr, h);      // Cyber: a cyan outline, the corner marks closing in (UiCyber.cs)
        uint fg = !enabled ? Theme.Faint : Theme.Mix(Theme.Text, Theme.OnAccent, a);
        if (icon != null && !string.IsNullOrEmpty(label))
        {
            float tw = P.MeasureWidth(label, fontSize, FontWeight.SemiBold);
            float total = fontSize + 8 + tw;
            float x = r.X + (r.Width - total) / 2;
            P.Icon(icon, new RectangleF(x, r.Y, fontSize, r.Height), fg, fontSize);
            P.Text(label, new RectangleF(x + fontSize + 8, r.Y, tw + 4, r.Height), fg, fontSize, FontWeight.SemiBold);
        }
        else if (icon != null)
            P.Icon(icon, rr, fg, fontSize + 3);
        else
            P.Text(label, r, fg, fontSize, FontWeight.SemiBold, TextAlignment.Center);
        return click;
    }

    public bool Toggle(string id, RectangleF r, string label, ref bool value, string hint = null, bool enabled = true)
    {
        if (CyberHud_) return HudToggle(id, r, label, ref value, hint, enabled);          // Cyber: an ON / OFF switch (UiHud.cs)
        bool click = Behave(id, r, out bool hover, out _) && enabled;
        if (click) { value = !value; Changed = true; }
        if (Theme.Classic)
        {
            // a check box: white sunken well with a tick, the label to its right
            const float bs = 16;
            var box = new RectangleF(r.X + 10, r.Y + (r.Height - bs) / 2, bs, bs);
            P.Fill(box, enabled ? Theme.W95Window : Theme.W95Face);
            P.Bevel(box, Painter.Edge.Sunken);
            if (value)
            {
                uint tick = enabled ? Theme.W95Dark : Theme.W95Shadow;
                P.Line(box.X + 4, box.Y + 8, box.X + 7, box.Y + 11.5f, tick, 2.2f);
                P.Line(box.X + 7, box.Y + 11.5f, box.X + 12.5f, box.Y + 4.5f, tick, 2.2f);
            }
            float lx = box.Right + 8, lw = r.Right - lx - 4;
            if (hint == null)
                ClassicText(label, new RectangleF(lx, r.Y, lw, r.Height), enabled, 15);
            else
            {
                ClassicText(label, new RectangleF(lx, r.Y + 2, lw, r.Height / 2), enabled, 15, FontWeight.Normal, ParagraphAlignment.Far);
                P.Text(hint, new RectangleF(lx, r.Y + r.Height / 2 + 1, lw, r.Height / 2 - 2), Theme.Dim, 12, FontWeight.Normal,
                    TextAlignment.Leading, ParagraphAlignment.Near);
            }
            return click;
        }
        float h = Anim(K(id, "#h"), hover && enabled ? 1 : 0, 18);
        float k = Anim(K(id, "#k"), value ? 1 : 0, 14);
        if (h > 0) P.Fill(r, Theme.WithAlpha(0x0FFFFFFF, h), 8);
        float sw = 46, sh = 26;
        var track = new RectangleF(r.Right - sw - 10, r.Y + (r.Height - sh) / 2, sw, sh);
        P.Fill(track, enabled ? Theme.Mix(Theme.Panel3, Theme.Accent, k) : Theme.Panel3, sh / 2);
        // ease-out-back feel: the knob slightly overshoots on the way
        float kx = track.X + sh / 2 + (sw - sh) * k;
        float kr = sh / 2 - 4 + 1.5f * MathF.Sin(MathF.PI * k);
        uint kc = enabled ? Theme.Mix(Theme.Dim, Theme.OnAccent, k) : Theme.Dim;
        if (Theme.Cyber) P.FillCut(new RectangleF(kx - kr, track.Y + sh / 2 - kr, 2 * kr, 2 * kr), kc, 0, 0, MathF.Round(kr * 0.7f), 0);   // a square knob
        else P.Circle(kx, track.Y + sh / 2, kr, kc);
        uint fg = enabled ? Theme.Text : Theme.Faint;
        if (hint == null)
            P.Text(label, new RectangleF(r.X + 10, r.Y, r.Width - sw - 30, r.Height), fg, 15);
        else
        {
            P.Text(label, new RectangleF(r.X + 10, r.Y + 2, r.Width - sw - 30, r.Height / 2), fg, 15, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Far);
            P.Text(hint, new RectangleF(r.X + 10, r.Y + r.Height / 2 + 1, r.Width - sw - 30, r.Height / 2 - 2), Theme.Dim, 12, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Near);
        }
        return click;
    }

    public bool Slider(string id, RectangleF r, string label, ref float value, float min, float max, Func<float, string> fmt, bool enabled = true)
    {
        Behave(id, r, out bool hover, out bool held);
        if (held) DragHeld = true;
        var track = new RectangleF(r.X + 10, r.Bottom - 18, r.Width - 20, 6);
        bool changed = false;
        if (enabled && (held || (_active == id && Released)))
        {
            float t = Math.Clamp((Mouse.X - track.X) / track.Width, 0, 1);
            float nv = min + t * (max - min);
            if (Math.Abs(nv - value) > 1e-5f) { value = nv; changed = true; }
        }
        if (enabled && hover && Wheel != 0 && !held)
        {
            value = Math.Clamp(value + Math.Sign(Wheel) * (max - min) / 50f, min, max);
            changed = true;
        }
        if (changed) Changed = true;
        if (CyberHud_) { HudSlider(id, r, label, value, min, max, fmt, enabled, hover, held); return changed; }     // Cyber: segments (UiHud.cs)
        if (Theme.Classic)
        {
            // a trackbar: label and value above, a sunken groove, a raised thumb
            ClassicText(label, new RectangleF(r.X + 10, r.Y, r.Width / 2, r.Height - 22), enabled, 15);
            P.Text(fmt(value), new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2 - 10, r.Height - 22), enabled ? Theme.Dim : Theme.Faint, 14,
                FontWeight.Normal, TextAlignment.Trailing);
            var groove = new RectangleF(track.X, track.Y - 1, track.Width, 8);
            P.Fill(groove, Theme.W95Window);
            P.Bevel(groove, Painter.Edge.Sunken);
            float tx = track.X + track.Width * Math.Clamp((value - min) / (max - min), 0, 1);
            var thumb = new RectangleF(tx - 6, track.Y + 3 - 11, 12, 22);
            P.Fill(thumb, Theme.W95Face);
            P.Bevel(thumb, Painter.Edge.Raised);
            return changed;
        }
        float h = Anim(K(id, "#h"), held ? 2 : hover && enabled ? 1 : 0, 18);
        uint fg = enabled ? Theme.Text : Theme.Faint;
        P.Text(label, new RectangleF(r.X + 10, r.Y, r.Width / 2, r.Height - 22), fg, 15);
        P.Text(fmt(value), new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2 - 10, r.Height - 22), enabled ? Theme.Dim : Theme.Faint, 14, FontWeight.Normal, TextAlignment.Trailing);
        float t2 = Math.Clamp((value - min) / (max - min), 0, 1);
        P.Fill(track, Theme.Panel3, 3);
        P.Fill(new RectangleF(track.X, track.Y, track.Width * t2, track.Height), enabled ? Theme.Accent : Theme.Faint, 3);
        float kx = track.X + track.Width * t2;
        if (Theme.Cyber)
        {
            // a bar across the track, its corner cut
            if (h > 0.01f && enabled) P.Fill(new RectangleF(kx - 9 - 3 * h, track.Y - 11 - 3 * h, 18 + 6 * h, 28 + 6 * h), Theme.WithAlpha(Theme.Accent, 0.18f));
            P.FillCut(new RectangleF(kx - 5 - h / 2, track.Y - 8 - h, 10 + h, 22 + 2 * h), enabled ? Theme.Text : Theme.Faint, 0, 0, 4, 0);
            return changed;
        }
        if (h > 0.01f && enabled) P.Circle(kx, track.Y + 3, 8 + 7 * h, Theme.WithAlpha(Theme.Accent, 0.18f));
        P.Circle(kx, track.Y + 3, 8 + h, enabled ? Theme.Text : Theme.Faint);
        return changed;
    }

    static readonly string[] Seg = { "/0", "/1", "/2", "/3", "/4", "/5", "/6", "/7", "/8", "/9" };

    public bool Segmented(string id, RectangleF r, string[] options, ref int index, bool enabled = true)
    {
        if (CyberHud_) return HudSegmented(id, r, options, ref index, enabled);            // Cyber: a tab strip (UiHud.cs)
        bool changed = false;
        if (Theme.Classic)
        {
            // a row of push buttons; the chosen one stays pushed in
            float cw = r.Width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var cell = new RectangleF(r.X + i * cw, r.Y, cw - (i < options.Length - 1 ? 2 : 0), r.Height);
                string cid = K(id, Seg[Math.Min(i, Seg.Length - 1)]);
                bool click = Behave(cid, cell, out _, out bool held) && enabled;
                if (click && index != i) { index = i; changed = true; Changed = true; }
                bool on = index == i || (held && enabled);
                P.Fill(cell, index == i ? Theme.W95Light : Theme.W95Face);
                P.Bevel(cell, on ? Painter.Edge.Pressed : Painter.Edge.Raised);
                float o = on ? P.Px : 0;
                ClassicText(options[i], new RectangleF(cell.X + o, cell.Y + o, cell.Width, cell.Height), enabled, 14,
                    index == i ? FontWeight.Bold : FontWeight.Normal, ParagraphAlignment.Center, TextAlignment.Center);
            }
            return changed;
        }
        P.Fill(r, Theme.Panel, 9);
        P.Stroke(r, Theme.Border, 1, 9);
        float w = r.Width / options.Length;
        // the highlight slides between segments
        float pos = Anim(K(id, "#sel"), index, 14);
        var hl = new RectangleF(r.X + pos * w + 3, r.Y + 3, w - 6, r.Height - 6);
        for (int i = 0; i < options.Length; i++)
        {
            var cell = new RectangleF(r.X + i * w + 3, r.Y + 3, w - 6, r.Height - 6);
            string cid = K(id, Seg[Math.Min(i, Seg.Length - 1)]);
            bool click = Behave(cid, cell, out bool hover, out _) && enabled;
            if (click && index != i) { index = i; changed = true; Changed = true; }
            float hv = Anim(K(cid, "#h"), hover && enabled && index != i ? 1 : 0, 18);
            if (hv > 0) P.Fill(cell, Theme.WithAlpha(0x18FFFFFF, hv), 7);
        }
        P.Fill(hl, enabled ? Theme.Accent : Theme.Panel3, 7);
        for (int i = 0; i < options.Length; i++)
        {
            var cell = new RectangleF(r.X + i * w + 3, r.Y + 3, w - 6, r.Height - 6);
            float onSel = Math.Clamp(1 - MathF.Abs(pos - i), 0, 1);
            uint fg = enabled ? Theme.Mix(Theme.Text, Theme.OnAccent, onSel) : Theme.Faint;
            P.Text(options[i], cell, fg, 14, onSel > 0.5f ? FontWeight.SemiBold : FontWeight.Normal, TextAlignment.Center);
        }
        return changed;
    }

    // Selectable card, returns true when clicked.
    public bool Card(string id, RectangleF r, bool selected, out bool hover)
    {
        bool click = Behave(id, r, out hover, out _);
        if (CyberHud_) { HudCard(id, r, selected, hover); return click; }                 // Cyber: an edged plate (UiHud.cs)
        if (Theme.Classic)
        {
            P.Fill(r, selected ? Theme.W95Light : Theme.W95Face);
            P.Bevel(r, selected ? Painter.Edge.Pressed : Painter.Edge.Raised);
            return click;
        }
        float h = Anim(K(id, "#h"), hover ? 1 : 0, 18);
        float s = Anim(K(id, "#s"), selected ? 1 : 0, 16);          // the selection eases in / out (the fill, the outline)
        P.Fill(r, Theme.Mix(Theme.Mix(Theme.Panel2, 0xFFFFFFFF, 0.05f * h), Theme.Panel3, s), 12);
        P.Stroke(r, Theme.Mix(Theme.Border, Theme.Accent, s), 1 + s, 12);
        return click;
    }

    // Round colour chip; returns true when clicked.
    public bool Swatch(string id, float cx, float cy, float r, uint color, bool selected, string inner = null)
    {
        var box = new RectangleF(cx - r - 3, cy - r - 3, 2 * r + 6, 2 * r + 6);
        bool click = Behave(id, box, out bool hover, out _);
        if (CyberHud_)
        {
            HudSwatch(box, cx, cy, r, color, selected, hover, inner, id);                 // Cyber: a cut square (UiHud.cs)
            if (click) Changed = true;
            return click;
        }
        if (Theme.Classic)
        {
            // a colour well: a sunken square; the chosen one gets a black frame
            var well = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            if (selected) P.Stroke(RectangleF.Inflate(well, 3, 3), Theme.W95Dark, 2);
            P.Fill(well, color);
            P.Bevel(well, Painter.Edge.Sunken);
            if (inner != null) P.Text(inner, well, Theme.Luma(color) > 0.55f ? 0xFF000000 : 0xFFFFFFFF, r * 0.9f, FontWeight.Bold, TextAlignment.Center);
            if (click) Changed = true;
            return click;
        }
        float s = Anim(K(id, "#s"), selected ? 1 : 0, 14);
        float h = Anim(K(id, "#h"), hover ? 1 : 0, 18);
        if (s > 0.01f) P.Ring(cx, cy, r + 1 + 3 * s, Theme.WithAlpha(Theme.Text, s), 2.5f);
        else if (h > 0.01f) P.Ring(cx, cy, r + 3, Theme.WithAlpha(Theme.Dim, h), 1.5f);
        float rr = r * (1 + 0.08f * h);
        P.Circle(cx, cy, rr, color);
        P.Ring(cx, cy, rr, 0x30FFFFFF, 1);
        if (inner != null) P.Text(inner, new RectangleF(cx - r, cy - r, 2 * r, 2 * r), Theme.Luma(color) > 0.55f ? 0xFF0B1220 : 0xFFFFFFFF, r * 0.9f, FontWeight.SemiBold, TextAlignment.Center);
        if (click) Changed = true;
        return click;
    }

    public void Label(RectangleF r, string text, uint color = Theme.Dim, float size = 13, FontWeight w = FontWeight.Normal, bool wrap = false)
    {
        if (CyberHud_) { HudLabel(r, text, color, size, w, wrap); return; }              // Cyber: monospace captions (UiHud.cs)
        P.Text(text, r, color, size, w, TextAlignment.Leading, wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center, null, wrap);
    }

    public void Section(RectangleF r, string title)
    {
        if (Theme.Cyber) { CyberSection(r, title); return; }    // a yellow tag (it slides in: UiCyber.cs)
        if (!Theme.Classic) { P.Text(title.ToUpperInvariant(), r, Theme.Dim, 12, FontWeight.SemiBold); return; }
        // a group box title: bold black, an etched line running on after it
        title = Lang.T(title);
        string t = char.ToUpperInvariant(title[0]) + title[1..].ToLowerInvariant();
        P.Text(t, r, Theme.W95Dark, 13, FontWeight.Bold);
        float tw = P.MeasureWidth(t, 13, FontWeight.Bold);
        if (r.X + tw + 8 < r.Right) P.EtchedLine(r.X + tw + 8, r.Right, r.Y + r.Height / 2);
    }

    // ---- 95 skin helpers ----

    // black text, or etched grey when disabled
    void ClassicText(string s, RectangleF r, bool enabled, float size, FontWeight w = FontWeight.Normal,
        ParagraphAlignment v = ParagraphAlignment.Center, TextAlignment h = TextAlignment.Leading)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (enabled) P.Text(s, r, Theme.W95Dark, size, w, h, v);
        else P.DisabledText(s, r, size, w, h, v);
    }

    // a button's icon and / or label, centred (black; etched grey when disabled)
    void ClassicContent(RectangleF r, string label, string icon, bool enabled, float fontSize)
    {
        uint fg = enabled ? Theme.W95Dark : Theme.W95Shadow;
        if (icon != null && !string.IsNullOrEmpty(label))
        {
            float tw = P.MeasureWidth(label, fontSize);
            float total = fontSize + 8 + tw;
            float x = r.X + (r.Width - total) / 2;
            if (!enabled) P.Icon(icon, new RectangleF(x + P.Px, r.Y + P.Px, fontSize, r.Height), Theme.W95Hi, fontSize);
            P.Icon(icon, new RectangleF(x, r.Y, fontSize, r.Height), fg, fontSize);
            ClassicText(label, new RectangleF(x + fontSize + 8, r.Y, tw + 6, r.Height), enabled, fontSize);
        }
        else if (icon != null)
        {
            if (!enabled) P.Icon(icon, new RectangleF(r.X + P.Px, r.Y + P.Px, r.Width, r.Height), Theme.W95Hi, fontSize + 3);
            P.Icon(icon, r, fg, fontSize + 3);
        }
        else
            ClassicText(label, r, enabled, fontSize, FontWeight.Normal, ParagraphAlignment.Center, TextAlignment.Center);
    }
}
