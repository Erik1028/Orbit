using System;
using System.Collections.Generic;
using Vortice.Direct2D1;

namespace Orbit;

// Minesweeper, the game: a board of W x H squares with some mines. The first square opened is never a mine and never
// next to one (so it opens an area); an empty square opens its neighbours (flood fill); a number whose flags all sit
// around it opens the rest of its neighbours (chord). The time runs from the first click to the end.
// Plays the same in both styles; the watch keeps one game per board size in memory (WristMines.cs).
public sealed class MinesGame
{
    public enum Phase { Ready, Playing, Won, Lost }

    public readonly int W, H, Count;
    readonly bool[] _mine;
    readonly byte[] _adj;
    readonly byte[] _st;                    // 0 hidden, 1 open, 2 flagged
    Random _rng;
    double _start, _end;

    public Phase State { get; private set; }
    public bool Placed { get; private set; }
    public int Flags { get; private set; }
    public int Opened { get; private set; }
    public int Hit { get; private set; } = -1;          // the mine that went off
    public int Version { get; private set; }            // goes up on every change (the 95 pictures are made again)

    public MinesGame(int w, int h, int mines, int? seed = null)
    {
        W = w; H = h; Count = Math.Clamp(mines, 1, w * h - 1);
        _mine = new bool[w * h];
        _adj = new byte[w * h];
        _st = new byte[w * h];
        _rng = seed is { } s ? new Random(s) : new Random();
    }

    public int Cells => W * H;
    public bool Over => State is Phase.Won or Phase.Lost;
    public int MinesLeft => Count - Flags;                // the counter: negative when there are more flags than mines
    public bool IsMine(int i) => Placed && _mine[i];
    public bool IsOpen(int i) => _st[i] == 1;
    public bool IsFlag(int i) => _st[i] == 2;
    public int Adj(int i) => _adj[i];

    // the seconds shown: 0 before the first click, stopped at the end
    public double Seconds(double now) => State == Phase.Ready ? 0 : Math.Max(0, (Over ? _end : now) - _start);

    public void Reset(int? seed = null)
    {
        Array.Clear(_mine);
        Array.Clear(_adj);
        Array.Clear(_st);
        if (seed is { } s) _rng = new Random(s);
        State = Phase.Ready;
        Placed = false;
        Flags = Opened = 0;
        Hit = -1;
        Version++;
    }

    // a click on a square: a hidden one opens, an open number chords; a flag protects its square
    public bool Click(int i, double now)
    {
        if (Over || i < 0 || i >= Cells || _st[i] == 2) return false;
        return _st[i] == 1 ? Chord(i, now) : Open(i, now);
    }

    public bool Open(int i, double now)
    {
        if (Over || i < 0 || i >= Cells || _st[i] != 0) return false;
        if (!Placed) Place(i);
        if (State == Phase.Ready) { State = Phase.Playing; _start = now; }
        if (_mine[i]) { Lose(i, now); return true; }
        Flood(i);
        if (Opened == Cells - Count) Win(now);
        Version++;
        return true;
    }

    // an open number with as many flags around it as it says: the other hidden neighbours open (a wrong flag loses)
    public bool Chord(int i, double now)
    {
        if (Over || i < 0 || i >= Cells || _st[i] != 1 || _adj[i] == 0) return false;
        int flags = 0, hidden = 0;
        foreach (int n in Around(i))
        {
            if (_st[n] == 2) flags++;
            else if (_st[n] == 0) hidden++;
        }
        if (flags != _adj[i] || hidden == 0) return false;
        int hit = -1;
        foreach (int n in Around(i))
        {
            if (_st[n] != 0) continue;
            if (_mine[n]) { if (hit < 0) hit = n; continue; }
            Flood(n);
        }
        if (hit >= 0) Lose(hit, now);
        else if (Opened == Cells - Count) Win(now);
        Version++;
        return true;
    }

    public bool ToggleFlag(int i)
    {
        if (Over || i < 0 || i >= Cells || _st[i] == 1) return false;
        if (_st[i] == 2) { _st[i] = 0; Flags--; }
        else { _st[i] = 2; Flags++; }
        Version++;
        return true;
    }

