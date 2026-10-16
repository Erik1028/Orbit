using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Vortice.DirectWrite;
using T = Orbit.TerminalSkin;

namespace Orbit;

// The settings on a green terminal: a BIOS-style setup utility on the 750 x 480 canvas. The title centred on the top line, the
// tabs a lit menu bar (the open one dark), the page in a single-line box, and along the bottom the key line: each of the 95
// sheet's buttons an F-key hint in the same place (F10 SAVE is OK, ESC EXIT is Close...). The Look page is the utility's item
// list - a name, its [VALUE], the current item lit - with the ITEM HELP column beside it. The other pages are the shared 95 ones
// drawn by the skin, so their controls stay where the laser (and the snapshot's clicks) expect them.
public sealed partial class SettingsView
{
    int _tRow;          // the Look list's current item (the one pointed at last): its help is shown

    static readonly string[] TStyleNames = StyleMenu.Names.Select(s => s.ToUpperInvariant()).ToArray();
    // the laser colour wells of the 95 page, by name: the style's own, then the presets (the last, white, is not offered)
    static readonly string[] TLaserNames = { "PHOSPHOR", "SKY BLUE", "BLUE", "VIOLET", "PINK", "RED", "ORANGE", "AMBER", "GREEN", "TEAL" };
    static readonly string[] TMotionNames = { "FULL", "REDUCED", "OFF" };       // Look > Motion (MotionLevel's order)
    // the Motion item's [VALUE] (the snapshot's check clicks it): the Look list's 8th line
    public static readonly RectangleF MotionTerm = new(262, 64 + 7 * 34, 92, 21);

    partial void SettingsTerm(Ui ui, ref bool done)
    {
        done = true;
        var p = ui.P;
        var sk = T.It;
        TBeginFrame(ui);
        var wn = WhatsNewShown;
        bool modal = wn != null && wn.Count > 0;
        ui.Inert = modal;
        var all = new RectangleF(0, 0, W95W, W95H);
        p.Fill(all, T.BG);
        T.Crt(p, all);
        var close = OnClose ?? OnClose95;

        // the title line, the help and close boxes where the 95 caption has them
        string title = Lang.F("ORBITOVERLAY SETUP UTILITY  v{0}", Updater.Current.ToString(3));
        sk.Put(p, title, MathF.Floor((W95W - T.W(title, 1, true)) / 2), 7, T.INK, 1, true);
        T.Dashes(p, 9, W95W - 9, 23, T.FAINT);
        if (ui.CaptionHelp95("p95.help", W95W - 5 - 16 - 2 - 16, 5)) Tab = TabGeneral;   // "What's this?" -> About
        if (ui.CaptionClose95("p95.close", W95W - 5 - 16, 5, close != null)) close?.Invoke();

        // the menu bar: lit from edge to edge, the open page dark in it
        var page = new RectangleF(6, 46, W95W - 12, 394);
        T.Lit(p, new RectangleF(6, 28, W95W - 12, 18));
        int t = Tab;
        if (ui.Tabs95("p95.tabs", 8, 28, Tabs, ref t)) Tab = t;
        p.Fill(page, T.BG);
        T.Box(p, page, T.FRAME);

        switch (Tab)
        {
            case TabDesktops: Desktops95(ui); break;
            case TabWindows: Windows95(ui); break;
            case TabLayouts: Layouts95(ui); break;
            case TabWrist: Wrist95(ui); break;
            case TabKeyboard: Keyboard95(ui); break;
            case TabLook: LookTerm(ui); break;
            case TabVrchat: Vrchat95(ui); break;
            default: General95(ui); TUpdateSpinner(); break;
        }

        // the key line: the 95 sheet's buttons, same places, as the utility's key hints
        const float by = 448;
        if (TFKey(ui, "p95.rc", new RectangleF(9, by, 75, 23), "F5", "RECENTER", _h.VrConnected)) _h.Recenter();
        if (TFKey(ui, "p95.kb", new RectangleF(90, by, 95, 23), "F6", C.Keyboard.Visible ? "HIDE KBD" : "SHOW KBD")) _h.ToggleKeyboard();
        if (TFKey(ui, "p95.desk", new RectangleF(191, by, 95, 23), "F7", C.General.DesktopsVisible ? "HIDE DESK" : "SHOW DESK"))
            _h.ToggleDesktops();
        string hint = "POINT:MOVE  CLICK:SELECT";
        sk.Put(p, hint, MathF.Floor(286 + (504 - 286 - T.W(hint)) / 2), by + 6, T.DIM);
        float x = W95W - 9 - 75;
        if (TFKey(ui, "p95.apply", new RectangleF(x, by, 75, 23), "F12", "APPLY", _h.Cfg.Dirty)) _h.Cfg.SaveIfDue(force: true);
        x -= 6 + 75;
        if (TFKey(ui, "p95.cl", new RectangleF(x, by, 75, 23), "ESC", "EXIT", close != null)) close?.Invoke();
        x -= 6 + 75;
        if (TFKey(ui, "p95.ok", new RectangleF(x, by, 75, 23), "F10", "SAVE"))
        {
            _h.Cfg.SaveIfDue(force: true);
            close?.Invoke();
        }
        T.Scanlines(p, all);
        if (modal)
        {
            ui.Inert = false;
            if (WhatsNewView.Draw95(ui, new RectangleF(155, 66, 440, 340), wn, WhatsNewSubShown)) CloseWhatsNew();
        }
        TScreenFx(p, RectangleF.Inflate(all, -3, -3));          // the tube switching on / off, the text typing in, the band
    }

