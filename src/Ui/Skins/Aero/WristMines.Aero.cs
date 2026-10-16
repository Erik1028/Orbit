using System;
using System.Drawing;
using System.Numerics;
using Vortice.DirectWrite;

namespace Orbit;

// Minesweeper in Windows 7: the game of 2009 instead of winmine's grey - glossy blue squares that light up under the laser,
// pale open squares with the bold coloured numbers, the red flag, the black mine (the square that went off red), the time
// and the mines left in two small blue boxes under the board, each with its picture. The board plays as the 95 one does
// (MinesInput: tap, hold to flag, chords, flag mode); New game and Flag mode stand beside it, as the 95 window has them.
public sealed partial class WristPanel
{
    // Windows 7's number colours
    static readonly uint[] MineInk7 = { 0, 0xFF3448B8, 0xFF1D7A1D, 0xFFB51F1F, 0xFF1B1E8C, 0xFF7E1414, 0xFF16797A, 0xFF202020, 0xFF6A6A6A };

    // ---------------- the opened game ----------------

    void MinesDetailAero(RectangleF win)
    {
        var g = _mines[0];
        double now = Clock.Now;
        var cl = AeroSkin.Window7(_p, win, 24, 0xFFFFFFFF);
        _p.GradientRaw(cl, 0xFFE6EEF9, 0xFFD3E0F2, 0xFFBACCE6);
        MineIcon7(win.X + 8, win.Y + 5, 14);
        Ae.GlowText(_p, "Minesweeper", new RectangleF(win.X + 27, win.Y + 3, 150, 18), 0xFF000000, 12);
        var rc = new RectangleF(win.Right - 6 - 43, win.Y + 1, 43, 18);
        _mCloseBtn = rc;
        bool x = _ui.Clickable("wd95x", RectangleF.Inflate(rc, 3, 3), out bool xh, out bool xd);
        AeroSkin.CaptionBox(_p, rc, CaptionKind.Close, true, xd, xh);
        if (x) { CloseDetail(); return; }

        // the board: squares as big as fit over the counters (never smaller than winmine's 16 px)
        float pitch = MathF.Max(16, MathF.Min(17, MathF.Floor((cl.Height - 31) / g.H)));
        var board = new RectangleF(cl.X + 7, cl.Y + 5, g.W * pitch, g.H * pitch);
        int under = MinesInput(g, board, pitch, now);
        var (press, prog, chord) = MinesPressInfo(g, now);
        bool rise = press >= 0 && prog > 0, had = rise && g.IsFlag(press);
        // the board's dark rim
        _p.Smooth(true);
        _p.FillRound(RectangleF.Inflate(board, 2, 2), 0xFF2B4A74, 3);
        _p.Smooth(false);
        _p.Fill(RectangleF.Inflate(board, 1, 1), 0xFF6F8DB5);
        Ripple7(g, under);
        for (int i = 0; i < g.Cells; i++)
        {
            var sq = new RectangleF(board.X + i % g.W * pitch, board.Y + i / g.W * pitch, pitch, pitch);
            var st = rise && i == press ? (had || _mFlagMode ? MineArt.Sq.Hidden : MineArt.Sq.Open) : Square95(g, i, press, chord);
            float k = Turned7(i);
            if (k < 1)
            {
                // still turning over in the ripple: the glass, the open square fading in over it
                Hidden7(sq, false);
                if (k > 0)
                {
                    _p.PushFade(k);
                    Square7(sq, st, false, false);
                    _p.PopFade();
                }
                continue;
            }
            Square7(sq, st, i == under && !g.Over && st == MineArt.Sq.Hidden, i == press);
            if (rise && i == press)
            {
                // the long press: the flag fades in (or out) on its square
                _p.PushFade(had ? 1 - prog : prog);
                Flag7(sq);
                _p.PopFade();
            }
        }

        // the time and the mines left under the board
        float cy = board.Bottom + 5;
        int secs = (int)Math.Min(999, g.Seconds(now));
        Counter7(new RectangleF(board.X + 18, cy, 44, 18), secs.ToString(System.Globalization.CultureInfo.InvariantCulture));
        StopwatchIcon7(board.X + 1, cy + 1, 16);
        Counter7(new RectangleF(board.Right - 18 - 44, cy, 44, 18), MinesLeftText(g));
        MineIcon7(board.Right - 16, cy + 1, 16);

        // beside it: New game (the face's job here) and Flag mode, how the game is, the best time
        float sx = board.Right + 12, sw = cl.Right - 8 - sx;
        var ng = new RectangleF(sx, cl.Y + 6, sw, 25);
        _mFaceBtn = ng;
        if (_ui.Button95("mn95new", ng, "New game")) MinesNew();
        var flagBtn = new RectangleF(sx, ng.Bottom + 6, sw, 25);
        _mFlagBtn = flagBtn;
        if (_ui.Button95("mn95flag", flagBtn, "Flag mode", true, false, _mFlagMode, W95Icons.Id.Flag)) { _mFlagMode = !_mFlagMode; _dirty = true; }
        float ty = flagBtn.Bottom + 10;
        string head = g.State switch { MinesGame.Phase.Won => "You won!", MinesGame.Phase.Lost => "You lost.", _ => null };
        string body = g.State switch
        {
            MinesGame.Phase.Won or MinesGame.Phase.Lost => "Tap New game to play again.",
            MinesGame.Phase.Ready => "Tap a square to start.",
            _ => "Hold a square to flag it.",
        };
        if (head != null)
        {
            _p.Text(head, new RectangleF(sx, ty - 2, sw, 20), AeroSkin.CpBlue, 13, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, Ae.Font);
            ty += 20;
        }
        _ui.Text95(body, new RectangleF(sx, ty, sw, 32), 0xFF1E1E1E, false, TextAlignment.Leading, true, true);
        int best = MinesBestOf(0);
        _ui.Text95(best > 0 ? Lang.F("Best time: {0} s", best) : "Best time: none", new RectangleF(sx, cl.Bottom - 8 - 15, sw, 15), 0xFF4C4C4C);
    }