    // the squares around i (3 to 8 of them)
    public IEnumerable<int> Around(int i)
    {
        int x = i % W, y = i / W;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < W && ny < H) yield return ny * W + nx;
            }
    }

    // the mines, away from the first square and its neighbours (when the board has room for that)
    void Place(int first)
    {
        var keep = new HashSet<int>(Around(first)) { first };
        var free = new List<int>(Cells);
        for (int c = 0; c < Cells; c++) if (!keep.Contains(c)) free.Add(c);
        if (free.Count < Count)
        {
            free.Clear();
            for (int c = 0; c < Cells; c++) if (c != first) free.Add(c);
        }
        for (int k = 0; k < Count; k++)
        {
            int j = k + _rng.Next(free.Count - k);
            (free[k], free[j]) = (free[j], free[k]);
            _mine[free[k]] = true;
        }
        Count3();
    }

    void Count3()
    {
        for (int c = 0; c < Cells; c++)
        {
            int n = 0;
            foreach (int a in Around(c)) if (_mine[a]) n++;
            _adj[c] = (byte)n;
        }
        Placed = true;
    }

    // opens i and, from every empty square reached, its hidden neighbours (flags stay)
    void Flood(int i)
    {
        var todo = new Stack<int>();
        todo.Push(i);
        while (todo.Count > 0)
        {
            int c = todo.Pop();
            if (_st[c] != 0 || _mine[c]) continue;
            _st[c] = 1;
            Opened++;
            if (_adj[c] != 0) continue;
            foreach (int n in Around(c)) if (_st[n] == 0) todo.Push(n);
        }
    }

    void Lose(int i, double now)
    {
        _st[i] = 1;
        Hit = i;
        State = Phase.Lost;
        _end = now;
        Version++;
    }

    // won: every mine gets its flag (the counter shows 0)
    void Win(double now)
    {
        State = Phase.Won;
        _end = now;
        for (int c = 0; c < Cells; c++)
            if (_mine[c] && _st[c] != 2) { _st[c] = 2; Flags++; }
    }

    // ---- tests (--snapshot): a board laid out by hand, a clock started earlier ----

    public void PlaceForTest(IEnumerable<int> mines)
    {
        Reset();
        foreach (int m in mines) _mine[m] = true;
        Count3();
    }

    public void StartedForTest(double start) => _start = start;
}

// The Windows 95 Minesweeper's pictures, pixel for pixel: the 16 x 16 squares (raised, open with the grey grid, the
// numbers in their colours, the flag, the mine, the red one that went off, the crossed-out wrong flag), the red
// seven-segment digits on black and the yellow face (normal, 'o' while a square is pressed, dead, sunglasses).
// Letters as in W95Icons: k black w white s silver g grey r red m maroon b blue e green n navy t teal y yellow.
public static class MineArt
{
    public static uint Pal(char c) => c switch
    {
        'k' => 0xFF000000, 'w' => 0xFFFFFFFF, 's' => 0xFFC0C0C0, 'g' => 0xFF808080, 'n' => 0xFF000080,
        'b' => 0xFF0000FF, 't' => 0xFF008080, 'r' => 0xFFFF0000, 'm' => 0xFF800000, 'y' => 0xFFFFFF00,
        'e' => 0xFF008000, 'o' => 0xFF808000, _ => 0,
    };

    // the numbers' colours, 1 to 8
    public static readonly uint[] NumberColor =
        { 0, 0xFF0000FF, 0xFF008000, 0xFFFF0000, 0xFF000080, 0xFF800000, 0xFF008080, 0xFF000000, 0xFF808080 };

    public static readonly string[] Mine16 =
    {
        "................",
        "................",
        "........k.......",
        "........k.......",
        "....k.kkkkk.k...",
        ".....kkkkkkk....",
        "....kkwwkkkkk...",
        "....kkwwkkkkk...",
        "..kkkkkkkkkkkkk.",
        "....kkkkkkkkk...",
        "....kkkkkkkkk...",
        ".....kkkkkkk....",
        "....k.kkkkk.k...",
        "........k.......",
        "........k.......",
        "................",
    };

