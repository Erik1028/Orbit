using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Orbit;

// --snapshot, 1.14 (alerts, B / Y twice, per game, sharing a watch page): the rules as "want" lines on made-up readings, app keys
// and files in a scratch folder (never the real clipboard, the real Documents or the real config), the new settings clicked in the
// Modern and the 95 pages, and their pictures in every style: extras/*.png. ORBIT_SNAP_ONLY=extras runs this alone.
static partial class Tools
{
    static int ExtrasRun(WristPanel wrist, KeyboardPanel kb, SettingsView view, SettingsView panelView, Ui ui, Surface surf, PointerState ptr,
        Painter painter, Gpu gpu, ConfigStore cfg, PanelData data, FakeHost host, string dir)
    {
        string xdir = Path.Combine(dir, "extras");
        Directory.CreateDirectory(xdir);
        var culture0 = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        var look0 = cfg.Data.Appearance;
        var wcfg = cfg.Data.Wrist;
        string face0 = wcfg.Face;
        var widgets0 = wcfg.Widgets;
        var pages0 = wcfg.Pages;
        var (name0, grid0, page0) = (wcfg.Name, wcfg.Grid, wcfg.Page);
        var (rules0, alerts0, double0) = (cfg.Data.GameRules, cfg.Data.General.Alerts, cfg.Data.General.DoublePress);
        var running0 = host.RunningApp;
        string folder0 = FaceShare.Folder;
        var set0 = FaceShare.SetClipboard;
        var get0 = FaceShare.GetClipboard;
        string clip = null;
        // the fakes: the faces folder in the snapshot's own folder (emptied first), the clipboard in memory, a running app
        FaceShare.Folder = Path.Combine(xdir, "faces");
        if (Directory.Exists(FaceShare.Folder)) Directory.Delete(FaceShare.Folder, true);
        FaceShare.SetClipboard = t => clip = t;
        FaceShare.GetClipboard = () => clip;
        host.RunningApp = ("steam.app.620980", "Beat Saber");
        int problems = 0;
        void Want(string line, bool ok) { if (!ok) problems++; Log.Info(line); }
        try
        {
            AlertWants(Want);
            GestureWants(Want, gpu, cfg, wrist, host);
            RuleWants(Want);
            ShareWants(Want);

            // ---------------- the settings, clicked ----------------
            void Look(string style) { cfg.Data.Appearance = new AppearanceConfig { Style = style, Clock24h = true }; Theme.Apply(cfg.Data.Appearance); }
            void FrameM(SettingsView v) { wrist.Draw(data); ui.Begin(ptr, 1.5f); painter.Begin(surf.Bitmap, Theme.Bg, 1.5f); v.Draw(ui); painter.End(); ui.End(); }
            void ClickM(float x, float y) { ptr.Move(x * 1.5f, y * 1.5f); FrameM(view); ptr.Press(); FrameM(view); ptr.Release(); FrameM(view); }
            void Frame95(SettingsView v) { wrist.Draw(data); v.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal); }
            void Click95(float x, float y) { ptr.Move(x * 2, y * 2); Frame95(view); ptr.Press(); Frame95(view); ptr.Release(); Frame95(view); Frame95(view); }
            void Settle(Action frame) { ptr.Leave(); wrist.Settle(); frame(); ui.ResetAnimations(); frame(); }
            void Save(string name) => surf.SavePng(gpu, Path.Combine(xdir, name + ".png"));
            string Ser() => cfg.Serialized();
            bool Written(string key) => Ser().Contains($"\"{key}\"");

            // a face with three pages for the editor and the rules
            void Face()
            {
                wcfg.Face = "custom";
                wcfg.Name = "Everyday";
                wcfg.Grid = null;
                wcfg.Widgets = WatchWidgets.PagePreset("everyday", GridSpec.G64);
                wcfg.Pages = new List<WatchPage> { WatchPages.Make("vrchat", "6x4"), WatchPages.Make("performance", "4x3") };
                wcfg.Page = 0;
            }
            Face();

            // ---- Modern: General > Alerts (a click on the frame-rate switch, 70 %, then the defaults again) ----
            Look("modern");
            view.Tab = SettingsView.TabGeneral;
            ui.ResetAnimations();
            bool none0 = !Written("Alerts");
            ClickM(618 - 10 + 116, 460 + 70 + 23);                      // Low frame rate
            ClickM(618 + 226 + 140 / 6f * 1, 460 + 76 + 17);           // 70 %
            bool fpsOn = cfg.Data.General.Alerts is { Fps: true, FpsPct: 70, Battery: true, BatteryPct: 15 } && Written("Alerts");
            Settle(() => FrameM(view));
            Save("general_alerts");
            ClickM(618 + 226 + 140 / 6f * 3, 460 + 76 + 17);           // 80 %
            ClickM(618 - 10 + 116, 460 + 70 + 23);                      // off again
            bool back = cfg.Data.General.Alerts == null && !Written("Alerts");
            Want($"extras alerts settings: nothing written before {none0} (want True); Low frame rate on at 70 % is written {fpsOn} (want True); " +
                 $"back to the defaults writes nothing {back} (want True)", none0 && fpsOn && back);

            // ---- Modern: VRChat > B / Y twice (Watch, then Keyboard again) ----
            view.Tab = SettingsView.TabVrchat;
            ui.ResetAnimations();
            float sx = 222 + 120, sw = (376 - 130) / 3f;
            ClickM(sx + sw * 2.5f, 496 + 19);
            bool watch = cfg.Data.General.DoublePress == "watch" && Written("DoublePress");
            Settle(() => FrameM(view));
            Save("vrchat_gesture");
            ClickM(sx + sw * 1.5f, 496 + 19);
            bool kbBack = cfg.Data.General.DoublePress == null && !Written("DoublePress");
            Want($"extras gesture settings: Watch picked is written {watch} (want True), Keyboard (the default) again writes nothing {kbBack} (want True)", watch && kbBack);

            // ---- Modern: Layouts > Per game (open, add VRChat, its layout one on, its page, Done) ----
            view.Tab = SettingsView.TabLayouts;
            ui.ResetAnimations();
            Settle(() => FrameM(view));
            Save("layouts_pergame_button");
            bool noRules = !Written("GameRules");
            ClickM(602 + 10 + 186, 394 + 19);                           // Per game: ...
            Settle(() => FrameM(view));
            Save("layouts_rules_empty");
            float aw = (382 - 10 - 12) / 3f;
            ClickM(612 + 1 * (aw + 6) + aw / 2, 202 + 17);              // + VRChat (the running app, Beat Saber, is first)
            var vr = cfg.Data.GameRules?.FirstOrDefault(r => r.App == GameRuleEngine.VrchatKey);
            bool added = vr != null && vr.Layout == cfg.Data.ActiveLayout;
            float bw = (372 - 30) / 2f;
            ClickM(622 + bw / 2, 134 + 27 + 14);                        // Layout: Desk -> Bed
            ClickM(632 + bw + bw / 2, 134 + 27 + 14);                   // Watch: keep -> Everyday (page 1)
            float aw2 = (382 - 10 - 6) / 2f;
            ClickM(612 + 1 * (aw2 + 6) + aw2 / 2, 134 + 66 + 17);       // + Other apps (now second of two: Beat Saber, Other apps)
            bool cycled = vr != null && vr.Layout == "Bed" && vr.Page == 0 && cfg.Data.GameRules.Count == 2 && cfg.Data.GameRules[1].App == GameRuleEngine.AnyOther;
            Settle(() => FrameM(view));
            Save("layouts_rules");
            ClickM(602 + 382 - 48, 97);                                 // Done
            Settle(() => FrameM(view));
            Save("layouts_rules_summary");
            Want($"extras per game settings: nothing written before {noRules} (want True); VRChat added with the layout in use {added} (want True), " +
                 $"its layout and page set by their buttons, Other apps added after it {cycled} (want True), written {Written("GameRules")} (want True)",
                 noRules && added && cycled && Written("GameRules"));

            // ---- Modern: the watch editor's Share (in VR: the faces folder; on the desktop: the file dialogs) ----
            view.Tab = SettingsView.TabWrist;
            view.EditingWatch = true;
            ui.ResetAnimations();
            Face();
            int n0 = WatchPages.Count(wcfg);
            string pagesBefore = JsonSerializer.Serialize(wcfg.Pages);
            ClickM(222 + 486 - 118 - 6 - 40 + 20, 76 + 16);             // the share button in the page strip
            ClickM(724 + 130, 130 + 18);                                // Save to Documents
            var files = FaceShare.List();
            bool savedVr = files.Count == 1 && host.Notes.Count == 1 && host.Notes[0].text.Contains("faces");
            ClickM(724 + 130, 172 + 18);                                // Copy as text
            bool copied = clip != null && clip.StartsWith("{\"OrbitFace\":1") && !clip.Contains('\n');
            ClickM(724 + 130, 266 + 14);                                // the file in the list
            ClickM(724 + 130, 266 + 32 + 2 + 18);                       // Import the picked file
            bool importedVr = WatchPages.Count(wcfg) == n0 + 1;
            Settle(() => FrameM(view));
            Save("editor_share_vr");
            ClickM(724 + 130, 266 + 32 + 44 + 18);                      // Paste text
            bool pasted = WatchPages.Count(wcfg) == n0 + 2 && wcfg.Page == n0 + 1;
            bool untouched = JsonSerializer.Serialize(wcfg.Pages.Take(n0 - 1).ToList()) == pagesBefore;
            Want($"extras share (in VR): Save to Documents wrote a file there {savedVr} (want True) and said where on a card ({host.Notes.FirstOrDefault().text}); " +
                 $"Copy as text is one line {copied} (want True); the file imported {importedVr} (want True), the text pasted {pasted} (want True), " +
                 $"the pages there before untouched {untouched} (want True)", savedVr && copied && importedVr && pasted && untouched);
            // on the desktop: Export asks for a file to save, Import for one to open (a fake dialog answers)
            var asked = new List<bool>();
            string deskFile = Path.Combine(FaceShare.Folder, "desk-export.json");
            view.AskFile = (save, name, then) => { asked.Add(save); then(save ? deskFile : files[0].FullName); };
            ShowPageForTest(view, wcfg, 1);
            ClickM(724 + 130, 130 + 18);                                // Export to a file
            bool exported = File.Exists(deskFile) && FaceShare.Parse(File.ReadAllText(deskFile), out _, out _) is { Name: "VRChat" };
            int nd = WatchPages.Count(wcfg);
            ClickM(724 + 130, 246 + 18);                                // Import a file...
            bool importedDesk = WatchPages.Count(wcfg) == nd + 1 && asked.SequenceEqual(new[] { true, false });
            Settle(() => FrameM(view));
            Save("editor_share");
            Want($"extras share (desktop): Export asked for a file to save and wrote page 2 there {exported} (want True), Import a file... asked for one to open and " +
                 $"added it {importedDesk} (want True)", exported && importedDesk);
            view.AskFile = null;
            // the 8-page limit (on the desktop: Paste text under Import a file...): the paste is refused, nothing added
            view.AskFile = (save, name, then) => then(null);
            while (WatchPages.Count(wcfg) < FaceShare.MaxPages - 1) wcfg.Pages.Add(WatchPages.Make("empty", "6x4"));
            ClickM(724 + 130, 246 + 42 + 18);                           // Paste text: the 8th page
            bool eighth = WatchPages.Count(wcfg) == FaceShare.MaxPages;
            ClickM(724 + 130, 246 + 42 + 18);                           // again: refused
            bool ninthUi = WatchPages.Count(wcfg) == FaceShare.MaxPages;
            Settle(() => FrameM(view));
            Save("editor_share_full");
            view.AskFile = null;
            Want($"extras share: Paste text adds the 8th page {eighth} (want True), a 9th is refused {ninthUi} (want True)", eighth && ninthUi);
            Face();
            ClickM(222 + 486 - 118 - 6 - 40 + 20, 76 + 16);             // closed again
            view.EditingWatch = false;

            // ---- the fit of the new texts, Modern and Cyber: nothing sticks out of its box ----
            foreach (var style in new[] { "modern", CyberHud.Id })
            {
                Look(style);
                int bad = FitNew(view, ui, ptr, painter, surf, wrist, data, wcfg, host, style == "modern" ? "" : "_cyber", xdir, gpu);
                Want($"extras fit {style}: the new texts sticking out of their box {bad} (want 0)", bad == 0);
            }

            // ---- every retro style: the sheet's pages, the two dialogs (95: clicked) ----
            foreach (var sk in Skins.All)
            {
                Look(sk.Id);
                string tag = "_" + sk.Tag;
                Face();
                cfg.Data.General.Alerts = new AlertsConfig { Fps = true };
                cfg.Data.General.DoublePress = "watch";
                foreach (int t in new[] { SettingsView.TabGeneral, SettingsView.TabVrchat, SettingsView.TabLayouts })
                {
                    view.Tab = t;
                    Settle(() => Frame95(view));
                    Save($"settings{t}{tag}");
                }
                view.Tab = SettingsView.TabLayouts;
                if (sk.Tag == "95")
                {
                    // the dialog: Add other apps, then OK; the rules there before kept
                    int r0 = cfg.Data.GameRules?.Count ?? 0;
                    cfg.Data.GameRules?.RemoveAll(r => r.App == GameRuleEngine.AnyOther);
                    Click95(388 + 55, 264 + 11);
                    bool open95 = OpenFor(view);
                    Click95(376 + 55, 104 + 186 + 11);
                    bool any = cfg.Data.GameRules?.Any(r => r.App == GameRuleEngine.AnyOther) == true;
                    Settle(() => Frame95(view));
                    Save("layouts_rules_95");
                    Click95(636 - 10 - 75 + 37, 392 - 31 + 11);
                    Want($"extras per game 95: Per game... opens its dialog {open95} (want True), Add other apps adds the rule {any} (want True), " +
                         $"OK closes it {!OpenFor(view)} (want True)", open95 && any && !OpenFor(view));
                }
                else
                {
                    SetOpen(view, "_rulesOpen", true);
                    Settle(() => Frame95(view));
                    Save($"layouts_rules{tag}");
                    SetOpen(view, "_rulesOpen", false);
                }
                // the watch editor and its Share dialog
                view.Tab = SettingsView.TabWrist;
                view.EditingWatch = true;
                Settle(() => Frame95(view));
                Save($"editor{tag}");
                if (sk.Tag == "95")
                {
                    int np = WatchPages.Count(wcfg);
                    clip = FaceShare.Export(wcfg, 0, compact: true);
                    Click95(610 + 56, 241 + 11);                        // Share...
                    Click95(164 + 12 + 126 + 75, 96 + 86 + 126 + 11);   // Paste from the clipboard (in VR: the files list above it)
                    bool pasted95 = WatchPages.Count(wcfg) == np + 1;
                    Settle(() => Frame95(view));
                    Save("editor_share_95");
                    Click95(154 + 442 - 10 - 37, 88 + 334 - 31 + 11);   // Close
                    Want($"extras share 95: the Share dialog pastes a page {pasted95} (want True) and closes {!ShareOpen(view)} (want True)", pasted95 && !ShareOpen(view));
                }
                else
                {
                    SetOpen(view, "_wshare", true);
                    Settle(() => Frame95(view));
                    Save($"editor_share{tag}");
                    SetOpen(view, "_wshare", false);
                }
                // B / Y twice in the 95 sheet's combo: Off, then Keyboard (the default: nothing written)
                if (sk.Tag == "95")
                {
                    view.Tab = SettingsView.TabVrchat;
                    view.EditingWatch = false;
                    float rowH = sk.DropRowH;
                    var f = new System.Drawing.RectangleF(118, 362, 150, 21);
                    float lh = 3 * rowH + 2, ly = f.Bottom + lh <= SettingsView.W95H ? f.Bottom : f.Y - lh;
                    void Pick(int i) { Click95(f.X + f.Width / 2, f.Y + f.Height / 2); Click95(f.X + f.Width / 2, ly + 1 + i * rowH + rowH / 2); }
                    Pick(0);
                    bool off = cfg.Data.General.DoublePress == "off";
                    Pick(1);
                    bool kb95 = cfg.Data.General.DoublePress == null;
                    Want($"extras gesture 95: Off picked in the drop-down {off} (want True), Keyboard again writes nothing {kb95} (want True)", off && kb95);
                    // the alerts' check box in the Notifications box: Low battery off, on again (back to the defaults: nothing written)
                    view.Tab = SettingsView.TabGeneral;
                    cfg.Data.General.Alerts = null;
                    Click95(392, 389);
                    bool batOff = cfg.Data.General.Alerts is { Battery: false };
                    Click95(392, 389);
                    Want($"extras alerts 95: Low battery unticked is written {batOff} (want True), ticked again writes nothing {cfg.Data.General.Alerts == null} (want True)",
                        batOff && cfg.Data.General.Alerts == null);
                }
                view.EditingWatch = false;
                cfg.Data.General.Alerts = null;
                cfg.Data.General.DoublePress = null;
                AlertCards(painter, gpu, cfg, xdir, tag);
            }
            Look("modern");
            AlertCards(painter, gpu, cfg, xdir, "");
            Look(CyberHud.Id);
            AlertCards(painter, gpu, cfg, xdir, "_cyber");
            Log.Info($"extras: {(problems == 0 ? "every check as wanted" : $"{problems} checks NOT as wanted")} (pictures in {xdir})");
        }
        finally
        {
            FaceShare.Folder = folder0;
            FaceShare.SetClipboard = set0;
            FaceShare.GetClipboard = get0;
            cfg.Data.Appearance = look0;
            Theme.Apply(look0);
            wcfg.Face = face0;
            wcfg.Widgets = widgets0;
            wcfg.Pages = pages0;
            (wcfg.Name, wcfg.Grid, wcfg.Page) = (name0, grid0, page0);
            (cfg.Data.GameRules, cfg.Data.General.Alerts, cfg.Data.General.DoublePress) = (rules0, alerts0, double0);
            host.RunningApp = running0;
            host.Notes.Clear();
            view.EditingWatch = false;
            view.AskFile = null;
            view.Tab = 0;
            ptr.Leave();
            ui.ResetAnimations();
            CultureInfo.CurrentCulture = culture0;
        }
        return problems == 0 ? 0 : 1;
    }

    static bool OpenFor(SettingsView v) => (bool)typeof(SettingsView).GetField("_rulesOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v);
    static bool ShareOpen(SettingsView v) => (bool)typeof(SettingsView).GetField("_wshare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v);
    static void SetOpen(SettingsView v, string field, bool on)
    {
        var f = typeof(SettingsView).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        f.SetValue(v, on);
        if (field == "_rulesOpen")
            typeof(SettingsView).GetField("_rulesFor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(v, typeof(SettingsView).GetField("_selLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v));
    }

    static void ShowPageForTest(SettingsView v, WristConfig w, int page) => w.Page = page;

    // the alert cards as they come up in VR (the style's own card)
    static void AlertCards(Painter painter, Gpu gpu, ConfigStore cfg, string xdir, string tag)
    {
        using var np = new NotificationPanel(gpu, painter, cfg);
        np.AddForPreview(Alerts.FpsCard(58, 90, 80));
        np.AddForPreview(Alerts.BatteryCard(new BatteryDevice(3, BatteryDevice.Left, 0.14f, false), 0, 15));
        np.Draw();
        np.Surface.SavePng(gpu, Path.Combine(xdir, $"alert_cards{tag}.png"));
    }

    // The new modern controls' texts against their boxes (Painter.FitSpy, as the fonts check does), only inside the new parts' areas:
    // General's Alerts, VRChat's gesture and controls, Layouts' rules, the editor's Share (in VR and on the desktop)
    static int FitNew(SettingsView view, Ui ui, PointerState ptr, Painter painter, Surface surf, WristPanel wrist, PanelData data, WristConfig wcfg,
        FakeHost host, string tag, string xdir, Gpu gpu)
    {
        var spied = new List<(string s, System.Drawing.RectangleF r, float size, Vortice.DirectWrite.FontWeight w, string fam, bool wrap,
            Vortice.DirectWrite.TextAlignment h, Vortice.DirectWrite.ParagraphAlignment v, float tr)>();
        int bad = 0;
        void Check(string what, System.Drawing.RectangleF area)
        {
            spied.Clear();
            painter.FitSpy = (s, r, size, w, fam, wrap, h, v, tr) => spied.Add((s, r, size, w, fam, wrap, h, v, tr));
            wrist.Draw(data);
            ui.ResetAnimations();
            ptr.Leave();
            ui.Begin(ptr, 1.5f); painter.Begin(surf.Bitmap, Theme.Bg, 1.5f); view.Draw(ui); painter.End(); ui.End();
            painter.FitSpy = null;
            surf.SavePng(gpu, Path.Combine(xdir, $"fit_{what}{tag}.png"));
            foreach (var t in spied)
            {
                if (t.fam == Theme.IconFont || string.IsNullOrWhiteSpace(t.s) || !area.IntersectsWith(t.r)) continue;
                float o = painter.Overhang(t.s, t.r, t.size, t.w, t.fam, t.wrap, t.h, t.v, t.tr);
                if (o >= 1) { bad++; Log.Warn($"extras fit{tag} {what}: '{t.s}' sticks out {o:0.#} px of its {t.r.Width:0} x {t.r.Height:0} box"); }
            }
        }
        var cfgG = host.Cfg.Data.General;
        cfgG.Alerts = new AlertsConfig { Fps = true, FpsPct = 90, BatteryPct = 25 };
        view.Tab = SettingsView.TabGeneral;
        Check("general", new System.Drawing.RectangleF(600, 455, 400, 185));
        view.Tab = SettingsView.TabVrchat;
        foreach (var mode in new[] { null, "off", "watch" })
        {
            cfgG.DoublePress = mode;
            cfgG.BlockGameInput = mode != "off";
            Check("vrchat" + (mode ?? "kb"), new System.Drawing.RectangleF(222, 490, 780, 150));
            Check("vrchat_controls", new System.Drawing.RectangleF(600, 380, 400, 260));
        }
        cfgG.BlockGameInput = true;
        cfgG.DoublePress = null;
        cfgG.Alerts = null;
        // the rules, with long names
        var c = host.Cfg.Data;
        c.GameRules = new List<GameRule>
        {
            new() { App = GameRuleEngine.VrchatKey, Name = "VRChat", Layout = "Couch", Page = 2 },
            new() { App = "steam.app.1", Name = "A Game With A Really Very Long Name: The Definitive Edition", Layout = "Bed" },
            new() { App = GameRuleEngine.AnyOther },
        };
        view.Tab = SettingsView.TabLayouts;
        Check("layouts_button", new System.Drawing.RectangleF(600, 390, 400, 46));
        SetOpen(view, "_rulesOpen", true);
        Check("layouts_rules", new System.Drawing.RectangleF(600, 70, 400, 365));
        SetOpen(view, "_rulesOpen", false);
        c.GameRules = null;
        // the share panel: in VR (with files and a message), on the desktop
        view.Tab = SettingsView.TabWrist;
        view.EditingWatch = true;
        SetOpen(view, "_wshare", true);
        Check("share_vr", new System.Drawing.RectangleF(720, 76, 270, 495));
        view.AskFile = (s, n, then) => then(null);
        Check("share_desktop", new System.Drawing.RectangleF(720, 76, 270, 495));
        view.AskFile = null;
        SetOpen(view, "_wshare", false);
        view.EditingWatch = false;
        return bad;
    }

    // ---- the rules, without SteamVR ----

    static void AlertWants(Action<string, bool> want)
    {
        // a battery: seen at 30 % for 10 s, then under 15 %
        var al = new Alerts();
        var fire = new List<BatteryDevice>();
        var dev = new BatteryDevice(3, BatteryDevice.Left, 0.30f, false);
        double t = 0;
        int Read(float pct, bool chg = false) { fire.Clear(); t += 1; al.Battery(t, new[] { dev with { Pct = pct, Charging = chg } }, 0.15f, fire); return fire.Count; }
        for (int i = 0; i < 10; i++) Read(0.30f);
        int one = Read(0.14f), two = Read(0.14f);
        int still = 0;
        for (int i = 0; i < 30; i++) still += Read(0.12f);
        for (int i = 0; i < 20; i++) still += Read(0.18f, true);          // charging to 18 %: not past 15 + 5
        int notYet = Read(0.14f) + Read(0.14f) + Read(0.13f);
        for (int i = 0; i < 20; i++) Read(0.25f, true);                   // past 20 %: armed again
        int again = Read(0.14f) + Read(0.14f) + Read(0.13f);
        // a tracker that just connected at 5 % (some say 0 for a moment): nothing in its first 10 s; a controller charging at 8 %: nothing
        var trk = new BatteryDevice(9, BatteryDevice.Tracker, 0.05f, false);
        var chgDev = new BatteryDevice(4, BatteryDevice.Right, 0.08f, true);
        int young = 0, charging = 0;
        for (int i = 0; i < 9; i++) { fire.Clear(); t += 1; al.Battery(t, new[] { trk, chgDev }, 0.15f, fire); young += fire.Count(f => f.Index == 9); charging += fire.Count(f => f.Index == 4); }
        for (int i = 0; i < 5; i++) { fire.Clear(); t += 1; al.Battery(t, new[] { trk, chgDev }, 0.15f, fire); charging += fire.Count(f => f.Index == 4); }
        var card = Alerts.BatteryCard(dev with { Pct = 0.14f }, 0, 15);
        want($"extras alerts: a battery under 15 % - one reading nothing {one == 0} (want True), the second fires {two == 1} (want True), " +
             $"then never while low {still == 0} (want True), not before it was charged past 20 % {notYet == 0} (want True), once again after that {again == 1} " +
             $"(want True); a tracker in its first 10 s {young == 0} (want True), a controller charging {charging == 0} (want True); the card: '{card.Title}' / '{card.Content}'",
             one == 0 && two == 1 && still == 0 && notYet == 0 && again == 1 && young == 0 && charging == 0);

        // the frame rate: readings every 1/8 s (exact in binary), all low
        var fa = new Alerts();
        int k = 0;
        bool Step(bool valid = true, bool low = true) => fa.Fps(++k * 0.125, valid, low);
        int early = 0;
        for (int i = 0; i < 80; i++) if (Step()) early++;                  // 9.875 s after the first low one
        bool at10 = Step();                                                // 10 s
        int quiet = 0;
        while ((k + 1) * 0.125 < 10.125 + 120 + 10) if (Step()) quiet++;   // the 2 minutes, then 10 s more (the 10 s count after the quiet)
        bool after = Step();
        // a loading second (invalid) breaks the 10 s: 9 s low, one invalid reading, 9 s low = nothing; then 1 s more = the card
        var fb = new Alerts();
        int j = 0, broke = 0;
        bool StepB(bool valid = true) => fb.Fps(++j * 0.125, valid, true);
        for (int i = 0; i < 72; i++) if (StepB()) broke++;
        StepB(false);
        for (int i = 0; i < 72; i++) if (StepB()) broke++;
        int resumed = 0;
        for (int i = 0; i < 16; i++) if (StepB()) resumed++;
        // fine again for a moment in the middle: the 10 s start over too
        var fc = new Alerts();
        int m = 0, dip = 0;
        for (int i = 0; i < 70; i++) if (fc.Fps(++m * 0.125, true, true)) dip++;
        fc.Fps(++m * 0.125, true, false);
        for (int i = 0; i < 70; i++) if (fc.Fps(++m * 0.125, true, true)) dip++;
        want($"extras alerts: the frame rate low for 9.9 s - nothing {early == 0} (want True), at 10 s the card {at10} (want True), then quiet for 2 minutes " +
             $"(and the 10 s after them) {quiet == 0} (want True), still low after that: the next card {after} (want True); a loading reading breaks the 10 s " +
             $"{broke == 0} (want True), 10 s after it the card {resumed == 1} (want True); a good reading starts them over {dip == 0} (want True)",
             early == 0 && at10 && quiet == 0 && after && broke == 0 && resumed == 1 && dip == 0);
    }

    static void GestureWants(Action<string, bool> want, Gpu gpu, ConfigStore cfg, WristPanel wrist, FakeHost host)
    {
        var dp = new DoublePress();
        bool P(double t, bool pressed = true, bool el = true, int h = 1) => dp.Update(h, pressed, el, t);
        dp.Reset(); P(1.0); bool quick = P(1.2);
        dp.Reset(); P(2.0); bool slow = P(2.5);
        dp.Reset(); P(3.0, el: false); bool monitor = P(3.2, el: false);
        dp.Reset(); P(4.0); P(4.1, pressed: false, el: false); bool left = P(4.2);
        dp.Reset(); int triple = (P(5.0) ? 1 : 0) + (P(5.15) ? 1 : 0) + (P(5.3) ? 1 : 0);
        dp.Reset(); P(6.0, h: 0); bool hands = P(6.2, h: 1);
        // the rule: Orbit's own panels only, the hand already kept from the game, Keep clicks on, not grabbing
        var mon = host.MonitorList.FirstOrDefault();
        IPointerTarget desk = mon == null ? null : new DesktopPanel(gpu, cfg, new MouseOwner(), mon, new DesktopConfig { Id = mon.Id, Name = mon.Name });
        IPointerTarget win = new WindowPanel(gpu, cfg, new MouseOwner(), new WindowConfig());
        bool own = DoublePress.Eligible(true, true, wrist, false);
        bool notMon = (desk == null || !DoublePress.Eligible(true, true, desk, false)) && !DoublePress.Eligible(true, true, win, false);
        bool notNothing = !DoublePress.Eligible(true, true, null, false);
        bool notFree = !DoublePress.Eligible(true, false, wrist, false) && !DoublePress.Eligible(false, true, wrist, false);
        bool notGrab = !DoublePress.Eligible(true, true, wrist, true);
        bool modes = DoublePress.Mode(null) == "keyboard" && DoublePress.Mode("watch") == "watch" && DoublePress.Mode("odd") == "keyboard" && DoublePress.Store("keyboard") == null;
        want($"extras gesture: B / Y twice within 0.35 s on a panel toggles {quick} (want True), 0.5 s apart {slow} (want False), not on a monitor / window / nothing " +
             $"{!monitor && notMon && notNothing} (want True), not when the beam left the panel between {!left} (want True), three quick presses toggle once {triple == 1} " +
             $"(want True), one press per hand {hands} (want False); counts only on Orbit's own panels {own} (want True), with the hand already kept from the game and " +
             $"Keep clicks on {notFree} (want True), not while grabbing {notGrab} (want True); setting null = Keyboard {modes} (want True)",
             quick && !slow && !monitor && notMon && notNothing && !left && triple == 1 && !hands && own && notFree && notGrab && modes);
    }

    static void RuleWants(Action<string, bool> want)
    {
        var rules = new List<GameRule>
        {
            new() { App = GameRuleEngine.VrchatKey, Name = "VRChat", Layout = "Bed", Page = 1 },
            new() { App = GameRuleEngine.AnyOther, Layout = "Couch" },
        };
        var eng = new GameRuleEngine();
        double t = 0;
        var got = new List<GameRule>();
        void Poll(string key, int times = 1) { for (int i = 0; i < times; i++) { t += 1; if (eng.Step(t, key, rules) is { } r) got.Add(r); } }
        // connected while VRChat runs (Orbit restarted mid-game): the baseline, nothing
        Poll(GameRuleEngine.VrchatKey, 10);
        bool baseline = got.Count == 0;
        // VRChat quits, Beat Saber starts: the any-other rule, once
        got.Clear(); Poll(null, 4); Poll("steam.app.620980", 10);
        bool other = got.Count == 1 && got[0].App == GameRuleEngine.AnyOther;
        // a fresh connection with nothing running, then VRChat: its rule once, after 2 s; then nothing however long it runs
        eng.Reset(); got.Clear();
        Poll(null, 3);
        Poll(GameRuleEngine.VrchatKey, 1);
        bool waits = got.Count == 0;
        Poll(GameRuleEngine.VrchatKey, 2);
        bool once = got.Count == 1 && got[0].App == GameRuleEngine.VrchatKey && got[0].Layout == "Bed" && got[0].Page == 1;
        Poll(GameRuleEngine.VrchatKey, 60);
        bool stays = got.Count == 1;
        // a quick restart (gone for a second): nothing
        Poll(null, 1); Poll(GameRuleEngine.VrchatKey, 5);
        bool restart = got.Count == 1;
        // no rule for that app and no any-other rule: nothing
        var only = new List<GameRule> { rules[0] };
        var e2 = new GameRuleEngine();
        double t2 = 0;
        int none = 0;
        foreach (var key in new[] { null, null, null, "steam.app.1", "steam.app.1", "steam.app.1", "steam.app.1" }) if (e2.Step(t2 += 1, key, only) != null) none++;
        want($"extras per game: an app already running at connect switches nothing {baseline} (want True), another app then gets the any-other rule once {other} " +
             $"(want True); VRChat starting waits 2 s {waits} (want True), then switches once to its layout and page {once} (want True), never again while it runs " +
             $"(your own pick stays) {stays} (want True), a quick restart switches nothing {restart} (want True), an app without a rule nothing {none == 0} (want True)",
             baseline && other && waits && once && stays && restart && none == 0);
    }

    static void ShareWants(Action<string, bool> want)
    {
        // a page with looks, colours, a design and a button group, on the 4 x 3 grid
        var w = new WristConfig
        {
            Face = "custom", Name = "Everyday", Widgets = WatchWidgets.PagePreset("everyday", GridSpec.G64),
            Pages = new List<WatchPage> { WatchPages.Make("vrchat", "4x3") },
        };
        var pg = w.Pages[0];
        pg.Name = "VRChat & friends";
        pg.Widgets[0].Style = "twotone"; pg.Widgets[0].Color = 0xFF14B8A6; pg.Widgets[0].Color2 = 0xFFA855F7; pg.Widgets[0].Variant = "flip";
        pg.Widgets[1].Style = "glass";
        var opts = new JsonSerializerOptions();
        string Of(WatchPage p) => $"{p.Name}|{p.Grid}|{JsonSerializer.Serialize(p.Widgets, opts)}";
        string json = FaceShare.Export(w, 1);
        var back = FaceShare.Parse(json, out string e1, out int d1);
        bool round = back != null && d1 == 0 && Of(back) == Of(pg);
        var back2 = FaceShare.Parse("here is my VRChat page:\n```json\n" + FaceShare.Export(w, 1, compact: true) + "\n```\nhave fun", out _, out int d2);
        bool discord = back2 != null && d2 == 0 && Of(back2) == Of(pg);
        // page 1 (Grid null = 6 x 4) the same way
        var back1 = FaceShare.Parse(FaceShare.Export(w, 0), out _, out _);
        bool round1 = back1 != null && back1.Grid == "6x4" && back1.Name == "Everyday" && JsonSerializer.Serialize(back1.Widgets) == JsonSerializer.Serialize(w.Widgets);
        string ok1 = "{\"Type\":\"time\",\"X\":0,\"Y\":0,\"W\":2,\"H\":1}";
        string[] bad =
        {
            "", "hello", "{", "[1,2,3]", "{\"OrbitFace\":1}",
            "{\"Grid\":\"6x4\",\"Widgets\":[" + ok1 + "]}",                                        // no marker
            "{\"OrbitFace\":2,\"Grid\":\"6x4\",\"Widgets\":[" + ok1 + "]}",                         // a newer format
            "{\"OrbitFace\":1,\"Grid\":\"5x5\",\"Widgets\":[" + ok1 + "]}",                         // a grid it does not know
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[]}",                                    // nothing on it
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[{\"Type\":\"hologram\",\"X\":0,\"Y\":0,\"W\":1,\"H\":1}]}",   // only kinds it does not know
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[{\"Type\":\"time\",\"X\":\"left\",\"Y\":0,\"W\":2,\"H\":1}]}", // a wrong value
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[{\"Type\":\"time\",\"X\":9,\"Y\":9,\"W\":2,\"H\":1}]}",     // outside the grid
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[" + ok1 + "],\"x\":[[[[[[[[[[1]]]]]]]]]]}",             // too deep
            "{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Name\":\"" + new string('a', 70000) + "\",\"Widgets\":[" + ok1 + "]}", // too big
        };
        var why = new List<string>();
        int refused = 0;
        foreach (var b in bad) { if (FaceShare.Parse(b, out string err, out _) == null) { refused++; why.Add(err); } }
        // what it does not know is left out, the rest kept: 2 unknown kinds of 5, an unknown look and design dropped, a long name cut
        string mixed = "{\"OrbitFace\":1,\"Name\":\"" + new string('N', 60) + "\",\"Grid\":\"6x4\",\"Widgets\":[" + ok1 + "," +
                       "{\"Type\":\"hologram\",\"X\":2,\"Y\":0,\"W\":1,\"H\":1},{\"Type\":\"fps\",\"X\":3,\"Y\":0,\"W\":1,\"H\":1,\"Style\":\"neon\",\"Variant\":\"x\"}," +
                       "{\"Type\":\"teleporter\",\"X\":4,\"Y\":0,\"W\":1,\"H\":1},{\"Type\":\"buttons\",\"X\":0,\"Y\":1,\"W\":2,\"H\":1,\"Items\":[\"keyboard\",\"warp\",\"night\"]}]}";
        var mp = FaceShare.Parse(mixed, out _, out int md);
        bool kept = mp != null && md == 2 && mp.Widgets.Count == 3 && mp.Widgets[1].Style == null && mp.Widgets[1].Variant == null &&
                    mp.Widgets[2].Items.SequenceEqual(new[] { "keyboard", "night" }) && mp.Name.Length == FaceShare.MaxName;
        // added after the others; the ones there untouched; the 9th refused
        string before = JsonSerializer.Serialize(w.Widgets) + JsonSerializer.Serialize(w.Pages);
        bool addOk = FaceShare.Add(w, back, out _);
        bool untouched = JsonSerializer.Serialize(w.Widgets) + JsonSerializer.Serialize(w.Pages.Take(1).ToList()) == before && w.Pages.Count == 2;
        while (WatchPages.Count(w) < FaceShare.MaxPages) FaceShare.Add(w, FaceShare.Parse(json, out _, out _), out _);
        bool ninth = !FaceShare.Add(w, FaceShare.Parse(json, out _, out _), out string full) && WatchPages.Count(w) == FaceShare.MaxPages;
        // files: a name from the page's, never over another file; one too big is not read
        Directory.CreateDirectory(FaceShare.Folder);
        string big = Path.Combine(FaceShare.Folder, "big.txt");
        File.WriteAllText(big, new string(' ', FaceShare.MaxBytes + 10));
        bool bigRefused = FaceShare.ReadFile(big, out _) == null;
        File.Delete(big);
        var now = new DateTime(2026, 10, 16, 15, 30, 12);
        string f1 = FaceShare.SaveToFolder(w, 1, now), f2 = FaceShare.SaveToFolder(w, 1, now);
        bool names = Path.GetFileName(f1) == "VRChat  friends-20261016-153012.json" && f1 != f2 && File.Exists(f2) && FaceShare.FileName("..\\..\\x:y*") == "xy.json";
        File.Delete(f1); File.Delete(f2);
        // a page too long for a Discord message (every cell a styled widget): a short code on the clipboard instead, read back exactly from
        // inside a message; a cut-off code and one that would unpack past 64 KB refused
        var bigW = new WristConfig { Face = "custom", Widgets = new List<WatchWidget>() };
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 6; x++)
                bigW.Widgets.Add(new WatchWidget { Type = (x + y) % 2 == 0 ? "fps" : "cpu", X = x, Y = y, Style = "twotone", Color = 0xFF14B8A6, Color2 = 0xFFA855F7 });
        string json24 = FaceShare.Export(bigW, 0, compact: true), clip24 = FaceShare.ClipboardText(bigW, 0);
        var back24 = FaceShare.Parse("my page: " + clip24 + " (from OrbitOverlay)", out _, out int d24);
        bool code = json24.Length > FaceShare.DiscordMax && clip24.StartsWith(FaceShare.CodePrefix) && clip24.Length < FaceShare.DiscordMax &&
                    back24 != null && d24 == 0 && JsonSerializer.Serialize(back24.Widgets) == JsonSerializer.Serialize(bigW.Widgets);
        bool cut = FaceShare.Parse(clip24[..(clip24.Length / 2)], out _, out _) == null;
        string bomb;
        using (var ms = new MemoryStream())
        {
            using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.SmallestSize, true))
                z.Write(System.Text.Encoding.UTF8.GetBytes("{\"OrbitFace\":1,\"Grid\":\"6x4\",\"Widgets\":[" + ok1 + "],\"Name\":\"" + new string('x', 300000) + "\"}"));
            bomb = FaceShare.CodePrefix + Convert.ToBase64String(ms.ToArray());
        }
        bool bombRefused = bomb.Length < 4000 && FaceShare.Parse(bomb, out string bombWhy, out _) == null && bombWhy.Contains("too big");
        want($"extras share: a page too long for Discord ({json24.Length} characters) goes as a short code ({clip24.Length}) and reads back exactly {code} " +
             $"(want True); half a code refused {cut} (want True), a code that unpacks past 64 KB refused {bombRefused} (want True)", code && cut && bombRefused);
        want($"extras share: export -> import gives the page exactly {round} (want True), page 1 too {round1} (want True), from a Discord message's code block " +
             $"{discord} (want True); bad text refused {refused} of {bad.Length} (want {bad.Length} of {bad.Length}); unknown kinds left out (2 of 5), unknown looks / " +
             $"designs / buttons dropped, a long name cut {kept} (want True); an import adds a page and leaves the others byte for byte {addOk && untouched} (want True), " +
             $"the 9th page refused {ninth} (want True: '{full}'); a too big file not read {bigRefused} (want True); file names {names} (want True)",
             round && round1 && discord && refused == bad.Length && kept && addOk && untouched && ninth && bigRefused && names);
        Log.Info("extras share: the refusals say: " + string.Join(" | ", why.Distinct()));
    }
}
