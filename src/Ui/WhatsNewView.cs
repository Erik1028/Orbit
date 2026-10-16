using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The "What's new" window, drawn the same in the VR panel (Panels/WhatsNewPanel.cs) and over the settings window /
// panel / dashboard (SettingsView): the versions since the one you came from, newest first - each a version chip with
// its title, then its points. Modern: a card with a scroll list and OK. Windows 95: a dialog with a read-only list
// and a real 95 scroll bar.
public static partial class WhatsNewView
{
    // Returns true when OK (or the close box) is pressed. scroll: pixels, kept by the caller.
    public static bool DrawModern(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref float scroll, uint bg)
    {
        var p = ui.P;
        p.Fill(r, bg, 28);
        p.Stroke(RectangleF.Inflate(r, -1, -1), 0x40FFFFFF, 2, 27);
        float pad = 26;
        p.Icon("\uE946", new RectangleF(r.X + pad - 4, r.Y + 20, 34, 34), Theme.Accent, 24);
        p.Text("What's new", new RectangleF(r.X + pad + 38, r.Y + 16, r.Width - 2 * pad - 40, 32), Theme.Text, 24, FontWeight.SemiBold);
        p.Text(sub, new RectangleF(r.X + pad + 38, r.Y + 46, r.Width - 2 * pad - 40, 22), Theme.Dim, 15);

        var area = new RectangleF(r.X + pad, r.Y + 84, r.Width - 2 * pad, r.Height - 84 - 78);
        float textW = area.Width - 34 - 12;        // bullet column + scroll bar room
        // lay the whole list out once, then draw what is in view
        float total = 0;
        foreach (var e in list)
        {
            total += 38;
            foreach (var pt in e.Points) total += p.MeasureHeight(pt, 16, FontWeight.Normal, textW) + 8;
            total += 14;
        }
        float max = MathF.Max(0, total - area.Height);
        if (ui.Wheel != 0 && ui.Hover(area)) scroll -= ui.Wheel * 48;
        scroll = Math.Clamp(scroll, 0, max);
        p.PushClip(area);
        float y = area.Y - scroll;
        foreach (var e in list)
        {
            string ver = e.Version.ToString(3);
            float cw = p.MeasureWidth(ver, 14, FontWeight.SemiBold) + 22;
            var chip = new RectangleF(area.X, y + 4, cw, 28);
            p.Fill(chip, Theme.WithAlpha(Theme.Accent, 0.2f), 14);
            p.Text(ver, chip, Theme.Accent, 14, FontWeight.SemiBold, TextAlignment.Center);
            p.Text(e.Title, new RectangleF(chip.Right + 12, y, area.Width - cw - 24, 36), Theme.Text, 18, FontWeight.SemiBold);
            y += 38;
            foreach (var pt in e.Points)
            {
                float h = p.MeasureHeight(pt, 16, FontWeight.Normal, textW);
                p.Circle(area.X + 14, y + 11, 3, Theme.Accent);
                p.Text(pt, new RectangleF(area.X + 34, y, textW, h + 4), Theme.Mix(Theme.Text, Theme.Dim, 0.25f), 16, FontWeight.Normal,
                    TextAlignment.Leading, ParagraphAlignment.Near, null, true);
                y += h + 8;
            }
            y += 14;
        }
        p.PopClip();
        if (max > 0)
        {
            // a thin scroll bar on the right
            float bh = area.Height * area.Height / total, by = area.Y + (area.Height - bh) * (scroll / max);
            p.Fill(new RectangleF(area.Right - 4, by, 4, bh), Theme.WithAlpha(Theme.Text, 0.3f), 2);
        }
        return ui.Button("wn.ok", new RectangleF(r.Right - pad - 150, r.Bottom - 64, 150, 44), "OK", Theme.IcoCheck, true, true, 16);
    }

    // Windows 95: r in canvas pixels. Returns true for OK / the close box.
    public static bool Draw95(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub)
    {
        bool closed = false;
        if (SkinWhatsNew(ui, r, list, sub, ref closed)) return closed;      // a style's own window (ScreenHooks.cs)
        var p = ui.P;
        p.Fill(r, Theme.W95Face);
        p.Bevel(r, Painter.Edge.Window);
        ui.Caption95(new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, 18), "What's New in OrbitOverlay");
        bool close = ui.CaptionClose95("wn95.x", r.Right - 5 - 16, r.Y + 5);
        W95Icons.Draw(p, W95Icons.Id.Info32, r.X + 12, r.Y + 30);
        ui.Text95(sub, new RectangleF(r.X + 54, r.Y + 34, r.Width - 66, 26), 0xFF000000, false, TextAlignment.Leading, true, true);
        // the text as lines in a read-only list: a version's title in bold, its points wrapped under a bullet
        var box = new RectangleF(r.X + 12, r.Y + 68, r.Width - 24, r.Height - 68 - 40);
        float textW = box.Width - 4 - 16 - 16;          // borders, the scroll bar, the bullet indent
        var lines = new List<(string text, bool bold, float indent)>();
        foreach (var e in list)
        {
            foreach (var l in Wrap95(ui, $"Version {e.Version.ToString(3)} - {e.Title}", textW + 10, true)) lines.Add((l, true, 2));
            foreach (var pt in e.Points)
            {
                bool first = true;
                foreach (var l in Wrap95(ui, pt, textW)) { lines.Add(((first ? "• " : "") + l, false, first ? 6 : 14)); first = false; }
            }
            lines.Add(("", false, 0));
        }
        ui.List95("wn95.list", box, lines.Count, -1, (i, row, on) =>
        {
            var (text, bold, indent) = lines[i];
            ui.Text95(text, new RectangleF(row.X + indent, row.Y, row.Width - indent, row.Height), 0xFF000000, bold);
        }, 14);
        bool ok = ui.Button95("wn95.ok", new RectangleF(r.Right - 12 - 75, r.Bottom - 32, 75, 23), "OK", true, isDefault: true);
        return ok || close;
    }

    // word wrap in the 95 font (8 pt MS Sans Serif at its pixel size)
    static List<string> Wrap95(Ui ui, string s, float width, bool bold = false)
    {
        var res = new List<string>();
        var line = "";
        foreach (var word in s.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && ui.Width95(next, bold) > width) { res.Add(line); line = word; }
            else line = next;
        }
        if (line.Length > 0) res.Add(line);
        return res;
    }
}