    // a key hint of the bottom line: "F10 SAVE", the key bright, its word dimmer; a line around it under the laser, lit while held
    bool TFKey(Ui ui, string id, RectangleF r, string key, string word, bool enabled = true)
    {
        var p = ui.P;
        bool click = ui.Clickable(id, r, out bool hover, out bool held) && enabled;
        if (click) TFlash(id);
        bool lit = (held || TFlashing(id)) && enabled;
        if (lit) T.Lit(p, r);
        else if (hover && enabled) T.Box(p, r, T.INK);
        word = Lang.T(word);                // measured as it is drawn
        string s = key + " " + word;
        float x = MathF.Floor(r.X + (r.Width - T.W(s)) / 2), y = MathF.Floor(r.Y + (r.Height - 7) / 2) - 2;
        T.It.Put(p, key, x, y, lit ? T.BG : !enabled ? T.DIM : T.HOT);
        T.It.Put(p, word, x + T.W(key + " "), y, lit ? T.BG : !enabled ? T.DIM : hover ? T.INK : T.MID);
        return click;
    }

    // a value in brackets, [ENABLED ]: a click changes it (lit while held)
    bool TValueBox(Ui ui, string id, RectangleF r, string value, bool enabled = true)
    {
        var p = ui.P;
        bool click = ui.Clickable(id, r, out bool hover, out bool held) && enabled;
        if (click) TFlash(id);
        bool lit = (held || TFlashing(id)) && enabled;
        if (lit) T.Lit(p, r);
        uint fc = lit ? T.BG : !enabled ? T.DIM : hover ? T.HOT : T.FRAME;
        T.Bracket(p, r.X, r.Y + 2, r.Bottom - 2, true, fc);
        T.Bracket(p, r.Right - 1, r.Y + 2, r.Bottom - 2, false, fc);
        T.It.Put(p, value, r.X + 5, MathF.Floor(r.Y + (r.Height - 7) / 2) - 2, lit ? T.BG : !enabled ? T.DIM : hover ? T.HOT : T.INK);
        return click;
    }

    // ---------------- Look: the setup utility's item list ----------------

