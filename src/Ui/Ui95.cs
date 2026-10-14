using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The Windows 95 controls, at their real 1995 metrics: 8 pt MS Sans Serif (11 px), 75 x 23 push buttons,
// 13 x 13 check boxes, 12 x 12 option buttons, etched group boxes, trackbars with a pointed thumb,
// block progress bars, property sheet tabs, list boxes with scroll bars, navy captions. Coordinates are
// pixels of the small 95 canvas (Painter.BeginPixel); the canvas is enlarged afterwards with whole pixels.
public sealed partial class Ui
{
    public const float F95 = 11;                       // 8 pt
    const uint Blk = 0xFF000000, Wh = 0xFFFFFFFF, Face = 0xFFC0C0C0, Sh = 0xFF808080, Lt = 0xFFDFDFDF, Navy = 0xFF000080;

    static readonly string[] CheckGlyph =
    {
        "......k", ".....kk", "k...kkk", "kk.kkk.", "kkkkk..", ".kkk...", "..k....",
    };
    static readonly string[] CloseGlyph =
    {
        "kk....kk", ".kk..kk.", "..kkkk..", "...kk...", "..kkkk..", ".kk..kk.", "kk....kk",
    };
    static readonly string[] HelpGlyph = { ".kkkk.", "kk..kk", "...kk.", "..kk..", "..kk..", "......", "..kk..", };
    static readonly string[] MinGlyph = { "kkkkkk", "kkkkkk" };
    static readonly string[] MaxGlyph =
    {
        "kkkkkkkkk", "kkkkkkkkk", "k.......k", "k.......k", "k.......k", "k.......k", "k.......k", "kkkkkkkkk",
    };
    static readonly string[] UpGlyph = { "...k...", "..kkk..", ".kkkkk.", "kkkkkkk" };
    static readonly string[] DownGlyph = { "kkkkkkk", ".kkkkk.", "..kkk..", "...k..." };

    // ---- text ----

    public void Text95(string s, RectangleF r, uint c = Blk, bool bold = false, TextAlignment al = TextAlignment.Leading,
        bool enabled = true, bool wrap = false, float size = F95)
    {
        if (string.IsNullOrEmpty(s)) return;
        var w = bold ? FontWeight.Bold : FontWeight.Normal;
        if (!enabled) { P.DisabledText(s, r, size, w, al, wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center); return; }
        P.Text(s, r, c, size, w, al, wrap ? ParagraphAlignment.Near : ParagraphAlignment.Center, Theme.ClassicFont, wrap);
    }

    public float Width95(string s, bool bold = false, float size = F95) =>
        P.MeasureWidth(s, size, bold ? FontWeight.Bold : FontWeight.Normal, Theme.ClassicFont);

    // ---- access keys: "&Size:" shows "Size:" with the S underlined (Alt+S in 1995); "&&" is a plain & ----

    static readonly Dictionary<string, (string text, int key)> _amp = new();

