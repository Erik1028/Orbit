using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The folder widget (WatchFolders) in the 95 family. Closed, each style's own (Windows 95 here; XP, 7, the Mac and the Terminal in their
// skin folders): a little window that is the folder - its icon, its name, what is in it as an Explorer view would list it. Opened,
// the folder's window over the whole watch, maximised, its widgets inside on the page's own cells (rows 1-3 of the 6 x 4 grid), drawn
// by the face's own code and live (Widgets95): Windows 95's Explorer - the title bar, the menu bar, the toolbar with the folder in its
// box and Up one level, the white client area. The close box and Up close it; a widget's own window opens over it and closes back to it.
public sealed partial class WristPanel
{
    // the area above the folder's cells when it is open: the page's first row and its gap (the cells start at y 58 on the canvas)
    static float FolderHead95 => WatchWidgets.CellRect(0, 0, 1, 1, true, GridSpec.Folder).Y - 4;

    static readonly string[] UpGlyph = { "...k...", "..kkk..", ".kkkkk.", "kkkkkkk", "..kkk..", "..kkk..", "..kkk.." };

    // ================= closed =================

    void Folder95Widget(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id)
    {
        var kids = WatchFolders.Kids(w);
        int n = Known(kids);
        switch (Theme.Retro)
        {
            case RetroStyle.XP: FolderTileXp(w, r, kids, n, d); return;
            case RetroStyle.Aero: FolderTileAero(w, r, kids, n, d); return;
            case RetroStyle.Mac: FolderTileMac(w, r, kids, n, d); return;
            case RetroStyle.Terminal: FolderTileTerm(w, r, kids, n, d); return;
        }
        FolderTile95(w, r, kids, n, d);
    }

    // Windows 95: one cell high, the folder's own icon with its name (the folder as the desktop shows it); two cells high, the folder's
    // window - its name on the title bar, the client white, what is in it with large icons where they fit, else as a list with the
    // values at hand (Details)
    void FolderTile95(WatchWidget w, RectangleF r, List<WatchWidget> kids, int n, PanelData d)
    {
        string name = WatchFolders.Name(w);
        bool titled = w.H >= 2;
        var c = Theme.RetroSkin.WidgetFrame(_p, r, titled ? Kept95(name, r.Width - 14, true) : null);
        if (!titled) { FolderIconName(c, name, n, Blk95); return; }
        var well = c;
        _p.Fill(well, Wht95);
        _p.Bevel(well, Painter.Edge.Sunken);
        FolderList95(RectangleF.Inflate(well, -2, -2), kids, n, d, Blk95, Theme.W95Navy);
    }

    // a folder at a glance: the big icon, the name under it - or, wide, beside it with how many things are in it
    void FolderIconName(RectangleF c, string name, int n, uint ink)
    {
        if (c.Width >= 90)
        {
            float iy = MathF.Floor(c.Y + (c.Height - 32) / 2);
            W95Icons.Draw(_p, W95Icons.Id.Folder, c.X + 4, iy, 2);
            float tx = c.X + 42, tw = c.Right - 2 - tx, ty = MathF.Floor(c.Y + (c.Height - 28) / 2);
            _ui.Text95(Kept95(name, tw, true), new RectangleF(tx, ty, tw, 14), ink, true);
            _ui.Text95(Kept95(Objects95(n), tw, false), new RectangleF(tx, ty + 15, tw, 13), Gry95);
            return;
        }
        float h = c.Height >= 46 ? 32 + 14 : 32;
        float y = MathF.Floor(c.Y + (c.Height - h) / 2);
        W95Icons.Draw(_p, W95Icons.Id.Folder, MathF.Floor(c.X + (c.Width - 32) / 2), y, 2);
        if (h > 32) _ui.Text95(Kept95(name, c.Width - 2, false), new RectangleF(c.X, y + 33, c.Width, 13), ink, false, TextAlignment.Center);
    }