    public static readonly string[] Flag16 =
    {
        "................",
        "................",
        "................",
        "......rrr.......",
        "....rrrrr.......",
        "...rrrrrr.......",
        "....rrrrr.......",
        "......rrr.......",
        "........k.......",
        "........k.......",
        "......kkkkk.....",
        "....kkkkkkkkk...",
        "....kkkkkkkkk...",
        "................",
        "................",
        "................",
    };

    // the flag's rows (for the long press: it rises out of the square)
    public const int FlagTop = 3, FlagBottom = 13;

    static readonly string[][] Digits =
    {
        null,
        new[] { "...kk...", "..kkk...", ".kkkk...", "kkkkk...", "...kk...", "...kk...", "...kk...", "...kk...", "kkkkkkkk", "kkkkkkkk" },
        new[] { ".kkkkkk.", "kkkkkkkk", "kk....kk", "......kk", "....kkk.", "..kkk...", ".kkk....", "kkk.....", "kkkkkkkk", "kkkkkkkk" },
        new[] { "kkkkkkk.", "kkkkkkkk", "......kk", "......kk", "..kkkkk.", "..kkkkk.", "......kk", "......kk", "kkkkkkkk", "kkkkkkk." },
        new[] { ".....kk.", "....kkk.", "...kkkk.", "..kk.kk.", ".kk..kk.", "kkkkkkkk", "kkkkkkkk", ".....kk.", ".....kk.", ".....kk." },
        new[] { "kkkkkkkk", "kkkkkkkk", "kk......", "kk......", "kkkkkkk.", "kkkkkkkk", "......kk", "......kk", "kkkkkkkk", "kkkkkkk." },
        new[] { ".kkkkkk.", "kkkkkkk.", "kk......", "kk......", "kkkkkkk.", "kkkkkkkk", "kk....kk", "kk....kk", "kkkkkkkk", ".kkkkkk." },
        new[] { "kkkkkkkk", "kkkkkkkk", "......kk", ".....kk.", ".....kk.", "....kk..", "....kk..", "...kk...", "...kk...", "...kk..." },
        new[] { ".kkkkkk.", "kkkkkkkk", "kk....kk", "kk....kk", ".kkkkkk.", ".kkkkkk.", "kk....kk", "kk....kk", "kkkkkkkk", ".kkkkkk." },
    };

    public enum Sq { Hidden, Open, N1, N2, N3, N4, N5, N6, N7, N8, Flag, Mine, Hit, Wrong }

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

    // n rings of light top-left / dark bottom-right, the two meeting on the diagonal at the other corners (1995's 3D edge)
    public static void Edge(uint[] px, int w, int x0, int y0, int ew, int eh, int n, uint tl, uint br)
    {
        for (int i = 0; i < n; i++)
        {
            int l = x0 + i, t = y0 + i, r = x0 + ew - 1 - i, b = y0 + eh - 1 - i;
            for (int x = l; x < r; x++) px[t * w + x] = tl;
            for (int y = t; y < b; y++) px[y * w + l] = tl;
            for (int x = l; x <= r; x++) px[b * w + x] = br;
            for (int y = t; y <= b; y++) px[y * w + r] = br;
        }
    }

    public static uint[] Square(Sq s)
    {
        var px = new uint[256];
        Array.Fill(px, 0xFFC0C0C0);
        if (s is Sq.Hidden or Sq.Flag)
        {
            Edge(px, 16, 0, 0, 16, 16, 2, 0xFFFFFFFF, 0xFF808080);
            if (s == Sq.Flag) Stamp(px, 16, Flag16, 0, 0);
            return px;
        }
        if (s == Sq.Hit)
            for (int y = 1; y < 16; y++) for (int x = 1; x < 16; x++) px[y * 16 + x] = 0xFFFF0000;
        for (int k = 0; k < 16; k++) { px[k] = 0xFF808080; px[k * 16] = 0xFF808080; }
        if (s >= Sq.N1 && s <= Sq.N8)
        {
            int n = s - Sq.N1 + 1;
            Stamp(px, 16, Digits[n], 4, 3, NumberColor[n]);
        }
        if (s is Sq.Mine or Sq.Hit or Sq.Wrong) Stamp(px, 16, Mine16, 0, 0);
        if (s == Sq.Wrong)
            for (int k = 0; k <= 10; k++)
            {
                // a red X over the mine, two pixels thick
                px[(3 + k) * 16 + 3 + k] = px[(3 + k) * 16 + 4 + k] = 0xFFFF0000;
                px[(3 + k) * 16 + 13 - k] = px[(3 + k) * 16 + 12 - k] = 0xFFFF0000;
            }
        return px;
    }