    // ---- 7's reveal ripple: the squares a tap opens turn over one after the other, outwards from the tapped one (a lost game's
    // mines from the one that went off); each fades from glass to open in 0.16 s, 30 ms later for each square further away ----
    bool[] _mShown7;              // each square revealed as last drawn
    double[] _mAt7;               // when it starts turning over
    double _mRippleEnd7;
    MinesGame _mGame7;
    const float Turn7 = 0.16f, Wave7 = 0.03f;

    bool Revealed7(MinesGame g, int i) => g.IsOpen(i) || g.State == MinesGame.Phase.Lost && g.IsMine(i);

    void Ripple7(MinesGame g, int under)
    {
        double now = XpAeroFx.Now;
        bool fresh = _mGame7 != g || _mShown7 == null || _mShown7.Length != g.Cells || !XpAeroFx.Moves;
        if (fresh)
        {
            _mGame7 = g;
            if (_mShown7 == null || _mShown7.Length != g.Cells) { _mShown7 = new bool[g.Cells]; _mAt7 = new double[g.Cells]; }
            for (int i = 0; i < g.Cells; i++) { _mShown7[i] = Revealed7(g, i); _mAt7[i] = 0; }
            _mRippleEnd7 = 0;
            return;
        }
        // where it starts: the mine that went off, the square tapped (under the laser), else the first one that opened
        int origin = g.State == MinesGame.Phase.Lost && g.Hit >= 0 && !_mShown7[g.Hit] ? g.Hit : -1;
        if (origin < 0 && under >= 0 && under < g.Cells && Revealed7(g, under) && !_mShown7[under]) origin = under;
        for (int i = 0; i < g.Cells && origin < 0; i++) if (Revealed7(g, i) && !_mShown7[i]) origin = i;
        for (int i = 0; i < g.Cells; i++)
        {
            bool rev = Revealed7(g, i);
            if (rev && !_mShown7[i] && origin >= 0)
            {
                int dx = i % g.W - origin % g.W, dy = i / g.W - origin / g.W;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                _mAt7[i] = now + Math.Min(0.4, dist * Wave7);
                _mRippleEnd7 = Math.Max(_mRippleEnd7, _mAt7[i] + Turn7);
            }
            _mShown7[i] = rev;
        }
        if (now < _mRippleEnd7) XpAeroFx.Want(now);
    }

    // how far a square has turned over (1: done)
    float Turned7(int i)
    {
        if (_mAt7 == null || i >= _mAt7.Length || XpAeroFx.Now >= _mRippleEnd7) return 1;
        return Math.Clamp((float)((XpAeroFx.Now - _mAt7[i]) / Turn7), 0, 1);
    }

