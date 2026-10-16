using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.Direct2D1;

namespace Orbit;

// Minesweeper in the Classic Mac style: the board at winmine's metrics (the same squares, the same face button - the taps land
// where they did), drawn the way the Mac's shareware Mines games drew it: Platinum squares, the numbers in Chicago, the bomb
// of the Mac's system error alert, the Happy Mac as the face (the Sad Mac when you lose), the counters in white wells.
public sealed partial class MacSkin
{
    public static class Mines
    {
        // k black  w white  s platinum  l light  g grey  d dark grey  r red  y spark yellow
        static uint Pal(char c) => c switch
        {
            'k' => 0xFF000000, 'w' => 0xFFFFFFFF, 's' => 0xFFDDDDDD, 'l' => 0xFFEEEEEE, 'g' => 0xFF999999, 'd' => 0xFF666666,
            'r' => 0xFFDD0000, 'y' => 0xFFFFCC00, 'a' => 0xFFBBBBBB, _ => 0,
        };

        // the numbers' colours, 1 to 8 (the Mac's 16 colours)
        static readonly uint[] Num = { 0, 0xFF0000D4, 0xFF006411, 0xFFDD0806, 0xFF000080, 0xFF90713A, 0xFF02ABEA, 0xFF000000, 0xFF808080 };

        // the bomb of "Sorry, a system error occurred"
        static readonly string[] Bomb =
        {
            "................",
            "...........y.y..",
            "..........y.y...",
            "........kk.y.y..",
            ".......k........",
            "......k.........",
            "....kkkkk.......",
            "...kkkkkkk......",
            "..kkwwkkkkk.....",
            "..kwwkkkkkk.....",
            "..kkkkkkkkk.....",
            "..kkkkkkkkk.....",
            "...kkkkkkk......",
            "....kkkkk.......",
            "................",
            "................",
        };

        // the Happy Mac's case and screen (17 x 17); the face goes on the screen
        static readonly string[] Compact =
        {
            "..kkkkkkkkkkkkk..",
            "..kssssssssssssk.",
            "..kskkkkkkkkksdk.",
            "..kskwwwwwwwkadk.",
            "..kskwwwwwwwkadk.",
            "..kskwwwwwwwkadk.",
            "..kskwwwwwwwkadk.",
            "..kskwwwwwwwkadk.",
            "..kskwwwwwwwkadk.",
            "..kskkkkkkkkksdk.",
            "..ksssssssssssdk.",
            "..kssskkkkssssdk.",
            "..ksssssssssssdk.",
            "..kddddddddddddk.",
            "..kkkkkkkkkkkkkk.",
            "...kssssssssssk..",
            "...kkkkkkkkkkkk..",
        };

        // the faces on its screen (x 6..10, y 3..8 inside the white)
        static readonly (int, int)[] Happy = { (7, 4), (10, 4), (7, 5), (10, 5), (9, 5), (8, 6), (9, 6), (6, 7), (11, 7), (7, 8), (8, 8), (9, 8), (10, 8) };
        static readonly (int, int)[] Oh = { (7, 4), (10, 4), (7, 5), (10, 5), (8, 6), (9, 6), (8, 7), (9, 7), (8, 8), (9, 8) };
        static readonly (int, int)[] Sad = { (6, 3), (8, 3), (7, 4), (6, 5), (8, 5), (9, 3), (11, 3), (10, 4), (9, 5), (11, 5), (7, 7), (8, 7), (9, 7), (10, 7), (6, 8), (11, 8) };
        static readonly (int, int)[] Cool = { (6, 4), (7, 4), (8, 4), (9, 4), (10, 4), (11, 4), (6, 5), (7, 5), (10, 5), (11, 5), (6, 7), (11, 7), (7, 8), (8, 8), (9, 8), (10, 8) };

        static void Stamp(uint[] px, int w, string[] map, int ox, int oy, uint only = 0)
        {
            for (int y = 0; y < map.Length; y++)
                for (int x = 0; x < map[y].Length; x++)
                {
                    char ch = map[y][x];
                    if (ch == '.') continue;
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= w || ty * w + tx >= px.Length) continue;
                    px[ty * w + tx] = only != 0 ? only : Pal(ch);
                }
        }

        static void Box(uint[] px, int w, int x0, int y0, int bw, int bh, uint c)
        {
            for (int y = y0; y < y0 + bh; y++) for (int x = x0; x < x0 + bw; x++) px[y * w + x] = c;
        }

