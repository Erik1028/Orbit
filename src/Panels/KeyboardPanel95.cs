using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The keyboard in the Windows 95 style: drawn at half size (740 x 338) with 95 push buttons and
// MS Sans Serif, then shown 2x. Keys stay where they are (hit testing is unchanged): a held key is
// pushed in, a locked modifier is latched (the dithered toolbar look), a one-shot modifier gets the
// black frame of a default button. No hover, no glow - 1995 had neither.
public sealed partial class KeyboardPanel
{
    const int CW95 = TW / 2, CH95 = TH / 2;
    const uint Blk95 = 0xFF000000, Gry95 = 0xFF808080, Navy95 = 0xFF000080;
    Surface _canvas95;
    readonly Key[] _held95 = new Key[3];      // per laser: the key it holds, top-bar buttons included

    static readonly string[] BackGlyph =
    {
        "...k.......", "..kk.......", ".kkkkkkkkkk", "kkkkkkkkkkk", ".kkkkkkkkkk", "..kk.......", "...k.......",
    };
    static readonly string[] EnterGlyph =
    {
        "........kk", "........kk", "...k....kk", "..kk....kk", ".kkkkkkkkk", "kkkkkkkkkk", ".kkkkkkkkk", "..kk......", "...k......",
    };
    static readonly string[] UpGlyph95 = { "...k...", "..kkk..", ".kkkkk.", "kkkkkkk" };
    static readonly string[] DownGlyph95 = { "kkkkkkk", ".kkkkk.", "..kkk..", "...k..." };
    static readonly string[] LeftGlyph95 = { "...k", "..kk", ".kkk", "kkkk", ".kkk", "..kk", "...k" };
    static readonly string[] RightGlyph95 = { "k...", "kk..", "kkk.", "kkkk", "kkk.", "kk..", "k..." };

    static string[] Glyph95(string icon) =>
        icon == Theme.IcoBack ? BackGlyph : icon == Theme.IcoEnter ? EnterGlyph : icon == Theme.IcoUp ? UpGlyph95
        : icon == Theme.IcoDown ? DownGlyph95 : icon == Theme.IcoLeft ? LeftGlyph95 : icon == Theme.IcoRight ? RightGlyph95 : null;

    // a key's place on the half-size canvas, on whole pixels
    static RectangleF Half(RectangleF r)
    {
        float x0 = MathF.Round(r.X / 2), y0 = MathF.Round(r.Y / 2), x1 = MathF.Round(r.Right / 2), y1 = MathF.Round(r.Bottom / 2);
        return new RectangleF(x0, y0, x1 - x0, y1 - y0);
    }

    void Text95(string s, RectangleF r, uint c, float size = Ui.F95, FontWeight w = FontWeight.Normal, TextAlignment al = TextAlignment.Center) =>
        _p.Text(s, r, c, size, w, al, ParagraphAlignment.Center, Theme.RetroFont);

    void Draw95()
    {
        if (SkinKeyboard()) return;         // a style's own keyboard (ScreenHooks.cs)
        _flash.Clear();                     // no glow in 1995
        _canvas95 ??= new Surface(_p.Gpu, CW95, CH95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var all = new RectangleF(0, 0, CW95, CH95);
        _p.Fill(all, Theme.W95Face);
        _p.Bevel(all, Painter.Edge.Window);
        bool shift = _mods["shift"] > 0, altgr = _mods["altgr"] > 0;

        if (_chat)
        {
            // a 95 text box with a black caret
            var f = Half(_chatField);
            _p.Fill(f, Theme.W95Window);
            _p.Bevel(f, Painter.Edge.Sunken);
            string text = _chatText.ToString();
            var tr = new RectangleF(f.X + 6, f.Y, f.Width - 66, f.Height);
            if (text.Length == 0)
                Text95("Type a message for the VRChat chatbox, Enter sends", tr, Gry95, 13, FontWeight.Normal, TextAlignment.Leading);
            else
            {
                string shown = text;
                while (shown.Length > 1 && _p.MeasureWidth(shown, 13, FontWeight.Normal, Theme.RetroFont) > tr.Width - 8) shown = shown[1..];
                if (shown.Length < text.Length) shown = "..." + shown;
                Text95(shown, tr, Blk95, 13, FontWeight.Normal, TextAlignment.Leading);
                if (_blink == 0)
                {
                    float cx = MathF.Round(tr.X + _p.MeasureWidth(shown, 13, FontWeight.Normal, Theme.RetroFont) + 1);
                    _p.Fill(new RectangleF(cx, f.Y + 8, 1, f.Height - 16), Blk95);
                }
            }
            Text95($"{text.Length}/{ChatMax}", new RectangleF(f.Right - 60, f.Y, 54, f.Height), Gry95, Ui.F95, FontWeight.Normal,
                TextAlignment.Trailing);
        }

        var drawList = new List<Key>(_keys);
        drawList.AddRange(_chat ? _barChat : _barNormal);
        foreach (var k in drawList)
        {
            var r = Half(k.R);
            bool down = Array.IndexOf(_down, k) >= 0 || Array.IndexOf(_held95, k) >= 0;
            int modState = k.Kind == Kind.Modifier ? _mods[k.Mod] : k.Kind == Kind.Caps && Caps ? 2
                         : k.Action == "chat" && _chat ? 2 : 0;
            bool latched = modState == 2 && !down;
            var b = Theme.RetroSkin.KeyCap(_p, r, modState == 1 && !down, down, latched, Array.IndexOf(_hover, k) >= 0);
            float o = down || latched ? 1 : 0;
            var tr = new RectangleF(b.X + o, b.Y + o, b.Width, b.Height);

            if (k.Kind == Kind.Action)
                Text95(k.Fixed, tr, Blk95);
            else if (k.Icon != null && Glyph95(k.Icon) is { } g)
                _p.Pixels(g, MathF.Floor(tr.X + (tr.Width - g[0].Length) / 2), MathF.Floor(tr.Y + (tr.Height - g.Length) / 2), Blk95);
            else if (k.Fixed != null)
                Text95(k.Fixed, tr, Blk95);
            else
            {
                string main = altgr && k.AltGr != null ? k.AltGr : shift ? k.Shifted ?? k.Normal : k.Normal;
                if (main != null && main.Length == 1 && char.IsLetter(main[0]) && Caps && !altgr)
                    main = shift ? main.ToLowerInvariant() : main.ToUpperInvariant();
                Text95(main, tr, Blk95, 16);
                // shifted symbol (for non-letters) top left, AltGr symbol bottom right
                if (!shift && k.Shifted != null && k.Normal != null && !char.IsLetter(k.Normal[0]))
                    Text95(k.Shifted, new RectangleF(tr.X + 4, tr.Y + 2, 14, 13), Blk95, Ui.F95, FontWeight.Normal, TextAlignment.Leading);
                if (!altgr && k.AltGr != null)
                    Text95(k.AltGr, new RectangleF(tr.Right - 18, tr.Bottom - 16, 14, 13), Navy95, Ui.F95, FontWeight.Normal, TextAlignment.Trailing);
            }
        }
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
    }
}
