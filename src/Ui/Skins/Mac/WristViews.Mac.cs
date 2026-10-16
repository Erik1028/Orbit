using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The opened Room, Hexpad and Minesweeper views in the Classic Mac style: Platinum windows with the close box on the left. The
// room is a Finder window in list view (the counts in its header strip, the columns under it), the Hexpad a control panel, the
// game its board with a Platinum window beside it.
public sealed partial class WristPanel
{
    // ---- the room: a Finder list view ----

    void RoomMacDetail(RectangleF win, PanelData d)
    {
        var m = Mac;
        var s = d.Room;
        var now = DateTime.Now;
        bool inRoom = s != null && s.InRoom;
        string title = inRoom && s.World.Length > 0 ? s.World : "Room";
        m.MacWindow(_p, win, Fit(title, win.Width - 60, Ui.F95, true));
        _roomClose95 = new RectangleF(MacSkin.CloseX(win), MacSkin.BoxY(win), 16, 14);
        if (_ui.CaptionClose95("wr95x", MacSkin.CloseX(win), MacSkin.BoxY(win), true, 6)) { CloseDetail(); return; }
        var here = RoomHere(s);
        int n = here.Count, gone = s?.Gone.Count ?? 0, count = n + gone, f = 0;
        foreach (var p in here) if (p.Friend) f++;
        var starred = _cfg.Data.Room.Starred;
        float x0 = win.X + 6, w = win.Width - 12;
        const float JoinW = 86;
        // the header strip: how many are here, the friends (VRCX), how long you have been there
        var strip = new RectangleF(x0, win.Y + 25, w, 16);
        string a = s == null ? "Not following" : !s.LogFound ? "No VRChat log" : !s.InRoom ? "Not in a world" : RoomCountText(n);
        bool vrcx = s?.VrcxFound == true;
        string b = vrcx ? RoomFriendsText(f) : "No VRCX";
        string stay = RoomStay(s, now);
        stay = stay == null ? "" : char.ToUpperInvariant(stay[0]) + stay[1..];
        float third = MathF.Floor(w / 3);
        _ui.Text95(Fit(a, third - 4), new RectangleF(strip.X + 2, strip.Y, third - 2, 15), MacBlack);
        _ui.Text95(Fit(b, third - 4), new RectangleF(strip.X + third, strip.Y, third, 15), MacBlack, false, TextAlignment.Center);
        _ui.Text95(Fit(stay, third - 4), new RectangleF(strip.Right - third, strip.Y, third - 2, 15), MacBlack, false, TextAlignment.Trailing);
        m.EtchedLine(_p, win.X + 2, win.Right - 2, strip.Bottom + 1);
        // the column headings of a list view
        var head = new RectangleF(x0, strip.Bottom + 4, w, 16);
        Head95(new RectangleF(head.X, head.Y, 20, 16), "");
        Head95(new RectangleF(head.X + 20, head.Y, head.Width - 20 - JoinW, 16), "Name");
        Head95(new RectangleF(head.Right - JoinW, head.Y, JoinW, 16), "Joined");
        var box = new RectangleF(x0, head.Bottom, w, win.Bottom - 6 - head.Bottom);
        if (count == 0)
        {
            m.Well(_p, box);
            string why = s == null ? Lang.T("Not following VRChat. Turn on \"Follow VRChat's log\" in Settings.") : Lang.T(RoomPanel.EmptyText(s)) + ".";
            if (s != null && s.LogFound && !s.VrcxFound) why += Lang.T(" Install VRCX to see your friends first.");
            _ui.Text95(why, new RectangleF(box.X + 8, box.Y + 6, box.Width - 16, box.Height - 12), MacBlack, false, TextAlignment.Leading, true, true);
            return;
        }
        _ui.List95("wr95list", box, count, -1, (i, r, on) =>
        {
            bool g = i >= n;
            var p = g ? s.Gone[i - n] : here[i];
            bool star = starred.Contains(p.Key);
            uint fg = g ? Gry95 : MacBlack;
            _ui.Text95(star ? "★" : "☆", new RectangleF(r.X + 4, r.Y, 14, r.Height), star ? MacBlack : Gry95);
            bool bold = p.Friend && !g;
            float nw = r.Width - 20 - JoinW - 4;
            _ui.Text95(Fit(p.Name + (p.Friend ? Lang.T("  (friend)") : ""), nw, Ui.F95, bold), new RectangleF(r.X + 22, r.Y, nw, r.Height), fg, bold);
            string when = g ? RoomPanel.LeftText(p.Left, now, 0) : RoomPanel.Ago(p.Joined, now);
            float jw = MathF.Min(JoinW - 2, r.Right - (head.Right - JoinW) - 2);
            _ui.Text95(Fit(when, jw), new RectangleF(head.Right - JoinW + 4, r.Y, jw, r.Height), fg);
        });
    }