    static (string text, int key) Amp(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('&') < 0) return (s, -1);
        if (_amp.TryGetValue(s, out var v)) return v;
        var sb = new System.Text.StringBuilder(s.Length);
        int key = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&' && i + 1 < s.Length)
            {
                i++;
                if (s[i] != '&' && key < 0) key = sb.Length;
            }
            sb.Append(s[i]);
        }
        if (_amp.Count > 500) _amp.Clear();
        return _amp[s] = (sb.ToString(), key);
    }

    // data (a window title, a song) can hold a real '&': escape it before it goes into a label
    public static string Esc95(string s) => s?.Replace("&", "&&");

    public float LabelWidth95(string s, bool bold = false) => Width95(Amp(s).text, bold);

    public void Label95(string s, RectangleF r, bool enabled = true, TextAlignment al = TextAlignment.Leading, uint c = Blk,
        bool bold = false)
    {
        var (t, key) = Amp(s);
        if (string.IsNullOrEmpty(t)) return;
        Text95(t, r, c, bold, al, enabled);
        if (key < 0) return;
        float full = Width95(t, bold);
        float x = al == TextAlignment.Center ? r.X + MathF.Floor((r.Width - full) / 2) : al == TextAlignment.Trailing ? r.Right - full : r.X;
        x = MathF.Round(x + Width95(t[..key], bold));
        float uw = Math.Max(1, MathF.Round(Width95(t.Substring(key, 1), bold)) - 1);
        float y = MathF.Floor(r.Y + (r.Height - 13) / 2) + 11;
        if (!enabled) P.Fill(new RectangleF(x + 1, y + 1, uw, 1), Wh);
        P.Fill(new RectangleF(x, y, uw, 1), enabled ? c : Sh);
    }

    // ---- push button ----

    // isDefault: the black outline of a dialog's default button; latched: a toggle that is in
    public bool Button95(string id, RectangleF r, string text, bool enabled = true, bool isDefault = false, bool latched = false,
        W95Icons.Id? icon = null)
    {
        bool click = Behave(id, r, out _, out bool held) && enabled;
        bool down = held && enabled;
        var b = ButtonFrame95(r, isDefault, down, latched);
        float o = down || latched ? 1 : 0;
        if (icon is { } ic)
        {
            int n = W95Icons.Size(ic);
            float tw = string.IsNullOrEmpty(text) ? 0 : LabelWidth95(text) + 4;
            float x = b.X + (b.Width - n - tw) / 2 + o, y = b.Y + (b.Height - n) / 2 + o;
            if (enabled) W95Icons.Draw(P, ic, MathF.Round(x), MathF.Round(y));
            else P.ImageNearest(W95Icons.Get(P, ic), new RectangleF(MathF.Round(x), MathF.Round(y), n, n));
            if (tw > 0) Label95(text, new RectangleF(MathF.Round(x + n + 4), b.Y + o, tw, b.Height), enabled);
        }
        else
            Label95(text, new RectangleF(b.X + o, b.Y + o, b.Width, b.Height), enabled, TextAlignment.Center);
        return click;
    }

    RectangleF ButtonFrame95(RectangleF r, bool isDefault, bool down, bool latched) => P.ButtonFace95(r, isDefault, down, latched);

    // a push button with a little pixel picture (the CD Player's |<<  >  >>|)
    public bool GlyphButton95(string id, RectangleF r, string[] glyph, bool enabled = true)
    {
        bool click = Behave(id, r, out _, out bool held) && enabled;
        bool down = held && enabled;
        var b = ButtonFrame95(r, false, down, false);
        float o = down ? 1 : 0, gw = glyph[0].Length, gh = glyph.Length;
        float gx = b.X + MathF.Floor((b.Width - gw) / 2) + o, gy = b.Y + MathF.Floor((b.Height - gh) / 2) + o;
        if (!enabled) P.Pixels(glyph, gx + 1, gy + 1, Wh);
        P.Pixels(glyph, gx, gy, enabled ? Blk : Sh);
        return click;
    }

    // ---- check box / option button ----

    public bool Check95(string id, float x, float y, string text, ref bool value, bool enabled = true)
    {
        var hit = new RectangleF(x, y - 2, 19 + LabelWidth95(text) + 4, 17);
        bool click = Behave(id, hit, out _, out bool held) && enabled;
        if (click) { value = !value; Changed = true; }
        var box = new RectangleF(x, y, 13, 13);
        P.Fill(box, enabled && !(held && enabled) ? Wh : Face);
        P.Bevel(box, Painter.Edge.Sunken);
        if (value) P.Pixels(CheckGlyph, x + 3, y + 3, enabled ? Blk : Sh);
        Label95(text, new RectangleF(x + 19, y - 1, 400, 15), enabled);
        return click;
    }

    public bool Radio95(string id, float x, float y, string text, bool on, bool enabled = true)
    {
        var hit = new RectangleF(x, y - 2, 18 + LabelWidth95(text) + 4, 16);
        bool click = Behave(id, hit, out _, out _) && enabled;
        if (click && !on) Changed = true;
        W95Icons.Draw(P, on ? W95Icons.Id.RadioOn : W95Icons.Id.RadioOff, x, y);
        Label95(text, new RectangleF(x + 18, y - 2, 400, 15), enabled);
        return click && enabled;
    }

    // a row of option buttons; returns true when the choice changed
    public bool Radios95(string id, float x, float y, string[] options, ref int index, bool enabled = true, float gap = 14)
    {
        bool changed = false;
        float cx = x;
        for (int i = 0; i < options.Length; i++)
        {
            if (Radio95(K(id, Seg[Math.Min(i, Seg.Length - 1)]), cx, y, options[i], index == i, enabled) && index != i)
            {
                index = i;
                changed = true;
            }
            cx += 18 + LabelWidth95(options[i]) + gap;
        }
        return changed;
    }

    // ---- group box, etched line ----

    public void Group95(RectangleF r, string title)
    {
        float y = r.Y + 6;
        P.Stroke(new RectangleF(r.X + 0.5f, y + 0.5f, r.Width - 2, r.Bottom - y - 2), Sh, 1);
        P.Stroke(new RectangleF(r.X + 1.5f, y + 1.5f, r.Width - 2, r.Bottom - y - 2), Wh, 1);
        if (string.IsNullOrEmpty(title)) return;
        float tw = LabelWidth95(title);
        P.Fill(new RectangleF(r.X + 6, r.Y, tw + 4, 13), Face);
        Label95(title, new RectangleF(r.X + 8, r.Y, tw + 2, 13));
    }

    // ---- trackbar ----

    public bool Track95(string id, RectangleF r, ref float value, float min, float max, bool enabled = true, int ticks = 11)
    {
        Behave(id, r, out bool hover, out bool held);
        var ch = new RectangleF(r.X + 6, r.Y + 8, r.Width - 12, 4);
        bool changed = false;
        if (enabled && (held || (_active == id && Released)))
        {
            float t = Math.Clamp((Mouse.X - ch.X) / ch.Width, 0, 1);
            float nv = min + t * (max - min);
            if (Math.Abs(nv - value) > 1e-5f) { value = nv; changed = true; }
        }
        if (enabled && hover && Wheel != 0 && !held)
        {
            value = Math.Clamp(value + Math.Sign(Wheel) * (max - min) / 50f, min, max);
            changed = true;
        }
        if (changed) Changed = true;
        // the channel: a 4 pixel groove
        P.Fill(ch, Wh);
        P.Bevel(ch, Painter.Edge.Sunken);
        // tick marks under it
        for (int i = 0; i < ticks; i++)
        {
            float tx = MathF.Round(ch.X + (ch.Width - 1) * i / (ticks - 1));
            P.Fill(new RectangleF(tx, r.Y + 24, 1, i == 0 || i == ticks - 1 ? 4 : 3), Blk);
        }
        // the thumb: 11 wide, square top, pointing down
        float t2 = Math.Clamp((value - min) / (max - min), 0, 1);
        float x = MathF.Round(ch.X + ch.Width * t2) - 5, y = r.Y;
        P.Fill(new RectangleF(x, y, 11, 15), Face);
        for (int i = 0; i < 5; i++) P.Fill(new RectangleF(x + i + 1, y + 15 + i, 9 - 2 * i, 1), Face);
        P.Fill(new RectangleF(x, y, 10, 1), Wh);                 // top + left light edge
        P.Fill(new RectangleF(x, y, 1, 15), Wh);
        P.Fill(new RectangleF(x + 10, y, 1, 15), Blk);             // right dark edge
        P.Fill(new RectangleF(x + 9, y + 1, 1, 14), Sh);
        for (int i = 0; i < 5; i++)
        {
            P.Fill(new RectangleF(x + i, y + 15 + i, 1, 1), Wh);                 // the point: light on the left
            P.Fill(new RectangleF(x + 10 - i, y + 15 + i, 1, 1), Blk);            // dark on the right
            if (i < 4) P.Fill(new RectangleF(x + 9 - i, y + 15 + i, 1, 1), Sh);
        }
        P.Fill(new RectangleF(x + 5, y + 20, 1, 1), Blk);
        if (!enabled) P.Fill(new RectangleF(x + 3, y + 3, 5, 9), Face);
        return changed;
    }

    // ---- progress bar (blocks) ----

    public void Progress95(RectangleF r, float frac, uint block = Navy)
    {
        P.Fill(r, Face);
        P.Fill(new RectangleF(r.X, r.Y, r.Width, 1), Sh);
        P.Fill(new RectangleF(r.X, r.Y, 1, r.Height), Sh);
        P.Fill(new RectangleF(r.X, r.Bottom - 1, r.Width, 1), Wh);
        P.Fill(new RectangleF(r.Right - 1, r.Y, 1, r.Height), Wh);
        var inner = RectangleF.Inflate(r, -2, -2);
        float bw = Math.Max(4, MathF.Round(inner.Height * 0.66f));
        int n = (int)((inner.Width + 2) / (bw + 2));
        int on = (int)MathF.Round(Math.Clamp(frac, 0, 1) * n);
        for (int i = 0; i < on; i++) P.Fill(new RectangleF(inner.X + i * (bw + 2), inner.Y, bw, inner.Height), block);
    }

    // ---- property sheet tabs + page ----

    public bool Tabs95(string id, float x, float y, string[] tabs, ref int sel)
    {
        bool changed = false;
        float cx = x;
        var rects = new RectangleF[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            float w = LabelWidth95(tabs[i]) + 14;
            rects[i] = new RectangleF(cx, y, w, 18);
            if (Behave(K(id, Seg[Math.Min(i, Seg.Length - 1)]), rects[i], out _, out _) && sel != i) { sel = i; changed = true; }
            cx += w;
        }
        for (int i = 0; i < tabs.Length; i++)
            if (i != sel) TabShape(rects[i], tabs[i], false);
        var s = rects[sel];
        TabShape(new RectangleF(s.X - 2, s.Y - 2, s.Width + 4, s.Height + 4), tabs[sel], true);
        return changed;
    }

    void TabShape(RectangleF r, string text, bool selected)
    {
        P.Fill(new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 1), Face);
        P.Fill(new RectangleF(r.X, r.Y + 2, 1, r.Height - 2), Wh);              // left
        P.Fill(new RectangleF(r.X + 1, r.Y + 1, 1, 1), Wh);                      // the chamfered corner
        P.Fill(new RectangleF(r.X + 2, r.Y, r.Width - 4, 1), Wh);               // top
        P.Fill(new RectangleF(r.Right - 2, r.Y + 1, 1, 1), Blk);
        float rh = r.Height - 2 - (selected ? 2 : 0);
        P.Fill(new RectangleF(r.Right - 1, r.Y + 2, 1, rh), Blk);       // right
        P.Fill(new RectangleF(r.Right - 2, r.Y + 2, 1, rh), Sh);
        Label95(text, new RectangleF(r.X, r.Y + (selected ? 1 : 2), r.Width, 14), true, TextAlignment.Center);
    }

    // ---- list box with a vertical scroll bar ----

    readonly Dictionary<string, int> _scroll95 = new();

    // returns the item clicked this frame (or -1); draw(index, row rect, selected) paints a row
    public int List95(string id, RectangleF r, int count, int selected, Action<int, RectangleF, bool> draw, float rowH = 16)
    {
        P.Fill(r, Wh);
        P.Bevel(r, Painter.Edge.Sunken);
        var inner = RectangleF.Inflate(r, -2, -2);
        int visible = Math.Max(1, (int)(inner.Height / rowH));
        bool bar = count > visible;
        _scroll95.TryGetValue(id, out int top);
        top = Math.Clamp(top, 0, Math.Max(0, count - visible));
        float rowsW = inner.Width - (bar ? 16 : 0);
        int clicked = -1;
        if (bar && Hover(inner) && Wheel != 0) top = Math.Clamp(top - Math.Sign(Wheel) * 3, 0, count - visible);
        for (int i = 0; i < visible && top + i < count; i++)
        {
            int idx = top + i;
            var row = new RectangleF(inner.X, inner.Y + i * rowH, rowsW, rowH);
            if (Behave(K(id, "/" + i), row, out _, out _)) clicked = idx;
            if (idx == selected) P.Fill(row, Navy);
            draw(idx, row, idx == selected);
        }
        if (bar)
        {
            var sb = new RectangleF(inner.Right - 16, inner.Y, 16, inner.Height);
            P.FillChecker(sb);
            var up = new RectangleF(sb.X, sb.Y, 16, 16);
            var dn = new RectangleF(sb.X, sb.Bottom - 16, 16, 16);
            if (ArrowButton(K(id, "#up"), up, UpGlyph)) top = Math.Max(0, top - 1);
            if (ArrowButton(K(id, "#dn"), dn, DownGlyph)) top = Math.Min(count - visible, top + 1);
            float track = sb.Height - 32;
            float th = Math.Max(8, track * visible / count);
            float ty = sb.Y + 16 + (track - th) * top / Math.Max(1, count - visible);
            var thumb = new RectangleF(sb.X, MathF.Round(ty), 16, MathF.Round(th));
            P.Fill(thumb, Face);
            P.Bevel(thumb, Painter.Edge.Raised);
        }
        _scroll95[id] = top;
        return clicked;
    }

    bool ArrowButton(string id, RectangleF r, string[] glyph)
    {
        bool click = Behave(id, r, out _, out bool held);
        P.Fill(r, Face);
        if (held) P.Stroke(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Sh, 1);
        else P.Bevel(r, Painter.Edge.Raised);
        float o = held ? 1 : 0;
        P.Pixels(glyph, r.X + 4 + o, r.Y + 6 + o, Blk);
        return click;
    }

    // ---- window parts ----

    public void Caption95(RectangleF r, string title, W95Icons.Id? icon = null, bool active = true)
    {
        P.Fill(r, active ? Navy : Sh);
        float tx = r.X + 3;
        if (icon is { } ic) { W95Icons.Draw(P, ic, r.X + 2, r.Y + (r.Height - 16) / 2); tx = r.X + 21; }
        Text95(title, new RectangleF(tx, r.Y, r.Width - (tx - r.X) - 40, r.Height), active ? Wh : Face, true);
    }

    // the boxes in a caption (16 x 14): close, help, minimise, maximise
    bool CaptionButton95(string id, float x, float y, string[] glyph, float gx, float gy, bool enabled)
    {
        var r = new RectangleF(x, y, 16, 14);
        bool click = Behave(id, r, out _, out bool held) && enabled;
        bool down = held && enabled;
        P.Fill(r, Face);
        P.Bevel(r, down ? Painter.Edge.Pressed : Painter.Edge.Raised);
        float o = down ? 1 : 0;
        if (!enabled) P.Pixels(glyph, x + gx + 1, y + gy + 1, Wh);
        P.Pixels(glyph, x + gx + o, y + gy + o, enabled ? Blk : Sh);
        return click;
    }

    public bool CaptionClose95(string id, float x, float y, bool enabled = true) => CaptionButton95(id, x, y, CloseGlyph, 4, 3, enabled);
    public bool CaptionHelp95(string id, float x, float y, bool enabled = true) => CaptionButton95(id, x, y, HelpGlyph, 5, 3, enabled);
    public bool CaptionMin95(string id, float x, float y, bool enabled = true) => CaptionButton95(id, x, y, MinGlyph, 4, 9, enabled);
    public bool CaptionMax95(string id, float x, float y, bool enabled = true) => CaptionButton95(id, x, y, MaxGlyph, 3, 2, enabled);

    // ---- text box (read-only here: Orbit types with its own keyboard, not into its settings) ----

    public void Edit95(RectangleF r, string text, bool enabled = true)
    {
        P.Fill(r, enabled ? Wh : Face);
        P.Bevel(r, Painter.Edge.Sunken);
        Text95(text, new RectangleF(r.X + 4, r.Y + 2, r.Width - 6, r.Height - 4), enabled ? Blk : Sh);
    }

    // ---- drop-down list: the field, the arrow button, and the list that opens under it ----

    string _drop;                     // the open one
    bool _dropSeen, _inPopup;         // drawn this frame (it closes when its tab goes away); drawing the list
    RectangleF _dropField, _dropList;
    string[] _dropItems;
    int _dropHot, _dropPick = -1;
    public SizeF Canvas95 = new(100000, 100000);   // so a list near the bottom opens upwards

    void BeginPopups95()
    {
        if (!Theme.Classic) { _drop = null; _dropPick = -1; _dropSeen = false; return; }   // Modern never has one
        _dropSeen = false;
        if (_drop != null && Pressed && !_dropList.Contains(Mouse.X, Mouse.Y) && !_dropField.Contains(Mouse.X, Mouse.Y))
        {
            _drop = null;             // a click elsewhere only closes the list, like 1995
            _dropPick = -1;
            Pressed = false;
        }
    }

    public bool Combo95(string id, RectangleF r, string[] items, ref int index, bool enabled = true)
    {
        bool changed = false;
        if (_drop == id)
        {
            if (!enabled) _drop = null;
            else
            {
                _dropSeen = true;
                _dropField = r;
                if (_dropPick >= 0)
                {
                    if (_dropPick != index && _dropPick < items.Length) { index = _dropPick; changed = Changed = true; }
                    _drop = null;
                    _dropPick = -1;
                }
            }
        }
        bool click = Behave(id, r, out _, out bool held) && enabled;
        if (click)
        {
            if (_drop == id) _drop = null;
            else
            {
                _drop = id; _dropSeen = true; _dropField = r; _dropItems = items; _dropHot = index; _dropPick = -1;
                float h = items.Length * 13 + 2;
                float y = r.Bottom + h <= Canvas95.Height ? r.Bottom : r.Y - h;
                _dropList = new RectangleF(r.X, y, r.Width, h);
            }
        }
        bool open = _drop == id, down = held && enabled;
        P.Fill(r, enabled ? Wh : Face);
        P.Bevel(r, Painter.Edge.Sunken);
        var b = new RectangleF(r.Right - 18, r.Y + 2, 16, r.Height - 4);
        P.Fill(b, Face);
        if (down) P.Stroke(new RectangleF(b.X + 0.5f, b.Y + 0.5f, b.Width - 1, b.Height - 1), Sh, 1);
        else P.Bevel(b, Painter.Edge.Raised);
        float o = down ? 1 : 0, gy = MathF.Floor(b.Y + (b.Height - 4) / 2);
        if (!enabled) P.Pixels(DownGlyph, b.X + 5, gy + 1, Wh);
        P.Pixels(DownGlyph, b.X + 4 + o, gy + o, enabled ? Blk : Sh);
        string t = index >= 0 && index < items.Length ? items[index] : "";
        var tr = new RectangleF(r.X + 3, r.Y + 3, b.X - r.X - 4, r.Height - 6);
        if (open) P.Fill(tr, Navy);
        Text95(t, new RectangleF(tr.X + 2, tr.Y, tr.Width - 2, tr.Height), open ? Wh : enabled ? Blk : Sh);
        return changed;
    }

    // the open list goes over everything: the page calls this after drawing itself
    public void Popups95()
    {
        if (_drop == null) return;
        if (!_dropSeen) { _drop = null; return; }
        var r = _dropList;
        _inPopup = true;
        for (int i = 0; i < _dropItems.Length; i++)
        {
            var row = new RectangleF(r.X + 1, r.Y + 1 + i * 13, r.Width - 2, 13);
            if (!Hover(row)) continue;
            _dropHot = i;                                   // the bar follows the pointer
            if (Released) { _dropPick = i; Animating = true; }   // taken by the combo in the next frame
        }
        _inPopup = false;
        P.Fill(r, Wh);
        P.Stroke(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Blk, 1);
        for (int i = 0; i < _dropItems.Length; i++)
        {
            var row = new RectangleF(r.X + 1, r.Y + 1 + i * 13, r.Width - 2, 13);
            bool on = i == _dropHot;
            if (on) P.Fill(row, Navy);
            Text95(_dropItems[i], new RectangleF(row.X + 2, row.Y, row.Width - 4, 13), on ? Wh : Blk);
        }
    }

    // status bar panes: thin sunken fields
    public void Pane95(RectangleF r, string text, uint dot = 0)
    {
        P.Fill(new RectangleF(r.X, r.Y, r.Width, 1), Sh);
        P.Fill(new RectangleF(r.X, r.Y, 1, r.Height), Sh);
        P.Fill(new RectangleF(r.X, r.Bottom - 1, r.Width, 1), Wh);
        P.Fill(new RectangleF(r.Right - 1, r.Y, 1, r.Height), Wh);
        float x = r.X + 4;
        if (dot != 0) { P.Fill(new RectangleF(x, r.Y + (r.Height - 6) / 2, 6, 6), dot); x += 10; }
        Text95(text, new RectangleF(x, r.Y, r.Right - x - 2, r.Height));
    }

    // a menu bar's item, its access key underlined
    public void Menu95(float x, float y, string text)
    {
        Text95(text, new RectangleF(x, y, Width95(text) + 2, 15));
        float u = Width95(text[..1]);
        P.Fill(new RectangleF(x, y + 12, u, 1), Blk);
    }
}
