using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The folder widget in Windows XP: Luna's Explorer. Closed, the folder's little window (XP's frame and blue title bar) - white, the blue
// task pane down its left where it is wide enough, what is in it as large icons or a list; one cell high, the folder's icon and name.
// Opened, Explorer maximised: the blue title bar, the menu bar, Back and Up and the Address bar, the white client with the widgets.
public sealed partial class WristPanel
{
    static readonly string[] XpBackGlyph = { "...k...", "..kk...", ".kkkkkk", "kkkkkkk", ".kkkkkk", "..kk...", "...k..." };
    static readonly string[] MenusXp = { "&File", "&Edit", "&View", "F&avorites", "&Tools", "&Help" };

    void FolderTileXp(WatchWidget w, RectangleF r, List<WatchWidget> kids, int n, PanelData d)
    {
        string name = WatchFolders.Name(w);
        bool titled = w.H >= 2;
        var c = Theme.RetroSkin.WidgetFrame(_p, r, titled ? Kept95(name, r.Width - 10, true) : null);
        if (!titled) { FolderIconName(c, name, n, Blk95); return; }
        _p.Fill(c, Wht95);
        var a = c;
        if (c.Width >= 140)
        {
            // the task pane: XP's blue strip with the folder on it
            var pane = new RectangleF(c.X, c.Y, 30, c.Height);
            RetroSkin.ColumnsGradient(_p, pane, 0xFF7BA2E7, 0xFF6375D6);
            W95Icons.Draw(_p, W95Icons.Id.Folder, pane.X + 7, pane.Y + 6);
            a = RectangleF.FromLTRB(pane.Right + 1, c.Y, c.Right, c.Bottom);
        }
        FolderList95(RectangleF.Inflate(a, -2, -2), kids, n, d, Blk95, XpBlueText);
    }

    // Explorer, maximised: its title bar square at the top (the frame off the screen), the menu bar, the toolbar with Back, Up and the
    // Address bar, the white client in XP's thin blue-grey edge
    RectangleF FolderChromeXp(string name, int n)
    {
        float head = FolderHead95;
        _p.Fill(new RectangleF(0, 0, TW95, head), Theme.W95Face);
        XpSkin.CornerBack = C.Background != "cards" ? XpSkin.DesktopBlue : 0;
        _ui.Caption95(new RectangleF(3, 3, TW95 - 6, 18), Kept95(name, TW95 - 70, true), W95Icons.Id.Folder);
        if (_ui.CaptionClose95("wf95x", TW95 - 5 - 16, 5, true, 6)) CloseDetail();
        MenuBar95(5, 22, MenusXp);
        _p.Fill(new RectangleF(0, 36, TW95, 1), 0xFFC5C2B2);
        // Back: the green disc with its white arrow and the word, a frame round it under the laser
        var back = new RectangleF(2, 38, 52, 15);
        bool click = _ui.Clickable("wf95up", back, out bool hover, out bool held);
        if (hover || held) { _p.Fill(back, held ? 0xFFE3E1D7 : 0xFFF6F5F0); _p.Stroke(new RectangleF(back.X + 0.5f, back.Y + 0.5f, back.Width - 1, back.Height - 1), 0xFFC1BCA9, 1); }
        _p.Circle(back.X + 9, back.Y + 7.5f, 6.5f, 0xFF2E9B2E);
        _p.Pixels(XpBackGlyph, back.X + 6, back.Y + 4, 0xFFFFFFFF);
        _ui.Text95("Back", new RectangleF(back.X + 18, back.Y + 1, 34, 13));
        if (click) CloseDetail();
        // the Address bar: the folder in its box
        _ui.Text95("Address", new RectangleF(60, 39, 44, 13), XpGrey);
        float ax = 60 + _ui.Width95("Address") + 5;
        var box = new RectangleF(ax, 38, TW95 - 4 - ax, 15);
        Theme.RetroSkin.ComboBox(_p, box, "", true, false, false, false);
        W95Icons.Draw(_p, W95Icons.Id.Folder, box.X + 2, box.Y);
        _ui.Text95(Kept95(name, box.Width - 40, false), new RectangleF(box.X + 20, box.Y + 1, box.Width - 38, 13));
        var client = new RectangleF(0, head, TW95, TH95 - head);
        _p.Fill(client, Wht95);
        _p.Bevel(client, Painter.Edge.Sunken);
        return RectangleF.Inflate(client, -2, -2);
    }
}
