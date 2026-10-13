using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Orbit;

static class Theme
{
    // Surface tones follow the chosen panel colour (Look tab); text colours are fixed.
    public static uint Bg = 0xFF15171C;
    public static uint Panel = 0xFF1D2027;
    public static uint Panel2 = 0xFF262A33;
    public static uint Panel3 = 0xFF30353F;
    public static uint Border = 0xFF353B47;
    public static uint OverlayBg = 0xEB15171C;   // wrist / keyboard background, with the chosen opacity
    public const uint Text = 0xFFE9EBEF;
    public const uint Dim = 0xFF9AA2AE;
    public const uint Faint = 0xFF5D6573;
    public static uint Accent = 0xFF45C1FF;      // cyan by default
    public static uint OnAccent = 0xFF0B1220;    // text on an accent fill
    public static uint Laser = 0xFF45C1FF;
    public const uint Accent2 = 0xFF6D5CFF;      // indigo (logo)
    public const uint Good = 0xFF4ADE80;
    public const uint Warn = 0xFFFBBF24;
    public const uint Bad = 0xFFF87171;
    public static int Version;                   // bumped on every appearance change

    public static readonly uint[] AccentPresets =
        { 0xFF45C1FF, 0xFF4F8BFF, 0xFF8B5CF6, 0xFFF472B6, 0xFFF87171, 0xFFFB923C, 0xFFFBBF24, 0xFF4ADE80, 0xFF2DD4BF, 0xFFE5E7EB };
    public static readonly uint[] PanelPresets =
        { 0xFF15171C, 0xFF0F172A, 0xFF1E1A2E, 0xFF10201B, 0xFF26201A, 0xFF050506 };
    public static readonly string[] PanelNames = { "Charcoal", "Midnight", "Plum", "Forest", "Mocha", "Black" };

    public static void Apply(AppearanceConfig a)
    {
        uint pc = 0xFF000000 | (a.PanelColor & 0xFFFFFF);
        Bg = pc;
        Panel = Mix(pc, 0xFFFFFFFF, 0.035f);
        Panel2 = Mix(pc, 0xFFFFFFFF, 0.07f);
        Panel3 = Mix(pc, 0xFFFFFFFF, 0.12f);
        Border = Mix(pc, 0xFFFFFFFF, 0.15f);
        OverlayBg = WithAlpha(pc, Math.Clamp(a.PanelOpacity, 0.3f, 1f));
        Accent = 0xFF000000 | (a.Accent & 0xFFFFFF);
        OnAccent = Luma(Accent) > 0.55f ? 0xFF0B1220 : 0xFFFFFFFF;
        Laser = a.LaserColor == 0 ? Accent : 0xFF000000 | (a.LaserColor & 0xFFFFFF);
        Version++;
    }

    public static readonly (string name, uint accent, uint panel)[] ThemePresets =
    {
        ("Classic", 0xFF45C1FF, 0xFF15171C), ("Ocean", 0xFF38BDF8, 0xFF0F172A), ("Sakura", 0xFFF472B6, 0xFF1E1A2E),
        ("Forest", 0xFF4ADE80, 0xFF10201B), ("Ember", 0xFFFB923C, 0xFF26201A), ("Mono", 0xFFE5E7EB, 0xFF050506),
    };

    public static uint Hsv(float h, float s, float v)
    {
        h = ((h % 360) + 360) % 360 / 60f;
        float c = v * s, x = c * (1 - MathF.Abs(h % 2 - 1)), m = v - c;
        (float r, float g, float b) = h switch
        {
            < 1 => (c, x, 0f), < 2 => (x, c, 0f), < 3 => (0f, c, x),
            < 4 => (0f, x, c), < 5 => (x, 0f, c), _ => (c, 0f, x),
        };
        uint B(float f) => (uint)Math.Clamp((f + m) * 255 + 0.5f, 0, 255);
        return 0xFF000000 | (B(r) << 16) | (B(g) << 8) | B(b);
    }

