using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The keyboard on a green terminal (the 95 keyboard's half-size canvas and its keys' places): every key a box with its legend in
// the dot-matrix font (a letter at 2x, a word at 1x), the shifted sign dim in the top-left corner, the AltGr one in the
// bottom-right; a held key or a locked modifier lit; the edit bar's keys in dotted boxes; the chatbox a command line,
// "CHAT>" and the block cursor.
public sealed partial class KeyboardPanel
{
    partial void KeyboardTerm(ref bool done)
    {
        done = true;
        var sk = T.It;
        _flash.Clear();
        _canvas95 ??= new Surface(_p.Gpu, CW95, CH95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var all = new RectangleF(0, 0, CW95, CH95);
        _p.Fill(all, T.BG);
        T.Crt(_p, all);
        bool shift = _mods["shift"] > 0, altgr = _mods["altgr"] > 0;

        if (_chat)
        {
            var f = Half(_chatField);
            T.Dashes(_p, f.X, f.Right, f.Bottom - 1, T.FAINT);
            float cap = MathF.Floor(f.Y + (f.Height - 7) / 2);
            sk.Put(_p, "CHAT>", f.X + 4, cap - 2, T.MID);
            float tx = f.X + 4 + T.W("CHAT> "), tw = f.Right - 64 - tx;
            string text = _chatText.ToString();
            if (text.Length == 0) sk.Put(_p, "type a message for the VRChat chatbox, ENTER sends", tx, cap - 2, T.DIM);
            else
            {
                string shown = text;
                while (shown.Length > 1 && T.W(shown) > tw - 8) shown = shown[1..];
                if (shown.Length < text.Length) shown = "\u2026" + shown;
                sk.Put(_p, shown, tx, cap - 2, T.INK);
                tx += T.W(shown) + 1;
            }
            if (_blink == 0 || text.Length == 0 || !Motion.Full) _p.Fill(new RectangleF(tx + 1, cap - 1, 5, 9), T.INK);     // blinks (Full)
            string count = $"{text.Length}/{ChatMax}";
            sk.Put(_p, count, f.Right - 4 - T.W(count), cap - 2, T.DIM);
        }

        var drawList = new List<Key>(_keys);
        var bar = _chat ? _barChat : _barNormal;
        drawList.AddRange(bar);
        foreach (var k in drawList)
        {
            var r = Half(k.R);
            bool down = Array.IndexOf(_down, k) >= 0 || Array.IndexOf(_held95, k) >= 0;
            int modState = k.Kind == Kind.Modifier ? _mods[k.Mod] : k.Kind == Kind.Caps && Caps ? 2
                         : k.Action == "chat" && _chat ? 2 : 0;
            bool hover = Array.IndexOf(_hover, k) >= 0;
            bool lit = down || modState == 2;
            bool edit = bar.Contains(k);
            var face = RectangleF.Inflate(r, -1, -1);
            if (lit) T.Lit(_p, face);
            else
            {
                _p.Fill(r, T.BG);
                if (edit) Dotted(r, hover ? T.INK : T.FRAME);
                else if (modState == 1) T.DoubleBox(_p, r, T.HOT);
                else T.Box(_p, r, hover ? T.INK : T.FRAME);
            }
            uint c = lit ? T.BG : modState == 1 || hover ? T.HOT : T.INK, sub = lit ? T.BG : T.DIM;

            string word = k.Kind == Kind.Action ? k.Fixed
                : k.Icon == Theme.IcoBack ? "BKSP" : k.Icon == Theme.IcoEnter ? "ENTER"
                : k.Icon == Theme.IcoUp ? "\u2191" : k.Icon == Theme.IcoDown ? "\u2193" : k.Icon == Theme.IcoLeft ? "\u2190" : k.Icon == Theme.IcoRight ? "\u2192"
                : k.Fixed;
            if (word != null)
            {
                word = T.Up(word);
                int n = word.Length == 1 && r.Height >= 22 ? 2 : 1;
                if (T.W(word, n) > face.Width - 4) word = T.Up(Fit(word, face.Width - 4));
                sk.Put(_p, word, new RectangleF(face.X, face.Y + MathF.Floor((face.Height - 7 * n) / 2) - 2 * n, face.Width, 11 * n), c, n, false,
                    TextAlignment.Center, ParagraphAlignment.Near);
                continue;
            }
            string main = altgr && k.AltGr != null ? k.AltGr : shift ? k.Shifted ?? k.Normal : k.Normal;
            if (main != null && main.Length == 1 && char.IsLetter(main[0]) && Caps && !altgr)
                main = shift ? main.ToLowerInvariant() : main.ToUpperInvariant();
            if (!string.IsNullOrEmpty(main))
            {
                int n = r.Height >= 22 && T.W(main, 2) <= face.Width - 8 ? 2 : 1;
                sk.Put(_p, main, new RectangleF(face.X, face.Y + MathF.Floor((face.Height - 7 * n) / 2) - 2 * n + (n == 2 ? 1 : 0), face.Width, 11 * n), c, n,
                    false, TextAlignment.Center, ParagraphAlignment.Near);
            }
            if (!shift && k.Shifted != null && k.Normal != null && !char.IsLetter(k.Normal[0]))
                sk.Put(_p, k.Shifted, face.X + 3, face.Y + 1, sub);
            if (!altgr && k.AltGr != null)
                sk.Put(_p, k.AltGr, face.Right - 3 - T.W(k.AltGr), face.Bottom - 10, lit ? T.BG : T.FRAME);
        }
        TTubeFx(RectangleF.Inflate(all, -3, -3));
        T.Scanlines(_p, RectangleF.Inflate(all, -3, -3));
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
    }

    // the tube switching on when the keyboard comes into view (its keys typed in row by row), collapsing when it goes away
    double _tOnAt = -10;

    void TTubeFx(RectangleF g)
    {
        if (Ov != null && Ov.ShownAt > _tOnAt) _tOnAt = Ov.ShownAt;
        if (_tOnAt <= 0) return;
        double now = Clock.Now;
        bool want = C.Visible && C.Place.M != null;
        float off = want ? 0 : Math.Clamp(1 - _vis, 0, 1);
        var t = T.TubeAt(now - _tOnAt, off, true, g.Height);
        if (t.Moving) AnimWake.At(now);
        T.Apply(_p, g, t, g.X + 6);
    }

    // a box of dots (the edit bar's keys)
    void Dotted(RectangleF r, uint c)
    {
        for (float x = r.X; x < r.Right; x += 2) { _p.Fill(new RectangleF(x, r.Y, 1, 1), c); _p.Fill(new RectangleF(x, r.Bottom - 1, 1, 1), c); }
        for (float y = r.Y + 2; y < r.Bottom - 1; y += 2) { _p.Fill(new RectangleF(r.X, y, 1, 1), c); _p.Fill(new RectangleF(r.Right - 1, y, 1, 1), c); }
    }

    string Fit(string s, float w) => T.It.Fit(_p, s, w);
}
