using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Orbit;

// The clock designs' geometry (WristClocks): digits as outlines (a font's glyphs as paths, made once per size), seven-segment
// segments (lines with pointed ends) and a vertical squash (a flip card's flap). One per painter, made on first use; the paths
// are factory resources, the one brush belongs to the painter's device. Modern only.
sealed class ClockGeo
{
    static readonly ConditionalWeakTable<Painter, ClockGeo> _all = new();
    public static ClockGeo For(Painter p) => _all.GetValue(p, x => new ClockGeo(x));

    readonly Painter _p;
    ID2D1DeviceContext Dc => _p.Gpu.D2D;
    ID2D1SolidColorBrush _brush;
    uint _brushColor;
    ID2D1StrokeStyle _pointed, _round;

    ClockGeo(Painter p) { _p = p; }

    ID2D1SolidColorBrush Brush(uint c)
    {
        var col = new Color4(((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f, ((c >> 24) & 0xFF) / 255f);
        if (_brush == null) { _brush = Dc.CreateSolidColorBrush(col); _brushColor = c; }
        else if (c != _brushColor) { _brush.Color = col; _brushColor = c; }
        return _brush;
    }

    // ---- seven-segment parts: a line whose ends are points (a hexagon), its tips 'width / 2' past the ends ----
    public void Pointed(float x1, float y1, float x2, float y2, uint c, float width)
    {
        _pointed ??= _p.Gpu.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
        {
            StartCap = CapStyle.Triangle, EndCap = CapStyle.Triangle, LineJoin = LineJoin.Miter,
        });
        Dc.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Brush(c), width, _pointed);
    }

    // ---- a squash toward a line (a flap folding down onto the split): Push / Pop around what it squashes ----
    readonly Stack<Matrix3x2> _saved = new();

    public void PushSquash(float sy, float cy)
    {
        _saved.Push(Dc.Transform);
        Dc.Transform = Matrix3x2.CreateScale(1, MathF.Max(0.001f, sy), new Vector2(0, cy)) * Dc.Transform;
    }

    public void PopSquash()
    {
        if (_saved.Count > 0) Dc.Transform = _saved.Pop();
    }

    // ---- digits as outlines: each glyph's path at a size, kept (a clock uses 11 characters at a size or two) ----
    readonly Dictionary<(string, FontWeight), IDWriteFontFace> _faces = new();
    readonly Dictionary<(char, float, FontWeight, string), (ID2D1PathGeometry geo, float advance)> _glyphs = new();

    IDWriteFontFace Face(string family, FontWeight weight)
    {
        if (_faces.TryGetValue((family, weight), out var f)) return f;
        try
        {
            using var fonts = _p.Gpu.DWrite.GetSystemFontCollection(false);
            if (!fonts.FindFamilyName(family, out uint i) && !fonts.FindFamilyName("Segoe UI", out i)) i = 0;
            using var fam = fonts.GetFontFamily(i);
            using var font = fam.GetFirstMatchingFont(weight, FontStretch.Normal, Vortice.DirectWrite.FontStyle.Normal);
            f = font.CreateFontFace();
        }
        catch (Exception e) { Log.Warn("clock outline font: " + e.Message); f = null; }
        _faces[(family, weight)] = f;
        return f;
    }

    (ID2D1PathGeometry geo, float advance) Glyph(char ch, float size, FontWeight weight, string family)
    {
        var key = (ch, size, weight, family);
        if (_glyphs.TryGetValue(key, out var g)) return g;
        if (_glyphs.Count >= 96)
        {
            foreach (var old in _glyphs.Values) old.geo?.Dispose();
            _glyphs.Clear();
        }
        var face = Face(family, weight);
        if (face == null) { _glyphs[key] = (null, size * 0.55f); return _glyphs[key]; }
        ushort index = face.GetGlyphIndices(new uint[] { ch })[0];
        var metrics = new GlyphMetrics[1];
        face.GetDesignGlyphMetrics(new[] { index }, metrics, false);
        float advance = metrics[0].AdvanceWidth * size / face.Metrics.DesignUnitsPerEm;
        var geo = _p.Gpu.D2DFactory.CreatePathGeometry();
        using (var sink = geo.Open())
        {
            face.GetGlyphRunOutline(size, new[] { index }, new[] { advance }, new[] { new GlyphOffset() }, false, false, sink);
            sink.Close();
        }
        _glyphs[key] = g = (geo, advance);
        return g;
    }

    // how wide s is drawn as outlines
    public float OutlineWidth(string s, float size, FontWeight weight, string family)
    {
        float w = 0;
        foreach (char ch in s) w += Glyph(ch, size, weight, family).advance;
        return w;
    }

    // s as outlines from x, its baseline at 'baseline': stroked 'width' wide in 'stroke' (round corners), filled with 'fill' (0: hollow)
    public float Outline(string s, float x, float baseline, float size, FontWeight weight, string family, uint stroke, float width, uint fill = 0)
    {
        _round ??= _p.Gpu.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
        {
            StartCap = CapStyle.Round, EndCap = CapStyle.Round, LineJoin = LineJoin.Round,
        });
        var old = Dc.Transform;
        float x0 = x;
        foreach (char ch in s)
        {
            var (geo, adv) = Glyph(ch, size, weight, family);
            if (geo != null && ch != ' ')
            {
                Dc.Transform = Matrix3x2.CreateTranslation(x, baseline) * old;
                if (fill != 0) Dc.FillGeometry(geo, Brush(fill));
                Dc.DrawGeometry(geo, Brush(stroke), width, _round);
            }
            x += adv;
        }
        Dc.Transform = old;
        return x - x0;
    }
}