    public static float Hue(uint col)
    {
        float r = ((col >> 16) & 0xFF) / 255f, g = ((col >> 8) & 0xFF) / 255f, b = (col & 0xFF) / 255f;
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b)), d = max - min;
        if (d < 1e-4f) return 0;
        float h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60 + 360) % 360;
    }

    public static float Luma(uint c) =>
        (0.2126f * ((c >> 16) & 0xFF) + 0.7152f * ((c >> 8) & 0xFF) + 0.0722f * (c & 0xFF)) / 255f;

    // Windows 11 fonts; ResolveFonts falls back to the Windows 10 ones (same icon code points)
    public static string UiFont = "Segoe UI Variable Text";
    public static string DisplayFont = "Segoe UI Variable Display";
    public static string IconFont = "Segoe Fluent Icons";
    public const string IcoWindow = "\uE8A7";      // open in new window
    public const string IcoAdd = "\uE710";
    public const string IcoNight = "\uE708";       // crescent moon (quiet hours)
    public const string IcoLayouts = "\uE8A9";     // grid of tiles (view all)
    public const string IcoDownload = "\uE896";
    public const string IcoDelete = "\uE74D";

    public static void ResolveFonts(Vortice.DirectWrite.IDWriteFactory dw)
    {
        try
        {
            using var fonts = dw.GetSystemFontCollection(false);
            bool has(string f) => fonts.FindFamilyName(f, out _);
            if (!has(UiFont)) UiFont = "Segoe UI";
            if (!has(DisplayFont)) DisplayFont = "Segoe UI";
            if (!has(IconFont)) IconFont = "Segoe MDL2 Assets";
        }
        catch { }
    }

    // Segoe Fluent Icons code points
    public const string IcoKeyboard = "";
    public const string IcoMonitor = "";
    public const string IcoRecenter = "";
    public const string IcoSettings = "";
    public const string IcoHide = "";
    public const string IcoShow = "";
    public const string IcoWatch = "";
    public const string IcoGeneral = "";
    public const string IcoBack = "";       // backspace
    public const string IcoEnter = "";
    public const string IcoShift = "";
    public const string IcoUp = "";
    public const string IcoDown = "";
    public const string IcoLeft = "";
    public const string IcoRight = "";
    public const string IcoWin = "";
    public const string IcoFolder = "";
    public const string IcoColor = "";
    public const string IcoLink = "";
    public const string IcoCheck = "";

    public static uint WithAlpha(uint c, float a) => ((uint)Math.Clamp(a * ((c >> 24) & 0xFF), 0, 255) << 24) | (c & 0xFFFFFF);

    public static uint Mix(uint a, uint b, float t)
    {
        uint ch(int s) => (uint)Math.Clamp(((a >> s) & 0xFF) * (1 - t) + ((b >> s) & 0xFF) * t, 0, 255);
        return (ch(24) << 24) | (ch(16) << 16) | (ch(8) << 8) | ch(0);
    }
}

// Thin drawing layer over the shared ID2D1DeviceContext with a font cache.
// Coordinates are logical; Begin() sets a scale so the same layout renders sharp at any size.
// One solid brush is recoloured per call: animations produce endless in-between colours, and a
// brush per colour used to pile up D2D objects for as long as the app ran.
public sealed class Painter : IDisposable
{
    readonly Gpu _g;
    ID2D1DeviceContext Dc => _g.D2D;
    ID2D1SolidColorBrush _brush;
    uint _brushColor;
    bool _drawing;
    readonly Dictionary<(string, float, FontWeight, string), float> _widths = new();
    readonly Dictionary<(string, float, FontWeight, TextAlignment, ParagraphAlignment, bool), IDWriteTextFormat> _formats = new();
    readonly Stack<int> _clips = new();
    public float Scale { get; private set; } = 1;

    public Painter(Gpu g) { _g = g; }

    public void Begin(ID2D1Image target, uint clear, float scale = 1)
    {
        if (_drawing) End();        // a previous frame threw half-way: close it before starting another
        _drawing = true;
        Scale = scale;
        Dc.Target = target;
        Dc.BeginDraw();
        Dc.Transform = Matrix3x2.CreateScale(scale);
        Dc.Clear(ToColor(clear));
    }

    public void End()
    {
        _drawing = false;
        while (_clips.Count > 0) PopClip();
        while (_layers.Count > 0) PopRoundedClip();
        var r = Dc.EndDraw();
        Dc.Target = null;
        if (r.Failure) Log.Warn("EndDraw: " + r);
    }