        // light top-left, dark bottom-right, n rings
        static void Edge(uint[] px, int w, int x0, int y0, int ew, int eh, uint tl, uint br)
        {
            int l = x0, t = y0, r = x0 + ew - 1, b = y0 + eh - 1;
            for (int x = l; x <= r; x++) { px[t * w + x] = tl; px[b * w + x] = br; }
            for (int y = t; y <= b; y++) { px[y * w + l] = tl; px[y * w + r] = br; }
            px[b * w + l] = br == tl ? tl : 0xFFBBBBBB;
            px[t * w + r] = br == tl ? tl : 0xFFBBBBBB;
        }

        // a Chicago digit (rows 2..10 of its glyph) at x, y
        static void Digit(uint[] px, int w, char ch, int x, int y, uint c)
        {
            if (!ChicagoGlyphs.TryGetValue(ch, out var g)) return;
            for (int r = 2; r < g.Length && r <= 10; r++)
                for (int k = 0; k < g[r].Length; k++)
                    if (g[r][k] != '.') px[(y + r - 2) * w + x + k] = c;
        }

        public static uint[] Square(MineArt.Sq q)
        {
            var px = new uint[256];
            if (q is MineArt.Sq.Hidden or MineArt.Sq.Flag)
            {
                // a Platinum bevel button
                Array.Fill(px, 0xFFDDDDDD);
                Edge(px, 16, 0, 0, 16, 16, 0xFFFFFFFF, 0xFF777777);
                for (int k = 1; k < 15; k++) { px[14 * 16 + k] = 0xFFAAAAAA; px[k * 16 + 14] = 0xFFAAAAAA; }
                px[14 * 16 + 1] = 0xFFDDDDDD; px[1 * 16 + 14] = 0xFFDDDDDD;
                if (q == MineArt.Sq.Flag) StampFlag(px, 0, 16);
                return px;
            }
            Array.Fill(px, q == MineArt.Sq.Hit ? 0xFFDD0000 : 0xFFF2F2F2);
            for (int k = 0; k < 16; k++) { px[k] = 0xFFAAAAAA; px[k * 16] = 0xFFAAAAAA; }
            if (q >= MineArt.Sq.N1 && q <= MineArt.Sq.N8)
            {
                int n = q - MineArt.Sq.N1 + 1;
                Digit(px, 16, (char)('0' + n), 5, 4, Num[n]);
            }
            if (q is MineArt.Sq.Mine or MineArt.Sq.Hit or MineArt.Sq.Wrong) Stamp(px, 16, Bomb, 0, 0);
            if (q == MineArt.Sq.Wrong)
                for (int k = 0; k <= 10; k++)
                {
                    px[(3 + k) * 16 + 3 + k] = px[(3 + k) * 16 + 4 + k] = 0xFFDD0000;
                    px[(3 + k) * 16 + 13 - k] = px[(3 + k) * 16 + 12 - k] = 0xFFDD0000;
                }
            return px;
        }

        // the flag's rows from 'from' to 'to' (a long press: it rises out of its square)
        static void StampFlag(uint[] px, int from, int to)
        {
            var flag = new uint[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    char ch = MineArt.Flag16[y][x];
                    if (ch != '.') flag[y * 16 + x] = ch == 'r' ? 0xFFDD0000 : 0xFF000000;
                }
            for (int r = Math.Max(0, from); r < Math.Min(16, to); r++)
                for (int c = 0; c < 16; c++)
                    if (flag[r * 16 + c] != 0) px[r * 16 + c] = flag[r * 16 + c];
        }

        // the face button (24 x 24): a rounded Platinum button, the Happy Mac on it
        public static uint[] FaceButton(MineArt.Face f, bool down)
        {
            var px = new uint[24 * 24];
            uint fill = down ? 0xFFAAAAAA : 0xFFDDDDDD;
            // a rounded rectangle (QuickDraw's corner steps 3, 2, 1) with a black edge
            static int In(int row) => row is 0 or 23 ? 3 : row is 1 or 22 ? 2 : row is 2 or 21 ? 1 : 0;
            for (int y = 0; y < 24; y++)
            {
                int a = In(y), b = 23 - a;
                for (int x = a; x <= b; x++) px[y * 24 + x] = y is 0 or 23 || x == a || x == b ? 0xFF000000 : fill;
            }
            px[1 * 24 + 2] = px[1 * 24 + 21] = px[22 * 24 + 2] = px[22 * 24 + 21] = 0xFF000000;
            if (!down)
                for (int k = 3; k <= 20; k++)
                {
                    px[1 * 24 + k] = px[k * 24 + 1] = 0xFFFFFFFF;
                    px[22 * 24 + k] = px[k * 24 + 22] = 0xFF888888;
                }
            int o = down ? 1 : 0;
            var face = FaceIcon(f);
            for (int y = 0; y < 17; y++)
                for (int x = 0; x < 17; x++)
                    if (face[y * 17 + x] != 0) px[(4 + o + y) * 24 + 3 + o + x] = face[y * 17 + x];
            return px;
        }

