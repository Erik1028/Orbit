using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The folder widget in Windows 7. Closed, a jump list: the light menu with its frosted edge, the folder's name as its heading, what is in
// it a row each (an icon, the name, the value where one is at hand); one cell high, 7's manila folder and its name on a glass tile.
// Opened, Explorer: the glass frame with the back button, the address bar's breadcrumbs (Watch > the folder) and the search box, the
// command bar, the white client with the widgets.
public sealed partial class WristPanel
{
    static readonly string[] BackAero = { "...k...", "..kk...", ".kkkkkk", "kkkkkkk", ".kkkkkk", "..kk...", "...k..." };

    void FolderTileAero(WatchWidget w, RectangleF r, List<WatchWidget> kids, int n, PanelData d)
    {
        string name = WatchFolders.Name(w);
        if (w.H < 2)
        {
            // a gadget: the folder big on the glass, its name under it (wide: beside it, with how many)
            AeroSkin.Tile(_p, r, 0xFF68ABEC, 0xFF3C7FCC);
            if (r.Width >= 90)
            {
                Folder7(r.X + 6, MathF.Floor(r.Y + (r.Height - 32) / 2), 32);
                float tx = r.X + 44, tw = r.Right - 4 - tx;
                Ae.GlowLight(_p, Kept95(name, tw, false), new RectangleF(tx, r.Y + r.Height / 2 - 15, tw, 15), 11, TextAlignment.Leading);
                _p.Text(Kept95(Items7(n), tw, false), new RectangleF(tx, r.Y + r.Height / 2 + 1, tw, 14), 0xFFD5E6F8, 11, FontWeight.Normal,
                    TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
                return;
            }
            Folder7(MathF.Floor(r.X + (r.Width - 24) / 2), r.Y + 5, 24);
            Ae.GlowLight(_p, Kept95(name, r.Width - 6, false), new RectangleF(r.X + 2, r.Bottom - 18, r.Width - 4, 15), 11);
            return;
        }
        // the jump list: its edge, the menu's light face, the heading in 7's blue, a line under it
        _p.Smooth(true);
        _p.FillRound(r, 0xFF97A7BC, 4);
        _p.Smooth(false);
        var i = RectangleF.Inflate(r, -1, -1);
        _p.GradientRaw(i, 0xFFFCFDFE, 0xFFEEF2F8, 3);
        _p.Text(Kept95(name, i.Width - 14, false), new RectangleF(i.X + 7, i.Y + 3, i.Width - 12, 16), 0xFF1E3287, 11, FontWeight.Normal,
            TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
        _p.Fill(new RectangleF(i.X + 6, i.Y + 20, i.Width - 12, 1), 0xFFD5DFEA);
        var a = RectangleF.FromLTRB(i.X + 4, i.Y + 23, i.Right - 4, i.Bottom - 3);
        if (n == 0) { _p.Text("(empty)", a, SubAero, 11, FontWeight.Normal, TextAlignment.Center, ParagraphAlignment.Center, Ae.Font); return; }
        const float Row = 18;
        int lines = Math.Max(1, (int)(a.Height / Row)), shown = n <= lines ? n : lines - 1;
        int j = 0;
        foreach (var c in kids)
        {
            var k = c == null ? null : WatchWidgets.Find(c.Type);
            if (k == null) continue;
            if (j >= shown) break;
            float y = a.Y + j * Row;
            W95Icons.Draw(_p, k.Icon95, a.X + 2, y + 1);
            string v = FolderValue(c, d);
            float vw = v == null ? 0 : WAero(v, 11) + 4;
            if (vw > a.Width * 0.45f) { v = null; vw = 0; }
            float tw = a.Width - 22 - vw;
            _p.Text(Kept95(k.Name, tw, false), new RectangleF(a.X + 22, y, tw + 2, Row), InkAero, 11, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, Ae.Font);
            if (v != null)
                _p.Text(v, new RectangleF(a.Right - vw, y, vw, Row), SubAero, 11, FontWeight.Normal, TextAlignment.Trailing, ParagraphAlignment.Center, Ae.Font);
            j++;
        }
        if (n > lines)
            _p.Text(Plus(n - shown), new RectangleF(a.X + 22, a.Y + j * Row, a.Width - 22, Row), SubAero, 11, FontWeight.Normal, TextAlignment.Leading,
                ParagraphAlignment.Center, Ae.Font);
    }

    // 7's folder: manila - the back with its tab, the front a lighter gradient over it (s: 16, 24 or 32)
    void Folder7(float x, float y, float s)
    {
        float u = s / 16f;
        _p.Smooth(true);
        _p.FillRound(new RectangleF(x + 1 * u, y + 2 * u, 6 * u, 3 * u), 0xFFC9962F, 1 * u);
        _p.FillRound(new RectangleF(x + 1 * u, y + 3 * u, 14 * u, 11 * u), 0xFFD9A93E, 1.2f * u);
        _p.Smooth(false);
        _p.GradientRaw(new RectangleF(x + 1 * u, y + 5.5f * u, 14 * u, 8.5f * u), 0xFFFFEDB0, 0xFFF0C55B, 1.2f * u);
    }

    // Explorer: the glass frame - the close box, the back button, the address bar with the breadcrumbs, the search box; the command bar
    // under it; the white client
    RectangleF FolderChromeAero(string name, int n)
    {
        var win = new RectangleF(0, 0, TW95, TH95);
        var cl = AeroSkin.Window7(_p, win, 38, Wht95, 2);
        var rc = new RectangleF(win.Right - 6 - 43, win.Y + 1, 43, 18);
        bool x = _ui.Clickable("wf95x", RectangleF.Inflate(rc, 4, 4), out bool xh, out bool xd);
        AeroSkin.CaptionBox(_p, rc, CaptionKind.Close, true, xd, xh);
        if (x) CloseDetail();
        // back: 7's blue orb with its arrow (to the watch's page)
        var orb = new RectangleF(4, 20, 16, 16);
        bool back = _ui.Clickable("wf95up", orb, out bool bh, out bool bd);
        AeroSkin.Orb(_p, 12, 28, 8, true, bh, bd, BackAero, true);
        if (back) CloseDetail();
        // the address bar: Watch > the folder; the search box beside it
        var addr = new RectangleF(26, 21, 196, 15);
        _p.Fill(addr, Theme.WithAlpha(0xFFFFFFFF, 0.92f));
        _p.Stroke(new RectangleF(addr.X + 0.5f, addr.Y + 0.5f, addr.Width - 1, addr.Height - 1), 0xFF8A9BB0, 1);
        Folder7(addr.X + 2, addr.Y, 15);
        float bx = addr.X + 21;
        _p.Text("▸", new RectangleF(bx, addr.Y, 8, addr.Height), SubAero, 9, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
        float ww = WAero(Lang.T("Watch"), 11);
        _p.Text("Watch", new RectangleF(bx + 9, addr.Y - 1, ww + 4, addr.Height + 2), InkAero, 11, FontWeight.Normal, TextAlignment.Leading,
            ParagraphAlignment.Center, Ae.Font);
        float nx = bx + 9 + ww + 4;
        _p.Text("▸", new RectangleF(nx, addr.Y, 8, addr.Height), SubAero, 9, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
        _p.Text(Kept95(name, addr.Right - nx - 14, false), new RectangleF(nx + 9, addr.Y - 1, addr.Right - nx - 12, addr.Height + 2), InkAero, 11,
            FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
        var search = new RectangleF(addr.Right + 6, 21, TW95 - 4 - addr.Right - 6, 15);
        _p.Fill(search, Theme.WithAlpha(0xFFFFFFFF, 0.92f));
        _p.Stroke(new RectangleF(search.X + 0.5f, search.Y + 0.5f, search.Width - 1, search.Height - 1), 0xFF8A9BB0, 1);
        _p.Text(Kept95(Search7(name), search.Width - 8, false), new RectangleF(search.X + 4, search.Y - 1, search.Width - 6, search.Height + 2), 0xFF8D99A8, 11,
            FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, Ae.Font);
        // the command bar: its light blue band, Organize and how many items
        float head = FolderHead95;
        var bar = new RectangleF(cl.X, cl.Y, cl.Width, head - cl.Y);
        _p.GradientRaw(bar, 0xFFF5F9FD, 0xFFDCE6F4);
        _p.Fill(new RectangleF(bar.X, bar.Bottom - 1, bar.Width, 1), 0xFFA0AFC3);
        _p.Text("Organize ▾", new RectangleF(bar.X + 6, bar.Y, 90, bar.Height), InkAero, 11, FontWeight.Normal, TextAlignment.Leading,
            ParagraphAlignment.Center, Ae.Font);
        _p.Text(Items7(n), new RectangleF(bar.X + 100, bar.Y, bar.Width - 106, bar.Height), SubAero, 11, FontWeight.Normal, TextAlignment.Trailing,
            ParagraphAlignment.Center, Ae.Font);
        return RectangleF.FromLTRB(cl.X, head, cl.Right, cl.Bottom);
    }

    // "5 items", "Search VRChat": made once
    string Items7(int n)
    {
        FolderWords();
        if (n >= _fItems.Length) return Lang.F("{0} items", n);
        return _fItems[n] ??= n == 1 ? Lang.T("1 item") : Lang.F("{0} items", n);
    }
    readonly string[] _fItems = new string[24];

    string _fSearchOf, _fSearch;
    int _fSearchLang = -1;
    string Search7(string name)
    {
        if (!ReferenceEquals(name, _fSearchOf) || _fSearchLang != Lang.Version) { _fSearchOf = name; _fSearchLang = Lang.Version; _fSearch = Lang.F("Search {0}", name); }
        return _fSearch;
    }
}