    // ---- the Hexpad: a control panel window ----

    void DockMacDetail(RectangleF win, PanelData d)
    {
        var m = Mac;
        var I = DockSync(d);
        var s = I.Snap;
        _dockReady = true;
        double now = Clock.Now;
        bool show = DockShown(I, d);
        string name = s.InFolder ? s.FolderName : s.PageName;
        string pages = s.InFolder ? (s.FolderPages > 1 ? $"{s.FolderPage + 1}/{s.FolderPages}" : "") : $"{s.Page + 1}/{s.Pages}";
        string title = show ? $"Hexpad - {name}{(pages.Length > 0 ? $" ({pages})" : "")}{(s.Ambient ? " - " + Lang.T("Idle") : "")}" : "Hexpad";
        m.MacWindow(_p, win, Fit(title, win.Width - 60, Ui.F95, true));
        _dockClose95 = new RectangleF(MacSkin.CloseX(win), MacSkin.BoxY(win), 16, 14);
        if (_ui.CaptionClose95("wdk95x", MacSkin.CloseX(win), MacSkin.BoxY(win), true, 6)) { CloseDetail(); return; }
        if (!show)
        {
            var msg = new RectangleF(win.X + 6, win.Y + 28, win.Width - 12, win.Height - 34);
            m.Well(_p, msg);
            if (d.Dock?.State == HexpadLink.LinkState.Connecting) MacSkin.WatchCursor(_p, msg.X + 14, msg.Y + 16, 2);     // being looked for
            else W95Icons.Draw(_p, W95Icons.Id.Dock, msg.X + 14, msg.Y + 16, 2);
            string line = d.Dock == null ? "Hexpad is not connected." : Lang.T(I.StateLine()) + ".";
            _ui.Text95(line, new RectangleF(msg.X + 54, msg.Y + 18, msg.Width - 64, msg.Height - 30), MacBlack, false, TextAlignment.Leading, true, true);
            return;
        }
        // the six keys: the dock's own pixels in black wells
        for (int i = 0; i < HexpadLink.Keys; i++)
        {
            string id = DockKeys[i];
            var box = new RectangleF(win.X + 6 + i % 3 * 76, win.Y + 27 + i / 3 * 76, 72, 72);
            I.Hit[id] = Tex2(box);
            bool down = I.Pressed == id;
            _p.Fill(box, MacBlack);
            var bmp = d.DockFaces?.Bitmaps[i];
            if (bmp != null && !s.Dark) _p.ImageNearest(bmp, new RectangleF(box.X + 2 + (down ? 1 : 0), box.Y + 2 + (down ? 1 : 0), 68, 68));
            _p.Bevel(box, down ? Painter.Edge.Pressed : Painter.Edge.Sunken);
            if (I.Hover == id || I.Flash(id, now)) Dotted95(RectangleF.Inflate(box, -4, -4), I.Flash(id, now) ? 0xFFFFFF00 : MacWhite);
        }
        for (int col = 0; col < 3; col++)
        {
            string id = DockBtns[col];
            var r = new RectangleF(win.X + 6 + col * 76, win.Y + 27 + 152, 72, 22);
            I.Hit[id] = Tex2(r);
            var inner = _p.ButtonFace95(r, false, I.Pressed == id || I.Flash(id, now), false);
            _ui.Text95(Fit(I.Ctl(id)?.Label ?? "", inner.Width - 4), inner, MacBlack, false, TextAlignment.Center);
        }
        float gx = win.X + 6 + 3 * 72 + 2 * 4 + 4;
        for (int k = 0; k < DockKnobs.Length; k++)
            DockKnob95(I, DockKnobs[k], new RectangleF(gx, win.Y + 23 + k * 62, win.Right - 6 - gx, 60), now);
        if (I.Confirm != null) DockConfirmMac(I, win);
    }

    // a Mac alert: no title bar, the caution icon, the question in Chicago, the buttons on the right (the default one ringed)
    void DockConfirmMac(DockInput I, RectangleF win)
    {
        var m = Mac;
        string label = I.ConfirmLabel();
        var box = new RectangleF(MathF.Floor(win.X + win.Width / 2 - 130), MathF.Floor(win.Y + win.Height / 2 - 52), 260, 104);
        MacSkin.AlertFrame(_p, box);
        MacSkin.Caution(_p, box.X + 14, box.Y + 14);
        _ui.Text95(Fit(Lang.F("Run \"{0}\"?", label), box.Width - 70, Ui.F95, true), new RectangleF(box.X + 56, box.Y + 14, box.Width - 66, 16), MacBlack, true);
        _ui.Text95("It could end your VR session.", new RectangleF(box.X + 56, box.Y + 32, box.Width - 66, 16));
        if (_ui.Button95("wdk95no", new RectangleF(box.Right - 14 - 75 - 10 - 75, box.Bottom - 35, 75, 23), "No")) I.ConfirmNo();
        if (_ui.Button95("wdk95yes", new RectangleF(box.Right - 12 - 75, box.Bottom - 37, 75, 26), "Yes", true, true)) I.ConfirmYes();
    }