        // a counter: a white well in a black frame, three Chicago digits - twice as big (Scale2x-smoothed) in the game's
        // header (50 x 25), plain on the watch face's tile (28 x 15)
        public const int BigW = 50, BigH = 25, SmallW = 28, SmallH = 15;

        static void Counter(uint[] px, int w, int x, int y, int value, bool big)
        {
            int cw = big ? BigW : SmallW, ch = big ? BigH : SmallH, s = big ? 2 : 1;
            Box(px, w, x, y, cw, ch, 0xFFFFFFFF);
            Edge(px, w, x, y, cw, ch, 0xFF999999, 0xFFFFFFFF);
            Edge(px, w, x + 1, y + 1, cw - 2, ch - 2, 0xFF000000, 0xFF000000);
            string str = MineArt.LedText(value);
            var tmp = new uint[64 * 18];
            int cx = 0;
            foreach (char c in str)
            {
                if (c == '-') { Box(tmp, 64, cx + s, 4 * s, 4 * s, s, 0xFF000000); cx += 7 * s; continue; }
                if (!ChicagoGlyphs.TryGetValue(c, out var g)) continue;
                int gw = 0;
                foreach (var row in g) gw = Math.Max(gw, row.Length);
                var rows = new string[9];
                for (int r = 0; r < 9; r++) rows[r] = (r + 2 < g.Length ? g[r + 2] : "").PadRight(gw, '.');
                if (big) rows = Scale2(rows);
                for (int r = 0; r < rows.Length; r++)
                    for (int k = 0; k < rows[r].Length; k++)
                        if (rows[r][k] != '.') tmp[r * 64 + cx + k] = 0xFF000000;
                cx += gw * s + s;
            }
            cx -= s;
            int ox = x + (cw - cx) / 2, oy = y + (ch - 9 * s) / 2;
            for (int yy = 0; yy < 9 * s; yy++)
                for (int xx = 0; xx < cx; xx++)
                    if (tmp[yy * 64 + xx] != 0) px[(oy + yy) * w + ox + xx] = tmp[yy * 64 + xx];
        }

        static readonly Dictionary<MineArt.Sq, uint[]> _sq = new();
        static uint[] Sq(MineArt.Sq q) => _sq.TryGetValue(q, out var v) ? v : _sq[q] = Square(q);

