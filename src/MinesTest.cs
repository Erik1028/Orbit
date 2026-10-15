using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Orbit;

// --snapshot's Minesweeper part. The rules on seeded and hand-laid boards (the first click safe and opening an area, flood
// fill, chord, the counter, the clock, win and loss), the laser's ways through the watch's own pointer (a tap opens, a held
// square flags, flag mode, a chord, the face starts again), and the pictures: mines_face_<state>.png (the widget at every
// size), mines_open_<state>.png (the opened game) for new / mid (flags placed) / lost / won, a long press half-way
// (mines_open_hold), the 16 x 16 board (modern), all of them on mines_sheet.png. tag: "" modern, "_95" the 95 style.
static class MinesTools
{
    const int Seed = 5;

    public static void Run(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir, string tag)
    {
        var wcfg = cfg.Data.Wrist;
        string face0 = wcfg.Face, bg0 = wcfg.Background;
        var widgets0 = wcfg.Widgets;
        if (!Theme.Classic) Rules();
        try
        {
            Pointer(wrist, data, wcfg, tag);
            Pictures(wrist, data, wcfg, gpu, dir, tag);
        }
        finally
        {
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
            wrist.MinesResetAll();
            wcfg.Face = face0;
            wcfg.Background = bg0;
            wcfg.Widgets = widgets0;
            wrist.Settle();
        }
    }

    static int Idx(int x, int y, int w = 9) => y * w + x;

    // every open empty square has all its neighbours open (flags aside), and no mine is open
    static bool FloodComplete(MinesGame g)
    {
        for (int i = 0; i < g.Cells; i++)
        {
            if (!g.IsOpen(i)) continue;
            if (g.IsMine(i) && i != g.Hit) return false;
            if (g.Adj(i) == 0 && g.Around(i).Any(n => !g.IsOpen(n) && !g.IsFlag(n))) return false;
        }
        return true;
    }

    static void Rules()
    {
        // the first click: never a mine, never next to one - so it opens an area - on both boards, anywhere (corners too)
        int n = 0, safe = 0, flood = 0;
        foreach (var (w, h, m, seeds) in new[] { (9, 9, 10, 300), (16, 16, 40, 100) })
            for (int s = 1; s <= seeds; s++)
            {
                n++;
                var g = new MinesGame(w, h, m, s);
                int at = s * 7 % g.Cells;
                g.Open(at, 100);
                if (g.State != MinesGame.Phase.Lost && g.IsOpen(at) && g.Adj(at) == 0 && g.Opened > 1) safe++;
                if (FloodComplete(g)) flood++;
            }
        Log.Info($"mines: the first click safe and opening an area {safe} of {n} (want {n} of {n}); flood fill complete {flood} of {n} (want {n} of {n})");

        // flood fill on a hand-laid board: nine mines down the right column - a click at the top-left opens the other 72 squares,
        // and that is every safe one: won, every mine flagged, the counter at 0, the clock stopped
        var f = new MinesGame(9, 9, 9);
        f.PlaceForTest(Enumerable.Range(0, 9).Select(y => Idx(8, y)));
        f.Open(0, 100);
        Log.Info($"mines: flood fill from a corner opened {f.Opened} (want 72), won {f.State == MinesGame.Phase.Won} (want True), mines left {f.MinesLeft} " +
                 $"(want 0), time {f.Seconds(100):0} s and still {f.Seconds(500):0} s later (want 0, 0)");

        // chord: mines at (0,0) and (2,0); a click in the middle opens all but the top-left three; (1,1) is a 2
        MinesGame Two()
        {
            var g = new MinesGame(9, 9, 2);
            g.PlaceForTest(new[] { Idx(0, 0), Idx(2, 0) });
            g.Open(Idx(4, 4), 100);
            return g;
        }
        var c = Two();
        int open0 = c.Opened;
        c.ToggleFlag(Idx(0, 0));
        bool early = c.Chord(Idx(1, 1), 101);                  // one flag of two: nothing
        c.ToggleFlag(Idx(2, 0));
        bool done = c.Chord(Idx(1, 1), 102);
        Log.Info($"mines: chord - a click opened {open0} (want 78), (1,1) says {c.Adj(Idx(1, 1))} (want 2); with one flag it did nothing {!early} (want True); " +
                 $"with both it opened (1,0) {c.IsOpen(Idx(1, 0))} (want True) and won {c.State == MinesGame.Phase.Won} (want True)");
        var wrong = Two();
        wrong.ToggleFlag(Idx(0, 0));
        wrong.ToggleFlag(Idx(1, 0));                            // a wrong flag: (1,0) is not a mine
        wrong.Chord(Idx(1, 1), 103);
        Log.Info($"mines: chord over a wrong flag lost {wrong.State == MinesGame.Phase.Lost} (want True), the mine that went off (2,0) {wrong.Hit == Idx(2, 0)} (want True)");

        // loss: a mine opened - lost, that square marked, the clock stopped; nothing opens any more
        var l = Two();
        l.Open(Idx(0, 0), 107);
        bool after = l.Open(Idx(1, 0), 108) || l.ToggleFlag(Idx(1, 0));
        Log.Info($"mines: a mine opened lost {l.State == MinesGame.Phase.Lost} (want True), hit (0,0) {l.Hit == 0} (want True), time {l.Seconds(200):0} s (want 7), " +
                 $"a click after it changed nothing {!after} (want True)");

        // the counter and the clock: flags before the first click count, the clock starts with the first click
        var k = new MinesGame(9, 9, 10, Seed);
        k.ToggleFlag(0); k.ToggleFlag(1); k.ToggleFlag(2);
        double t0 = k.Seconds(50);
        k.ToggleFlag(1);
        k.Open(40, 60);
        Log.Info($"mines: counter with 2 flags {k.MinesLeft} (want 8), the clock before the first click {t0:0} s (want 0), 5 s after it {k.Seconds(65):0} s (want 5)");
    }

