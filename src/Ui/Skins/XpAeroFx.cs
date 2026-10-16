using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Orbit;

// The motion of the Windows XP and Windows 7 styles. What their skins animate (a hover fading in, a combo box's list sliding
// open, a window zooming, a default button throbbing...) is kept here per pixel canvas - every panel draws its own, and
// Painter.BeginPixel says which (RetroSkin.BeginCanvas) - and per element (its rectangle and what it is), timed by the clock,
// never by the frames. While something moves, the canvas asks for its next frame (RetroSkin.NextFrame: the panel that drew it
// reads it right after drawing); a loop asks at its own low rate, and only while it is drawn. Nothing moves = no frames asked.
// Look > Motion: Off = everything at once; Reduced = the short transitions (Motion.Any); Full = the loops too (Motion.Full).
// No allocations per frame: the state is a dictionary of structs, found again by a struct key.
static class XpAeroFx
{
    // what an element is (part of its key: two things on one rectangle keep apart)
    public const int Hot = 1, Drop = 2, Scroll = 3, Window = 4, Link = 5, Task = 6, Cell = 7, Glow = 8, Check = 9, Tab = 10, Thumb = 11,
        Caption = 12, Swatch = 13, Mine = 14, Dialog = 15;

    sealed class Canvas { public object Target; public int Index, Frame; public double Due = double.PositiveInfinity; }

    readonly record struct Key(int Canvas, int Kind, int X, int Y, int W, int H);

    struct Entry
    {
        public float From, To;      // a fade / an eased value: from, to, since Start
        public double Start, Born, Seen;
        public int Frame;           // the canvas frame it was last drawn in
    }

    static readonly List<Canvas> _canvases = new();
    static readonly Dictionary<Key, Entry> _e = new();
    static Canvas _cur;
    static int _made, _begins;

    // the time of the frame being drawn (one time for the whole frame)
    public static double Now;
    // --animstrip: frames at chosen moments instead of the clock
    public static double? Fixed;
    // the snapshot tools draw the settled state (hovers on or off, nothing half way, loops at rest)
    public static readonly bool Settled = Array.Exists(Environment.GetCommandLineArgs(),
        a => a is "--snapshot" or "--faceshot" or "--clockshot" or "--selftest" or "--scene");

    // transitions may run (Look > Motion: Reduced or Full), loops too (Full)
    public static bool Moves => Motion.Any && !Settled;
    public static bool Loops => Motion.Full && !Settled;

    // a pixel canvas starts drawing
    public static void Begin(object target)
    {
        Now = Fixed ?? Clock.Now;
        Canvas c = null;
        foreach (var k in _canvases) if (ReferenceEquals(k.Target, target)) { c = k; break; }
        if (c == null)
        {
            if (_canvases.Count >= 24) _canvases.RemoveAt(0);       // canvases made again (a resized theater bar): the old ones go
            c = new Canvas { Target = target, Index = ++_made };
            _canvases.Add(c);
        }
        c.Frame++;
        c.Due = double.PositiveInfinity;
        _cur = c;
        if (++_begins % 600 == 0)
            foreach (var (k, v) in _e) if (Now - v.Seen > 20) _e.Remove(k);       // elements gone for a while
    }

    // when the canvas drawn last wants its next frame
    public static double NextFrame => _cur?.Due ?? double.PositiveInfinity;

    // a frame at 'at' (now: as soon as possible)
    public static void Want(double at) { if (_cur != null && at < _cur.Due) _cur.Due = at; }

    // a loop's next frame at its rate (fps), on whole ticks of it
    public static void WantTick(float fps) => Want((Math.Floor(Now * fps) + 1) / fps);

    static ref Entry Get(int kind, RectangleF r, out bool fresh)
    {
        var key = new Key(_cur.Index, kind, (int)MathF.Round(r.X), (int)MathF.Round(r.Y), (int)MathF.Round(r.Width), (int)MathF.Round(r.Height));
        ref var e = ref CollectionsMarshal.GetValueRefOrAddDefault(_e, key, out bool exists);
        // new, or not drawn in this canvas's last frame (it went away and came back): it starts over
        fresh = !exists || e.Frame < _cur.Frame - 1;
        e.Frame = _cur.Frame;
        e.Seen = Now;
        if (fresh) e.Born = Now;
        return ref e;
    }

    static float Value(in Entry e, float inSec, float outSec)
    {
        float dur = (e.To > e.From ? inSec : outSec) * MathF.Abs(e.To - e.From);
        if (dur <= 0) return e.To;
        float k = (float)((Now - e.Start) / dur);
        return k >= 1 ? e.To : e.From + (e.To - e.From) * MathF.Max(0, k);
    }

    // 0..1: an element's state fading in (on, over inSec) or out (over outSec), linear in time. Something that just appeared
    // starts as it is (a page opened under the pointer does not fade its button in).
    public static float Fade(int kind, RectangleF r, bool on, float inSec, float outSec)
    {
        float target = on ? 1 : 0;
        if (_cur == null || !Moves) return target;
        ref var e = ref Get(kind, r, out bool fresh);
        if (fresh) { e.From = e.To = target; e.Start = Now - 100; return target; }
        if (e.To != target)
        {
            e.From = Value(e, inSec, outSec);
            e.To = target;
            e.Start = Now;
        }
        float v = Value(e, inSec, outSec);
        if (v != e.To) Want(Now);
        return v;
    }

    // seconds since an element appeared (or came back after a frame without it); always drawn = it keeps counting
    public static float Age(int kind, RectangleF r)
    {
        if (_cur == null || !Moves) return 1e6f;
        ref var e = ref Get(kind, r, out _);
        return (float)(Now - e.Born);
    }

    // 0..1 of an element's first 'sec' seconds (an opening), eased out; asks for frames until it is done
    public static float Opening(int kind, RectangleF r, float sec)
    {
        float t = Age(kind, r) / sec;
        if (t >= 1) return 1;
        Want(Now);
        return 1 - (1 - t) * (1 - t) * (1 - t);
    }

    // a value easing to its target over sec (ease-out cubic): smooth scrolling. A new element is at its target.
    public static float Ease(int kind, RectangleF r, float target, float sec)
    {
        if (_cur == null || !Moves) return target;
        ref var e = ref Get(kind, r, out bool fresh);
        if (fresh) { e.From = e.To = target; e.Start = Now - 100; return target; }
        if (e.To != target)
        {
            e.From = Eased(e, sec);
            e.To = target;
            e.Start = Now;
        }
        float v = Eased(e, sec);
        if (v != e.To) Want(Now);
        return v;
    }

    static float Eased(in Entry e, float sec)
    {
        float k = (float)((Now - e.Start) / sec);
        if (k >= 1) return e.To;
        k = MathF.Max(0, k);
        return e.From + (e.To - e.From) * (1 - (1 - k) * (1 - k) * (1 - k));
    }

    // a fade level in steps (a colour mixed by it makes one of a few gradient brushes, not a new one every frame)
    public static float Step(float v, int steps = 8) => v <= 0 ? 0 : v >= 1 ? 1 : MathF.Max(1, MathF.Round(v * steps)) / steps;
}