    static Color4 ToColor(uint c) => new(((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f, ((c >> 24) & 0xFF) / 255f);

    ID2D1SolidColorBrush Brush(uint c)
    {
        if (_brush == null) { _brush = Dc.CreateSolidColorBrush(ToColor(c)); _brushColor = c; }
        else if (c != _brushColor) { _brush.Color = ToColor(c); _brushColor = c; }
        return _brush;
    }

    IDWriteTextFormat Format(string family, float size, FontWeight w, TextAlignment h, ParagraphAlignment v, bool wrap)
    {
        var key = (family, size, w, h, v, wrap);
        if (!_formats.TryGetValue(key, out var f))
        {
            f = _g.DWrite.CreateTextFormat(family, null, w, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, size, "en-us");
            f.TextAlignment = h;
            f.ParagraphAlignment = v;
            f.WordWrapping = wrap ? WordWrapping.Wrap : WordWrapping.NoWrap;
            _formats[key] = f;
        }
        return f;
    }

    static Vortice.RawRectF Raw(RectangleF r) => new(r.Left, r.Top, r.Right, r.Bottom);

    public void Fill(RectangleF r, uint c, float radius = 0)
    {
        if (radius > 0) Dc.FillRoundedRectangle(new RoundedRectangle(r, radius, radius), Brush(c));
        else Dc.FillRectangle(Raw(r), Brush(c));
    }

    public void Stroke(RectangleF r, uint c, float width = 1, float radius = 0)
    {
        if (radius > 0) Dc.DrawRoundedRectangle(new RoundedRectangle(r, radius, radius), Brush(c), width);
        else Dc.DrawRectangle(Raw(r), Brush(c), width);
    }

    public void Line(float x1, float y1, float x2, float y2, uint c, float width = 1) =>
        Dc.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Brush(c), width);

    public void Circle(float cx, float cy, float r, uint c) =>
        Dc.FillEllipse(new Ellipse(new Vector2(cx, cy), r, r), Brush(c));

    public void Ring(float cx, float cy, float r, uint c, float width) =>
        Dc.DrawEllipse(new Ellipse(new Vector2(cx, cy), r, r), Brush(c), width);

    public void Text(string s, RectangleF r, uint c, float size, FontWeight w = FontWeight.Normal,
        TextAlignment h = TextAlignment.Leading, ParagraphAlignment v = ParagraphAlignment.Center,
        string family = null, bool wrap = false)
    {
        if (string.IsNullOrEmpty(s)) return;
        var f = Format(family ?? Theme.UiFont, size, w, h, v, wrap);
        Dc.DrawText(s, f, new Rect(r.X, r.Y, r.Width, r.Height), Brush(c), DrawTextOptions.Clip);
    }

    public void Icon(string glyph, RectangleF r, uint c, float size) =>
        Text(glyph, r, c, size, FontWeight.Normal, TextAlignment.Center, ParagraphAlignment.Center, Theme.IconFont);

    public float MeasureWidth(string s, float size, FontWeight w = FontWeight.Normal, string family = null)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        family ??= Theme.UiFont;
        var key = (s, size, w, family);
        if (_widths.TryGetValue(key, out float cached)) return cached;
        var f = Format(family, size, w, TextAlignment.Leading, ParagraphAlignment.Near, false);
        using var layout = _g.DWrite.CreateTextLayout(s, f, 10000, 1000);
        float width = layout.Metrics.WidthIncludingTrailingWhitespace;
        if (_widths.Count > 2000) _widths.Clear();     // chat text etc. would grow it forever
        _widths[key] = width;
        return width;
    }

    // premultiplied BGRA pixels -> bitmap (album art, icons)
    public unsafe ID2D1Bitmap CreateBitmap(uint[] px, int w, int h)
    {
        fixed (uint* p = px)
            return Dc.CreateBitmap(new SizeI(w, h), (IntPtr)p, (uint)(w * 4),
                new BitmapProperties(new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
    }

    public void Image(ID2D1Bitmap bmp, RectangleF dst, float opacity = 1) =>
        Dc.DrawBitmap(bmp, Raw(dst), opacity, Vortice.Direct2D1.BitmapInterpolationMode.Linear, null);

    readonly Stack<ID2D1Geometry> _layers = new();

    // clips everything drawn until PopRoundedClip to a rounded rectangle (antialiased edges)
    public void PushRoundedClip(RectangleF r, float radius)
    {
        var geo = _g.D2DFactory.CreateRoundedRectangleGeometry(new RoundedRectangle(r, radius, radius));
        Dc.PushLayer(new LayerParameters1
        {
            ContentBounds = new Vortice.RawRectF(-100000, -100000, 100000, 100000),
            GeometricMask = geo,
            MaskAntialiasMode = AntialiasMode.PerPrimitive,
            MaskTransform = Matrix3x2.Identity,
            Opacity = 1,
        }, null);
        _layers.Push(geo);
    }

    public void PopRoundedClip()
    {
        if (_layers.Count == 0) return;
        Dc.PopLayer();
        _layers.Pop().Dispose();
    }

    public void PushClip(RectangleF r)
    {
        Dc.PushAxisAlignedClip(Raw(r), AntialiasMode.Aliased);
        _clips.Push(1);
    }

    public void PopClip()
    {
        if (_clips.Count == 0) return;
        _clips.Pop();
        Dc.PopAxisAlignedClip();
    }

    public void Dispose()
    {
        _brush?.Dispose();
        _brush = null;
        foreach (var f in _formats.Values) f.Dispose();
        _formats.Clear();
    }
}
