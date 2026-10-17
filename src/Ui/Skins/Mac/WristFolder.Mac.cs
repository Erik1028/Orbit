using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The folder widget on the classic Mac: the Finder. Closed, the folder's Finder window (its name on the title bar, two cells high) with
// the info strip ("5 items") and the icon view - the Mac's icons, the names under them, or a list where they do not fit; one cell high,
// the folder icon and its name, as on the desktop. Opened, its Finder window over the whole watch: the title bar with the close box,
// the info strip, the white icon view with the widgets in it.
public sealed partial class WristPanel
{
    void FolderTileMac(WatchWidget w, RectangleF r, List<WatchWidget> kids, int n, PanelData d)
    {
        string name = WatchFolders.Name(w);
        bool titled = w.H >= 2;
        var m = Mac;
        var c = m.WidgetFrame(_p, r, titled ? Kept95(name, r.Width - 40, true) : null);
        if (!titled) { FolderIconName(c, name, n, MacBlack); return; }
        // the info strip, a line under it, the icon view in white
        var info = new RectangleF(c.X, c.Y, c.Width, 13);
        _ui.Text95(Kept95(ItemsMac(n), info.Width - 6, false), new RectangleF(info.X + 3, info.Y, info.Width - 6, 13), MacBlack, false, TextAlignment.Center);
        _p.Fill(new RectangleF(c.X, info.Bottom, c.Width, 1), MacBlack);
        var a = RectangleF.FromLTRB(c.X, info.Bottom + 1, c.Right, c.Bottom);
        _p.Fill(a, MacWhite);
        FolderList95(RectangleF.Inflate(a, -2, -2), kids, n, d, MacBlack, MacBlack);
    }

    // the Finder window: the Platinum frame and title bar with its close box, the info strip with how many items, the icon view
    RectangleF FolderChromeMac(string name, int n)
    {
        var win = new RectangleF(0, 0, TW95, TH95);
        var m = Mac;
        var cl = m.MacWindow(_p, win, Kept95(name, TW95 - 90, true));
        if (_ui.CaptionClose95("wf95x", MacSkin.CloseX(win), MacSkin.BoxY(win), true, 6)) CloseDetail();
        float head = FolderHead95;
        var info = new RectangleF(cl.X + 4, cl.Y + 3, cl.Width - 8, 14);
        _ui.Text95(ItemsMac(n), new RectangleF(info.X, info.Y, info.Width / 2, 13), MacBlack);
        _ui.Text95("on the watch", new RectangleF(info.X + info.Width / 2, info.Y, info.Width / 2, 13), MacBlack, false, TextAlignment.Trailing);
        // the Finder's double line over the view
        _p.Fill(new RectangleF(cl.X, head - 4, cl.Width, 1), MacBlack);
        _p.Fill(new RectangleF(cl.X, head - 2, cl.Width, 1), MacBlack);
        var view = RectangleF.FromLTRB(cl.X, head - 1, cl.Right, cl.Bottom);
        _p.Fill(view, MacWhite);
        return view;
    }

    // "5 items" (the Finder's words)
    string ItemsMac(int n) => Items7(n);
}
