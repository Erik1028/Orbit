using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Orbit;

// A bitmap font for a skin whose era font Windows does not have (a Chicago-like system font, a VGA ROM font): every glyph a few
// rows of pixels, one letter per pixel like W95Icons ('.' or ' ' = empty, anything else = ink). A skin hands it out from
// RetroSkin.PixelFont; Painter then draws all pixel-mode text in that family with it (Text, MeasureWidth, MeasureHeight), so
// Text95 / Label95 / the panels' own text need no change. Each glyph is one FillOpacityMask from an atlas, in the brush colour.
//
//     static readonly PixelFont Chicago = new(16, 12, new Dictionary<char, string[]>
//     {
//         ['A'] = new[] { "......", "..kk..", ".k..k.", ... },      // up to 16 rows, as wide as its widest row
//     }) { Spacing = 1 };
public sealed class PixelFont
{
    public int Height { get; }          // the line: every glyph's rows (top-aligned), the distance between wrapped lines
    public int Ascent { get; }          // from the top of the line to the baseline
    public int Spacing { get; init; } = 1;          // empty columns after every glyph
    public int SpaceWidth { get; init; } = -1;      // ' ' when it has no glyph (-1: half the height)
    public bool Smear { get; init; } = true;        // bold: each glyph twice, 1 px apart, and 1 px wider (the Mac / VGA way)
    public char Fallback { get; init; } = '?';

    readonly Dictionary<char, string[]> _glyphs;
    readonly Dictionary<char, (int x, int w)> _at = new();
    int _atlasW;
    readonly Dictionary<Painter, ID2D1Bitmap> _atlas = new();

    public PixelFont(int height, int ascent, IReadOnlyDictionary<char, string[]> glyphs)
    {
        Height = height;
        Ascent = ascent;
        _glyphs = new Dictionary<char, string[]>(glyphs);
        int x = 0;
        foreach (var (ch, rows) in _glyphs)
        {
            int w = 0;
            foreach (var row in rows) w = Math.Max(w, row.Length);
            _at[ch] = (x, w);
            x += w + 1;
        }
        _atlasW = Math.Max(1, x);
    }

    public bool Has(char ch) => _at.ContainsKey(ch);

    // the glyph drawn for ch: itself, the other case, the fallback; none = a space (or a hollow box past the fallback)
    bool Find(char ch, out (int x, int w) g)
    {
        if (_at.TryGetValue(ch, out g)) return true;
        if (char.IsLetter(ch) && (_at.TryGetValue(char.ToUpperInvariant(ch), out g) || _at.TryGetValue(char.ToLowerInvariant(ch), out g))) return true;
        if (ch == ' ') return false;
        return _at.TryGetValue(Fallback, out g);
    }

    int Space => SpaceWidth >= 0 ? SpaceWidth : Math.Max(2, Height / 2);

    public int Advance(char ch, bool bold) =>
        Find(ch, out var g) ? g.w + Spacing + (bold && Smear ? 1 : 0) : ch == ' ' ? Space + Spacing : Math.Max(2, Height / 2) + Spacing;

    public float Measure(string s, bool bold)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0;
        foreach (char ch in s) if (ch != '\n') w += Advance(ch, bold);
        return w;
    }

    public List<string> Wrap(string s, float width, bool bold)
    {
        var lines = new List<string>();
        foreach (var para in s.Split('\n'))
        {
            string line = "";
            foreach (var word in para.Split(' '))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(next, bold) > width) { lines.Add(line); line = word; }
                else line = next;
            }
            lines.Add(line);
        }
        return lines;
    }

    public float MeasureHeight(string s, float width, bool bold) => Wrap(s, width, bold).Count * Height;

    ID2D1Bitmap Atlas(Painter p)
    {
        if (_atlas.TryGetValue(p, out var b)) return b;
        var px = new uint[_atlasW * Height];
        foreach (var (ch, rows) in _glyphs)
        {
            var (x0, _) = _at[ch];
            for (int y = 0; y < rows.Length && y < Height; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] != '.' && rows[y][x] != ' ') px[y * _atlasW + x0 + x] = 0xFFFFFFFF;
        }
        b = p.CreateBitmap(px, _atlasW, Height);
        _atlas[p] = b;
        return b;
    }

    // one line, in a list kept for it (a list was made for every text drawn)
    readonly List<string> _one = new(1);
    List<string> One(string line)
    {
        _one.Clear();
        _one.Add(line);
        return _one;
    }

    // s in r like Painter.Text lays it out (whole pixels; cut to r)
    public void Draw(Painter p, string s, RectangleF r, uint c, bool bold, TextAlignment h, ParagraphAlignment v, bool wrap)
    {
        if (string.IsNullOrEmpty(s)) return;
        var lines = wrap ? Wrap(s, r.Width, bold) : One(s.Replace("\n", " "));
        float total = lines.Count * Height;
        float y = v switch
        {
            ParagraphAlignment.Near => r.Y,
            ParagraphAlignment.Far => r.Bottom - total,
            _ => r.Y + MathF.Floor((r.Height - total) / 2),
        };
        y = MathF.Round(y);
        bool clip = total > r.Height;
        foreach (var l in lines) if (Measure(l, bold) > r.Width) clip = true;
        if (clip) p.PushClip(r);
        var atlas = Atlas(p);
        foreach (var line in lines)
        {
            float w = Measure(line, bold);
            float x = MathF.Round(h switch
            {
                TextAlignment.Center => r.X + MathF.Floor((r.Width - w) / 2),
                TextAlignment.Trailing => r.Right - w,
                _ => r.X,
            });
            foreach (char ch in line)
            {
                if (Find(ch, out var g))
                {
                    var src = new RectangleF(g.x, 0, g.w, Height);
                    p.FillMask(atlas, new RectangleF(x, y, g.w, Height), src, c);
                    if (bold && Smear) p.FillMask(atlas, new RectangleF(x + 1, y, g.w, Height), src, c);
                }
                else if (ch != ' ')
                {
                    int bw = Math.Max(2, Height / 2);
                    p.Stroke(new RectangleF(x + 0.5f, y + 1.5f, bw - 1, Ascent - 2), c, 1);
                }
                x += Advance(ch, bold);
            }
            y += Height;
        }
        if (clip) p.PopClip();
    }
}