    // one square: blue glass while hidden (lighter under the laser), pale once open with its number, the flag, the mine
    void Square7(RectangleF r, MineArt.Sq st, bool hot, bool pressed)
    {
        switch (st)
        {
            case MineArt.Sq.Hidden:
            case MineArt.Sq.Flag:
                // the glass lights up under the laser at once and dims slowly (7's squares)
                float h = XpAeroFx.Fade(XpAeroFx.Mine, r, hot, 0.06f, 0.35f);
                if (h >= 1 || h <= 0) Hidden7(r, h >= 1);
                else
                {
                    Hidden7(r, false);
                    _p.PushFade(h);
                    Hidden7(r, true);
                    _p.PopFade();
                }

                if (st == MineArt.Sq.Flag) Flag7(r);
                return;
            case MineArt.Sq.Wrong:
                Hidden7(r, false);
                Flag7(r);
                _p.Smooth(true);
                _p.RoundLine(r.X + 3, r.Y + 3, r.Right - 3, r.Bottom - 3, 0xFFD01818, 1.6f);
                _p.RoundLine(r.Right - 3, r.Y + 3, r.X + 3, r.Bottom - 3, 0xFFD01818, 1.6f);
                _p.Smooth(false);
                return;
        }
        // open: pale, a hair darker at its edges
        bool hit = st == MineArt.Sq.Hit;
        _p.Fill(r, hit ? 0xFFB0281C : 0xFFA9BCD6);
        var i = new RectangleF(r.X + 1, r.Y + 1, r.Width - 1, r.Height - 1);
        if (hit) _p.GradientRaw(i, 0xFFF0766A, 0xFFD8392B);
        else _p.GradientRaw(i, pressed ? 0xFFC9D8EC : 0xFFE9EFF8, 0xFFD6E1EF);
        if (st is MineArt.Sq.Mine or MineArt.Sq.Hit) { Mine7(r.X + r.Width / 2, r.Y + r.Height / 2, r.Width * 0.30f); return; }
        if (st < MineArt.Sq.N1 || st > MineArt.Sq.N8) return;
        int n = st - MineArt.Sq.N1 + 1;
        _p.Text(MineDigit[n], new RectangleF(r.X, r.Y - 2, r.Width + 1, r.Height + 4), MineInk7[n], 13, FontWeight.Bold, TextAlignment.Center,
            ParagraphAlignment.Center, Ae.Font);
    }

    // a hidden square: a rounded tile of blue glass in the board's dark grid, its top half bright
    void Hidden7(RectangleF r, bool hot)
    {
        _p.Fill(r, 0xFF26497F);
        var t = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        _p.Smooth(true);
        _p.FillRound(t, hot ? 0xFF4C8FE0 : 0xFF2C62B4, 2);
        _p.Smooth(false);
        var i = RectangleF.Inflate(t, -1, -1);
        _p.PushRoundedClip(i, 1.5f);
        if (hot) AeroSkin.Gloss(_p, i, 0xFFE3F3FF, 0xFFB4DCFF, 0xFF7CBBF8, 0xFFA8D6FF);
        else AeroSkin.Gloss(_p, i, 0xFFA9D2FA, 0xFF79AEEE, 0xFF3F7BD3, 0xFF5D96E2);
        _p.PopRoundedClip();
        _p.Fill(new RectangleF(i.X + 1, i.Y, i.Width - 2, 1), Theme.WithAlpha(0xFFFFFFFF, hot ? 0.8f : 0.55f));
    }

    // the flag: a dark pole on a little foot, the red pennant
    void Flag7(RectangleF r)
    {
        float s = r.Width / 17f, x = MathF.Round(r.X + r.Width * 0.38f), top = r.Y + 3 * s, bottom = r.Bottom - 3 * s;
        _p.Fill(new RectangleF(x, top, MathF.Max(1, MathF.Round(s)), bottom - top), 0xFF2A2A2A);
        _p.Fill(new RectangleF(MathF.Round(x - 3 * s), MathF.Round(bottom - 2 * s), MathF.Round(8 * s), MathF.Round(2 * s)), 0xFF2A2A2A);
        _p.Smooth(true);
        var pts = new[] { new Vector2(x + 1, top), new Vector2(x + 1 + 8 * s, top + 3.2f * s), new Vector2(x + 1, top + 6.4f * s) };
        _p.Polygon(pts, 0xFFE0261C);
        var hi = new[] { new Vector2(x + 1, top), new Vector2(x + 1 + 8 * s, top + 3.2f * s), new Vector2(x + 1, top + 2.6f * s) };
        _p.Polygon(hi, 0xFFFF7A60);
        _p.Smooth(false);
    }

    // the mine: a black ball with its spikes and a white shine
    void Mine7(float cx, float cy, float rad)
    {
        _p.Smooth(true);
        for (int k = 0; k < 4; k++)
        {
            float a = k * MathF.PI / 4, dx = MathF.Cos(a) * rad * 1.45f, dy = MathF.Sin(a) * rad * 1.45f;
            _p.RoundLine(cx - dx, cy - dy, cx + dx, cy + dy, 0xFF101010, MathF.Max(1, rad * 0.32f));
        }
        _p.Circle(cx, cy, rad, 0xFF161616);
        _p.Circle(cx - rad * 0.35f, cy - rad * 0.35f, rad * 0.3f, 0xFFE8E8E8);
        _p.Smooth(false);
    }

