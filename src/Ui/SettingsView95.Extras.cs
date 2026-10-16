using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Vortice.DirectWrite;

namespace Orbit;

// 1.14's settings on the 95 sheet - and so in every retro style, which all draw these pages with the shared 95 code in their own
// skin (XP, 7, the Mac, the Terminal): General's Notifications box gets the two alerts; VRChat a "Quick gesture" box under
// Controllers; Layouts a "Per game..." button that opens a dialog of the rules; the watch editor a "Share..." button that opens a
// dialog to export / import / copy / paste the page. A dialog is a window of the style over the page, the page under it only a
// picture until it closes (as a 95 modal dialog). The logic is the Modern pages' (SettingsView.Extras.cs).
public sealed partial class SettingsView
{
    // ---- a dialog over the page ----

    // at the top of a page that can open one: while it is open the page is only a picture (returns how Inert was, for the end)
    bool Dialog95Begin(Ui ui, bool open)
    {
        bool was = ui.Inert;
        if (open) ui.Inert = true;
        return was;
    }

    // the dialog's window in the style (its title bar), the client area inside
    static RectangleF Window95(Ui ui, RectangleF r, string title)
    {
        var c = Theme.RetroSkin.WidgetFrame(ui.P, r, title);
        if (Theme.Retro != RetroStyle.Aero) return c;
        // 7's frame is glass all through (a gadget's): a dialog's client is the page's white inside it, a hairline round it
        var ci = new RectangleF(c.X + 4, c.Y + 2, c.Width - 8, c.Height - 6);
        ui.P.Fill(ci, 0xFF8EA4C1);
        ci = RectangleF.Inflate(ci, -1, -1);
        ui.P.Fill(ci, 0xFFFFFFFF);
        return ci;
    }

    // ================= General > Notifications: the alerts =================

    static readonly string[] BatteryPct95 = { "10%", "15%", "25%" }, FpsPct95 = { "70%", "80%", "90%" };

    // under Height in the Notifications box: a line saying what an alert is, then low battery and low frame rate, each with its
    // threshold; the drop-downs line up after the longer label (the styles' fonts differ in width)
    void Alerts95(Ui ui)
    {
        var a = AlertsNow;
        const string lb = "Low &battery, under", lf = "Low frame &rate, under";
        ui.Text95("Alerts - a card and a buzz in the controller:", new RectangleF(388, 364, 334, 13), K95);
        float cx = MathF.Ceiling(388 + 19 + MathF.Max(ui.LabelWidth95(lb), ui.LabelWidth95(lf)) + 8);
        bool b = a.Battery;
        ui.Check95("g95.abat", 388, 383, lb, ref b);
        if (b != a.Battery) SetAlerts(x => x.Battery = b);
        int bi = Nearest(BatteryPcts, a.BatteryPct), bi0 = bi;
        if (ui.Combo95("g95.abatp", new RectangleF(cx, 381, 58, RowH), BatteryPct95, ref bi, a.Battery) && bi != bi0)
            SetAlerts(x => x.BatteryPct = BatteryPcts[bi]);
        b = a.Fps;
        ui.Check95("g95.afps", 388, 406, lf, ref b);
        if (b != a.Fps) SetAlerts(x => x.Fps = b);
        int fi = Nearest(FpsPcts, a.FpsPct), fi0 = fi;
        if (ui.Combo95("g95.afpsp", new RectangleF(cx, 404, 58, RowH), FpsPct95, ref fi, a.Fps) && fi != fi0)
            SetAlerts(x => x.FpsPct = FpsPcts[fi]);
        ui.Text95(Fit95(ui, "of the rate, 10 s", 722 - (cx + 64)), new RectangleF(cx + 64, 408, 722 - (cx + 64), 13), K95, false, TextAlignment.Leading, a.Fps);
    }

    // ================= VRChat: the quick gesture =================

    static readonly string[] Double95 = { "Off", "Keyboard on / off", "Watch pinned on / off" };

    void Gesture95(Ui ui)
    {
        var g = C.General;
        ui.Group95(new RectangleF(16, 342, 350, 88), "Quick gesture");
        int di = DoubleIndex(g.DoublePress), d0 = di;
        if (ComboRow(ui, "vc95.dbl", 28, 362, 118, 150, "B / Y t&wice:", Double95, ref di) && di != d0)
        {
            g.DoublePress = DoublePress.Store(DoubleIds[di]);
            ui.Changed = true;
        }
        Hint(ui, 28, 390, 326, di > 0 && !g.BlockGameInput
                ? "Needs \"Keep clicks from the game\" above: only then can VRChat never get the presses."
                : "Only on OrbitOverlay panels, so VRChat never gets it. On monitors B / Y stays the right click.", 30);
    }

    // ================= Layouts > Per game: a button, its dialog =================

