using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Vortice.Direct2D1;

namespace Orbit;

// The weather widget's pictures - clear, partly cloudy, cloudy, fog, drizzle, rain, snow and storm, by day and by night (a moon in the
// clear and partly cloudy ones) - made of a few shapes in a 1 x 1 box: discs, rounded bars, strokes, the moon's crescent and the bolt's
// two quads. One model, drawn two ways: smooth with the painter in a palette (Modern in colour or in the widget's own look's text
// colour, Cyber's yellow / cyan / red, XP and 7), and as pixel art in the sixteen VGA colours, black-edged like the era's icons (95 and
// the Mac: the same shapes rasterised once per size and kept as bitmaps). Nothing is made per frame: the models are built once, the
// crescent's strip and the bolt go through the painter's kept shapes.
public static class WeatherGlyphs
{
    public enum Part : byte { Sun, Moon, Star, CloudBack, Cloud, CloudDark, Rain, Snow, Bolt, Fog }
    enum Shape : byte { Disc, Bar, Stroke, Crescent, Quad }

    // Disc: x, y, r. Bar: x0, y0, x1, y1, radius. Stroke: x0, y0, x1, y1, width. Crescent: x, y, r, minus the disc cx, cy, cr.
    // Quad: four corners (convex).
    readonly record struct Prim(Shape S, Part P, float A, float B, float C, float D, float E = 0, float F = 0, float G = 0, float H = 0);

    // a colour for each part
    public readonly record struct Palette(uint Sun, uint Moon, uint Star, uint CloudBack, uint Cloud, uint CloudDark, uint Rain, uint Snow,
        uint Bolt, uint Fog)
    {
        public uint Of(Part p) => p switch
        {
            Part.Sun => Sun, Part.Moon => Moon, Part.Star => Star, Part.CloudBack => CloudBack, Part.Cloud => Cloud, Part.CloudDark => CloudDark,
            Part.Rain => Rain, Part.Snow => Snow, Part.Bolt => Bolt, _ => Fog,
        };
    }

    // in colour (Modern's default card, XP, 7): a warm sun, a pale moon, white clouds over grey ones, blue rain, a yellow bolt
    public static readonly Palette Colour = new(0xFFFFC53D, 0xFFF3E3A0, 0xFFF3E3A0, 0xFFA9B3C3, 0xFFF1F4F9, 0xFF8C93A4, 0xFF5AB4FF, 0xFFFFFFFF,
        0xFFFFD43B, 0xFFC4CAD6);

    // a widget with a look of its own: its text colour, the sun and the back cloud a step towards its grey (a cloud stays clear of them)
    public static Palette Mono(uint text, uint sub)
    {
        uint mid = Theme.Mix(text, sub, 0.55f);
        return new(mid, mid, sub, sub, text, mid, text, text, text, sub);
    }

    // Cyber: the HUD's yellow sun and moon, white clouds over grey, cyan rain, a red bolt
    public static readonly Palette Cyber = new(CyberHud.Yellow, CyberHud.Yellow, CyberHud.Yellow, CyberHud.Grey, CyberHud.White, CyberHud.Grey,
        CyberHud.Cyan, CyberHud.White, CyberHud.Red, CyberHud.Grey);

    // ---- the models ----

    static readonly Prim[][] _models = Build();

    static Prim[] Model(WxKind k, bool night) => _models[(int)k * 2 + (night ? 1 : 0)];

    static Prim[][] Build()
    {
        var all = new Prim[16][];
        for (int k = 0; k < 8; k++)
            for (int n = 0; n < 2; n++)
            {
                var l = new List<Prim>();
                Make(l, (WxKind)k, n == 1);
                all[k * 2 + n] = l.ToArray();
            }
        return all;
    }

