using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The floating Room and Hexpad panels in the Classic Mac style: Platinum windows with the close box on the left. The room is a
// Finder window in list view - the counts in its header strip, the columns under it; the Hexpad keeps its keys, buttons and
// knob groups, its page in the placards along the bottom, and asks with a Mac alert.
public sealed partial class RoomPanel
{
    partial void RoomMac(DateTime now, ref bool done)
    {
        done = true;
        var m = (MacSkin)Skins.Mac;
        _canvas95 ??= new Surface(_p.Gpu, W95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(W95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var win = new RectangleF(0, 0, W95, H95);
        m.MacWindow(_p, win, _s.InRoom && _s.World.Length > 0 ? Fit95(_s.World, W95 - 60) : "Room");
        if (_ui.CaptionClose95("rm95.x", MacSkin.CloseX(win), MacSkin.BoxY(win), true, 6)) Toggle();

        var people = Ordered();
        var lines = new List<(RoomWatcher.Person p, bool gone)>();
        foreach (var p in people) lines.Add((p, false));
        foreach (var p in _s.Gone) lines.Add((p, true));
        // the header strip: how many, the friends, VRCX
        int friends = _s.Here.FindAll(p => p.Friend).Count;
        float third = MathF.Floor((W95 - 16) / 3f);
        _ui.Text95(_s.InRoom ? (people.Count == 1 ? "1 person" : $"{people.Count} people") : "Not in a world", new RectangleF(10, 24, third, 15));
        _ui.Text95(friends == 1 ? "1 friend" : $"{friends} friends", new RectangleF(8 + third, 24, third, 15), Blk95, false, TextAlignment.Center);
        _ui.Text95(Fit95(_s.VrcxFound ? $"VRCX: {_s.Friends} friends" : "VRCX not found", third - 4), new RectangleF(W95 - 10 - third, 24, third, 15),
            Blk95, false, TextAlignment.Trailing);
        m.EtchedLine(_p, 2, W95 - 2, 41);
        var head = new RectangleF(8, 45, W95 - 16, 16);
        HeaderCell(new RectangleF(head.X, head.Y, 20, 16), "");
        HeaderCell(new RectangleF(head.X + 20, head.Y, head.Width - 20 - 100, 16), "Name");
        HeaderCell(new RectangleF(head.Right - 100, head.Y, 100, 16), "Joined");
        var box = new RectangleF(8, 61, W95 - 16, H95 - 8 - 61);
        if (lines.Count == 0)
        {
            m.Well(_p, box);
            _ui.Text95(Empty() + ".", new RectangleF(box.X + 8, box.Y + 6, box.Width - 16, 16));
        }
        else
        {
            int hit = _ui.List95("rm95.list", box, lines.Count, _sel95, (i, r, on) =>
            {
                var (p, gone) = lines[i];
                uint fg = on ? 0xFFFFFFFF : gone ? Gry95 : Blk95;
                _ui.Text95(Starred(p) ? "★" : "☆", new RectangleF(r.X + 4, r.Y, 14, r.Height), on ? fg : Starred(p) ? Blk95 : Gry95);
                string name = p.Name + (p.Friend ? "  (friend)" : "");
                _ui.Text95(Fit95(name, r.Width - 22 - 104), new RectangleF(r.X + 22, r.Y, r.Width - 126, r.Height), fg, p.Friend && !gone);
                string when = gone ? "left " + Ago(p.Left, now) : Ago(p.Joined, now);
                _ui.Text95(Fit95(when, 96), new RectangleF(r.Right - 98, r.Y, 96, r.Height), fg);
            });
            if (hit >= 0)
            {
                _sel95 = hit;
                if (_ui.Mouse.X < box.X + 22) ToggleStar(lines[hit].p);
            }
        }
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }
}

public sealed partial class DockPanel
{
    partial void DockMac(double now, ref bool done)
    {
        done = true;
        var m = (MacSkin)Skins.Mac;
        _canvas95 ??= new Surface(_p.Gpu, W95, H95);
        _ui.Begin(_ptr, 2);
        _ui.Canvas95 = new SizeF(W95, H95);
        _p.BeginPixel(_canvas95.Bitmap, 0x00000000);
        var win = new RectangleF(0, 0, W95, H95);
        string title = string.IsNullOrEmpty(_s.Profile) ? "Hexpad" : $"Hexpad - {_s.Profile}";
        m.MacWindow(_p, win, Fit95(title, W95 - 60));
        if (_ui.CaptionClose95("dk95.x", MacSkin.CloseX(win), MacSkin.BoxY(win), true, 6)) Toggle();

        bool show = Live || _s.State == HexpadLink.LinkState.ScreenOff && _faces.Any;
        if (!show)
        {
            var msg = new RectangleF(8, 27, 228, 149);
            m.Well(_p, msg);
            W95Icons.Draw(_p, W95Icons.Id.Info32, msg.X + 14, msg.Y + 16);
            _ui.Text95(StateLine() + ".", new RectangleF(msg.X + 54, msg.Y + 18, msg.Width - 64, msg.Height - 30), Blk95, false,
                TextAlignment.Leading, true, true);
        }
        for (int i = 0; i < HexpadLink.Keys && show; i++)
        {
            string id = $"key{i + 1}";
            var box = new RectangleF(8 + i % 3 * 78, 27 + i / 3 * 78, 72, 72);
            _hit[id] = Tex(box);
            bool down = _pressed == id;
            _p.Fill(box, Blk95);
            var bmp = _faces.Bitmaps[i];
            if (bmp != null && !_s.Dark)
                _p.ImageNearest(bmp, new RectangleF(box.X + 2 + (down ? 1 : 0), box.Y + 2 + (down ? 1 : 0), 68, 68));
            _p.Bevel(box, down ? Painter.Edge.Pressed : Painter.Edge.Sunken);
            if (_hover == id || Flash(id, now)) Dotted(RectangleF.Inflate(box, -4, -4), Flash(id, now) ? 0xFFFFFF00 : Wht95);
        }
        for (int col = 0; col < 3; col++)
        {
            string id = $"btn{col + 7}";
            var r = new RectangleF(8 + col * 78, 183, 72, 22);
            _hit[id] = Tex(r);
            var inner = _p.ButtonFace95(r, false, _pressed == id || Flash(id, now), false);
            _ui.Text95(Fit95(Ctl(id)?.Label ?? "", inner.Width - 4), inner, Blk95, false, TextAlignment.Center, show);
        }
        for (int k = 0; k < Knobs.Length; k++)
            SliderGroup95(Knobs[k], new RectangleF(243, 24 + k * 63, 152, 61), show, now);
        // the page, the name and the state as placards along the bottom (the Finder's)
        string pages = _s.InFolder ? (_s.FolderPages > 1 ? $"Folder {_s.FolderPage + 1} of {_s.FolderPages}" : "Folder")
            : $"Page {_s.Page + 1} of {_s.Pages}";
        string name = _s.InFolder ? _s.FolderName : _s.PageName;
        string state = Live ? (_s.Ambient ? "Idle" : "Connected") : StateLine();
        if (show)
        {
            _ui.Pane95(new RectangleF(4, 214, 96, 20), pages);
            _ui.Pane95(new RectangleF(102, 214, 140, 20), Fit95(name, 130));
            _ui.Pane95(new RectangleF(244, 214, W95 - 248, 20), Fit95(state, W95 - 262), Live ? 0xFF008000 : 0xFF808080);
        }
        else _ui.Pane95(new RectangleF(4, 214, W95 - 8, 20), Fit95(StateLine(), W95 - 20));
        if (_confirm != null) ConfirmMac();
        _p.End();
        _p.Begin(Surf.Bitmap, 0x00000000);
        _p.ImageNearest(_canvas95.Bitmap, new RectangleF(0, 0, TW, TH));
        _p.End();
        _ui.End();
    }

    // a Mac alert: the caution icon, the question, No and the ringed Yes on the right
    void ConfirmMac()
    {
        string label = ConfirmLabel();
        var box = new RectangleF(W95 / 2 - 130, H95 / 2 - 52, 260, 104);
        MacSkin.AlertFrame(_p, box);
        MacSkin.Caution(_p, box.X + 14, box.Y + 14);
        _ui.Text95(Fit95($"Run \"{label}\"?", box.Width - 70), new RectangleF(box.X + 56, box.Y + 14, box.Width - 66, 16), Blk95, true);
        _ui.Text95("It could end your VR session.", new RectangleF(box.X + 56, box.Y + 32, box.Width - 66, 16));
        if (_ui.Button95("dk95.no", new RectangleF(box.Right - 14 - 75 - 10 - 75, box.Bottom - 35, 75, 23), "No")) _in.ConfirmNo();
        if (_ui.Button95("dk95.yes", new RectangleF(box.Right - 12 - 75, box.Bottom - 37, 75, 26), "Yes", true, true)) _in.ConfirmYes();
    }
}