    // ---- the seven-segment digits: 13 x 23, red on black, the unlit segments dark red ----
    // segments a b c d e f g; '-' lights g only; ' ' none
    static readonly string[] Lit = { "abcdef", "bc", "abged", "abgcd", "fgbc", "afgcd", "afgedc", "abc", "abcdefg", "abcdfg" };

    static bool InSeg(char seg, int x, int y) => seg switch
    {
        'a' => y >= 1 && y <= 3 && x >= y + 1 && x <= 11 - y,
        'g' => (y == 11 && x >= 2 && x <= 10) || ((y == 10 || y == 12) && x >= 3 && x <= 9),
        'd' => y >= 19 && y <= 21 && x >= 23 - y && x <= y - 11,
        'f' => x >= 1 && x <= 3 && y >= x + 1 && y <= 11 - x,
        'b' => x >= 9 && x <= 11 && y >= 13 - x && y <= x - 1,
        'e' => x >= 1 && x <= 3 && y >= x + 11 && y <= 21 - x,
        'c' => x >= 9 && x <= 11 && y >= 23 - x && y <= x + 9,
        _ => false,
    };

    public static uint[] Led(char ch)
    {
        var px = new uint[13 * 23];
        Array.Fill(px, 0xFF000000);
        string lit = ch == '-' ? "g" : ch >= '0' && ch <= '9' ? Lit[ch - '0'] : "";
        foreach (char seg in "abcdefg")
            for (int y = 0; y < 23; y++)
                for (int x = 0; x < 13; x++)
                    if (InSeg(seg, x, y)) px[y * 13 + x] = lit.IndexOf(seg) >= 0 ? 0xFFFF0000 : 0xFF800000;
        return px;
    }

    // ---- the face: 17 x 17, yellow with a black outline ----
    public enum Face { Smile, Oh, Dead, Cool }

    static readonly string[] FaceDisc =
    {
        "......kkkkk......",
        "....kkyyyyykk....",
        "...kyyyyyyyyyk...",
        "..kyyyyyyyyyyyk..",
        ".kyyyyyyyyyyyyyk.",
        ".kyyyyyyyyyyyyyk.",
        "kyyyyyyyyyyyyyyyk",
        "kyyyyyyyyyyyyyyyk",
        "kyyyyyyyyyyyyyyyk",
        "kyyyyyyyyyyyyyyyk",
        "kyyyyyyyyyyyyyyyk",
        ".kyyyyyyyyyyyyyk.",
        ".kyyyyyyyyyyyyyk.",
        "..kyyyyyyyyyyyk..",
        "...kyyyyyyyyyk...",
        "....kkyyyyykk....",
        "......kkkkk......",
    };

    static readonly (int x, int y)[] Eyes = { (5, 5), (6, 5), (5, 6), (6, 6), (10, 5), (11, 5), (10, 6), (11, 6) };
    static readonly (int x, int y)[] Smile = { (4, 10), (12, 10), (5, 11), (11, 11), (6, 12), (7, 12), (8, 12), (9, 12), (10, 12) };
    static readonly (int x, int y)[] OhMouth = { (7, 9), (8, 9), (9, 9), (6, 10), (10, 10), (6, 11), (10, 11), (7, 12), (8, 12), (9, 12) };
    static readonly (int x, int y)[] XEyes =
        { (4, 4), (6, 4), (5, 5), (4, 6), (6, 6), (10, 4), (12, 4), (11, 5), (10, 6), (12, 6) };
    static readonly (int x, int y)[] Frown = { (6, 11), (7, 11), (8, 11), (9, 11), (10, 11), (5, 12), (11, 12), (4, 13), (12, 13) };
    static readonly (int x, int y)[] Shades =
    {
        (2, 5), (3, 5), (4, 5), (5, 5), (6, 5), (7, 5), (8, 5), (9, 5), (10, 5), (11, 5), (12, 5), (13, 5), (14, 5),
        (3, 6), (4, 6), (5, 6), (6, 6), (7, 6), (9, 6), (10, 6), (11, 6), (12, 6), (13, 6),
        (4, 7), (5, 7), (6, 7), (10, 7), (11, 7), (12, 7),
    };

