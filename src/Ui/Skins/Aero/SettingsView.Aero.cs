using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

// The settings in Windows 7: an Explorer window. In the glass along the top the round Back / Forward buttons, the breadcrumb
// address bar (OrbitOverlay > Settings > the page) and the search box; under it Explorer's command bar with the pages; the
// pages on white with Explorer's group headers; Look laid out as a Control Panel page (a blue heading, the styles as
// thumbnails, the colour swatches, the sliders). The controls stay exactly where the 95 sheet has them (the same hit areas).
public sealed partial class SettingsView
{
    static AeroSkin Ae7 => (AeroSkin)Skins.Aero;

    static readonly string[] BackGlyph7 = { "...k....", "..kk....", ".kkkkkkk", "kkkkkkkk", ".kkkkkkk", "..kk....", "...k...." };
    static readonly string[] FwdGlyph7 = { "....k...", "....kk..", "kkkkkkk.", "kkkkkkkk", "kkkkkkk.", "....kk..", "....k..." };
    static readonly string[] Crumb7 = { "k..", "kk.", "kkk", "kk.", "k.." };
    static readonly string[] Lens7 = { ".kkk..", "k...k.", "k...k.", "k...k.", ".kkkk.", "....kk", ".....k" };

    partial void SettingsAero(Ui ui, ref bool done)
    {
        done = true;
        var p = ui.P;
        var sk = Ae7;
        var wn = WhatsNewShown;
        bool modal = wn != null && wn.Count > 0;
        ui.Inert = modal;
        var close = OnClose ?? OnClose95;
        p.Gpu.D2D.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 0));     // the window's round corners show what is behind it
        var win = new RectangleF(0, 0, W95W, W95H);
        var client = AeroSkin.Window7(p, win, 28, 0xFFFFFFFF, 6);

        // the caption boxes: Help (-> About) and Close
        var rc = new RectangleF(W95W - 7 - 43, 1, 43, 18);
        var rh = new RectangleF(rc.X - 25, 1, 26, 18);
        bool hc = ui.Clickable("p95.close", RectangleF.Inflate(rc, 0, 2), out bool chv, out bool chd) && close != null;
        bool hh = ui.Clickable("p95.help", RectangleF.Inflate(rh, 0, 2), out bool hhv, out bool hhd);
        AeroSkin.CaptionBox(p, rh, CaptionKind.Help, true, hhd, hhv);
        AeroSkin.CaptionBox(p, rc, CaptionKind.Close, close != null, chd, chv);
        if (hh) Tab = TabGeneral;
        if (hc) close?.Invoke();

        // Back / Forward: the previous and the next page
        var pill = new RectangleF(6, 3, 54, 24);
        p.Smooth(true);
        p.FillRound(pill, Theme.WithAlpha(0xFF203A5C, 0.30f), 12);
        p.Smooth(false);
        bool canB = Tab > 0, canF = Tab < Tabs.Length - 1;
        bool back = ui.Clickable("p7.back", new RectangleF(6, 2, 26, 26), out bool bh, out bool bd) && canB;
        bool fwd = ui.Clickable("p7.fwd", new RectangleF(34, 2, 26, 26), out bool fh, out bool fd) && canF;
        AeroSkin.Orb(p, 18, 15, 11, canB, bh && canB, bd && canB, BackGlyph7, canB);
        AeroSkin.Orb(p, 47, 15, 10, canF, fh && canF, fd && canF, FwdGlyph7, canF);
        if (back) Tab--;
        if (fwd) Tab++;

        // the address bar: the breadcrumb
        var addr = new RectangleF(66, 5, 400, 22);
        Bar7(p, addr);
        W95Icons.Draw(p, W95Icons.Id.Orbit, addr.X + 3, addr.Y + 3);
        float cx = addr.X + 23;
        string[] crumbs = { "OrbitOverlay", "Settings", Tabs[Tab] };
        for (int i = 0; i < crumbs.Length; i++)
        {
            bool last = i == crumbs.Length - 1;
            float w = p.MeasureWidth(crumbs[i], Ui.F95, last ? FontWeight.Bold : FontWeight.Normal, sk.Font);
            p.Text(crumbs[i], new RectangleF(cx, addr.Y - 1, w + 4, addr.Height + 2), 0xFF000000, Ui.F95, last ? FontWeight.Bold : FontWeight.Normal,
                TextAlignment.Leading, ParagraphAlignment.Center, sk.Font);
            cx += w + 6;
            if (!last) { p.Pixels(Crumb7, cx, addr.Y + 8, 0xFF3A3A3A); cx += 10; }
        }
        p.Pixels(new[] { "kkkkk", ".kkk.", "..k.." }, addr.Right - 14, addr.Y + 10, 0xFF3A3A3A);
        p.Fill(new RectangleF(addr.Right - 22, addr.Y + 3, 1, addr.Height - 6), 0xFFD5DDE8);

        // the search box (Orbit types with its own keyboard, not into its settings: it only shows where it would be)
        var search = new RectangleF(472, 5, 190, 22);
        Bar7(p, search);
        p.Text("Search Settings", new RectangleF(search.X + 6, search.Y - 1, search.Width - 30, search.Height + 2), 0xFF8D8D8D, Ui.F95,
            FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Center, sk.Font);
        p.Pixels(Lens7, search.Right - 16, search.Y + 7, 0xFF59708C);

        // Explorer's command bar: the pages
        var cmd = new RectangleF(client.X, client.Y, client.Width, 23);
        p.GradientRaw(cmd, 0xFFF6FAFE, 0xFFE9F1FB, 0xFFDCE7F5);
        p.Fill(new RectangleF(cmd.X, cmd.Bottom - 1, cmd.Width, 1), 0xFFA9B9CE);
        p.Fill(new RectangleF(cmd.X, cmd.Y, cmd.Width, 1), 0xFFFFFFFF);
        float nx = cmd.X + 6;
        for (int i = 0; i < Tabs.Length; i++)
        {
            float w = p.MeasureWidth(Tabs[i], Ui.F95, FontWeight.Normal, sk.Font) + 18;
            var r = new RectangleF(nx, cmd.Y + 2, w, cmd.Height - 5);
            bool click = ui.Clickable(Ui.Id("p7.nav", i), r, out bool hv, out bool hd);
            bool on = i == Tab;
            if (on || hd) AeroSkin.RoundBox(p, r, 3, 0xFF7C9BC2, 0xFFE3EEFA, 0xFFD5E5F7, 0xFFC8DCF3, 0xFFB5CFEE, 0xFFC4D9F2);
            else if (hv) AeroSkin.RoundBox(p, r, 3, 0xFFA7C3E3, 0xFFFBFDFF, 0xFFF3F8FE, 0xFFE8F1FC, 0xFFDCEAFB, 0xFFE6F0FC);
            p.Text(Tabs[i], new RectangleF(r.X, r.Y - 2, r.Width, r.Height + 4), on ? 0xFF000000 : 0xFF1E395B, Ui.F95, FontWeight.Normal,
                TextAlignment.Center, ParagraphAlignment.Center, sk.Font);
            if (click) Tab = i;
            nx += w + 2;
        }

        // the page, on white: Explorer's group headers instead of 95's etched boxes
        sk.HeaderGroups = true;
        sk.GroupBg = 0xFFFFFFFF;
        try
        {
            switch (Tab)
            {
                case TabDesktops: Desktops95(ui); break;
                case TabWindows: Windows95(ui); break;
                case TabLayouts: Layouts95(ui); break;
                case TabWrist: Wrist95(ui); break;
                case TabKeyboard: Keyboard95(ui); break;
                case TabLook: LookAero(ui); break;
                case TabVrchat: Vrchat95(ui); break;
                default: General95(ui); break;
            }
        }
        finally
        {
            sk.HeaderGroups = false;
            sk.GroupBg = 0;
        }

        // the buttons along the bottom, under a separator (the same as the 95 sheet's)
        p.Fill(new RectangleF(16, 440, W95W - 32, 1), AeroSkin.GroupLine);
        const float by = 448;
        if (ui.Button95("p95.rc", new RectangleF(9, by, 75, 23), "Re&center", _h.VrConnected)) _h.Recenter();
        if (ui.Button95("p95.kb", new RectangleF(90, by, 95, 23), C.Keyboard.Visible ? "Hide &keyboard" : "Show &keyboard"))
            _h.ToggleKeyboard();
        if (ui.Button95("p95.desk", new RectangleF(191, by, 95, 23), C.General.DesktopsVisible ? "Hide &desktops" : "Show &desktops"))
            _h.ToggleDesktops();
        float x = W95W - 9 - 75;
        if (ui.Button95("p95.apply", new RectangleF(x, by, 75, 23), "&Apply", _h.Cfg.Dirty)) _h.Cfg.SaveIfDue(force: true);
        x -= 6 + 75;
        if (ui.Button95("p95.cl", new RectangleF(x, by, 75, 23), "Close", close != null)) close?.Invoke();
        x -= 6 + 75;
        if (ui.Button95("p95.ok", new RectangleF(x, by, 75, 23), "OK", true, isDefault: true))
        {
            _h.Cfg.SaveIfDue(force: true);
            close?.Invoke();
        }
        if (modal)
        {
            ui.Inert = false;
            if (WhatsNewView.Draw95(ui, new RectangleF(155, 66, 440, 340), wn, WhatsNewSubShown)) CloseWhatsNew();
        }
    }

    // the address bar's and the search box's field: white, a blue-grey border
    static void Bar7(Painter p, RectangleF r)
    {
        p.Fill(r, 0xFF8EA4C1);
        p.GradientRaw(RectangleF.Inflate(r, -1, -1), 0xFFFFFFFF, 0xFFF8FAFD);
        p.Fill(new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, 1), 0xFFE3E9F1);
    }

    // ---------------- Look, as a Control Panel page ----------------

    void LookAero(Ui ui)
    {
        var p = ui.P;
        var sk = Ae7;
        var a = C.Appearance;
        bool changed = false;
        p.Text("Change the appearance of OrbitOverlay", new RectangleF(24, 56, 600, 24), AeroSkin.CpBlue, 16, FontWeight.Normal, TextAlignment.Leading,
            ParagraphAlignment.Center, sk.Font);
        ui.Text95("Choose a style for the watch, the keyboard, the settings and the panels. It applies at once.", new RectangleF(24, 80, 690, 15),
            0xFF333333);

        // the styles: a little window of each, the chosen one selected the way Explorer selects
        ui.Group95(new RectangleF(24, 102, 702, 100), $"Styles ({Skins.StyleIds.Length})");
        int cur = Skins.IndexOf(a.Style);
        const float cw = 106, gap = 8;
        float x0 = 24 + MathF.Floor((702 - (Skins.StyleIds.Length * cw + (Skins.StyleIds.Length - 1) * gap)) / 2);
        for (int i = 0; i < Skins.StyleIds.Length; i++)
        {
            var cell = new RectangleF(x0 + i * (cw + gap), 120, cw, 80);
            bool click = ui.Clickable(Ui.Id("lk7.s", i), cell, out bool hv, out bool hd);
            bool on = i == cur;
            if (on || hd) AeroSkin.RoundBox(p, cell, 3, 0xFF84ACDD, 0xFFF2F8FE, 0xFFEAF3FD, 0xFFDDEBFC, 0xFFD3E5FB, 0xFFC5DDFA);
            else if (hv) AeroSkin.RoundBox(p, cell, 3, 0xFFB8D6FB, 0xFFFAFCFF, 0xFFF4F9FE, 0xFFEBF4FE, 0xFFE3EFFD, 0xFFDCEBFD);
            var pic = new RectangleF(cell.X + 7, cell.Y + 6, cell.Width - 14, 50);
            StyleChip7(p, pic, Skins.StyleIds[i]);
            ui.Text95(Skins.StyleNames[i], new RectangleF(cell.X, cell.Y + 59, cell.Width, 16), 0xFF000000, false, TextAlignment.Center);
            if (click && !on) { a.Style = Skins.StyleIds[i]; changed = true; }
        }

        // the laser's colour: "as the style", then the presets - 7's glossy swatches
        ui.Group95(new RectangleF(24, 212, 702, 60), "Laser colour");
        var wells = new List<uint> { 0 };
        wells.AddRange(Theme.AccentPresets.Take(Theme.AccentPresets.Length - 1));
        for (int i = 0; i < wells.Count; i++)
        {
            var r = new RectangleF(36 + i * 36, 232, 28, 28);
            uint c = wells[i] == 0 ? Theme.ClassicLaser : wells[i];
            bool on = wells[i] == 0 ? a.LaserColor == 0 : a.LaserColor != 0 && (a.LaserColor & 0xFFFFFF) == (c & 0xFFFFFF);
            bool click = ui.Clickable(Ui.Id("lk7.c", i), RectangleF.Inflate(r, 3, 3), out bool hv, out _);
            Swatch7(p, r, c, on, hv);
            if (click) { a.LaserColor = wells[i]; changed = true; }
        }
        ui.Text95(a.LaserColor == 0 ? "As the style (Windows 7 blue)" : "", new RectangleF(36 + wells.Count * 36 + 6, 238, 260, 15), 0xFF6D6D6D);

        // the sliders, then the switches
        float th = a.LaserThickness;
        if (TrackRow(ui, "lk95.th", 24, 282, 200, 400, 80, "Laser thickness:", ref th, 1.5f, 8f, $"{a.LaserThickness:0.0} mm"))
        { a.LaserThickness = MathF.Round(th * 2) / 2; changed = true; }
        float nb = a.NightBrightness;
        if (TrackRow(ui, "lk95.nb", 24, 316, 200, 400, 80, "Night brightness:", ref nb, 0.15f, 0.9f, $"{a.NightBrightness * 100:0}%"))
        { a.NightBrightness = nb; changed = true; }
        p.Fill(new RectangleF(24, 354, 702, 1), AeroSkin.GroupLine);
        bool always = a.LaserAlways;
        ui.Check95("lk95.always", 24, 366, "&Always show the beam (otherwise only while pointing at a panel)", ref always);
        if (always != a.LaserAlways) { a.LaserAlways = always; changed = true; }
        bool night = a.NightMode;
        ui.Check95("lk95.night", 24, 390, "&Night mode: dims every panel in VR (the watch has a button for it too)", ref night);
        if (night != a.NightMode) { a.NightMode = night; changed = true; }
        // Control Panel's link: the modern colours back to their defaults
        const string link = "Restore the default colours of the Modern style";
        float lw = ui.Width95(link);
        var lr = new RectangleF(24, 414, lw + 2, 15);
        bool lc = ui.Clickable("lk95.reset", RectangleF.Inflate(lr, 2, 3), out bool lh, out _);
        ui.Text95(link, lr, lh ? 0xFF3399FF : AeroSkin.LinkBlue);
        if (lh) p.Fill(new RectangleF(lr.X, lr.Bottom - 1, lw, 1), 0xFF3399FF);
        if (lc)
        {
            C.Appearance = a = new AppearanceConfig
            {
                LaserThickness = a.LaserThickness, LaserAlways = a.LaserAlways, Clock24h = a.Clock24h,
                NightMode = a.NightMode, NightBrightness = a.NightBrightness, Style = a.Style, Font = a.Font, LaserColor = a.LaserColor,
            };
            changed = true;
        }
        if (changed) { ui.Changed = true; _h.AppearanceChanged(); }
    }

    // 7's colour swatch: rounded, glossy, the chosen one ringed in the selection blue
    static void Swatch7(Painter p, RectangleF r, uint c, bool on, bool hover)
    {
        if (on || hover)
        {
            p.Smooth(true);
            p.FillRound(RectangleF.Inflate(r, 3, 3), on ? 0xFF3399FF : 0xFFA9CFF5, 5);
            p.FillRound(RectangleF.Inflate(r, 2, 2), 0xFFFFFFFF, 4);
            p.Smooth(false);
        }
        p.Smooth(true);
        p.FillRound(r, Theme.Mix(c, 0xFF000000, 0.35f), 3);
        p.Smooth(false);
        var i = RectangleF.Inflate(r, -1, -1);
        p.GradientRaw(i, Theme.Mix(c, 0xFFFFFFFF, 0.45f), c, Theme.Mix(c, 0xFF000000, 0.12f), 2);
        p.Fill(new RectangleF(i.X + 1, i.Y + 1, i.Width - 2, MathF.Round(i.Height * 0.42f)), Theme.WithAlpha(0xFFFFFFFF, 0.25f));
    }

    // a little window in each style's colours (the Look page's thumbnails)
    static void StyleChip7(Painter p, RectangleF r, string id)
    {
        p.Fill(new RectangleF(r.X + 1, r.Y + 1, r.Width, r.Height), Theme.WithAlpha(0xFF000000, 0.22f));
        switch (id)
        {
            case "classic":
                p.Fill(r, 0xFF018080);         // (one off 1995's teal and navy: MapColor turns those into 7's)
                var w95 = new RectangleF(r.X + 8, r.Y + 6, r.Width - 16, r.Height - 12);
                p.Fill(w95, 0xFFC0C0C0);
                p.Fill(new RectangleF(w95.X, w95.Y, w95.Width, 1), 0xFFFFFFFF);
                p.Fill(new RectangleF(w95.X, w95.Y, 1, w95.Height), 0xFFFFFFFF);
                p.Fill(new RectangleF(w95.Right - 1, w95.Y, 1, w95.Height), 0xFF000000);
                p.Fill(new RectangleF(w95.X, w95.Bottom - 1, w95.Width, 1), 0xFF000000);
                p.Fill(new RectangleF(w95.X + 2, w95.Y + 2, w95.Width - 4, 7), 0xFF000081);
                p.Fill(new RectangleF(w95.Right - 9, w95.Y + 3, 6, 5), 0xFFC0C0C0);
                break;
            case "xp":
                p.GradientRaw(r, 0xFF5A8FE0, 0xFF3A6EC8);
                var wx = new RectangleF(r.X + 8, r.Y + 6, r.Width - 16, r.Height - 12);
                p.Fill(wx, 0xFF0831D9);
                p.GradientRaw(new RectangleF(wx.X, wx.Y, wx.Width, 9), 0xFF3C8CF5, 0xFF0A5CE6);
                p.Fill(new RectangleF(wx.X + 2, wx.Y + 9, wx.Width - 4, wx.Height - 11), 0xFFECE9D8);
                p.Smooth(true);
                p.FillRound(new RectangleF(wx.Right - 9, wx.Y + 2, 7, 6), 0xFFE0573D, 1.5f);
                p.Smooth(false);
                break;
            case "aero":
                AeroSkin.Harmony(p, r);
                var w7 = new RectangleF(r.X + 8, r.Y + 6, r.Width - 16, r.Height - 12);
                p.Smooth(true);
                p.FillRound(w7, AeroSkin.GlassEdge, 3);
                p.Smooth(false);
                p.GradientRaw(RectangleF.Inflate(w7, -1, -1), AeroSkin.GlassTop, AeroSkin.GlassBottom, 2);
                p.Fill(new RectangleF(w7.X + 3, w7.Y + 9, w7.Width - 6, w7.Height - 12), 0xFFFFFFFF);
                p.Smooth(true);
                p.FillRound(new RectangleF(w7.Right - 12, w7.Y + 1, 10, 6), 0xFFD25A3D, 1.5f);
                p.Smooth(false);
                break;
            case "mac":
                p.Fill(r, 0xFF9DA5B5);
                var wm = new RectangleF(r.X + 8, r.Y + 6, r.Width - 16, r.Height - 12);
                p.Fill(wm, 0xFF000000);
                p.Fill(RectangleF.Inflate(wm, -1, -1), 0xFFDDDDDD);
                for (int i = 0; i < 4; i++) p.Fill(new RectangleF(wm.X + 2, wm.Y + 2 + i * 2, wm.Width - 4, 1), 0xFF8A8A8A);
                p.Fill(new RectangleF(wm.X + 4, wm.Y + 2, 7, 7), 0xFFDDDDDD);
                p.Fill(new RectangleF(wm.X + 1, wm.Y + 10, wm.Width - 2, 1), 0xFF000000);
                break;
            case "terminal":
                p.Fill(r, 0xFF050805);
                for (int i = 0; i < 5; i++)
                    p.Fill(new RectangleF(r.X + 6, r.Y + 6 + i * 8, (i * 37 % 50) + 20, 3), Theme.WithAlpha(0xFF33FF66, i == 4 ? 1f : 0.7f));
                p.Fill(new RectangleF(r.X + 6, r.Y + 6 + 5 * 8, 6, 3), 0xFF33FF66);
                break;
            default:          // modern: a dark card with its cards
                p.Fill(r, 0xFF15171C);
                p.Smooth(true);
                p.FillRound(new RectangleF(r.X + 6, r.Y + 6, r.Width * 0.45f, r.Height - 12), 0xFF262A33, 4);
                p.FillRound(new RectangleF(r.X + r.Width * 0.45f + 10, r.Y + 6, r.Width * 0.55f - 16, (r.Height - 16) / 2), 0xFF30353F, 4);
                p.FillRound(new RectangleF(r.X + r.Width * 0.45f + 10, r.Y + 10 + (r.Height - 16) / 2, r.Width * 0.55f - 16, (r.Height - 16) / 2), 0xFF45C1FF, 4);
                p.Smooth(false);
                break;
        }
    }
}
