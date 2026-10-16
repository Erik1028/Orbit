using System;
using System.Drawing;
using System.Globalization;
using Vortice.DirectWrite;
using T = Orbit.TerminalSkin;

namespace Orbit;

// A notification on a green terminal: the way a message reaches every terminal of a Unix box (wall / write) - a little CRT with
// "Broadcast message from VRCX (tty1) (Fri Oct 16 18:39):", the title in capitals under it and the text. No pictures: the app's
// icon is a bitmap, the tube has none.
public sealed partial class NotificationPanel
{
    partial void CardTerm(Notification c, float y, ref bool done)
    {
        done = true;
        var sk = T.It;
        var r = new RectangleF(10, y, TW / 2 - 20, CardH / 2);
        _p.Fill(r, T.BG);
        T.Crt(_p, r);
        float x = r.X + 10, w = r.Width - 20, ty = r.Y + 8;
        var now = DateTime.Now;
        string from = string.IsNullOrEmpty(c.Source) ? "orbit" : c.Source;
        string head = $"Broadcast message from {from} (tty1) ({now.ToString("ddd MMM d HH:mm", CultureInfo.InvariantCulture)}):";
        if (T.W(head) > w) head = $"Broadcast message from {from}:";
        if (T.W(head) > w) head = sk.Fit(_p, head, w);
        sk.Put(_p, head, x, ty, T.DIM);
        ty += 15;
        if (!string.IsNullOrEmpty(c.Title))
        {
            sk.Put(_p, sk.Fit(_p, T.Up(c.Title), w), x, ty, T.HOT);
            ty += 13;
        }
        if (!string.IsNullOrEmpty(c.Content))
            sk.Put(_p, c.Content, new RectangleF(x, ty, w, r.Bottom - 6 - ty), T.INK, 1, false, TextAlignment.Leading, ParagraphAlignment.Near, true);
        T.Scanlines(_p, RectangleF.Inflate(r, -3, -3));
    }
}