    public static uint[] FacePixels(Face f)
    {
        var px = new uint[17 * 17];
        Stamp(px, 17, FaceDisc, 0, 0);
        var feats = f switch
        {
            Face.Oh => new[] { Eyes, OhMouth },
            Face.Dead => new[] { XEyes, Frown },
            Face.Cool => new[] { Shades, Smile },
            _ => new[] { Eyes, Smile },
        };
        foreach (var set in feats)
            foreach (var (x, y) in set) px[y * 17 + x] = 0xFF000000;
        return px;
    }

    // the face button, 24 x 24: raised (held: pushed in - flat, a grey line along its top and left, the face a pixel lower
    // right); the window draws a 1 px grey outline round it
    public static uint[] FaceButton(Face f, bool down)
    {
        var px = new uint[24 * 24];
        Array.Fill(px, 0xFFC0C0C0);
        if (down)
            for (int k = 0; k < 24; k++) px[k] = px[k * 24] = 0xFF808080;
        else Edge(px, 24, 0, 0, 24, 24, 2, 0xFFFFFFFF, 0xFF808080);
        Blit(px, 24, FacePixels(f), 17, 17, down ? 4 : 3, down ? 4 : 3);
        return px;
    }

    // src (sw x sh, 0 = transparent) onto dst at x, y
    static void Blit(uint[] dst, int dw, uint[] src, int sw, int sh, int x, int y)
    {
        for (int r = 0; r < sh; r++)
            for (int c = 0; c < sw; c++)
            {
                uint v = src[r * sw + c];
                if (v != 0) dst[(y + r) * dw + x + c] = v;
            }
    }

    // ---- the window's inside (winmine.exe's client area) at its real metrics: 12 px either side of the squares, 12 under
    // them; the header box at (9, 9), 37 high, 2 px sunken; the mines counter at (16, 15) and the time 57 px from the right
    // (41 x 25 with their 1 px sunken edge); the face button centred at y 16 in a 1 px grey outline; the board's 3 px sunken
    // frame at (9, 52), the squares from (12, 55). Beginner: 168 x 211. ----
    public const int BoardX = 12, BoardY = 55, FaceY = 16;
    public static int ClientW(int cols) => 24 + cols * 16;
    public static int ClientH(int rows) => 67 + rows * 16;
    public static int FaceX(int cols) => (ClientW(cols) - 24) / 2;

    static uint[][] _squares, _leds;
    static uint[] SquarePx(Sq s) => (_squares ??= new uint[(int)Sq.Wrong + 1][])[(int)s] ??= Square(s);
    static uint[] LedPx(char c)
    {
        _leds ??= new uint[12][];
        int k = c == '-' ? 10 : c >= '0' && c <= '9' ? c - '0' : 11;
        return _leds[k] ??= Led(c);
    }

    public static string LedText(int value) =>
        value < 0 ? "-" + Math.Min(99, -value).ToString("00") : Math.Min(999, value).ToString("000");

    static void LedAt(uint[] px, int w, int x, int y, int value)
    {
        Edge(px, w, x, y, 41, 25, 1, 0xFF808080, 0xFFFFFFFF);
        string s = LedText(value);
        for (int k = 0; k < 3; k++) Blit(px, w, LedPx(s[k]), 13, 23, x + 1 + 13 * k, y + 1);
    }