    // what is in it, in white: large icons (32 px, the name under each) where two rows of them fit, else a row each - the small icon, the
    // name, the value at the right where it has one at hand; the last line says how many more
    void FolderList95(RectangleF a, List<WatchWidget> kids, int n, PanelData d, uint ink, uint sel)
    {
        if (n == 0) { _ui.Text95("(empty)", a, Gry95, false, TextAlignment.Center); return; }
        const float CellW = 48, CellH = 46;
        int cols = (int)(a.Width / CellW), rows = (int)(a.Height / CellH);
        if (cols >= 2 && rows >= 2)
        {
            int cap = cols * rows, shown = n <= cap ? n : cap - 1;
            float x0 = MathF.Floor(a.X + (a.Width - cols * CellW) / 2), y0 = a.Y + 2;
            int j = 0;
            foreach (var ch in kids)
            {
                var k = ch == null ? null : WatchWidgets.Find(ch.Type);
                if (k == null) continue;
                if (j >= shown) break;
                float x = x0 + j % cols * CellW, y = y0 + j / cols * CellH;
                W95Icons.Draw(_p, k.Icon95, x + 8, y, 2);
                _ui.Text95(Kept95(k.Title95, CellW - 2, false), new RectangleF(x, y + 33, CellW, 13), ink, false, TextAlignment.Center);
                j++;
            }
            if (n > cap)
                _ui.Text95(More95(n - shown), new RectangleF(x0 + j % cols * CellW, y0 + j / cols * CellH + 14, CellW, 13), Gry95, false, TextAlignment.Center);
            return;
        }
        const float Row = 17;
        int lines = Math.Max(1, (int)((a.Height + 1) / Row));
        int cap2 = lines, shown2 = n <= cap2 ? n : cap2 - 1;
        float ly = MathF.Floor(a.Y + (a.Height - Math.Min(n, cap2) * Row) / 2);
        int i = 0;
        foreach (var ch in kids)
        {
            var k = ch == null ? null : WatchWidgets.Find(ch.Type);
            if (k == null) continue;
            if (i >= shown2) break;
            float y = ly + i * Row;
            W95Icons.Draw(_p, k.Icon95, a.X + 1, y);
            string v = FolderValue(ch, d);
            float vw = v == null ? 0 : _ui.Width95(v) + 4;
            if (vw > a.Width * 0.45f) { v = null; vw = 0; }
            float tw = a.Width - 20 - vw;
            _ui.Text95(Kept95(k.Title95, tw, false), new RectangleF(a.X + 20, y + 1, tw, 13), ink);
            if (v != null) _ui.Text95(v, new RectangleF(a.Right - vw, y + 1, vw, 13), sel, false, TextAlignment.Trailing);
            i++;
        }
        if (n > cap2) _ui.Text95(More95(n - shown2), new RectangleF(a.X + 20, ly + i * Row + 1, a.Width - 20, 13), Gry95);
    }

    // ================= open =================

    // DrawDetail95 with a widget opened from a folder: the folder's window under it (a picture); the widget's window zooms from its place
    // on the folder's grid
    List<WatchWidget> FolderBehind95(PanelData d, List<WatchWidget> list)
    {
        if (!FolderHolds(list)) return list;
        bool inert = _ui.Inert;
        _ui.Inert = true;
        FolderWindowBody(d, _folder);
        _ui.Inert = inert;
        _grid = GridSpec.Folder;
        return _folder.Children;
    }

    void FolderEditView95(PanelData d, List<WatchWidget> list)
    {
        if (!EditFolderOn(list)) return;
        _open = _folder = null;
        _closing = false;
        bool inert = _ui.Inert;
        _ui.Inert = true;
        FolderWindowBody(d, EditFolder);
        _ui.Inert = inert;
    }

    // the folder opened: the style's zoom from its tile to the whole watch (and back), then its window
    void FolderWindow95(PanelData d, WatchWidgets.Kind k)
    {
        var full = new RectangleF(0, 0, TW95, TH95);
        var tile = WatchWidgets.Rect(_open, true, _grid);
        double zs = Motion.Any ? Theme.RetroSkin.ZoomSeconds : 0;
        double t = zs > 0 ? (Clock.Now - _openStart) / zs : 1;
        bool live = Theme.RetroSkin.LiveZoom;
        if (_closing)
        {
            if (t >= 1) { _open = null; _closing = false; return; }
            _ui.Animating = true;
            if (!live) { Zoom95(full, tile, (float)t); return; }
        }
        else if (t < 1)
        {
            _ui.Animating = true;
            if (!live) { Zoom95(tile, full, (float)t); return; }
        }
        // 7: the window itself grows from 90 % and fades in (closing the other way), taking no taps on its way - as its other windows
        float e = 1;
        if (live && zs > 0)
        {
            float tt = (float)Math.Clamp(t, 0, 1);
            e = _closing ? (1 - tt) * (1 - tt) : 1 - (1 - tt) * (1 - tt) * (1 - tt);
        }
        bool fx = e < 1, inert = _ui.Inert;
        if (fx)
        {
            _p.PushFade(e);
            _p.PushScale(0.9f + 0.1f * e, TW95 / 2f, TH95 / 2f);
            _ui.Inert = true;
        }
        FolderWindowBody(d, _open);
        if (fx)
        {
            _p.PopScale();
            _p.PopFade();
            _ui.Inert = inert;
        }
    }