    // ---- Minesweeper: its board, a Platinum window beside it ----

    void MinesMacDetail(RectangleF win)
    {
        var m = Mac;
        var g = _mines[0];
        double now = Clock.Now;
        int cw = MineArt.ClientW(g.W), ch = MineArt.ClientH(g.H);
        var cl = new RectangleF(win.X, win.Y + MathF.Floor((win.Height - ch) / 2), cw, ch);
        var board = new RectangleF(cl.X + MineArt.BoardX, cl.Y + MineArt.BoardY, g.W * 16, g.H * 16);
        MinesInput(g, board, 16, now);
        var (press, prog, chord) = MinesPressInfo(g, now);
        var fb = new RectangleF(cl.X + MineArt.FaceX(g.W) - 1, cl.Y + MineArt.FaceY - 1, 26, 26);
        _mFaceBtn = fb;
        bool newGame = _ui.Clickable("mn95face", fb, out _, out bool faceHeld);
        int secs = (int)Math.Min(999, g.Seconds(now));
        bool rise = press >= 0 && prog > 0, had = rise && g.IsFlag(press);
        int rows = rise ? (int)MathF.Round((had ? 1 - prog : prog) * (MineArt.FlagBottom - MineArt.FlagTop)) : 0;
        var face = faceHeld ? MineArt.Face.Smile : MacFace(g, MinesFace95(g));
        MacSkin.Mines.Draw(_p, g, cl, secs, face, faceHeld, i => rise && i == press ? (had || _mFlagMode ? MineArt.Sq.Hidden : MineArt.Sq.Open) : Square95(g, i, press, chord),
            press, rows);
        if (cl.Bottom < win.Bottom)
            _p.Fill(new RectangleF(cl.X, cl.Bottom, cw, win.Bottom - cl.Bottom), C.Background == "cards" ? Theme.W95Face : Theme.W95Teal);
        if (newGame) MinesNew();
        var sw = new RectangleF(cl.Right, win.Y, win.Right - cl.Right, win.Height);
        m.MacWindow(_p, sw, "Mines");
        _mCloseBtn = new RectangleF(MacSkin.CloseX(sw), MacSkin.BoxY(sw), 16, 14);
        if (_ui.CaptionClose95("wd95x", MacSkin.CloseX(sw), MacSkin.BoxY(sw), true, 6)) { CloseDetail(); return; }
        float x0 = sw.X + 8, w = sw.Width - 16;
        if (_ui.Button95("mn95new", new RectangleF(x0, sw.Y + 31, w, 23), "New Game")) MinesNew();
        var flagBtn = new RectangleF(x0, sw.Y + 60, w, 23);
        _mFlagBtn = flagBtn;
        if (_ui.Button95("mn95flag", flagBtn, "Flag mode", true, false, _mFlagMode, W95Icons.Id.Flag)) { _mFlagMode = !_mFlagMode; _dirty = true; }
        float ty = flagBtn.Bottom + 10;
        switch (g.State)
        {
            case MinesGame.Phase.Won:
                _ui.Text95("You win!", new RectangleF(x0, ty, w, 15), MacBlack, true);
                _ui.Text95("Click the face to play again.", new RectangleF(x0, ty + 16, w, 30), MacBlack, false, TextAlignment.Leading, true, true);
                break;
            case MinesGame.Phase.Lost:
                _ui.Text95("Game over.", new RectangleF(x0, ty, w, 15), MacBlack, true);
                _ui.Text95("Click the face to play again.", new RectangleF(x0, ty + 16, w, 30), MacBlack, false, TextAlignment.Leading, true, true);
                break;
            default:
                _ui.Text95(g.State == MinesGame.Phase.Ready ? "Click a square to start." : "Hold a square to flag it.", new RectangleF(x0, ty, w, 30),
                    MacBlack, false, TextAlignment.Leading, true, true);
                break;
        }
        int best = MinesBestOf(0);
        _ui.Text95(best > 0 ? Lang.F("Best time: {0} s", best) : "Best time: none", new RectangleF(x0, sw.Bottom - 8 - 15, w, 15));
    }
}