    // the whole inside: sq gives each square's picture; rise: a square whose flag shows only its lowest 'rows' rows (a long
    // press: the flag rises out of it)
    public static uint[] Client(MinesGame g, int secs, Face face, bool faceDown, Func<int, Sq> sq, int riseCell, int riseRows)
    {
        int w = ClientW(g.W), h = ClientH(g.H);
        var px = new uint[w * h];
        Array.Fill(px, 0xFFC0C0C0);
        Edge(px, w, 0, 0, w, h, 3, 0xFFFFFFFF, 0xFF808080);
        Edge(px, w, 9, 9, w - 18, 37, 2, 0xFF808080, 0xFFFFFFFF);
        LedAt(px, w, 16, 15, g.MinesLeft);
        LedAt(px, w, w - 57, 15, secs);
        int fx = FaceX(g.W);
        Edge(px, w, fx - 1, FaceY - 1, 26, 26, 1, 0xFF808080, 0xFF808080);
        Blit(px, w, FaceButton(face, faceDown), 24, 24, fx, FaceY);
        Edge(px, w, 9, 52, w - 18, g.H * 16 + 6, 3, 0xFF808080, 0xFFFFFFFF);
        for (int i = 0; i < g.Cells; i++)
        {
            int x = BoardX + i % g.W * 16, y = BoardY + i / g.W * 16;
            Blit(px, w, SquarePx(sq(i)), 16, 16, x, y);
            if (i != riseCell || riseRows <= 0) continue;
            var flag = new uint[256];
            Stamp(flag, 16, Flag16, 0, 0);
            for (int r = FlagBottom - riseRows; r < FlagBottom; r++)
                for (int c = 0; c < 16; c++)
                    if (flag[r * 16 + c] != 0) px[(y + r) * w + x + c] = flag[r * 16 + c];
        }
        return px;
    }

    // ---- a small picture of the board (the watch face's 95 widget): squares of cs pixels, a pixel of each number's colour ----
    public static uint[] Mini(MinesGame g, int cs)
    {
        int w = g.W * cs, h = g.H * cs;
        var px = new uint[w * h];
        Array.Fill(px, 0xFFC0C0C0);
        int dot = cs >= 7 ? 3 : 2, d0 = 1 + (cs - 1 - dot) / 2;
        for (int i = 0; i < g.Cells; i++)
        {
            int x0 = i % g.W * cs, y0 = i / g.W * cs;
            bool open = g.IsOpen(i), flag = g.IsFlag(i), mine = g.State == MinesGame.Phase.Lost && g.IsMine(i);
            void Dot(uint c) { for (int y = 0; y < dot; y++) for (int x = 0; x < dot; x++) px[(y0 + d0 + y) * w + x0 + d0 + x] = c; }
            if (!open && !(mine && !flag))
            {
                Edge(px, w, x0, y0, cs, cs, 1, 0xFFFFFFFF, 0xFF808080);
                if (flag) Dot(0xFFFF0000);
                continue;
            }
            for (int k = 0; k < cs; k++) { px[y0 * w + x0 + k] = 0xFF808080; px[(y0 + k) * w + x0] = 0xFF808080; }
            if (i == g.Hit)
                for (int y = 1; y < cs; y++) for (int x = 1; x < cs; x++) px[(y0 + y) * w + x0 + x] = 0xFFFF0000;
            if (mine) Dot(0xFF000000);
            else if (g.Adj(i) > 0) Dot(NumberColor[g.Adj(i)]);
        }
        return px;
    }

    // ---- bitmaps, made once per painter ----
    static readonly Dictionary<(Painter, string), ID2D1Bitmap> _cache = new();

    static ID2D1Bitmap Get(Painter p, string key, Func<uint[]> make, int w, int h)
    {
        if (_cache.TryGetValue((p, key), out var b)) return b;
        b = p.CreateBitmap(make(), w, h);
        _cache[(p, key)] = b;
        return b;
    }

    public static ID2D1Bitmap SquareBmp(Painter p, Sq s) => Get(p, "sq" + (int)s, () => Square(s), 16, 16);
    public static ID2D1Bitmap LedBmp(Painter p, char c) => Get(p, "led" + c, () => Led(c), 13, 23);
    public static ID2D1Bitmap FaceBmp(Painter p, Face f) => Get(p, "face" + (int)f, () => FacePixels(f), 17, 17);
    public static ID2D1Bitmap ButtonBmp(Painter p, Face f, bool down) => Get(p, "btn" + (int)f + (down ? "d" : "u"), () => FaceButton(f, down), 24, 24);
    public static ID2D1Bitmap FlagBmp(Painter p) => Get(p, "flag", () =>
    {
        var px = new uint[256];
        Stamp(px, 16, Flag16, 0, 0);
        return px;
    }, 16, 16);
}
