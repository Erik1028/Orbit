using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The keyboard in the Classic Mac style: an Apple Extended Keyboard's light caps on Platinum, the letters in Chicago, the
// modifiers named the Apple way - lower case, "return", "delete" - a locked modifier black with white words (the Mac's
// caps lock look), the Edit commands along the top as the Edit menu names them. The keys stay where they are.
public sealed partial class KeyboardPanel
{
    // Apple's names on the special keys
    static string MacKeyName(Key k) => k.Fixed switch
    {
        "Esc" => "esc",
        "Tab" => "tab",
        "Caps" => "caps lock",
        "Shift" => "shift",
        "Ctrl" => "control",
        "Win" => "win",
        "Alt" => "alt",
        "AltGr" => "alt gr",
        "Del" => "del",
        "Select all" => "Select All",
        _ => k.Fixed,
    };

    partial void KeyboardMac(ref bool done)
    {
        done = true;
        _flash.Clear();
        _canvas95 ??= new Surface(_p.Gpu, CW95, CH95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var m = (MacSkin)Skins.Mac;
        var all = new RectangleF(0, 0, CW95, CH95);
        _p.Fill(all, m.Face);
        _p.Bevel(all, Painter.Edge.Window);
        bool shift = _mods["shift"] > 0, altgr = _mods["altgr"] > 0;

        if (_chat)
        {
            // a Mac text field: white, a black frame, the blinking insertion point
            var f = Half(_chatField);
            m.Well(_p, f);
            string text = _chatText.ToString();
            var tr = new RectangleF(f.X + 6, f.Y, f.Width - 66, f.Height);
            if (text.Length == 0)
                Text95("Type a message for the VRChat chatbox, return sends", tr, Gry95, 13, FontWeight.Normal, TextAlignment.Leading);
            else
            {
                string shown = text;
                while (shown.Length > 1 && _p.MeasureWidth(shown, 13, FontWeight.Normal, Theme.RetroFont) > tr.Width - 8) shown = shown[1..];
                if (shown.Length < text.Length) shown = "..." + shown;
                Text95(shown, tr, Blk95, 13, FontWeight.Normal, TextAlignment.Leading);
                if (_blink == 0)
                {
                    float cx = MathF.Round(tr.X + _p.MeasureWidth(shown, 13, FontWeight.Normal, Theme.RetroFont) + 1);
                    _p.Fill(new RectangleF(cx, f.Y + 7, 1, f.Height - 14), Blk95);
                }
            }
            Text95($"{text.Length}/{ChatMax}", new RectangleF(f.Right - 60, f.Y, 54, f.Height), Gry95, Ui.F95, FontWeight.Normal, TextAlignment.Trailing);
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
            var b = m.KeyCap(_p, r, modState == 1 && !down, down, latched, Array.IndexOf(_hover, k) >= 0);
            uint ink = latched ? 0xFFFFFFFF : Blk95;
            var tr = b;

            if (k.Kind == Kind.Action)
                Text95(MacKeyName(k), tr, ink);
            else if (k.Icon == Theme.IcoEnter)
                Text95("return", tr, ink, Ui.F95, FontWeight.Bold);
            else if (k.Icon == Theme.IcoBack)
                Text95("delete", tr, ink, Ui.F95, FontWeight.Bold);
            else if (k.Icon != null && Glyph95(k.Icon) is { } g)
                _p.Pixels(g, MathF.Floor(tr.X + (tr.Width - g[0].Length) / 2), MathF.Floor(tr.Y + (tr.Height - g.Length) / 2), ink);
            else if (k.Fixed != null)
            {
                string name = MacKeyName(k);
                // the modifiers' names sit low on the left of their caps, as Apple printed them
                bool mod = k.Kind is Kind.Modifier or Kind.Caps || k.Fixed is "Esc" or "Tab" or "Del";
                if (mod && tr.Width >= _p.MeasureWidth(name, Ui.F95, FontWeight.Normal, Theme.RetroFont) + 10)
                    Text95(name, new RectangleF(tr.X + 5, tr.Bottom - 16, tr.Width - 8, 13), ink, Ui.F95, FontWeight.Normal, TextAlignment.Leading);
                else Text95(name, tr, ink);
            }
            else
            {
                string main = altgr && k.AltGr != null ? k.AltGr : shift ? k.Shifted ?? k.Normal : k.Normal;
                if (main != null && main.Length == 1 && char.IsLetter(main[0]) && Caps && !altgr)
                    main = shift ? main.ToLowerInvariant() : main.ToUpperInvariant();
                Text95(main, tr, ink, 16);
                if (!shift && k.Shifted != null && k.Normal != null && !char.IsLetter(k.Normal[0]))
                    Text95(k.Shifted, new RectangleF(tr.X + 4, tr.Y + 1, 14, 13), ink, Ui.F95, FontWeight.Normal, TextAlignment.Leading);
                if (!altgr && k.AltGr != null)
                    Text95(k.AltGr, new RectangleF(tr.Right - 18, tr.Bottom - 15, 14, 13), latched ? ink : Navy95, Ui.F95, FontWeight.Normal,
                        TextAlignment.Trailing);
            }
        }
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
    }
}
