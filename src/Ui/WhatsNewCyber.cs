using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// What's new in the Cyber style: a PATCH NOTES console - an olive frame with cut corners and yellow cuts, a yellow tag and the
// title in the condensed bold, the versions as cyan monospace build numbers in brackets, each point behind a yellow marker, a
// segmented scroll bar, OK as the yellow plate. Same places as the modern card (the OK button where it was), same scrolling.
public static partial class WhatsNewView
{
    static bool DrawCyber(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref float scroll, uint bg)
    {
        var p = ui.P;
        const float cut = 18;
        p.FillCut(r, Theme.WithAlpha(CyberHud.Page, ((bg >> 24) & 0xFF) / 255f), cut, cut, cut, cut);
        var f = RectangleF.Inflate(r, -1.5f, -1.5f);
        p.StrokeCut(f, CyberHud.Frame, 2, cut - 1, cut - 1, cut - 1, cut - 1);
        float c = cut - 1;
        p.Line(f.X + 1, f.Y + 1 + c, f.X + 1 + c, f.Y + 1, CyberHud.Yellow, 3);
        p.Line(f.Right - 1, f.Bottom - 1 - c, f.Right - 1 - c, f.Bottom - 1, CyberHud.Yellow, 3);
        float pad = 26;
        float tw = CyberHud.Tag(p, r.X + pad, r.Y + 20, "PATCH NOTES", size: 12, h: 18);
        CyberHud.MonoAt(p, Lang.F("BUILD {0}", Updater.Current.ToString(3)), r.X + pad + tw + 10, r.Y + 34, 200, CyberHud.Grey, 11, FontWeight.SemiBold);
        var (title, ts) = ui.HudFit("What's new", r.Width - 2 * pad, 26, FontWeight.Bold, Theme.Display);
        p.Text(title, new RectangleF(r.X + pad - 1, r.Y + 42, r.Width - 2 * pad, 34), CyberHud.White, ts, FontWeight.Bold, TextAlignment.Leading,
            ParagraphAlignment.Center, Theme.Display);
        ui.HudHint(sub, new RectangleF(r.X + pad + 2, r.Y + 64 + 12, r.Width - 2 * pad - 4, 18), CyberHud.Grey, 11.5f);
        p.Fill(new RectangleF(r.X + pad, r.Y + 98, r.Width - 2 * pad, 1), CyberHud.Line);

        var area = new RectangleF(r.X + pad, r.Y + 106, r.Width - 2 * pad, r.Height - 106 - 80);
        float textW = area.Width - 30 - 18;
        float total = 0;
        foreach (var e in list)
        {
            total += 40;
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
            string ver = "v" + e.Version.ToString(3);
            float vw = p.MeasureWidth(ver, 13, FontWeight.Bold, CyberHud.Mono) + 18;
            var chip = new RectangleF(area.X, y + 6, vw, 26);
            p.FillCut(chip, Theme.WithAlpha(CyberHud.Cyan, 0.12f), 0, 0, 6, 0);
            p.StrokeCut(RectangleF.Inflate(chip, -0.6f, -0.6f), CyberHud.CyanDim, 1.25f, 0, 0, 5.7f, 0);
            CyberHud.Brackets(p, chip, CyberHud.Cyan, 5, 2, 1.5f);
            p.Text(ver, chip, CyberHud.Cyan, 13, FontWeight.Bold, TextAlignment.Center, ParagraphAlignment.Center, CyberHud.Mono);
            p.Text(e.Title, new RectangleF(chip.Right + 12, y, area.Width - vw - 24, 38), CyberHud.White, 19, FontWeight.SemiBold);
            y += 40;
            foreach (var pt in e.Points)
            {
                float h = p.MeasureHeight(pt, 16, FontWeight.Normal, textW);
                p.Fill(new RectangleF(area.X + 10, y + 8, 7, 7), CyberHud.Yellow);
                p.Text(pt, new RectangleF(area.X + 30, y, textW, h + 4), Theme.Mix(CyberHud.White, CyberHud.Grey, 0.3f), 16, FontWeight.Normal,
                    TextAlignment.Leading, ParagraphAlignment.Near, null, true);
                y += h + 8;
            }
            y += 14;
        }
        p.PopClip();
        if (max > 0)
        {
            // a segmented scroll bar: the blocks in view lit
            var bar = new RectangleF(area.Right - 6, area.Y, 6, area.Height);
            int n = Math.Max(8, (int)(bar.Height / 14));
            float seg = bar.Height / n;
            float a0 = scroll / total, a1 = (scroll + area.Height) / total;
            for (int i = 0; i < n; i++)
            {
                float m = (i + 0.5f) / n;
                p.Fill(new RectangleF(bar.X, bar.Y + i * seg + 1, bar.Width, seg - 3), m >= a0 && m <= a1 ? CyberHud.Cyan : CyberHud.Track);
            }
        }
        CyberHud.Brackets(p, r, CyberHud.Cyan, 12, 8, 2, topRight: true, bottomLeft: false);
        return ui.HudButton("wn.ok", new RectangleF(r.Right - pad - 150, r.Bottom - 64, 150, 44), "OK", Theme.IcoCheck, true, true, 16);
    }
}
