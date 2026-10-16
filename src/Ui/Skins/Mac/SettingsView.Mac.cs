using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

// The settings in the Classic Mac style: a Platinum window ("OrbitOverlay Settings", the close box on the left), Mac OS 8 tabs
// over the pages, the buttons at the bottom right in the Mac's order - OK last, ringed as the default. The pages keep the 95
// sheet's places (the tests and the laser click the same spots); Look is laid out the way a Mac control panel is: labels set
// right against their controls, lines between the parts instead of boxes.
public sealed partial class SettingsView
{
    partial void SettingsMac(Ui ui, ref bool done)
    {
        done = true;
        var p = ui.P;
        var m = (MacSkin)Skins.Mac;
        var wn = WhatsNewShown;
        bool modal = wn != null && wn.Count > 0;
        ui.Inert = modal;
        var all = new RectangleF(0, 0, W95W, W95H);
        var close = OnClose ?? OnClose95;
        // the VR panel zooms out of its middle as it opens and back in as it closes (MacMotion.cs)
        if (ShownAt > 0 && Motion.Any && (Closing || Vis < 1))
        {
            p.ClearRect(all);
            float z = Math.Clamp(Closing ? 1 - Vis : Vis, 0, 1);
            if (Closing) MacSkin.ZoomRects(p, all, MacSkin.Seed(all), z);
            else MacSkin.ZoomRects(p, MacSkin.Seed(all), all, z);
            AnimWake.At(Clock.Now);
            return;
        }
        m.MacWindow(p, all, "OrbitOverlay Settings");
        if (ui.CaptionClose95("p95.close", MacSkin.CloseX(all), MacSkin.BoxY(all), close != null)) close?.Invoke();

        var page = new RectangleF(6, 46, W95W - 12, 394);
        p.Fill(page, Theme.W95Face);
        p.Bevel(page, Painter.Edge.Page);
        int t = Tab;
        if (ui.Tabs95("p95.tabs", 8, 28, Tabs, ref t)) Tab = t;

        switch (Tab)
        {
            case TabDesktops: Desktops95(ui); break;
            case TabWindows: Windows95(ui); break;
            case TabLayouts: Layouts95(ui); break;
            case TabWrist: Wrist95(ui); break;
            case TabKeyboard: Keyboard95(ui); break;
            case TabLook: LookMac(ui); break;
            case TabVrchat: Vrchat95(ui); break;
            default: General95(ui); MacUpdateBusy(ui); break;
        }

        // the quick actions on the left; Apply, Close and the default OK on the right (everything applies at once: Close, not Cancel)
        const float by = 448;
        if (ui.Button95("p95.rc", new RectangleF(9, by, 75, 23), "Recenter", _h.VrConnected)) _h.Recenter();
        if (ui.Button95("p95.kb", new RectangleF(90, by, 95, 23), C.Keyboard.Visible ? "Hide keyboard" : "Show keyboard"))
            _h.ToggleKeyboard();
        if (ui.Button95("p95.desk", new RectangleF(191, by, 95, 23), C.General.DesktopsVisible ? "Hide desktops" : "Show desktops"))
            _h.ToggleDesktops();
        float x = W95W - 9 - 75;
        if (ui.Button95("p95.ok", new RectangleF(x, by - 2, 75, 27), "OK", true, isDefault: true))
        {
            _h.Cfg.SaveIfDue(force: true);
            close?.Invoke();
        }
        x -= 10 + 75;
        if (ui.Button95("p95.cl", new RectangleF(x, by, 75, 23), "Close", close != null)) close?.Invoke();
        x -= 10 + 75;
        if (ui.Button95("p95.apply", new RectangleF(x, by, 75, 23), "Apply", _h.Cfg.Dirty)) _h.Cfg.SaveIfDue(force: true);
        if (modal)
        {
            ui.Inert = false;
            if (WhatsNewView.Draw95(ui, new RectangleF(155, 66, 440, 340), wn, WhatsNewSubShown)) CloseWhatsNew();
        }
    }

    // General: while the update check (the install) runs, Mac OS 8's barber pole under its line; the download, a progress bar
    void MacUpdateBusy(Ui ui)
    {
        var (us, _, uprog, _) = _h.Updates.Snapshot();
        var r = new RectangleF(524, 217, 198, 12);
        if (us == Updater.State.Downloading) ui.Progress95(r, uprog, Theme.W95Navy);
        else if (us is Updater.State.Checking or Updater.State.Starting) MacSkin.BarberPole(ui.P, r);
    }

    // a Mac dialog's row label: Chicago, set right against the control at x
    void MacLabel(Ui ui, float right, float y, string text, bool enabled = true) =>
        ui.Label95(text, new RectangleF(right - 200, y, 200, RowH), enabled, TextAlignment.Trailing, K95, true);

    // a slider row: label, trackbar, value
    bool MacTrack(Ui ui, string id, float right, float y, float cx, float tw, string label, ref float v, float min, float max, string value)
    {
        MacLabel(ui, right, y, label);
        bool ch = ui.Track95(id, new RectangleF(cx - 6, y, tw + 12, 30), ref v, min, max);
        ui.Text95(Fit95(ui, value, 90), new RectangleF(cx + tw + 12, y + 3, 90, 15));
        return ch;
    }