    // ---- through the watch's pointer (the laser's trigger) ----

    static void Draw2(WristPanel wrist, PanelData data) { wrist.Draw(data); wrist.Draw(data); }

    static void Tap(WristPanel wrist, PanelData data, Vector2 p)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
    }

    static void Hold(WristPanel wrist, PanelData data, Vector2 p, int ms)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        Thread.Sleep(ms);
        wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
    }

    // a seeded game with the opening click in the middle, the clock started 'ago' seconds back
    static MinesGame Opened(int level, double ago)
    {
        var g = level == 0 ? new MinesGame(9, 9, 10, Seed) : new MinesGame(16, 16, 40, Seed);
        double now = Clock.Now;
        g.Open(g.W / 2 + g.H / 2 * g.W, now);
        g.StartedForTest(now - ago);
        return g;
    }

    // a hidden square next to an open number: a mine or not
    static int HiddenBy(MinesGame g, bool mine)
    {
        for (int i = 0; i < g.Cells; i++)
            if (g.IsOpen(i) && g.Adj(i) > 0)
                foreach (int nb in g.Around(i))
                    if (!g.IsOpen(nb) && !g.IsFlag(nb) && g.IsMine(nb) == mine) return nb;
        return -1;
    }

    static void Pointer(WristPanel wrist, PanelData data, WristConfig wcfg, string tag)
    {
        wcfg.Face = "custom";
        wcfg.Background = "panel";
        // the 1x1 (its default size) in the middle of the face, a game going on
        wcfg.Widgets = new List<WatchWidget> { new() { Type = "minesweeper", X = 4, Y = 1, W = 1, H = 1 } };
        wrist.MinesResetAll();
        var g = new MinesGame(9, 9, 10, Seed);
        wrist.MinesSetGame(0, g);
        wrist.Settle();
        Draw2(wrist, data);
        g.Open(40, Clock.Now);
        // a tap on the tile grows the game out of it; a tap there while it grows does nothing (in the grown game a button sits
        // under it: the face, 95 Flag mode); then it is big and playable
        var cr = WatchWidgets.CellRect(4, 1, 1, 1, Theme.Classic);
        float s = Theme.Classic ? 2 : 1;
        var tile = new Vector2((cr.X + cr.Width / 2) * s, (cr.Y + cr.Height / 2) * s);
        Tap(wrist, data, tile);
        bool opened = wrist.IsOpen;
        Tap(wrist, data, tile);
        bool quiet = wrist.IsOpen && g.State == MinesGame.Phase.Playing && !wrist.MinesFlagMode;
        Settle(wrist, data);
        bool grown = wrist.IsOpen;
        Tap(wrist, data, wrist.MinesFacePoint);           // a new game to play by taps
        // a tap on the middle square: the game starts there, an area opens
        Tap(wrist, data, wrist.MinesSquarePoint(40, 9));
        bool started = g.State == MinesGame.Phase.Playing && g.IsOpen(40) && g.Opened > 1;
        // a held square (0.45 s) gets a flag and does not open; held again, the flag goes
        int h = HiddenBy(g, true);
        Hold(wrist, data, wrist.MinesSquarePoint(h, 9), 450);
        bool flagged = g.IsFlag(h) && !g.IsOpen(h);
        Hold(wrist, data, wrist.MinesSquarePoint(h, 9), 450);
        bool unflagged = !g.IsFlag(h) && !g.IsOpen(h);
        // a quick tap is not a long press: a safe square opens, no flag
        int safe = HiddenBy(g, false);
        Tap(wrist, data, wrist.MinesSquarePoint(safe, 9));
        bool tapOpened = g.IsOpen(safe) && !g.IsFlag(safe);
        // flag mode: a tap flags (and a tap on a flag in normal mode does nothing)
        Tap(wrist, data, wrist.MinesFlagButtonPoint);
        bool modeOn = wrist.MinesFlagMode;
        Tap(wrist, data, wrist.MinesSquarePoint(h, 9));
        bool modeFlag = g.IsFlag(h) && !g.IsOpen(h);
        Tap(wrist, data, wrist.MinesFlagButtonPoint);
        bool modeOff = !wrist.MinesFlagMode;
        Tap(wrist, data, wrist.MinesSquarePoint(h, 9));
        bool kept = g.IsFlag(h) && !g.IsOpen(h) && g.State == MinesGame.Phase.Playing;
        // a chord: a number with all its mines flagged, tapped - its other hidden neighbours open
        int num = -1;
        for (int i = 0; i < g.Cells && num < 0; i++)
            if (g.IsOpen(i) && g.Adj(i) > 0 && g.Around(i).Any(nb => !g.IsOpen(nb) && !g.IsMine(nb))) num = i;
        int before = g.Opened;
        if (num >= 0)
        {
            foreach (int nb in g.Around(num)) if (g.IsMine(nb) && !g.IsFlag(nb)) g.ToggleFlag(nb);
            Tap(wrist, data, wrist.MinesSquarePoint(num, 9));
        }
        bool chorded = num >= 0 && g.Opened > before && g.State != MinesGame.Phase.Lost;
        // the face: a new game
        Tap(wrist, data, wrist.MinesFacePoint);
        bool fresh = g.State == MinesGame.Phase.Ready && g.Opened == 0 && g.Flags == 0;
        // played to the end by taps: won, the best time kept in the config (the snapshot's own)
        var best0 = wcfg.MinesBest;
        wcfg.MinesBest = null;
        Tap(wrist, data, wrist.MinesSquarePoint(40, 9));
        for (int i = 0; i < g.Cells && !g.Over; i++)
            if (!g.IsMine(i) && !g.IsOpen(i)) Tap(wrist, data, wrist.MinesSquarePoint(i, 9));
        bool won = g.State == MinesGame.Phase.Won && g.MinesLeft == 0;
        int best = wcfg.MinesBest is { Length: 2 } b ? b[0] : -1;
        wcfg.MinesBest = best0;
        Log.Info($"mines{tag}: a tap started it with an area open {started} (want True); " +
                 $"held 0.45 s flagged {flagged} (want True), held again unflagged {unflagged} (want True); a quick tap opened, no flag {tapOpened} (want True)");
        Log.Info($"mines{tag}: flag mode on {modeOn} (want True), a tap flagged {modeFlag} (want True), off {modeOff} (want True), a tap on a flag kept it " +
                 $"{kept} (want True); a tap on a number with its flags chorded {chorded} (want True); the face started a new game {fresh} (want True)");
        // its X: it shrinks back into the tile, then it is closed
        Tap(wrist, data, wrist.MinesClosePoint);
        bool shrinking = !wrist.IsOpen && wrist.MinesShown;
        Settle(wrist, data);
        bool closed = !wrist.MinesShown;
        Log.Info($"mines{tag}: played to the end by taps won {won} (want True), its best time kept {best >= 1} (want True)");
        Log.Info($"mines{tag}: a tap on the 1x1 grew the game out of it {opened} (want True), a tap while it grew changed nothing {quiet} (want True), " +
                 $"then it was big {grown} (want True); its X shrank it back {shrinking} (want True) and closed it {closed} (want True)");
        wrist.CloseForTest();
        wrist.PointerLeave(Hand.Left);
    }

    // frames over 0.45 s: the zoom (spring / 95 outline) comes to rest
    static void Settle(WristPanel wrist, PanelData data)
    {
        for (int k = 0; k < 11; k++) { Thread.Sleep(40); wrist.Draw(data); }
    }

    // ---- the pictures ----

    enum St { New, Mid, Lost, Won }

    static MinesGame Make(St st, int level)
    {
        if (st == St.New) return level == 0 ? new MinesGame(9, 9, 10, Seed) : new MinesGame(16, 16, 40, Seed);
        var g = Opened(level, 83);
        // flags on three mines beside the open area
        for (int k = 0; k < 3; k++) { int m = HiddenBy(g, true); if (m >= 0) g.ToggleFlag(m); }
        if (st == St.Mid) return g;
        double now = Clock.Now;
        if (st == St.Lost)
        {
            int w = HiddenBy(g, false);
            if (w >= 0) g.ToggleFlag(w);                        // a wrong flag, crossed out when it is over
            int boom = HiddenBy(g, true);
            for (int i = 0; i < g.Cells && boom < 0; i++) if (g.IsMine(i) && !g.IsFlag(i)) boom = i;
            g.Open(boom, now);
            return g;
        }
        for (int i = 0; i < g.Cells; i++) if (!g.IsMine(i) && !g.IsOpen(i)) { if (g.IsFlag(i)) g.ToggleFlag(i); g.Open(i, now); }
        return g;
    }

    static void Pictures(WristPanel wrist, PanelData data, WristConfig wcfg, Gpu gpu, string dir, string tag)
    {
        var sheet = new List<string>();
        string Save(string name)
        {
            string p = Path.Combine(dir, $"mines_{name}{tag}.png");
            wrist.Surf.SavePng(gpu, p);
            sheet.Add(p);
            return p;
        }
        wcfg.Face = "custom";
        wcfg.Background = "panel";
        var best0 = wcfg.MinesBest;
        foreach (St st in Enum.GetValues<St>())
        {
            string name = st.ToString().ToLowerInvariant();
            wcfg.MinesBest = st == St.Won ? new[] { 83, 0 } : null;       // won in 1:23: a new best time
            wrist.MinesResetAll();
            wrist.MinesSetGame(0, Make(st, 0));
            // the widget at every size, with a few others round it
            wcfg.Widgets = new List<WatchWidget>
            {
                new() { Type = "minesweeper", X = 0, Y = 0, W = 2, H = 2 }, new() { Type = "minesweeper", X = 2, Y = 0, W = 3, H = 2 },
                new() { Type = "minesweeper", X = 5, Y = 0, W = 1, H = 1 }, new() { Type = "battery", X = 5, Y = 1, W = 1, H = 1 },
                new() { Type = "time", X = 0, Y = 2, W = 3, H = 2 }, new() { Type = "calendar", X = 3, Y = 2, W = 2, H = 2 },
                new() { Type = "night", X = 5, Y = 2, W = 1, H = 1 }, new() { Type = "settings", X = 5, Y = 3, W = 1, H = 1 },
            };
            wrist.Settle();
            Draw2(wrist, data);
            Save($"face_{name}");
            wrist.OpenForTest("minesweeper");
            if (st == St.Mid)
            {
                // the laser over a hidden square (its hover mark)
                Draw2(wrist, data);
                var g = wrist.MinesGameAt(0);
                int hv = HiddenBy(g, false);
                if (hv >= 0) wrist.PointerMove(Hand.Left, wrist.MinesSquarePoint(hv, 9));
            }
            Draw2(wrist, data);
            Save($"open_{name}");
            if (st == St.Mid) Cost(wrist, data, gpu, $"mines{tag}: the opened 9 x 9 game");
            wrist.PointerLeave(Hand.Left);
            wrist.CloseForTest();
        }
        wcfg.MinesBest = best0;
        // a long press half-way: the ring (95: the flag rising out of the pressed square)
        wrist.MinesResetAll();
        wrist.MinesSetGame(0, Make(St.Mid, 0));
        wcfg.Widgets = new List<WatchWidget> { new() { Type = "minesweeper", X = 0, Y = 0, W = 2, H = 2 } };
        wrist.Settle();
        wrist.OpenForTest("minesweeper");
        Draw2(wrist, data);
        var gm = wrist.MinesGameAt(0);
        var hp = wrist.MinesSquarePoint(HiddenBy(gm, false), 9);
        wrist.PointerMove(Hand.Left, hp); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, hp); wrist.Draw(data);
        Thread.Sleep(250);
        wrist.Draw(data);
        Save("open_hold");
        // let go off the board: nothing happens
        var off = new Vector2(3, 3);
        wrist.PointerMove(Hand.Left, off); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, off); wrist.Draw(data);
        Log.Info($"mines{tag}: a long press let go off the board changed nothing {!gm.IsFlag(HiddenBy(gm, false)) && gm.State == MinesGame.Phase.Playing} (want True)");
        wrist.PointerLeave(Hand.Left);
        wrist.CloseForTest();
        if (!Theme.Classic)
        {
            // the 16 x 16 board, mid-game: opened and on the face
            wrist.MinesResetAll();
            wrist.MinesSetGame(1, Make(St.Mid, 1));
            wrist.MinesLevel = 1;
            wcfg.Widgets = new List<WatchWidget>
            {
                new() { Type = "minesweeper", X = 0, Y = 0, W = 2, H = 2 }, new() { Type = "minesweeper", X = 2, Y = 0, W = 3, H = 2 },
                new() { Type = "minesweeper", X = 5, Y = 0, W = 1, H = 1 },
            };
            wrist.Settle();
            Draw2(wrist, data);
            Save("face_mid16");
            wrist.OpenForTest("minesweeper");
            Draw2(wrist, data);
            Save("open_mid16");
            Cost(wrist, data, gpu, "mines: the opened 16 x 16 game");
            wrist.CloseForTest();
        }
        // the 1x1 growing into the game and shrinking back (modern: the spring; 95: the zoom outline), on a full face
        wrist.MinesResetAll();
        wrist.MinesSetGame(0, Make(St.Mid, 0));
        wcfg.Widgets = new List<WatchWidget>
        {
            new() { Type = "time", X = 0, Y = 0, W = 4, H = 2 }, new() { Type = "battery", X = 4, Y = 0, W = 2, H = 1 },
            new() { Type = "minesweeper", X = 4, Y = 1, W = 1, H = 1 }, new() { Type = "fps", X = 5, Y = 1, W = 1, H = 1 },
            new() { Type = "media", X = 0, Y = 2, W = 6, H = 1 },
        };
        string[] buttons = { "keyboard", "desktops", "recenter", "layout", "night", "settings" };
        for (int i = 0; i < buttons.Length; i++) wcfg.Widgets.Add(new WatchWidget { Type = buttons[i], X = i, Y = 3 });
        wrist.Settle();
        Draw2(wrist, data);
        Save("grow_0");
        var tc = WatchWidgets.CellRect(4, 1, 1, 1, Theme.Classic);
        float k2 = Theme.Classic ? 2 : 1;
        Tap(wrist, data, new Vector2((tc.X + tc.Width / 2) * k2, (tc.Y + tc.Height / 2) * k2));
        Thread.Sleep(25); wrist.Draw(data);
        Save("grow_1");
        Thread.Sleep(45); wrist.Draw(data);
        Save("grow_2");
        Settle(wrist, data);
        Save("grow_3");
        Tap(wrist, data, wrist.MinesClosePoint);
        Thread.Sleep(60); wrist.Draw(data);
        Save("shrink");
        Settle(wrist, data);
        wrist.PointerLeave(Hand.Left);
        wrist.CloseForTest();
        if (Theme.Retro == RetroStyle.Win95) RealSideBySide(dir, tag);      // winmine's own pixels: the 95 style's promise
        Sheet(Path.Combine(dir, $"mines_sheet{tag}.png"), sheet);
    }

    // ---- winmine.exe beside the watch's: the real window recreated at its metrics (frame, title bar, Game / Help menu,
    // the client area), the watch's opened 95 game as the snapshot drew it, the same mid-game board; and a check that the
    // watch shows that client area pixel for pixel at (4, 4) of its canvas ----
    static MineArt.Sq RealSquare(MinesGame g, int i)
    {
        bool open = g.IsOpen(i), flag = g.IsFlag(i);
        if (g.State == MinesGame.Phase.Lost)
        {
            if (i == g.Hit) return MineArt.Sq.Hit;
            if (g.IsMine(i)) return flag ? MineArt.Sq.Flag : MineArt.Sq.Mine;
            if (flag) return MineArt.Sq.Wrong;
        }
        if (open) return g.Adj(i) == 0 ? MineArt.Sq.Open : MineArt.Sq.N1 + g.Adj(i) - 1;
        return flag ? MineArt.Sq.Flag : MineArt.Sq.Hidden;
    }

    static void RealSideBySide(string dir, string tag)
    {
        var g = Make(St.Mid, 0);
        int secs = (int)g.Seconds(Clock.Now);
        int cw = MineArt.ClientW(g.W), ch = MineArt.ClientH(g.H);
        var client = MineArt.Client(g, secs, MineArt.Face.Smile, false, i => RealSquare(g, i), -1, 0);
        // the watch's picture (2x): its client area at canvas (4, 4)
        string shot = Path.Combine(dir, $"mines_open_mid{tag}.png");
        int differ = 0;
        using (var w = new System.Drawing.Bitmap(shot))
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    if ((uint)w.GetPixel((4 + x) * 2, (4 + y) * 2).ToArgb() != client[y * cw + x]) differ++;
        Log.Info($"mines{tag}: the watch's Minesweeper is winmine's client area ({cw} x {ch}) pixel for pixel: {differ} px differ (want 0)");

        // the real window: a 3 px frame, an 18 px title bar, a 19 px menu bar, then the client area
        int ww = cw + 6, wh = 3 + 18 + 1 + 19 + ch + 3, top = 3 + 18 + 1 + 19;
        using var win = new System.Drawing.Bitmap(ww, wh);
        using (var gr = System.Drawing.Graphics.FromImage(win))
        {
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            var face = System.Drawing.Color.FromArgb(192, 192, 192);
            gr.Clear(face);
            void Px(int x, int y, int w, int h, int r, int gg, int b) =>
                gr.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(r, gg, b)), x, y, w, h);
            // the frame: light grey / black, then white / grey
            Px(0, 0, ww, 1, 223, 223, 223); Px(0, 0, 1, wh, 223, 223, 223); Px(0, wh - 1, ww, 1, 0, 0, 0); Px(ww - 1, 0, 1, wh, 0, 0, 0);
            Px(1, 1, ww - 2, 1, 255, 255, 255); Px(1, 1, 1, wh - 2, 255, 255, 255); Px(1, wh - 2, ww - 2, 1, 128, 128, 128); Px(ww - 2, 1, 1, wh - 2, 128, 128, 128);
            // the title bar: navy, the mine icon, "Minesweeper" bold white, minimise and close
            Px(3, 3, ww - 6, 18, 0, 0, 128);
            using var bold = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold);
            using var norm = new System.Drawing.Font("Microsoft Sans Serif", 8.25f);
            gr.DrawString("Minesweeper", bold, System.Drawing.Brushes.White, 21, 5);
            void Btn(int x, int y)
            {
                Px(x, y, 16, 14, 192, 192, 192);
                Px(x, y, 16, 1, 255, 255, 255); Px(x, y, 1, 14, 255, 255, 255); Px(x, y + 13, 16, 1, 0, 0, 0); Px(x + 15, y, 1, 14, 0, 0, 0);
                Px(x + 1, y + 12, 14, 1, 128, 128, 128); Px(x + 14, y + 1, 1, 12, 128, 128, 128);
            }
            Btn(ww - 5 - 16, 5);
            Btn(ww - 5 - 16 - 2 - 16, 5);
            for (int k = 0; k < 6; k++)
            {
                // the close button's X and the minimise bar
                int cx = ww - 5 - 16 + 4, cy = 5 + 3;
                Px(cx + k, cy + k, 2, 1, 0, 0, 0); Px(cx + 6 - k, cy + k, 2, 1, 0, 0, 0);
            }
            Px(ww - 5 - 16 - 2 - 16 + 4, 5 + 9, 6, 2, 0, 0, 0);
            // the menu bar
            gr.DrawString("Game", norm, System.Drawing.Brushes.Black, 8, 24);
            gr.DrawString("Help", norm, System.Drawing.Brushes.Black, 44, 24);
        }
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char c = MineArt.Mine16[y][x];
                if (c != '.') win.SetPixel(3 + 2 + x, 3 + 1 + y, System.Drawing.Color.FromArgb((int)MineArt.Pal(c)));
            }
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++) win.SetPixel(3 + x, top + y, System.Drawing.Color.FromArgb((int)client[y * cw + x]));

        // side by side at 2x, with the notes under them
        using var watch = new System.Drawing.Bitmap(shot);
        const int M = 16, Head = 26, Notes = 150;
        int W = M + ww * 2 + M + watch.Width + M, H = Head + Math.Max(wh * 2, watch.Height) + Notes;
        using var sheet = new System.Drawing.Bitmap(W, H);
        using (var gr = System.Drawing.Graphics.FromImage(sheet))
        {
            gr.Clear(System.Drawing.Color.FromArgb(0, 128, 128));
            gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            using var font = new System.Drawing.Font("Segoe UI", 10.5f);
            gr.DrawString("winmine.exe (95), recreated, 2x", font, System.Drawing.Brushes.White, M, 4);
            gr.DrawString("The watch in the 95 style: the opened game (its 320 x 220 canvas, 2x)", font, System.Drawing.Brushes.White, M + ww * 2 + M, 4);
            gr.DrawImage(win, new System.Drawing.Rectangle(M, Head, ww * 2, wh * 2));
            gr.DrawImage(watch, new System.Drawing.Rectangle(M + ww * 2 + M, Head, watch.Width, watch.Height));
            string notes =
                $"winmine's client area (Beginner): {cw} x {ch}; header box 150 x 37 at (9, 9), 2 px sunken; counters 41 x 25 (three 13 x 23 digits) at (16, 15) " +
                $"and ({cw - 57}, 15); face 24 x 24 at ({MineArt.FaceX(g.W)}, 16) in a 1 px grey outline; board frame 150 x 150 at (9, 52), 3 px sunken; squares 16 x 16 " +
                "from (12, 55). The window adds a 3 px frame, an 18 px title bar and a 19 px Game / Help menu: 255 px in all.\n" +
                $"The watch's 95 canvas is 212 px high inside its margins: the client area is drawn whole, 1:1 at (4, 4) - {differ} px differ from the recreation " +
                "on the left - and the title bar and menu go beside it: the name and X, New game, Flag mode (VR: one trigger), how the game is, the best time.";
            gr.DrawString(notes, font, System.Drawing.Brushes.White, new System.Drawing.RectangleF(M, Head + Math.Max(wh * 2, watch.Height) + 8, W - 2 * M, Notes - 10));
        }
        sheet.Save(Path.Combine(dir, $"mines{tag}_vs_real.png"), System.Drawing.Imaging.ImageFormat.Png);
    }

    // what a frame of it costs (the watch draws at 10 fps while a game is open, every frame while a square is held)
    static void Cost(WristPanel wrist, PanelData data, Gpu gpu, string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 30; i++) { wrist.Draw(data); gpu.Ctx.Flush(); }
        Log.Info($"{what}: {sw.Elapsed.TotalMilliseconds / 30:0.00} ms per draw");
    }

    // the pictures side by side, two per row, each with its name
    static void Sheet(string path, List<string> files)
    {
        const int W = WristPanel.TW, H = WristPanel.TH, Label = 22, Gap = 10;
        int rows = (files.Count + 1) / 2;
        using var bmp = new System.Drawing.Bitmap(2 * W + 3 * Gap, rows * (H + Label + Gap) + Gap);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 40, 44, 52));
        using var font = new System.Drawing.Font("Segoe UI", 11);
        for (int i = 0; i < files.Count; i++)
        {
            int x = Gap + i % 2 * (W + Gap), y = Gap + i / 2 * (H + Label + Gap);
            g.DrawString(Path.GetFileNameWithoutExtension(files[i]), font, System.Drawing.Brushes.Gainsboro, x, y);
            using var src = new System.Drawing.Bitmap(files[i]);
            g.DrawImage(src, new System.Drawing.Rectangle(x, y + Label, W, H));
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
