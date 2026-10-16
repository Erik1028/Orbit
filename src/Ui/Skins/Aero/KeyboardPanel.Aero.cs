using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The keyboard in Windows 7: the On-Screen Keyboard (osk.exe) - a glass window, dark slate keys with white Segoe UI legends,
// blue under the laser, light blue while held; a locked modifier lit blue, a one-shot one ringed; the shortcut row as blue glass.
// The keys stay where they are (hit testing is unchanged).
public sealed partial class KeyboardPanel
{
    static AeroSkin Ae => (AeroSkin)Skins.Aero;

    partial void KeyboardAero(ref bool done)
    {
        done = true;
        _flash.Clear();
        _canvas95 ??= new Surface(_p.Gpu, CW95, CH95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var all = new RectangleF(0, 0, CW95, CH95);
        // osk.exe's window: a thick glass frame, the keys on a slate-blue client area
        var inner = AeroSkin.Window7(_p, all, 8, 0xFF3F5272, 7);
        _p.GradientRaw(inner, 0xFF566B8E, 0xFF425677, 0xFF303F5A);
        _p.Fill(new RectangleF(inner.X, inner.Y, inner.Width, 1), Theme.WithAlpha(0xFFFFFFFF, 0.18f));
        bool shift = _mods["shift"] > 0, altgr = _mods["altgr"] > 0;

        if (_chat)
        {
            // a 7 text box with its caret
            var f = Half(_chatField);
            _p.Fill(f, 0xFFFFFFFF);
            AeroSkin.Field(_p, f, true);
            string text = _chatText.ToString();
            var tr = new RectangleF(f.X + 6, f.Y, f.Width - 66, f.Height);
            if (text.Length == 0)
                TextA("Type a message for the VRChat chatbox, Enter sends", tr, 0xFF8D8D8D, 13, FontWeight.Normal, TextAlignment.Leading);
            else
            {
                string shown = text;
                while (shown.Length > 1 && _p.MeasureWidth(shown, 13, FontWeight.Normal, Ae.Font) > tr.Width - 8) shown = shown[1..];
                if (shown.Length < text.Length) shown = "..." + shown;
                TextA(shown, tr, 0xFF000000, 13, FontWeight.Normal, TextAlignment.Leading);
                if (_blink == 0)
                {
                    float cx = MathF.Round(tr.X + _p.MeasureWidth(shown, 13, FontWeight.Normal, Ae.Font) + 1);
                    _p.Fill(new RectangleF(cx, f.Y + 7, 1, f.Height - 14), 0xFF000000);
                }
            }
            TextA($"{text.Length}/{ChatMax}", new RectangleF(f.Right - 60, f.Y, 54, f.Height), 0xFF8D8D8D, Ui.F95, FontWeight.Normal,
                TextAlignment.Trailing);
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
            OskKey(r, bar.Contains(k), down, modState == 2 && !down, modState == 1 && !down, hover);
            bool lit = down || (modState == 2);
            uint ink = 0xFFFFFFFF, sub = lit ? 0xFFE6F3FF : 0xFFA9B9CE;

            if (k.Kind == Kind.Action)
                TextA(k.Fixed, r, ink, Ui.F95, FontWeight.Normal);
            else if (k.Icon != null && Glyph7(k.Icon) is { } g)
                _p.Pixels(g, MathF.Floor(r.X + (r.Width - g[0].Length) / 2), MathF.Floor(r.Y + (r.Height - g.Length) / 2), ink);
            else if (k.Fixed != null)
                TextA(k.Fixed, r, ink, Ui.F95, FontWeight.Normal);
            else
            {
                string main = altgr && k.AltGr != null ? k.AltGr : shift ? k.Shifted ?? k.Normal : k.Normal;
                if (main != null && main.Length == 1 && char.IsLetter(main[0]) && Caps && !altgr)
                    main = shift ? main.ToLowerInvariant() : main.ToUpperInvariant();
                TextA(main, r, ink, 15, FontWeight.Normal);
                if (!shift && k.Shifted != null && k.Normal != null && !char.IsLetter(k.Normal[0]))
                    TextA(k.Shifted, new RectangleF(r.X + 5, r.Y + 2, 14, 14), sub, Ui.F95, FontWeight.Normal, TextAlignment.Leading);
                if (!altgr && k.AltGr != null)
                    TextA(k.AltGr, new RectangleF(r.Right - 19, r.Bottom - 17, 14, 14), lit ? 0xFFFFFFFF : 0xFF7FC4F8, Ui.F95, FontWeight.Normal,
                        TextAlignment.Trailing);
            }
        }
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
    }

    // the OSK's key pictures: line arrows, the backspace tag, the return arrow
    static readonly string[] Back7 =
    {
        "....kkkkkkkkkk", "...kk........k", "..kk..k...k..k", ".kk....k.k...k", "kk......k....k", ".kk....k.k...k", "..kk..k...k..k",
        "...kk........k", "....kkkkkkkkkk",
    };
    static readonly string[] Enter7 =
    {
        "..........kk", "..........kk", "...k......kk", "..kk......kk", ".kkkkkkkkkkk", "kkkkkkkkkkkk", ".kkkkkkkkkk.", "..kk........", "...k........",
    };
    static readonly string[] Left7 = { "...kk......", "..kk.......", ".kk........", "kkkkkkkkkkk", "kkkkkkkkkkk", ".kk........", "..kk.......", "...kk......" };
    static readonly string[] Right7 = { "......kk...", ".......kk..", "........kk.", "kkkkkkkkkkk", "kkkkkkkkkkk", "........kk.", ".......kk..", "......kk..." };
    static readonly string[] Up7 = { "...kk...", "..kkkk..", ".kkkkkk.", "kk.kk.kk", "...kk...", "...kk...", "...kk...", "...kk...", "...kk...", "...kk..." };
    static readonly string[] Down7 = { "...kk...", "...kk...", "...kk...", "...kk...", "...kk...", "...kk...", "kk.kk.kk", ".kkkkkk.", "..kkkk..", "...kk..." };

    static string[] Glyph7(string icon) =>
        icon == Theme.IcoBack ? Back7 : icon == Theme.IcoEnter ? Enter7 : icon == Theme.IcoUp ? Up7
        : icon == Theme.IcoDown ? Down7 : icon == Theme.IcoLeft ? Left7 : icon == Theme.IcoRight ? Right7 : null;

    void TextA(string s, RectangleF r, uint c, float size, FontWeight w, TextAlignment al = TextAlignment.Center) =>
        _p.Text(s, new RectangleF(r.X, r.Y - 3, r.Width, r.Height + 6), c, size, w, al, ParagraphAlignment.Center, Ae.Font);

    // an OSK key: dark slate with a top light, blue under the laser, light blue held, lit blue when locked, a blue ring one-shot;
    // the shortcut row blue glass
    void OskKey(RectangleF r, bool action, bool down, bool latched, bool oneShot, bool hover)
    {
        uint border, a, b, c, d;
        if (down) (border, a, b, c, d) = (0xFF0E3D6E, 0xFFA4DBFF, 0xFF6BBEF7, 0xFF3D9BE6, 0xFF5AB0F0);
        else if (latched) (border, a, b, c, d) = (0xFF0E3D6E, 0xFF6FB6F2, 0xFF3D8EDB, 0xFF2271C4, 0xFF3784D3);
        else if (hover) (border, a, b, c, d) = action ? (0xFF0B1B30, 0xFF7DAEE6, 0xFF4E86C8, 0xFF2E66AA, 0xFF3D76B9) : (0xFF0B1B30, 0xFF5C7899, 0xFF41597A, 0xFF2D4462, 0xFF37506F);
        else if (action) (border, a, b, c, d) = (0xFF0B1B30, 0xFF5D8FCB, 0xFF3E72B1, 0xFF285A98, 0xFF2F64A3);
        else (border, a, b, c, d) = (0xFF070D16, 0xFF465468, 0xFF333F52, 0xFF232C3B, 0xFF283242);
        AeroSkin.RoundBox(_p, r, 3, border, oneShot ? 0xFF7FC8F8 : hover && !down ? 0xFF8EC9F5 : Theme.Mix(a, 0xFFFFFFFF, 0.18f), a, b, c, d);
        if (oneShot)
        {
            _p.Smooth(true);
            _p.StrokeRound(new RectangleF(r.X + 1.5f, r.Y + 1.5f, r.Width - 3, r.Height - 3), 0xFF7FC8F8, 1, 2);
            _p.Smooth(false);
        }
    }
}