    static void Make(List<Prim> l, WxKind k, bool night)
    {
        switch (k)
        {
            case WxKind.Clear when night:
                l.Add(new(Shape.Crescent, Part.Moon, 0.46f, 0.52f, 0.30f, 0.60f, 0.40f, 0.25f));
                l.Add(new(Shape.Disc, Part.Star, 0.80f, 0.64f, 0.035f, 0));
                l.Add(new(Shape.Disc, Part.Star, 0.74f, 0.22f, 0.025f, 0));
                break;
            case WxKind.Clear:
                Sun(l, 0.5f, 0.5f, 0.2f);
                break;
            case WxKind.Partly:
                if (night)
                {
                    l.Add(new(Shape.Crescent, Part.Moon, 0.36f, 0.38f, 0.22f, 0.46f, 0.30f, 0.18f));
                    l.Add(new(Shape.Disc, Part.Star, 0.80f, 0.17f, 0.025f, 0));
                }
                else Sun(l, 0.36f, 0.36f, 0.15f);
                Cloud(l, Part.Cloud, 0.20f, 0.42f, 0.72f);
                break;
            case WxKind.Cloudy:
                Cloud(l, Part.CloudBack, 0.08f, 0.20f, 0.60f);
                Cloud(l, Part.Cloud, 0.24f, 0.38f, 0.70f);
                break;
            case WxKind.Fog:
                Cloud(l, Part.Cloud, 0.16f, 0.12f, 0.68f);
                l.Add(new(Shape.Stroke, Part.Fog, 0.16f, 0.66f, 0.84f, 0.66f, 0.07f));
                l.Add(new(Shape.Stroke, Part.Fog, 0.26f, 0.77f, 0.90f, 0.77f, 0.07f));
                l.Add(new(Shape.Stroke, Part.Fog, 0.10f, 0.88f, 0.68f, 0.88f, 0.07f));
                break;
            case WxKind.Drizzle:
                Cloud(l, Part.Cloud, 0.14f, 0.14f, 0.72f);
                foreach (var (x, y) in new[] { (0.34f, 0.68f), (0.52f, 0.68f), (0.70f, 0.68f), (0.43f, 0.82f), (0.61f, 0.82f) })
                    l.Add(new(Shape.Stroke, Part.Rain, x, y, x - 0.02f, y + 0.05f, 0.065f));
                break;
            case WxKind.Rain:
                Cloud(l, Part.Cloud, 0.14f, 0.14f, 0.72f);
                foreach (float x in new[] { 0.36f, 0.54f, 0.72f })
                    l.Add(new(Shape.Stroke, Part.Rain, x, 0.66f, x - 0.07f, 0.86f, 0.07f));
                break;
            case WxKind.Snow:
                Cloud(l, Part.Cloud, 0.14f, 0.14f, 0.72f);
                foreach (var (x, y) in new[] { (0.33f, 0.76f), (0.52f, 0.85f), (0.71f, 0.76f) })
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * MathF.PI / 3, dx = 0.07f * MathF.Cos(a), dy = 0.07f * MathF.Sin(a);
                        l.Add(new(Shape.Stroke, Part.Snow, x - dx, y - dy, x + dx, y + dy, 0.04f));
                    }
                break;
            default:      // a storm: a dark cloud and the bolt (two convex quads that make it together)
                Cloud(l, Part.CloudDark, 0.14f, 0.12f, 0.72f);
                l.Add(new(Shape.Quad, Part.Bolt, 0.57f, 0.48f, 0.41f, 0.71f, 0.51f, 0.71f, 0.55f, 0.64f));
                l.Add(new(Shape.Quad, Part.Bolt, 0.55f, 0.64f, 0.51f, 0.71f, 0.44f, 0.94f, 0.65f, 0.64f));
                break;
        }
    }

    // a sun: the disc and eight rays round it
    static void Sun(List<Prim> l, float x, float y, float r)
    {
        l.Add(new(Shape.Disc, Part.Sun, x, y, r, 0));
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4, c = MathF.Cos(a), s = MathF.Sin(a);
            l.Add(new(Shape.Stroke, Part.Sun, x + c * r * 1.45f, y + s * r * 1.45f, x + c * r * 2.05f, y + s * r * 2.05f, r * 0.32f));
        }
    }

    // a cloud s wide from (x, y), 0.6 s tall: a flat bottom with three puffs on it
    static void Cloud(List<Prim> l, Part p, float x, float y, float s)
    {
        l.Add(new(Shape.Bar, p, x, y + 0.32f * s, x + s, y + 0.60f * s, 0.14f * s));
        l.Add(new(Shape.Disc, p, x + 0.26f * s, y + 0.36f * s, 0.20f * s, 0));
        l.Add(new(Shape.Disc, p, x + 0.53f * s, y + 0.27f * s, 0.26f * s, 0));
        l.Add(new(Shape.Disc, p, x + 0.79f * s, y + 0.38f * s, 0.17f * s, 0));
    }

    // ---- smooth: the painter ----

    static readonly Vector2[] _strip = new Vector2[4 * CrescentSteps], _quad = new Vector2[4];
    const int CrescentSteps = 16;

    // the picture in the middle of box (its smaller side), in pal's colours; smooth: anti-aliased even on a retro canvas (XP, 7)
    public static void Draw(Painter p, WxKind k, bool night, RectangleF box, in Palette pal, bool smooth = false)
    {
        float s = MathF.Min(box.Width, box.Height), x0 = box.X + (box.Width - s) / 2, y0 = box.Y + (box.Height - s) / 2;
        if (smooth) p.Smooth(true);
        foreach (var q in Model(k, night))
        {
            uint c = pal.Of(q.P);
            switch (q.S)
            {
                case Shape.Disc:
                    p.Circle(x0 + q.A * s, y0 + q.B * s, q.C * s, c);
                    break;
                case Shape.Bar:
                    p.FillRound(RectangleF.FromLTRB(x0 + q.A * s, y0 + q.B * s, x0 + q.C * s, y0 + q.D * s), c, q.E * s);
                    break;
                case Shape.Stroke:
                    p.RoundLine(x0 + q.A * s, y0 + q.B * s, x0 + q.C * s, y0 + q.D * s, c, MathF.Max(1, q.E * s));
                    break;
                case Shape.Crescent:
                    Crescent(p, x0 + q.A * s, y0 + q.B * s, q.C * s, x0 + q.D * s, y0 + q.E * s, q.F * s, c);
                    break;
                case Shape.Quad:
                    _quad[0] = new(x0 + q.A * s, y0 + q.B * s);
                    _quad[1] = new(x0 + q.C * s, y0 + q.D * s);
                    _quad[2] = new(x0 + q.E * s, y0 + q.F * s);
                    _quad[3] = new(x0 + q.G * s, y0 + q.H * s);
                    p.Polygon(_quad, 4, c);
                    break;
            }
        }
        if (smooth) p.Smooth(false);
    }

    // The disc (ox, oy, r) without the disc (ix, iy, ir): its outer arc (the far side) and the inner arc (the bite) between the two
    // points where the circles cross, joined as a strip of quads - one kept shape, moved where it is drawn
    static void Crescent(Painter p, float ox, float oy, float r, float ix, float iy, float ir, uint c)
    {
        float dx = ix - ox, dy = iy - oy, d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 1e-4f || d >= r + ir || d <= MathF.Abs(r - ir)) { p.Circle(ox, oy, r, c); return; }
        float ux = dx / d, uy = dy / d;
        float a = (r * r - ir * ir + d * d) / (2 * d), h = MathF.Sqrt(MathF.Max(0, r * r - a * a));
        float p1x = ox + a * ux - h * uy, p1y = oy + a * uy + h * ux, p2x = ox + a * ux + h * uy, p2y = oy + a * uy - h * ux;
        float far = MathF.Atan2(-uy, -ux);
        var (t0, dt) = Sweep(MathF.Atan2(p1y - oy, p1x - ox), MathF.Atan2(p2y - oy, p2x - ox), far);
        var (s0, ds) = Sweep(MathF.Atan2(p1y - iy, p1x - ix), MathF.Atan2(p2y - iy, p2x - ix), far);
        Vector2 O(int k) { float t = t0 + dt * k / CrescentSteps; return new(ox + r * MathF.Cos(t), oy + r * MathF.Sin(t)); }
        Vector2 I(int k) { float t = s0 + ds * k / CrescentSteps; return new(ix + ir * MathF.Cos(t), iy + ir * MathF.Sin(t)); }
        for (int k = 0; k < CrescentSteps; k++)
        {
            _strip[4 * k] = O(k);
            _strip[4 * k + 1] = O(k + 1);
            _strip[4 * k + 2] = I(k + 1);
            _strip[4 * k + 3] = I(k);
        }
        p.Quads(_strip, CrescentSteps, c);
    }

    // from angle a0 to a1 the way that passes 'via'
    static (float start, float delta) Sweep(float a0, float a1, float via)
    {
        static float Norm(float a) { a %= MathF.Tau; return a < 0 ? a + MathF.Tau : a; }
        float toVia = Norm(via - a0), toEnd = Norm(a1 - a0);
        return toVia < toEnd ? (a0, toEnd) : (a0, toEnd - MathF.Tau);
    }

    // ---- pixel art: 95 and the Mac ----

    // the sixteen VGA colours of each part: the fill, its edge (0: none - a stroke never has one)
    static (uint fill, uint edge) Vga(Part p) => p switch
    {
        Part.Sun => (0xFFFFFF00, 0xFF808000),
        Part.Moon => (0xFFFFFF00, 0xFF808000),
        Part.Star => (0xFFFFFF00, 0),
        Part.CloudBack => (0xFFC0C0C0, 0xFF808080),
        Part.Cloud => (0xFFFFFFFF, 0xFF000000),
        Part.CloudDark => (0xFF808080, 0xFF000000),
        Part.Rain => (0xFF0000FF, 0),
        Part.Snow => (0xFF008080, 0),
        Part.Bolt => (0xFFFFFF00, 0xFF000000),
        _ => (0xFF808080, 0),
    };

    static readonly Dictionary<(Painter, int), ID2D1Bitmap> _pixels = new();

    // the picture as a size x size icon at x, y (whole pixels, never smoothed)
    public static void Pixel(Painter p, WxKind k, bool night, float x, float y, int size)
    {
        var key = (p, ((int)k * 2 + (night ? 1 : 0)) * 256 + size);
        if (!_pixels.TryGetValue(key, out var bmp)) _pixels[key] = bmp = p.CreateBitmap(Rasterise(k, night, size), size, size);
        p.ImageNearest(bmp, new RectangleF(MathF.Round(x), MathF.Round(y), size, size));
    }

    // every pixel's centre tested against the shapes in their order (the last one wins); a shape's pixel next to one of something
    // under it (or nothing) is its edge
    static uint[] Rasterise(WxKind k, bool night, int n)
    {
        var m = Model(k, night);
        var who = new int[n * n];
        for (int i = 0; i < who.Length; i++) who[i] = -1;
        for (int j = 0; j < m.Length; j++)
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (Inside(m[j], (x + 0.5f) / n, (y + 0.5f) / n, n)) who[y * n + x] = j;
        var px = new uint[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int j = who[y * n + x];
                if (j < 0) continue;
                var q = m[j];
                var (fill, edge) = Vga(q.P);
                bool edged = false;
                if (edge != 0 && q.S != Shape.Stroke)
                    foreach (var (ex, ey) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        int o = ex < 0 || ey < 0 || ex >= n || ey >= n ? -1 : who[ey * n + ex];
                        if (o < 0 || (o < j && m[o].P != q.P)) { edged = true; break; }
                    }
                px[y * n + x] = edged ? edge : fill;
            }
        return px;
    }

    static bool Inside(in Prim q, float x, float y, int n)
    {
        switch (q.S)
        {
            case Shape.Disc: return Sq(x - q.A) + Sq(y - q.B) <= Sq(q.C);
            case Shape.Bar:
            {
                float cx = Math.Clamp(x, q.A + q.E, q.C - q.E), cy = Math.Clamp(y, q.B + q.E, q.D - q.E);
                return x >= q.A && x <= q.C && y >= q.B && y <= q.D && Sq(x - cx) + Sq(y - cy) <= Sq(q.E);
            }
            case Shape.Stroke:
            {
                // at least a pixel wide
                float w = MathF.Max(q.E, 1.05f / n) / 2;
                float vx = q.C - q.A, vy = q.D - q.B, len = vx * vx + vy * vy;
                float t = len <= 0 ? 0 : Math.Clamp(((x - q.A) * vx + (y - q.B) * vy) / len, 0, 1);
                return Sq(x - (q.A + t * vx)) + Sq(y - (q.B + t * vy)) <= w * w;
            }
            case Shape.Crescent:
                return Sq(x - q.A) + Sq(y - q.B) <= Sq(q.C) && Sq(x - q.D) + Sq(y - q.E) > Sq(q.F);
            default:
            {
                Span<float> c = stackalloc float[] { q.A, q.B, q.C, q.D, q.E, q.F, q.G, q.H };
                int sign = 0;
                for (int i = 0; i < 4; i++)
                {
                    float ax = c[2 * i], ay = c[2 * i + 1], bx = c[(2 * i + 2) % 8], by = c[(2 * i + 3) % 8];
                    float cr = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
                    int sg = cr > 0 ? 1 : cr < 0 ? -1 : 0;
                    if (sg == 0) continue;
                    if (sign == 0) sign = sg;
                    else if (sg != sign) return false;
                }
                return true;
            }
        }
    }

    static float Sq(float v) => v * v;
}