    void LookTerm(Ui ui)
    {
        var p = ui.P;
        var sk = T.It;
        var a = C.Appearance;
        bool changed = false;
        const float lx = 22, vx = 262, top = 64, pitch = 34, divX = 528;

        string[] names = { "Style", "Laser colour", "Laser thickness", "Always show the beam", "Night mode", "Night brightness", "Modern colours",
            "Motion" };
        int rows = names.Length;
        for (int i = 0; i < rows; i++)
            if (ui.Hover(new RectangleF(8, top + i * pitch - 6, divX - 10, pitch))) _tRow = i;
        _tRow = Math.Clamp(_tRow, 0, rows - 1);
        // the selection bar: it moves to the item pointed at (a quick slide); a name is dark where the bar is under it
        float barY = TBarY(top + _tRow * pitch);
        T.Lit(p, new RectangleF(lx - 6, barY, vx - lx - 2, 21));
        for (int i = 0; i < rows; i++)
        {
            float y = top + i * pitch;
            bool cur = MathF.Abs(y - barY) < 10.5f;
            sk.Put(p, T.Up(names[i]), lx, y + 5, cur ? T.BG : T.INK);
        }

        // Style
        float yy = top;
        int si = StyleMenu.IndexOf(a.Style);
        if (ui.Combo95("lkT.style", new RectangleF(vx, yy, 172, 21), TStyleNames, ref si)) { a.Style = StyleMenu.Ids[si]; changed = true; }
        // Laser colour
        yy += pitch;
        var wells = new List<uint> { 0 };
        wells.AddRange(Theme.AccentPresets.Take(Theme.AccentPresets.Length - 1));
        int li = 0;
        if (a.LaserColor != 0)
            for (int i = 1; i < wells.Count; i++)
                if ((a.LaserColor & 0xFFFFFF) == (wells[i] & 0xFFFFFF)) li = i;
        string[] lnames = TLaserNames.Take(wells.Count).ToArray();
        if (ui.Combo95("lkT.laser", new RectangleF(vx, yy, 172, 21), lnames, ref li)) { a.LaserColor = wells[li]; changed = true; }
        // Laser thickness
        yy += pitch;
        float th = a.LaserThickness;
        if (ui.Track95("lkT.th", new RectangleF(vx - 6, yy, 162, 30), ref th, 1.5f, 8f))
        { a.LaserThickness = MathF.Round(th * 2) / 2; changed = true; }
        sk.Put(p, $"{a.LaserThickness:0.0} MM", vx + 166, yy + 5, T.INK);
        // Always show the beam
        yy += pitch;
        if (TValueBox(ui, "lkT.always", new RectangleF(vx, yy, 92, 21), a.LaserAlways ? "ENABLED" : "DISABLED"))
        { a.LaserAlways = !a.LaserAlways; changed = ui.Changed = true; }
        // Night mode
        yy += pitch;
        if (TValueBox(ui, "lkT.night", new RectangleF(vx, yy, 92, 21), a.NightMode ? "ENABLED" : "DISABLED"))
        { a.NightMode = !a.NightMode; changed = ui.Changed = true; }
        // Night brightness
        yy += pitch;
        float nb = a.NightBrightness;
        if (ui.Track95("lkT.nb", new RectangleF(vx - 6, yy, 162, 30), ref nb, 0.15f, 0.9f)) { a.NightBrightness = nb; changed = true; }
        sk.Put(p, $"{a.NightBrightness * 100:0}%", vx + 166, yy + 5, T.INK);
        // Modern colours: back to the default look
        yy += pitch;
        if (TValueBox(ui, "lkT.reset", new RectangleF(vx, yy, 92, 21), "RESET"))
        {
            C.Appearance = a = new AppearanceConfig
            {
                LaserThickness = a.LaserThickness, LaserAlways = a.LaserAlways, Clock24h = a.Clock24h,
                NightMode = a.NightMode, NightBrightness = a.NightBrightness, Style = a.Style, Font = a.Font, Motion = a.Motion,
                LookVersion = 1,     // the reset IS the Bento look: never the 1.10 migration again (1.13.2 put pink over a green accent)
            };
            changed = ui.Changed = true;
        }
        // Motion: FULL -> REDUCED -> OFF -> FULL at each click (Gfx/Motion.cs; written only when not FULL, it applies at once)
        int mi = MotionIndex(a.Motion);
        if (TValueBox(ui, "lkT.motion", MotionTerm, TMotionNames[mi]))           // (top + 7 pitches: under Modern colours)
        { a.Motion = Motion.Id((MotionLevel)((mi + 1) % TMotionNames.Length)); changed = ui.Changed = true; }

        // the help column
        p.Fill(new RectangleF(divX, 47, 1, 392), T.FRAME);
        var help = new RectangleF(divX + 8, 52, W95W - 6 - divX - 16, 380);
        string head = "ITEM HELP";
        sk.Put(p, head, MathF.Floor(help.X + (help.Width - T.W(head)) / 2), help.Y + 4, T.INK);
        p.Fill(new RectangleF(divX + 1, help.Y + 20, W95W - 7 - divX - 1, 1), T.FRAME);
        string era = "the real pixel size of its day";
        string text = _tRow switch
        {
            0 => Lang.F("How OrbitOverlay looks: the settings, the watch, the keyboard and the notifications.\n\nChoices: MODERN, WINDOWS 95, XP, 7, CLASSIC MAC, TERMINAL.\n\n" +
                 "{0} is drawn at {1}, enlarged without smoothing. The change shows in VR at once.", Lang.T(Theme.RetroSkin.Name), Lang.T(era)),
            1 => "The colour of the laser beam in VR.\n\nPHOSPHOR: the style's own green. The others are the Modern accent colours.",
            2 => "How thick the beam is: 1.5 to 8 mm.",
            3 => "ENABLED: the beam always shows.\n\nDISABLED: only while it points at a panel.",
            4 => "Dims every panel in VR.\n\nThe watch has a key for it too (NIGHT).",
            5 => "How bright the panels stay in night mode.",
            7 => "How much moves.\n\nFULL: every animation of the style - the tube switching on and off, the cursor blinking, the band.\n\n" +
                 "REDUCED: short movements only, no loops.\n\nOFF: nothing moves, everything changes at once.",
            _ => "The accent colour, the panel colour, the themes and the opacity belong to the Modern style: switch to it to change them.\n\n" +
                 "RESET puts them back.",
        };
        sk.Put(p, text, new RectangleF(help.X, help.Y + 30, help.Width, help.Height - 34), T.MID, 1, false, TextAlignment.Leading,
            ParagraphAlignment.Near, true);

        if (changed) { ui.Changed = true; _h.AppearanceChanged(); }
    }
}