    void PerGame95(Ui ui, bool inertWas)
    {
        if (ui.Button95("l95.games", new RectangleF(388, 264, 110, 23), "Per &game..."))
            OpenRules(true);
        ui.Text95(Fit95(ui, RulesSummary(), 214), new RectangleF(506, 269, 216, 13), Grey95);
        ui.Inert = inertWas;
        if (_rulesOpen) Rules95(ui);
    }

    void Rules95(Ui ui)
    {
        var p = ui.P;
        var c = Window95(ui, new RectangleF(110, 76, 530, 320), "Per-Game Layouts");
        float x = c.X + 10, w = c.Width - 20, y = c.Y + 6;
        ui.Text95("When one of these SteamVR apps starts, OrbitOverlay switches to its layout and watch page - once; what you pick after " +
                  "that stays until the next app starts.", new RectangleF(x, y, w, 28), K95, false, TextAlignment.Leading, true, true);
        var rules = C.GameRules ?? new List<GameRule>();
        _ruleSel = rules.Count == 0 ? -1 : Math.Clamp(_ruleSel, 0, rules.Count - 1);
        float c1 = 220, c2 = 340;
        ui.Text95("App", new RectangleF(x + 4, y + 36, 200, 13), K95, true);
        ui.Text95("Layout", new RectangleF(x + c1, y + 36, 110, 13), K95, true);
        ui.Text95("Watch page", new RectangleF(x + c2, y + 36, w - c2, 13), K95, true);
        int hit = ui.List95("r95.list", new RectangleF(x, y + 52, w, 4 + 5 * 16), rules.Count, _ruleSel, (i, row, on) =>
        {
            var r = rules[i];
            uint ink = on ? 0xFFFFFFFF : K95;
            ui.Text95(Fit95(ui, SceneApp.Label(r), c1 - 12), new RectangleF(row.X + 2, row.Y, c1 - 8, row.Height), ink);
            ui.Text95(r.Layout ?? "(keep)", new RectangleF(row.X + c1 - 4, row.Y, c2 - c1 - 6, row.Height), ink);
            ui.Text95(Fit95(ui, r.Page is int pg ? PageLabel(pg) : "(keep)", w - c2 - 20), new RectangleF(row.X + c2 - 4, row.Y, w - c2 - 16, row.Height), ink);
        });
        if (hit >= 0) _ruleSel = hit;
        if (rules.Count == 0) ui.Text95("(no rules yet - add one below)", new RectangleF(x + 4, y + 56, 300, 13), Grey95);

        // the selected rule: what it switches to
        var sel = _ruleSel >= 0 && _ruleSel < rules.Count ? rules[_ruleSel] : null;
        float ry = y + 148;
        var layNames = new List<string> { "(keep)" };
        layNames.AddRange(C.Layouts.Select(l => Ui.Esc95(l.Name)));
        int li = sel?.Layout == null ? 0 : 1 + C.Layouts.FindIndex(l => l.Name == sel.Layout), li0 = li;
        if (li < 0) li = li0 = 0;
        Lbl(ui, x, ry, "&Layout:", sel != null);
        if (ui.Combo95("r95.lay", new RectangleF(x + 50, ry, 110, RowH), layNames.ToArray(), ref li, sel != null) && li != li0 && sel != null)
        { sel.Layout = li == 0 ? null : C.Layouts[li - 1].Name; _h.Cfg.MarkDirty(); }
        var wc = C.Wrist;
        var pageNames = new List<string> { "(keep)" };
        for (int i = 0; i < WatchPages.Count(wc); i++) pageNames.Add($"{i + 1}  {Ui.Esc95(WatchPages.Name(wc, i))}");
        int pi = sel?.Page is int sp && sp < WatchPages.Count(wc) ? sp + 1 : 0, pi0 = pi;
        bool custom = wc.Face == "custom";
        Lbl(ui, x + 180, ry, "Watch &page:", sel != null && custom);
        if (ui.Combo95("r95.page", new RectangleF(x + 252, ry, 150, RowH), pageNames.ToArray(), ref pi, sel != null && custom) && pi != pi0 && sel != null)
        { sel.Page = pi == 0 ? null : pi - 1; _h.Cfg.MarkDirty(); }

        // add one: the app running now, VRChat, any other app; remove the selected one
        float by = y + 186, bx = x;
        var choices = RuleChoices();
        bool room = rules.Count < MaxRules;
        var (rk, rn) = _h.RunningApp;
        bool running = !string.IsNullOrEmpty(rk) && choices.Any(ch => ch.key == rk);
        if (ui.Button95("r95.run", new RectangleF(bx, by, 150, 23), running ? Fit95(ui, "Add " + Ui.Esc95(rn ?? rk), 140) : "Add r&unning app", room && running))
            AddRule(rk, rn);
        bx += 156;
        if (ui.Button95("r95.vrc", new RectangleF(bx, by, 90, 23), "Add &VRChat", room && choices.Any(ch => ch.key == GameRuleEngine.VrchatKey)))
            AddRule(GameRuleEngine.VrchatKey, "VRChat");
        bx += 96;
        if (ui.Button95("r95.any", new RectangleF(bx, by, 110, 23), "Add &other apps", room && choices.Any(ch => ch.key == GameRuleEngine.AnyOther)))
            AddRule(GameRuleEngine.AnyOther, null);
        bx += 116;
        if (ui.Button95("r95.rm", new RectangleF(bx, by, 75, 23), "&Remove", sel != null)) RemoveRule(sel);
        ui.Text95("\"Any other app\": every app without a rule of its own. (keep): that one is left as it is. Up to " + MaxRules + " rules.",
            new RectangleF(x, by + 34, w, 28), K95, false, TextAlignment.Leading, true, true);
        if (ui.Button95("r95.ok", new RectangleF(c.Right - 10 - 75, c.Bottom - 31, 75, 23), "OK", true, isDefault: true)) OpenRules(false);
    }