    // the window: the style's chrome round the client, then the folder's widgets in it (or a word when there are none)
    void FolderWindowBody(PanelData d, WatchWidget f)
    {
        var kids = WatchFolders.Kids(f);
        int n = Known(kids);
        string name = WatchFolders.Name(f);
        var client = Theme.Retro switch
        {
            RetroStyle.XP => FolderChromeXp(name, n),
            RetroStyle.Aero => FolderChromeAero(name, n),
            RetroStyle.Mac => FolderChromeMac(name, n),
            RetroStyle.Terminal => FolderChromeTerm(name, n),
            _ => FolderChrome95(name, n),
        };
        if (n == 0)
        {
            var mid = new RectangleF(client.X, MathF.Floor(client.Y + client.Height / 2 - 14), client.Width, 13);
            _ui.Text95("This folder is empty.", mid, Theme.Retro == RetroStyle.Terminal ? Theme.RetroSkin.GrayText : Gry95, false, TextAlignment.Center);
            _ui.Text95(EmptyHint, new RectangleF(client.X, mid.Bottom + 3, client.Width, 13), Theme.Retro == RetroStyle.Terminal ? Theme.RetroSkin.GrayText : Gry95, false,
                TextAlignment.Center);
            return;
        }
        // XP's windows round their top corners over what is behind them: here the white client
        uint cb = XpSkin.CornerBack, wb = XpSkin.WidgetBack;
        if (Theme.Retro == RetroStyle.XP) XpSkin.CornerBack = XpSkin.WidgetBack = Wht95;
        var g = _grid;
        _grid = GridSpec.Folder;
        Widgets95(kids, GridSpec.Folder, d, DateTime.Now);
        _grid = g;
        (XpSkin.CornerBack, XpSkin.WidgetBack) = (cb, wb);
    }

    // Windows 95's Explorer, maximised (its frame off the screen): the title bar with the folder's icon and name, the menu bar, the
    // toolbar - the folder in its box, Up One Level - and the white client area, sunken
    RectangleF FolderChrome95(string name, int n)
    {
        float head = FolderHead95;
        _p.Fill(new RectangleF(0, 0, TW95, head), Theme.W95Face);
        _ui.Caption95(new RectangleF(0, 0, TW95, 18), Kept95(name, TW95 - 60, true), W95Icons.Id.Folder);
        if (_ui.CaptionClose95("wf95x", TW95 - 2 - 16, 2, true, 6)) CloseDetail();
        MenuBar95(4, 20, Menus95);
        _p.EtchedLine(0, TW95, 35);
        var box = new RectangleF(4, 37, 168, 16);
        Theme.RetroSkin.ComboBox(_p, box, "", true, false, false, false);
        W95Icons.Draw(_p, W95Icons.Id.Folder, box.X + 3, box.Y);
        _ui.Text95(Kept95(name, box.Width - 42, false), new RectangleF(box.X + 21, box.Y + 1, box.Width - 40, 14));
        if (_ui.GlyphButton95("wf95up", new RectangleF(box.Right + 6, 37, 23, 16), UpGlyph, true)) CloseDetail();
        _ui.Text95(Kept95(Objects95(n), TW95 - box.Right - 40, false), new RectangleF(box.Right + 34, 38, TW95 - box.Right - 38, 13), Gry95,
            false, TextAlignment.Trailing);
        var client = new RectangleF(0, head, TW95, TH95 - head);
        _p.Fill(client, Wht95);
        _p.Bevel(client, Painter.Edge.Sunken);
        return RectangleF.Inflate(client, -2, -2);
    }

    static readonly string[] Menus95 = { "&File", "&Edit", "&View", "&Help" };

    // a menu bar's words (their access keys underlined), from x
    void MenuBar95(float x, float y, string[] menus)
    {
        foreach (var m in menus)
        {
            float w = _ui.LabelWidth95(m);
            _ui.Label95(m, new RectangleF(x, y, w + 4, 13));
            x += w + 12;
        }
    }

    // ---- words, made once ----

    // "5 object(s)" (Windows 95's status words)
    string Objects95(int n)
    {
        FolderWords();
        if (n >= _fObj.Length) return Lang.F("{0} object(s)", n);
        return _fObj[n] ??= Lang.F("{0} object(s)", n);
    }
    readonly string[] _fObj = new string[24];

    string More95(int n) => Plus(n);

    // a text cut to a width in the style's font with "...", kept (a folder's name is the user's: cut once)
    string Kept95(string s, float w, bool bold)
    {
        int how = 16 + (bold ? 1 : 0) + (int)Theme.Retro * 2;
        return CutKept(s, w, Ui.F95, how, out var c) ? c : CutKeep(s, w, Ui.F95, how, Fit(s, w, Ui.F95, bold));
    }
}