    void LookMac(Ui ui)
    {
        var p = ui.P;
        var m = (MacSkin)Skins.Mac;
        var a = C.Appearance;
        bool changed = false;
        const float L = 214, X = 224;          // the labels' right edge, the controls' left

        int si = Skins.IndexOf(a.Style);
        MacLabel(ui, L, 66, "Style:");
        if (ui.Combo95("lk95.style", new RectangleF(X, 66, 190, RowH), Skins.StyleNames, ref si))
        {
            a.Style = Skins.StyleIds[si];
            changed = true;
        }
        Hint(ui, X, 94, 480, $"{Theme.RetroSkin.Name} draws these settings, the watch, the keyboard and the notifications at the real " +
                             "pixel size of its day, enlarged without smoothing.", 30);
        MacLabel(ui, L, 132, "Colours:");
        Hint(ui, X, 135, 480, "The accent colour, the panel colour, the themes and the opacity belong to the Modern style; switch to it " +
                              "to change them.", 30);
        if (ui.Button95("lk95.reset", new RectangleF(X, 168, 100, 23), "Default look"))
        {
            C.Appearance = a = new AppearanceConfig
            {
                LaserThickness = a.LaserThickness, LaserAlways = a.LaserAlways, Clock24h = a.Clock24h,
                NightMode = a.NightMode, NightBrightness = a.NightBrightness, Style = a.Style, Font = a.Font,
            };
            changed = true;
        }
        m.EtchedLine(p, 30, W95W - 30, 204);

        // the laser: its colour (the style's own first), its thickness, always or only at a panel
        MacLabel(ui, L, 218, "Laser colour:");
        var wells = new List<uint> { 0 };
        wells.AddRange(Theme.AccentPresets.Take(Theme.AccentPresets.Length - 1));
        for (int i = 0; i < wells.Count; i++)
        {
            var r = new RectangleF(X + i * 28, 218, 22, 20);
            uint c = wells[i] == 0 ? Theme.ClassicLaser : wells[i];
            bool on = wells[i] == 0 ? a.LaserColor == 0 : a.LaserColor != 0 && (a.LaserColor & 0xFFFFFF) == (c & 0xFFFFFF);
            if (on)
            {
                p.Fill(RectangleF.Inflate(r, 3, 3), K95);
                p.Fill(RectangleF.Inflate(r, 2, 2), W95);
            }
            p.Fill(r, c);
            p.Bevel(r, Painter.Edge.Sunken);
            if (ui.Hover(RectangleF.Inflate(r, 2, 2)) && ui.Pressed) { a.LaserColor = wells[i]; changed = true; }
        }
        float th = a.LaserThickness;
        if (MacTrack(ui, "lk95.th", L, 250, X, 280, "Laser thickness:", ref th, 1.5f, 8f, $"{a.LaserThickness:0.0} mm"))
        { a.LaserThickness = MathF.Round(th * 2) / 2; changed = true; }
        float nb = a.NightBrightness;
        if (MacTrack(ui, "lk95.nb", L, 286, X, 280, "Night brightness:", ref nb, 0.15f, 0.9f, $"{a.NightBrightness * 100:0}%"))
        { a.NightBrightness = nb; changed = true; }
        m.EtchedLine(p, 30, W95W - 30, 326);

        bool night = a.NightMode;
        ui.Check95("lk95.night", X, 342, "Night mode", ref night);
        Hint(ui, X + 19, 358, 460, "dims every panel in VR - the watch has a button for it too");
        if (night != a.NightMode) { a.NightMode = night; changed = true; }
        bool always = a.LaserAlways;
        ui.Check95("lk95.always", X, 384, "Always show the beam", ref always);
        Hint(ui, X + 19, 400, 460, "otherwise only while pointing at a panel");
        if (always != a.LaserAlways) { a.LaserAlways = always; changed = true; }
        if (changed) { ui.Changed = true; _h.AppearanceChanged(); }
    }
}

public static partial class WhatsNewView
{
    // a Platinum window: the close box on the left, the note icon, the versions in a scrolling list, OK ringed at the bottom right
    static partial void WhatsNewMac(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done)
    {
        done = true;
        var p = ui.P;
        var m = (MacSkin)Skins.Mac;
        m.MacWindow(p, r, "What's New in OrbitOverlay");
        bool close = ui.CaptionClose95("wn95.x", MacSkin.CloseX(r), MacSkin.BoxY(r));
        W95Icons.Draw(p, W95Icons.Id.Info32, r.X + 12, r.Y + 30);
        ui.Text95(sub, new RectangleF(r.X + 54, r.Y + 34, r.Width - 66, 26), 0xFF000000, true, TextAlignment.Leading, true, true);
        var box = new RectangleF(r.X + 12, r.Y + 68, r.Width - 24, r.Height - 68 - 44);
        float textW = box.Width - 4 - 16 - 16;
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
        bool ok = ui.Button95("wn95.ok", new RectangleF(r.Right - 12 - 75, r.Bottom - 36, 75, 27), "OK", true, isDefault: true);
        closed = ok || close;
    }
}