    // ================= the watch editor: Share... and its dialog =================

    void Share95(Ui ui, bool inertWas)
    {
        if (ui.Button95("we95.share", new RectangleF(610, 241, 112, 23), "S&hare...")) OpenShare(true);
        ui.Inert = inertWas;
        if (_wshare) ShareDialog95(ui);
    }

    void ShareDialog95(Ui ui)
    {
        var w = C.Wrist;
        int ep = EditPage;
        var c = Window95(ui, new RectangleF(150, 66, 450, 360), "Share Watch Page");
        float x = c.X + 10, cw = c.Width - 20, y = c.Y + 8;
        W95Icons.Draw(ui.P, W95Icons.Id.Clock, x, y);
        ui.Text95(Fit95(ui, $"Page {ep + 1}: {WatchPages.Name(w, ep)} ({WatchPages.GridId(w, ep).Replace("x", " x ")})", cw - 26), new RectangleF(x + 22, y + 2, cw - 24, 15), K95, true);
        bool room = WatchPages.Count(w) < FaceShare.MaxPages;

        ui.Group95(new RectangleF(x, y + 26, cw, 52), "This page");
        if (ui.Button95("ws95.export", new RectangleF(x + 12, y + 46, 150, 23), InVr ? "Save to &Documents" : "E&xport to a file...")) ExportPage();
        if (ui.Button95("ws95.copy", new RectangleF(x + 170, y + 46, 120, 23), "&Copy as text")) CopyPage();

        float gy = y + 86;
        if (InVr)
        {
            ui.Group95(new RectangleF(x, gy, cw, 158), "Add a page from");
            ui.Text95("Documents\\OrbitOverlay\\faces:", new RectangleF(x + 12, gy + 18, cw - 24, 13), K95);
            var files = ShareFiles();
            int hit = ui.List95("ws95.files", new RectangleF(x + 12, gy + 34, cw - 24, 4 + 5 * 16), files.Count, _shareSel, (i, row, on) =>
                ui.Text95(Fit95(ui, Path.GetFileNameWithoutExtension(files[i].Name), row.Width - 6), new RectangleF(row.X + 2, row.Y, row.Width - 4, row.Height),
                    on ? 0xFFFFFFFF : K95));
            if (hit >= 0) _shareSel = hit;
            if (files.Count == 0) ui.Text95("(no files there yet)", new RectangleF(x + 16, gy + 38, 200, 13), Grey95);
            if (ui.Button95("ws95.import", new RectangleF(x + 12, gy + 126, 120, 23), "&Import", room && _shareSel >= 0 && _shareSel < files.Count)) ImportFile();
            if (ui.Button95("ws95.paste", new RectangleF(x + 138, gy + 126, 150, 23), "&Paste from the clipboard", room)) PastePage();
            gy += 166;
        }
        else
        {
            ui.Group95(new RectangleF(x, gy, cw, 52), "Add a page from");
            if (ui.Button95("ws95.import", new RectangleF(x + 12, gy + 20, 150, 23), "&Import a file...", room)) ImportFile();
            if (ui.Button95("ws95.paste", new RectangleF(x + 170, gy + 20, 150, 23), "&Paste text", room)) PastePage();
            gy += 60;
        }
        string msg = !room ? $"The watch has {FaceShare.MaxPages} pages: remove one to add another." : _shareMsg ??
            "A page goes as a small file or as text: its widgets, grid, name and looks. Adding one never changes your other pages.";
        ui.Text95(msg, new RectangleF(x, gy, cw, c.Bottom - 36 - gy), _shareBad || !room ? 0xFF800000 : K95, false, TextAlignment.Leading, true, true);
        if (ui.Button95("ws95.close", new RectangleF(c.Right - 10 - 75, c.Bottom - 31, 75, 23), "Close", true, isDefault: true)) OpenShare(false);
    }
}