        // the whole inside at winmine's metrics (MineArt.ClientW x ClientH)
        public static uint[] Client(MinesGame g, int secs, MineArt.Face face, bool faceDown, Func<int, MineArt.Sq> sq, int riseCell, int riseRows)
        {
            int w = MineArt.ClientW(g.W), h = MineArt.ClientH(g.H);
            var px = new uint[w * h];
            Array.Fill(px, 0xFFDDDDDD);
            Edge(px, w, 0, 0, w, h, 0xFF000000, 0xFF000000);
            Edge(px, w, 1, 1, w - 2, h - 2, 0xFFFFFFFF, 0xFF999999);
            // the header: an inset placard
            Edge(px, w, 9, 9, w - 18, 37, 0xFF999999, 0xFFFFFFFF);
            Edge(px, w, 10, 10, w - 20, 35, 0xFFFFFFFF, 0xFF999999);
            Counter(px, w, 14, 15, g.MinesLeft, true);
            Counter(px, w, w - 14 - BigW, 15, secs, true);
            int fx = MineArt.FaceX(g.W);
            var fb = FaceButton(face, faceDown);
            for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) if (fb[y * 24 + x] != 0) px[(MineArt.FaceY + y) * w + fx + x] = fb[y * 24 + x];
            // the board in a black frame
            Edge(px, w, 9, 52, w - 18, g.H * 16 + 6, 0xFF999999, 0xFFFFFFFF);
            Edge(px, w, 10, 53, w - 20, g.H * 16 + 4, 0xFF000000, 0xFF000000);
            Box(px, w, 11, 54, w - 22, 1, 0xFF000000);
            for (int i = 0; i < g.Cells; i++)
            {
                int x = MineArt.BoardX + i % g.W * 16, y = MineArt.BoardY + i / g.W * 16;
                var s = Sq(sq(i));
                if (i == riseCell && riseRows > 0)
                {
                    s = (uint[])s.Clone();
                    StampFlag(s, MineArt.FlagBottom - riseRows, MineArt.FlagBottom);
                }
                for (int r = 0; r < 16; r++) Array.Copy(s, r * 16, px, (y + r) * w + x, 16);
            }
            return px;
        }

        static ID2D1Bitmap _bmp;
        static Painter _bmpP;
        static object _key;

        public static void Draw(Painter p, MinesGame g, RectangleF cl, int secs, MineArt.Face face, bool faceDown, Func<int, MineArt.Sq> sq,
            int riseCell, int riseRows)
        {
            // the squares' pictures are part of the key through the game's version and the press
            var key = (g, g.Version, secs, face, faceDown, riseCell, riseRows, Signature(g, sq));
            if (_bmp == null || _bmpP != p || !key.Equals(_key))
            {
                _bmp?.Dispose();
                _bmp = p.CreateBitmap(Client(g, secs, face, faceDown, sq, riseCell, riseRows), MineArt.ClientW(g.W), MineArt.ClientH(g.H));
                _bmpP = p;
                _key = key;
            }
            p.ImageNearest(_bmp, cl);
        }

        static int Signature(MinesGame g, Func<int, MineArt.Sq> sq)
        {
            int h = 17;
            for (int i = 0; i < g.Cells; i++) h = h * 31 + (int)sq(i);
            return h;
        }

        // ---- the small pieces for the watch face's tile ----

        static readonly Dictionary<(Painter, string), ID2D1Bitmap> _cache = new();

        static ID2D1Bitmap Get(Painter p, string key, Func<uint[]> make, int w, int h)
        {
            if (_cache.TryGetValue((p, key), out var b)) return b;
            if (_cache.Count > 200) { foreach (var v in _cache.Values) v.Dispose(); _cache.Clear(); }
            b = p.CreateBitmap(make(), w, h);
            _cache[(p, key)] = b;
            return b;
        }

        public static ID2D1Bitmap CounterBmp(Painter p, int value) => Get(p, "n" + MineArt.LedText(value), () =>
        {
            var px = new uint[SmallW * SmallH];
            Counter(px, SmallW, 0, 0, value, false);
            return px;
        }, SmallW, SmallH);

        public static ID2D1Bitmap FaceBmp(Painter p, MineArt.Face f) => Get(p, "f" + (int)f, () => FaceButton(f, false), 24, 24);

        // the Happy Mac alone (17 x 17)
        public static ID2D1Bitmap FaceIconBmp(Painter p, MineArt.Face f) => Get(p, "i" + (int)f, () => FaceIcon(f), 17, 17);

        static uint[] FaceIcon(MineArt.Face f)
        {
            var face = new uint[17 * 17];
            Stamp(face, 17, Compact, 0, 0);
            var feats = f switch { MineArt.Face.Oh => Oh, MineArt.Face.Dead => Sad, MineArt.Face.Cool => Cool, _ => Happy };
            foreach (var (x, y) in feats) face[y * 17 + x] = 0xFF000000;
            return face;
        }

        // a small picture of the board: squares of cs pixels, a pixel of each number's colour
        public static uint[] Mini(MinesGame g, int cs)
        {
            int w = g.W * cs, h = g.H * cs;
            var px = new uint[w * h];
            Array.Fill(px, 0xFFDDDDDD);
            int dot = cs >= 7 ? 3 : 2, d0 = 1 + (cs - 1 - dot) / 2;
            for (int i = 0; i < g.Cells; i++)
            {
                int x0 = i % g.W * cs, y0 = i / g.W * cs;
                bool open = g.IsOpen(i), flag = g.IsFlag(i), mine = g.State == MinesGame.Phase.Lost && g.IsMine(i);
                void Dot(uint c) { for (int y = 0; y < dot; y++) for (int x = 0; x < dot; x++) px[(y0 + d0 + y) * w + x0 + d0 + x] = c; }
                if (!open && !(mine && !flag))
                {
                    Edge(px, w, x0, y0, cs, cs, 0xFFFFFFFF, 0xFF777777);
                    if (flag) Dot(0xFFDD0000);
                    continue;
                }
                Box(px, w, x0, y0, cs, cs, i == g.Hit ? 0xFFDD0000 : 0xFFF2F2F2);
                for (int k = 0; k < cs; k++) { px[y0 * w + x0 + k] = 0xFFAAAAAA; px[(y0 + k) * w + x0] = 0xFFAAAAAA; }
                if (mine) Dot(0xFF000000);
                else if (g.Adj(i) > 0) Dot(Num[g.Adj(i)]);
            }
            return px;
        }
    }
}