    // a counter's box: dark blue glass, the number white
    void Counter7(RectangleF r, string text)
    {
        _p.Smooth(true);
        _p.FillRound(r, 0xFF173C70, 4);
        _p.Smooth(false);
        var i = RectangleF.Inflate(r, -1, -1);
        _p.PushRoundedClip(i, 3);
        AeroSkin.Gloss(_p, i, 0xFF5D8FD0, 0xFF3A6CB2, 0xFF1F4C8E, 0xFF2D5EA4);
        _p.PopRoundedClip();
        float size = WAero(text, 12, FontWeight.Bold) <= r.Width - 4 ? 12 : 10;
        _p.Text(text, new RectangleF(r.X, r.Y - 2, r.Width, r.Height + 4), 0xFFFFFFFF, size, FontWeight.Bold, TextAlignment.Center,
            ParagraphAlignment.Center, Ae.Font);
    }

    // the stopwatch: a white face in a blue ring, its hand and the button on top
    void StopwatchIcon7(float x, float y, float s)
    {
        float cx = x + s / 2, cy = y + s / 2 + 1, r = s / 2 - 1.5f;
        _p.Smooth(true);
        _p.Fill(new RectangleF(MathF.Round(cx - 1.5f), y - 1, 3, 3), 0xFF2B4F86);
        _p.Circle(cx, cy, r + 1, 0xFF1F3F72);
        _p.Circle(cx, cy, r, 0xFFEFF5FC);
        _p.RoundLine(cx, cy, cx, cy - r * 0.7f, 0xFF1F3F72, 1.2f);
        _p.RoundLine(cx, cy, cx + r * 0.5f, cy, 0xFF1F3F72, 1.2f);
        _p.Smooth(false);
    }

    void MineIcon7(float x, float y, float s) => Mine7(x + s / 2, y + s / 2, s * 0.27f);

    // ---------------- on the face: a gadget ----------------

    // 1x1: the mines left and the time; bigger: a picture of the board (its squares as small blue / pale cells) with the two
    // counters
    void MinesTileAero(RectangleF c, WatchWidget w)
    {
        var g = _mines[0];
        int secs = (int)Math.Min(999, g.Seconds(Clock.Now));
        string left = MinesLeftText(g), time = secs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (w.H < 2)
        {
            float y = MathF.Floor(c.Y + (c.Height - 40) / 2);
            MineIcon7(c.X + 4, y + 1, 16);
            _p.Text(left, new RectangleF(c.X + 21, y - 2, c.Width - 23, 22), 0xFFFFFFFF, 15, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, Ae.Font);
            StopwatchIcon7(c.X + 4, y + 22, 16);
            _p.Text(time, new RectangleF(c.X + 21, y + 19, c.Width - 23, 22), 0xFFD5E6F8, 15, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, Ae.Font);
            return;
        }
        bool wide = c.Width >= 124;
        int cs = (int)Math.Min(wide ? 8 : 5, MathF.Floor((c.Height - 4) / g.H));
        float bw = g.W * cs, bx = c.X + 4;
        float by = MathF.Floor(c.Y + (c.Height - g.H * cs) / 2);
        _p.Fill(new RectangleF(bx - 1, by - 1, bw + 2, g.H * cs + 2), 0xFF1E3D6B);
        for (int i = 0; i < g.Cells; i++)
        {
            var sq = new RectangleF(bx + i % g.W * cs, by + i / g.W * cs, cs, cs);
            bool open = g.IsOpen(i), flag = g.IsFlag(i);
            bool boom = g.State == MinesGame.Phase.Lost && g.IsMine(i);
            uint fill = boom ? (i == g.Hit ? 0xFFD8392B : 0xFF2A2A2A) : flag ? 0xFFE0261C : open ? 0xFFE3EBF6 : 0xFF4F88D8;
            _p.Fill(new RectangleF(sq.X, sq.Y, cs - 1, cs - 1), fill);
            if (!open && !flag && !boom) _p.Fill(new RectangleF(sq.X, sq.Y, cs - 1, 1), 0xFF8DBDF2);
            if (open && g.Adj(i) > 0 && cs >= 5) _p.Fill(new RectangleF(sq.X + (cs - 3) / 2f, sq.Y + (cs - 3) / 2f, 2, 2), MineInk7[g.Adj(i)]);
        }
        float rx = bx + bw + 6, rw = c.Right - 3 - rx;
        if (wide) { rx = MathF.Floor(rx + (rw - 62) / 2); rw = 62; }
        float ry = MathF.Floor(c.Y + (c.Height - 50) / 2);
        MineIcon7(rx, ry + 2, 14);
        Counter7(new RectangleF(rx + 16, ry, rw - 16, 18), left);
        StopwatchIcon7(rx, ry + 32, 14);
        Counter7(new RectangleF(rx + 16, ry + 30, rw - 16, 18), time);
    }
}
