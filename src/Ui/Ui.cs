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
public sealed class Ui
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

    public void Begin(PointerState s, float scale)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        _dt = _lastBegin == 0 ? 1 / 60f : (float)Math.Clamp((now - _lastBegin) / (double)System.Diagnostics.Stopwatch.Frequency, 0.001, 0.1);
        _lastBegin = now;
        Mouse = s.Pos / scale;
        Inside = s.Inside;
        Down = s.Down;
        Pressed = s.PressedLatch;
        Released = s.ReleasedLatch;
        Wheel = s.Wheel;
        s.PressedLatch = s.ReleasedLatch = false;
        s.Wheel = 0;
        Changed = false;
        Animating = false;
        _hot.Clear();
    }

    // The pointer moved: does the picture change? Only when it gets onto another widget (hover
    // glow) or while something is pressed / dragged - otherwise the last frame is still right.
    public bool NeedsRedrawForMove(PointerState s, float scale)
    {
        if (_active != null || s.Down) return true;
        var m = s.Pos / scale;
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

    // Eases a value towards 'target' (exponential, ~1/speed seconds). The first call snaps.
    public float Anim(string id, float target, float speed = 16f)
    {
        if (!_anim.TryGetValue(id, out float v)) v = target;
        v += (target - v) * (1 - MathF.Exp(-speed * _dt));
        if (MathF.Abs(v - target) < 0.003f) v = target;
        else Animating = true;
        _anim[id] = v;
        return v;
    }

    public bool Hover(RectangleF r)
    {
        _hot.Add(r);
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
        bool click = Behave(id, r, out bool hover, out bool held) && enabled;
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
        bool click = Behave(id, r, out bool hover, out _) && enabled;
        if (click) { value = !value; Changed = true; }
        float h = Anim(K(id, "#h"), hover && enabled ? 1 : 0, 18);
        float k = Anim(K(id, "#k"), value ? 1 : 0, 14);
        if (h > 0) P.Fill(r, Theme.WithAlpha(0x0FFFFFFF, h), 8);
        float sw = 46, sh = 26;
        var track = new RectangleF(r.Right - sw - 10, r.Y + (r.Height - sh) / 2, sw, sh);
        P.Fill(track, enabled ? Theme.Mix(Theme.Panel3, Theme.Accent, k) : Theme.Panel3, sh / 2);
        // ease-out-back feel: the knob slightly overshoots on the way
        float kx = track.X + sh / 2 + (sw - sh) * k;
        float kr = sh / 2 - 4 + 1.5f * MathF.Sin(MathF.PI * k);
        P.Circle(kx, track.Y + sh / 2, kr, enabled ? Theme.Mix(Theme.Dim, Theme.OnAccent, k) : Theme.Dim);
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
        float h = Anim(K(id, "#h"), held ? 2 : hover && enabled ? 1 : 0, 18);
        uint fg = enabled ? Theme.Text : Theme.Faint;
        P.Text(label, new RectangleF(r.X + 10, r.Y, r.Width / 2, r.Height - 22), fg, 15);
        P.Text(fmt(value), new RectangleF(r.X + r.Width / 2, r.Y, r.Width / 2 - 10, r.Height - 22), enabled ? Theme.Dim : Theme.Faint, 14, FontWeight.Normal, TextAlignment.Trailing);
        float t2 = Math.Clamp((value - min) / (max - min), 0, 1);
        P.Fill(track, Theme.Panel3, 3);
        P.Fill(new RectangleF(track.X, track.Y, track.Width * t2, track.Height), enabled ? Theme.Accent : Theme.Faint, 3);
        float kx = track.X + track.Width * t2;
        if (h > 0.01f && enabled) P.Circle(kx, track.Y + 3, 8 + 7 * h, Theme.WithAlpha(Theme.Accent, 0.18f));
        P.Circle(kx, track.Y + 3, 8 + h, enabled ? Theme.Text : Theme.Faint);
        return changed;
    }

    static readonly string[] Seg = { "/0", "/1", "/2", "/3", "/4", "/5", "/6", "/7", "/8", "/9" };

    public bool Segmented(string id, RectangleF r, string[] options, ref int index, bool enabled = true)
    {
        bool changed = false;
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
        float h = Anim(K(id, "#h"), hover ? 1 : 0, 18);
        P.Fill(r, selected ? Theme.Panel3 : Theme.Mix(Theme.Panel2, 0xFFFFFFFF, 0.05f * h), 12);
        P.Stroke(r, selected ? Theme.Accent : Theme.Border, selected ? 2 : 1, 12);
        return click;
    }

    // Round colour chip; returns true when clicked.
    public bool Swatch(string id, float cx, float cy, float r, uint color, bool selected, string inner = null)
    {
        var box = new RectangleF(cx - r - 3, cy - r - 3, 2 * r + 6, 2 * r + 6);
        bool click = Behave(id, box, out bool hover, out _);
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

    public void Label(RectangleF r, string text, uint color = Theme.Dim, float size = 13, FontWeight w = FontWeight.Normal, bool wrap = false) =>
        P.Text(text, r, color, size, w, TextAlignment.Leading, wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center, null, wrap);

    public void Section(RectangleF r, string title) =>
        P.Text(title.ToUpperInvariant(), r, Theme.Dim, 12, FontWeight.SemiBold);
}
