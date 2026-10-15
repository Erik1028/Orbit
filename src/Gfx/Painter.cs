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
    // Surface tones follow the chosen panel colour (Look tab); text colours are fixed. The modern watch draws with its
    // own tokens on top of these (Bento, derived in Apply).
    public static uint Bg = 0xFF15171C;
    public static uint Panel = 0xFF1D2027;
    public static uint Panel2 = 0xFF262A33;
    public static uint Panel3 = 0xFF30353F;
    public static uint Border = 0xFF353B47;
    public static uint OverlayBg = 0xEB15171C;   // wrist / keyboard background, with the chosen opacity
    public const uint Text = 0xFFE9EBEF;
    public const uint Dim = 0xFF9AA2AE;
    public const uint Faint = 0xFF5D6573;
    public static uint Accent = 0xFF45C1FF;      // the config's (Apply; the static constructor applies the default look)
    public static uint OnAccent = 0xFF0B1220;    // text on an accent fill
    public static uint Laser = 0xFF45C1FF;
    public const uint Accent2 = 0xFF6D5CFF;      // indigo (logo)
    public const uint Good = 0xFF4ADE80;
    public const uint Warn = 0xFFFBBF24;
    public const uint Bad = 0xFFF87171;
    public static int Version;                   // bumped on every appearance change

    // ---- the Windows 95 skin (Look > Style) ----
    // Like Mixtape's: it swaps the whole token layer at once - surfaces, text, accent, corners, typeface -
    // so the call sites need no branch; only the places that draw a SHAPE 1995 drew differently (a raised
    // button, a sunken field, a navy caption, a checkbox) ask Classic. The modern look is untouched.
    public static bool Classic { get; private set; }
    public const uint ClassicLaser = 0xFF1084D0;   // "same as accent" in the 95 skin: navy would vanish in a dark world
    public const uint W95Face = 0xFFC0C0C0, W95Light = 0xFFDFDFDF, W95Hi = 0xFFFFFFFF, W95Shadow = 0xFF808080,
                      W95Dark = 0xFF000000, W95Navy = 0xFF000080, W95Teal = 0xFF008080, W95Window = 0xFFFFFFFF;

    // the modern text colours are constants (used as default arguments); under the skin they are drawn as
    // the 1995 ones - RGB swapped, alpha kept
    public static uint Skin(uint c)
    {
        if (!Classic) return c;
        uint rgb = c & 0xFFFFFF, a = c & 0xFF000000;
        return rgb switch
        {
            Text & 0xFFFFFF => a | 0x000000,
            Dim & 0xFFFFFF => a | 0x404040,
            Faint & 0xFFFFFF => a | 0x808080,
            _ => c,          // only the text greys: Good / Warn / Bad are also colours people pick (swatches)
        };
    }

    public static readonly uint[] AccentPresets =
        { 0xFF45C1FF, 0xFF4F8BFF, 0xFF8B5CF6, 0xFFF472B6, 0xFFF87171, 0xFFFB923C, 0xFFFBBF24, 0xFF4ADE80, 0xFF2DD4BF, 0xFFE5E7EB };
    public static readonly uint[] PanelPresets =
        { 0xFF15171C, 0xFF0F172A, 0xFF1E1A2E, 0xFF10201B, 0xFF26201A, 0xFF050506 };
    public static readonly string[] PanelNames = { "Charcoal", "Midnight", "Plum", "Forest", "Mocha", "Black" };

    public static void Apply(AppearanceConfig a)
    {
        Classic = a.Style == "classic";
        if (Classic)
        {
            // one grey window face for every piece of chrome, white wells, black text, navy selection
            Bg = Panel = Panel2 = W95Face;
            Panel3 = W95Light;
            Border = W95Shadow;
            OverlayBg = W95Face;
            Accent = W95Navy;
            OnAccent = 0xFFFFFFFF;
            Laser = a.LaserColor == 0 ? ClassicLaser : 0xFF000000 | (a.LaserColor & 0xFFFFFF);
            Version++;
            return;
        }
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
        Bento.Derive(pc, Accent);          // the watch's cards, tiles and tracks (modern only)
        Version++;
    }

    // the default look until the config is applied (tools that never apply one draw in it too)
    static Theme() => Apply(new AppearanceConfig());

    public static readonly (string name, uint accent, uint panel)[] ThemePresets =
    {
        ("Classic", 0xFF45C1FF, 0xFF15171C), ("Ocean", 0xFF38BDF8, 0xFF0F172A), ("Bento", 0xFFFF5D93, 0xFF0A0B10),
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

    // HSV saturation, 0..1
    public static float Sat(uint col)
    {
        float r = ((col >> 16) & 0xFF) / 255f, g = ((col >> 8) & 0xFF) / 255f, b = (col & 0xFF) / 255f;
        float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b));
        return max < 1e-4f ? 0 : (max - min) / max;
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

    public static string ClassicFont = "Microsoft Sans Serif";   // the TrueType twin of MS Sans Serif

    public static void ResolveFonts(Vortice.DirectWrite.IDWriteFactory dw)
    {
        try
        {
            using var fonts = dw.GetSystemFontCollection(false);
            bool has(string f) => fonts.FindFamilyName(f, out _);
            if (!has(UiFont)) UiFont = "Segoe UI";
            if (!has(DisplayFont)) DisplayFont = "Segoe UI";
            if (!has(IconFont)) IconFont = "Segoe MDL2 Assets";
            if (!has(ClassicFont)) ClassicFont = "Tahoma";
        }
        catch { }
    }

    // the typeface for UI text and big numbers in the current skin
    public static string Font => Classic ? ClassicFont : UiFont;
    public static string Display => Classic ? ClassicFont : DisplayFont;

    // Segoe Fluent Icons code points
    public const string IcoKeyboard = "\uE765";
    public const string IcoMonitor = "\uE7F4";
    public const string IcoRecenter = "\uE72C";
    public const string IcoSettings = "\uE713";
    public const string IcoHide = "\uED1A";
    public const string IcoShow = "\uE890";
    public const string IcoWatch = "\uE916";
    public const string IcoGeneral = "\uE80F";
    public const string IcoBack = "\uE750";       // backspace
    public const string IcoEnter = "\uE751";
    public const string IcoShift = "\uE752";
    public const string IcoUp = "\uE70E";
    public const string IcoDown = "\uE70D";
    public const string IcoLeft = "\uE76B";
    public const string IcoRight = "\uE76C";
    public const string IcoWin = "\uE782";
    public const string IcoFolder = "\uE838";
    public const string IcoColor = "\uE790";
    public const string IcoLink = "\uE71B";
    public const string IcoCheck = "\uE73E";

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
    // Laid-out text, kept: DrawText lays its string out again on every call (~50 us each, 80 % of a watch frame).
    // Keyed by the format object too, so a format thrown away (the cap below) takes its layouts with it.
    readonly Dictionary<(string, IDWriteTextFormat, float, float), IDWriteTextLayout> _layouts = new();
    public int FormatCount => _formats.Count;
    public int WidthCount => _widths.Count;
    public int LayoutCount => _layouts.Count;
    readonly Stack<int> _clips = new();
    public float Scale { get; private set; } = 1;

    public Painter(Gpu g) { _g = g; }

    public Gpu Gpu => _g;

    // ---- pixel mode: the Windows 95 look, drawn at real 1995 pixel size ----
    // Aliased shapes, monochrome hinted glyphs measured like GDI's - text the way MS Sans Serif sat on a
    // 640 x 480 screen. The canvas is then blown up with whole pixels (ImageNearest).
    public bool PixelMode { get; private set; }
    IDWriteRenderingParams _aliasedText;

    public void BeginPixel(ID2D1Image target, uint clear)
    {
        Begin(target, clear, 1);
        PixelMode = true;
        Dc.AntialiasMode = AntialiasMode.Aliased;
        Dc.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Aliased;
        _aliasedText ??= _g.DWrite.CreateCustomRenderingParams(1.8f, 0f, 0f, PixelGeometry.Flat, RenderingMode.Aliased);
        Dc.TextRenderingParams = _aliasedText;
    }

    // the 50 % dither of 1995 (a latched toolbar button, a scroll bar's track): alternate pixels of two colours
    ID2D1BitmapBrush _checker;
    (uint, uint) _checkerColors;

    public void FillChecker(RectangleF r, uint a = 0xFFFFFFFF, uint b = 0xFFC0C0C0)
    {
        if (_checker == null || _checkerColors != (a, b))
        {
            _checker?.Dispose();
            using var bmp = CreateBitmap(new[] { a, b, b, a }, 2, 2);
            _checker = Dc.CreateBitmapBrush(bmp, new BitmapBrushProperties
            {
                ExtendModeX = ExtendMode.Wrap, ExtendModeY = ExtendMode.Wrap,
                InterpolationMode = Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor,
            });
            _checkerColors = (a, b);
        }
        Dc.FillRectangle(Raw(r), _checker);
    }

    // a little pixel pattern ('k' = a pixel in colour c) - the check mark, the caption buttons' glyphs
    public void Pixels(string[] map, float x, float y, uint c)
    {
        for (int row = 0; row < map.Length; row++)
            for (int col = 0; col < map[row].Length; col++)
                if (map[row][col] == 'k') Fill(new RectangleF(x + col, y + row, 1, 1), c);
    }

    // a bitmap at whole-pixel magnification, no smoothing
    public void ImageNearest(ID2D1Bitmap bmp, RectangleF dst) =>
        Dc.DrawBitmap(bmp, Raw(dst), 1, Vortice.Direct2D1.BitmapInterpolationMode.NearestNeighbor, null);

    public void Begin(ID2D1Image target, uint clear, float scale = 1)
    {
        if (_drawing) End();        // a previous frame threw half-way: close it before starting another
        _drawing = true;
        Scale = scale;
        Dc.Target = target;
        Dc.BeginDraw();
        Dc.Transform = Matrix3x2.CreateScale(scale);
        // 1995 had no anti-aliasing: the skin's bevels and squares stay pixel-sharp
        Dc.AntialiasMode = Theme.Classic ? AntialiasMode.Aliased : AntialiasMode.PerPrimitive;
        PixelMode = false;
        Dc.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;     // what Gpu sets up (1.3.3)
        Dc.TextRenderingParams = null;
        Dc.Clear(ToColor(clear));
    }

    public void End()
    {
        _drawing = false;
        while (_fades > 0) PopFade();
        while (_transforms.Count > 0) PopScale();
        while (_clips.Count > 0) PopClip();
        while (_layers.Count > 0) PopRoundedClip();
        var r = Dc.EndDraw();
        Dc.Target = null;
        if (r.Failure) Log.Warn("EndDraw: " + r);
    }

    static Color4 ToColor(uint c) => new(((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f, ((c >> 24) & 0xFF) / 255f);

    ID2D1SolidColorBrush Brush(uint c)
    {
        c = Theme.Skin(c);
        if (_brush == null) { _brush = Dc.CreateSolidColorBrush(ToColor(c)); _brushColor = c; }
        else if (c != _brushColor) { _brush.Color = ToColor(c); _brushColor = c; }
        return _brush;
    }

    IDWriteTextFormat Format(string family, float size, FontWeight w, TextAlignment h, ParagraphAlignment v, bool wrap)
    {
        var key = (family, size, w, h, v, wrap);
        if (!_formats.TryGetValue(key, out var f))
        {
            if (_formats.Count > 600)       // fitted / animated sizes: start over rather than grow forever
            {
                DropLayouts();
                foreach (var old in _formats.Values) old.Dispose();
                _formats.Clear();
            }
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
        if (Theme.Classic) radius = 0;        // square corners in the 95 skin
        if (radius > 0) Dc.FillRoundedRectangle(new RoundedRectangle(r, radius, radius), Brush(c));
        else Dc.FillRectangle(Raw(r), Brush(c));
    }

    public void Stroke(RectangleF r, uint c, float width = 1, float radius = 0)
    {
        if (Theme.Classic) radius = 0;
        if (radius > 0) Dc.DrawRoundedRectangle(new RoundedRectangle(r, radius, radius), Brush(c), width);
        else Dc.DrawRectangle(Raw(r), Brush(c), width);
    }

    // ---- linear gradients (the modern watch's cards: the top light, two-tone fills, a glass sheen) ----
    // One brush per stop set, kept: all thrown away when the look changes; past 64 the one used longest ago goes (the watch, the
    // settings and the editor's style chips share this painter - a many-coloured face must not rebuild them every frame). Its
    // start and end move per call, like the one recoloured solid brush.
    readonly Dictionary<(uint, uint, uint, int), (ID2D1LinearGradientBrush brush, long used)> _grads = new();
    int _gradVersion = -1;
    long _gradTick;
    const int GradCap = 64;
    public int GradientCount => _grads.Count;
    public int GradientsMade { get; private set; }     // brushes made so far (--perfbench: none per frame once warm)

    // c0 -> c1 along angleDeg (0 = left to right, 90 = top to bottom, 45 = top-left to bottom-right) across the rectangle,
    // or done after 'span' of it (c1 from there on)
    public void FillGradient(RectangleF r, uint c0, uint c1, float radius, float angleDeg = 90, float span = 1) =>
        Gradient(r, c0, c1, c1, 2, radius, angleDeg, span);

    // three stops: a, b half-way, c
    public void FillGradient(RectangleF r, uint a, uint b, uint c, float radius, float angleDeg = 90, float span = 1) =>
        Gradient(r, a, b, c, 3, radius, angleDeg, span);

    void Gradient(RectangleF r, uint a, uint b, uint c, int n, float radius, float angleDeg, float span)
    {
        if (Theme.Classic || PixelMode) { Fill(r, a); return; }
        if (a == b && (n == 2 || b == c)) { Fill(r, a, radius); return; }       // flat: the solid brush
        var brush = Place(GradBrush(a, b, c, n), r, angleDeg, span);
        if (radius > 0) Dc.FillRoundedRectangle(new RoundedRectangle(r, radius, radius), brush);
        else Dc.FillRectangle(Raw(r), brush);
    }

    // a disc filled with c0 -> c1 laid over 'area' the way FillGradient lays it there: on that card it matches the pixels under it.
    // replace: it takes the place of what is under it, alpha included (a card that is not opaque, drawn over again, would be a
    // touch more solid there)
    public void CircleGradient(float cx, float cy, float rad, RectangleF area, uint c0, uint c1, float angleDeg = 90, float span = 1,
        bool replace = false)
    {
        if (Theme.Classic || PixelMode) { Circle(cx, cy, rad, c0); return; }
        var e = new Ellipse(new Vector2(cx, cy), rad, rad);
        if (replace) Dc.PrimitiveBlend = PrimitiveBlend.Copy;
        if (c0 == c1) Dc.FillEllipse(e, Brush(c0));
        else Dc.FillEllipse(e, Place(GradBrush(c0, c1, c1, 2), area, angleDeg, span));
        if (replace) Dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
    }

    static ID2D1LinearGradientBrush Place(ID2D1LinearGradientBrush brush, RectangleF r, float angleDeg, float span)
    {
        float t = angleDeg * MathF.PI / 180, dx = MathF.Cos(t), dy = MathF.Sin(t);
        float len = MathF.Abs(r.Width * dx) + MathF.Abs(r.Height * dy);
        var s = new Vector2(r.X + r.Width / 2 - dx * len / 2, r.Y + r.Height / 2 - dy * len / 2);
        brush.StartPoint = s;
        brush.EndPoint = s + new Vector2(dx, dy) * (len * MathF.Max(0.01f, span));
        return brush;
    }

    ID2D1LinearGradientBrush GradBrush(uint a, uint b, uint c, int n)
    {
        if (_gradVersion != Theme.Version) { DropGradients(); _gradVersion = Theme.Version; }
        var key = (a, b, c, n);
        if (_grads.TryGetValue(key, out var e)) { _grads[key] = (e.brush, ++_gradTick); return e.brush; }
        if (_grads.Count >= GradCap)
        {
            // full: the one used longest ago makes room
            var old = key;
            long oldest = long.MaxValue;
            foreach (var (k, v) in _grads) if (v.used < oldest) { oldest = v.used; old = k; }
            _grads[old].brush.Dispose();
            _grads.Remove(old);
        }
        var stops = n == 2 ? new[] { new GradientStop(0, ToColor(a)), new GradientStop(1, ToColor(b)) }
            : new[] { new GradientStop(0, ToColor(a)), new GradientStop(0.5f, ToColor(b)), new GradientStop(1, ToColor(c)) };
        ID2D1LinearGradientBrush br;
        using (var col = Dc.CreateGradientStopCollection(stops, ExtendMode.Clamp))
            br = Dc.CreateLinearGradientBrush(new LinearGradientBrushProperties(Vector2.Zero, Vector2.UnitY), col);
        GradientsMade++;
        _grads[key] = (br, ++_gradTick);
        return br;
    }

    void DropGradients()
    {
        foreach (var g in _grads.Values) g.brush.Dispose();
        _grads.Clear();
    }

    // ---- Windows 95 shapes ----

    // one device pixel - two from 1.5x up - so the 3D edges stay crisp at any scale
    public float Px => MathF.Max(1, MathF.Round(Scale)) / Scale;

    public enum Edge { Raised, Window, Pressed, Sunken, Etched }

    // the two-pixel 3D edges of the era: Raised = a button, Window = a window frame,
    // Pressed = a button held down, Sunken = a text box / list / colour well, Etched = a group box line
    public void Bevel(RectangleF r, Edge e)
    {
        float p = Px;
        var (tl1, br1, tl2, br2) = e switch
        {
            Edge.Raised => (Theme.W95Hi, Theme.W95Dark, Theme.W95Light, Theme.W95Shadow),
            Edge.Window => (Theme.W95Light, Theme.W95Dark, Theme.W95Hi, Theme.W95Shadow),
            Edge.Pressed => (Theme.W95Dark, Theme.W95Hi, Theme.W95Shadow, Theme.W95Light),
            Edge.Sunken => (Theme.W95Shadow, Theme.W95Hi, Theme.W95Dark, Theme.W95Light),
            _ => (Theme.W95Shadow, Theme.W95Hi, Theme.W95Hi, Theme.W95Shadow),
        };
        Frame(r, tl1, br1, p);
        Frame(RectangleF.Inflate(r, -p, -p), tl2, br2, p);
    }

    void Frame(RectangleF r, uint tl, uint br, float p)
    {
        Fill(new RectangleF(r.X, r.Y, r.Width, p), tl);
        Fill(new RectangleF(r.X, r.Y, p, r.Height), tl);
        Fill(new RectangleF(r.X, r.Bottom - p, r.Width, p), br);
        Fill(new RectangleF(r.Right - p, r.Y, p, r.Height), br);
    }

    // a 95 push button: raised; held = black frame + flat grey edge; latched (a toggle that is in) =
    // dithered + pushed in; isDefault = the black frame of a dialog's default button. Returns the face.
    public RectangleF ButtonFace95(RectangleF r, bool isDefault, bool down, bool latched)
    {
        var b = r;
        if (isDefault || down)
        {
            Stroke(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Theme.W95Dark, 1);
            b = RectangleF.Inflate(r, -1, -1);
        }
        if (latched && !down) FillChecker(b);
        else Fill(b, Theme.W95Face);
        if (down)
            Stroke(new RectangleF(b.X + 0.5f, b.Y + 0.5f, b.Width - 1, b.Height - 1), Theme.W95Shadow, 1);
        else
            Bevel(b, latched ? Edge.Pressed : Edge.Raised);
        return b;
    }

    // a groove across: the line between sections of a 95 dialog
    public void EtchedLine(float x1, float x2, float y)
    {
        float p = Px;
        Fill(new RectangleF(x1, y, x2 - x1, p), Theme.W95Shadow);
        Fill(new RectangleF(x1, y + p, x2 - x1, p), Theme.W95Hi);
    }

    // an active window's navy title bar
    public void Caption(RectangleF r, string text, float size = 13)
    {
        Fill(r, Theme.W95Navy);
        Text(text, new RectangleF(r.X + 6, r.Y, r.Width - 12, r.Height), 0xFFFFFFFF, size, FontWeight.Bold);
    }

    // greyed-out text the 95 way: etched - white, then grey on top one pixel up-left
    public void DisabledText(string s, RectangleF r, float size, FontWeight w = FontWeight.Normal,
        TextAlignment h = TextAlignment.Leading, ParagraphAlignment v = ParagraphAlignment.Center)
    {
        float p = Px;
        Text(s, new RectangleF(r.X + p, r.Y + p, r.Width, r.Height), Theme.W95Hi, size, w, h, v);
        Text(s, r, Theme.W95Shadow, size, w, h, v);
    }

    // ---- for the modern watch's widgets: rings, and a scale / fade around a widget while it animates ----

    ID2D1StrokeStyle _roundCaps;

    // a ring segment with round ends, Apple-watch style: 'start' and 'sweep' in turns (0 = 12 o'clock, clockwise)
    public void Arc(float cx, float cy, float r, float start, float sweep, uint c, float width)
    {
        if (sweep <= 0.0005f || r <= 0) return;
        _roundCaps ??= _g.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
        {
            StartCap = CapStyle.Round, EndCap = CapStyle.Round, LineJoin = LineJoin.Round,
        });
        if (sweep >= 0.9995f) { Dc.DrawEllipse(new Ellipse(new Vector2(cx, cy), r, r), Brush(c), width); return; }
        Vector2 At(float t) => new(cx + r * MathF.Sin(t * MathF.Tau), cy - r * MathF.Cos(t * MathF.Tau));
        using var geo = _g.D2DFactory.CreatePathGeometry();
        using (var sink = geo.Open())
        {
            sink.BeginFigure(At(start), FigureBegin.Hollow);
            sink.AddArc(new ArcSegment
            {
                Point = At(start + sweep), Size = new Vortice.Mathematics.Size(r, r), RotationAngle = 0,
                SweepDirection = SweepDirection.Clockwise, ArcSize = sweep > 0.5f ? ArcSize.Large : ArcSize.Small,
            });
            sink.EndFigure(FigureEnd.Open);
            sink.Close();
        }
        Dc.DrawGeometry(geo, Brush(c), width, _roundCaps);
    }

    // a line with round ends (clock hands)
    public void RoundLine(float x1, float y1, float x2, float y2, uint c, float width)
    {
        _roundCaps ??= _g.D2DFactory.CreateStrokeStyle(new StrokeStyleProperties
        {
            StartCap = CapStyle.Round, EndCap = CapStyle.Round, LineJoin = LineJoin.Round,
        });
        Dc.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Brush(c), width, _roundCaps);
    }

    readonly Stack<Matrix3x2> _transforms = new();

    // draw scaled by s around (cx, cy) until PopScale
    public void PushScale(float s, float cx, float cy)
    {
        _transforms.Push(Dc.Transform);
        Dc.Transform = Matrix3x2.CreateScale(s, new Vector2(cx, cy)) * Dc.Transform;
    }

    public void PopScale()
    {
        if (_transforms.Count > 0) Dc.Transform = _transforms.Pop();
    }

    int _fades;

    // everything until PopFade at this opacity (a layer: only while something animates)
    public void PushFade(float opacity)
    {
        Dc.PushLayer(new LayerParameters1
        {
            ContentBounds = new Vortice.RawRectF(-100000, -100000, 100000, 100000),
            MaskAntialiasMode = AntialiasMode.PerPrimitive,
            MaskTransform = Matrix3x2.Identity,
            Opacity = Math.Clamp(opacity, 0, 1),
        }, null);
        _fades++;
    }

    public void PopFade()
    {
        if (_fades == 0) return;
        _fades--;
        Dc.PopLayer();
    }

    // a filled polygon (the 95 clock's hands)
    public void Polygon(Vector2[] pts, uint c)
    {
        using var geo = _g.D2DFactory.CreatePathGeometry();
        using (var sink = geo.Open())
        {
            sink.BeginFigure(pts[0], FigureBegin.Filled);
            for (int i = 1; i < pts.Length; i++) sink.AddLine(pts[i]);
            sink.EndFigure(FigureEnd.Closed);
            sink.Close();
        }
        Dc.FillGeometry(geo, Brush(c));
    }

    public void Line(float x1, float y1, float x2, float y2, uint c, float width = 1) =>
        Dc.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Brush(c), width);

    public void Circle(float cx, float cy, float r, uint c) =>
        Dc.FillEllipse(new Ellipse(new Vector2(cx, cy), r, r), Brush(c));

    public void Ring(float cx, float cy, float r, uint c, float width) =>
        Dc.DrawEllipse(new Ellipse(new Vector2(cx, cy), r, r), Brush(c), width);

    // --snapshot's contrast check: every text drawn (and where, in what colour); NoText leaves all text out (what it sits on)
    public Action<string, RectangleF, uint, float, FontWeight, string> TextSpy;
    public bool NoText;

    public void Text(string s, RectangleF r, uint c, float size, FontWeight w = FontWeight.Normal,
        TextAlignment h = TextAlignment.Leading, ParagraphAlignment v = ParagraphAlignment.Center,
        string family = null, bool wrap = false)
    {
        if (string.IsNullOrEmpty(s) || NoText) return;
        TextSpy?.Invoke(s, r, c, size, w, family);
        var f = Format(family ?? Theme.Font, size, w, h, v, wrap);
        // the 95 skin's GDI-measured pixel text stays exactly as it was
        if (PixelMode)
        {
            Dc.DrawText(s, f, new Rect(r.X, r.Y, r.Width, r.Height), Brush(c), DrawTextOptions.Clip, Vortice.DCommon.MeasuringMode.GdiClassic);
            return;
        }
        float lw = MathF.Max(0, r.Width), lh = MathF.Max(0, r.Height);
        var key = (s, f, lw, lh);
        if (!_layouts.TryGetValue(key, out var lay))
        {
            // a ticking clock or a springing card makes new ones all the time: start over rather than grow forever
            if (_layouts.Count >= 1000) DropLayouts();
            lay = _g.DWrite.CreateTextLayout(s, f, lw, lh);
            _layouts[key] = lay;
        }
        Dc.DrawTextLayout(new Vector2(r.X, r.Y), lay, Brush(c), ClipText ? DrawTextOptions.Clip : DrawTextOptions.None);
    }

    // Text cut to its rectangle (DrawTextOptions.Clip): the default. Off, a draw that has made sure its text fits (the modern
    // watch's custom face: cut with "...", sized to fit) saves a clip per string - about a third of a watch frame.
    public bool ClipText { get; set; } = true;

    void DropLayouts()
    {
        foreach (var l in _layouts.Values) l.Dispose();
        _layouts.Clear();
        foreach (var l in _tracked.Values) l.Dispose();
        _tracked.Clear();
    }

    // ---- letter-spaced text (the modern watch's UPPERCASE captions) ----
    readonly Dictionary<(string, IDWriteTextFormat, float, float, float), IDWriteTextLayout> _tracked = new();

    // text with 'tracking' px after every letter but the last (so centred and right-aligned text stays where it belongs);
    // the 95 skin's pixel text has none
    public void TrackedText(string s, RectangleF r, uint c, float size, FontWeight w, float tracking,
        TextAlignment h = TextAlignment.Leading, ParagraphAlignment v = ParagraphAlignment.Center, string family = null)
    {
        if (string.IsNullOrEmpty(s) || NoText) return;
        if (PixelMode || tracking == 0 || s.Length < 2) { Text(s, r, c, size, w, h, v, family); return; }
        TextSpy?.Invoke(s, r, c, size, w, family);
        var f = Format(family ?? Theme.Font, size, w, h, v, false);
        float lw = MathF.Max(0, r.Width), lh = MathF.Max(0, r.Height);
        var key = (s, f, lw, lh, tracking);
        if (!_tracked.TryGetValue(key, out var lay))
        {
            if (_tracked.Count >= 300) { foreach (var l in _tracked.Values) l.Dispose(); _tracked.Clear(); }
            lay = _g.DWrite.CreateTextLayout(s, f, lw, lh);
            using (var l1 = lay.QueryInterface<IDWriteTextLayout1>())
                l1.SetCharacterSpacing(0, tracking, 0, new TextRange(0, (uint)(s.Length - 1)));
            _tracked[key] = lay;
        }
        Dc.DrawTextLayout(new Vector2(r.X, r.Y), lay, Brush(c), ClipText ? DrawTextOptions.Clip : DrawTextOptions.None);
    }

    public float MeasureTracked(string s, float size, FontWeight w, float tracking, string family = null) =>
        string.IsNullOrEmpty(s) ? 0 : MeasureWidth(s, size, w, family) + (PixelMode ? 0 : tracking * (s.Length - 1));

    // ---- line metrics: where the baseline sits, so a value and its unit share one ----
    readonly Dictionary<(float, FontWeight, string), (float baseline, float height)> _lines = new();

    // from the top of a line of this text to its baseline, and the line's height (text drawn ParagraphAlignment.Near)
    public (float baseline, float height) TextLine(float size, FontWeight w = FontWeight.Normal, string family = null)
    {
        family ??= Theme.Font;
        var key = (size, w, family);
        if (_lines.TryGetValue(key, out var m)) return m;
        var f = Format(family, size, w, TextAlignment.Leading, ParagraphAlignment.Near, false);
        using var layout = _g.DWrite.CreateTextLayout("0", f, 1000, 1000);
        var lm = new LineMetrics[1];
        layout.GetLineMetrics(lm, out _);
        m = (lm[0].Baseline, lm[0].Height);
        if (_lines.Count > 400) _lines.Clear();
        _lines[key] = m;
        return m;
    }

    public void Icon(string glyph, RectangleF r, uint c, float size) =>
        Text(glyph, r, c, size, FontWeight.Normal, TextAlignment.Center, ParagraphAlignment.Center, Theme.IconFont);

    public float MeasureWidth(string s, float size, FontWeight w = FontWeight.Normal, string family = null)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        family ??= Theme.Font;
        var key = (s, size, w, PixelMode ? family + "\u0001px" : family);
        if (_widths.TryGetValue(key, out float cached)) return cached;
        var f = Format(family, size, w, TextAlignment.Leading, ParagraphAlignment.Near, false);
        using var layout = PixelMode
            ? _g.DWrite.CreateGdiCompatibleTextLayout(s, (uint)s.Length, f, 10000, 1000, 1f, null, false)
            : _g.DWrite.CreateTextLayout(s, f, 10000, 1000);
        float width = layout.Metrics.WidthIncludingTrailingWhitespace;
        if (_widths.Count > 2000) _widths.Clear();     // chat text etc. would grow it forever
        _widths[key] = width;
        return width;
    }

    // the height of wrapped text in a column of this width (laid out once, kept)
    readonly Dictionary<(string, float, FontWeight, float, string), float> _heights = new();
    public float MeasureHeight(string s, float size, FontWeight w, float width, string family = null)
    {
        if (string.IsNullOrEmpty(s) || width <= 0) return 0;
        family ??= Theme.Font;
        var key = (s, size, w, width, family);
        if (_heights.TryGetValue(key, out float cached)) return cached;
        var f = Format(family, size, w, TextAlignment.Leading, ParagraphAlignment.Near, true);
        using var layout = _g.DWrite.CreateTextLayout(s, f, width, 100000);
        float h = layout.Metrics.Height;
        if (_heights.Count > 1000) _heights.Clear();
        _heights[key] = h;
        return h;
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
        DropGradients();
        DropLayouts();
        foreach (var f in _formats.Values) f.Dispose();
        _formats.Clear();
    }
}
