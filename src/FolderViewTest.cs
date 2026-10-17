using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Orbit;

// --snapshot's part for the folder widget (WatchFolders, WristFolder*.cs, SettingsView.Folder.cs):
//  - once: a folder with widgets through the config (written and read back the same, nothing written for a face without one) and
//    through a watch page's export / import;
//  - each style: a tap opens it, a button in it acts, a widget in it with a view of its own opens over it and its X comes back to the
//    folder, the folder's X closes it; no garbage a frame at rest and open; wrist_folder_<case><tag>.png - and folder_preview.png;
//  - the editor (Modern): Open folder, a widget added into it from the palette, Move out puts it on the page.
static class FolderViewTools
{
    static WatchWidget W(int x, int y, int w, int h, string type) => new() { Type = type, X = x, Y = y, W = w, H = h };

    // a folder of every sort of thing: a clock, readings, a button, the batteries, the volume, the weather
    public static WatchWidget Folder(int x, int y, int w, int h) => new()
    {
        Type = "folder", X = x, Y = y, W = w, H = h, Name = "VRChat",
        Children = new List<WatchWidget>
        {
            W(0, 0, 2, 1, "time"), W(2, 0, 1, 1, "fps"), W(3, 0, 1, 1, "keyboard"), W(4, 0, 2, 2, "battery"),
            W(0, 1, 2, 1, "volume"), W(2, 1, 2, 1, "weather"), W(0, 2, 1, 1, "night"), W(1, 2, 1, 1, "cpu"),
        },
    };

    static List<WatchWidget> Face(WatchWidget f) => new()
    {
        f, W(2, 0, 4, 2, "time"), W(2, 2, 2, 1, "media"), W(4, 2, 1, 1, "fps"), W(5, 2, 1, 1, "battery"),
        W(0, 3, 1, 1, "keyboard"), W(1, 3, 1, 1, "desktops"), W(2, 3, 1, 1, "night"), W(3, 3, 1, 1, "settings"),
    };

    // ---- the config, the share files ----

    public static void DataChecks()
    {
        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        string plain = System.Text.Json.JsonSerializer.Serialize(new WatchWidget { Type = "cpu" }, opts);
        var f = Folder(0, 0, 2, 2);
        string js = System.Text.Json.JsonSerializer.Serialize(f, opts);
        var back = System.Text.Json.JsonSerializer.Deserialize<WatchWidget>(js, opts);
        bool same = back.Name == "VRChat" && back.Children?.Count == 8 && System.Text.Json.JsonSerializer.Serialize(back, opts) == js;
        Log.Info($"folder config: a widget without a folder writes no Name / Children {!plain.Contains("Name") && !plain.Contains("Children")} (want True), " +
                 $"a folder with 8 widgets reads back the same {same} (want True)");
        var wc = new WristConfig { Face = "custom", Widgets = Face(Folder(0, 0, 2, 2)) };
        var page = FaceShare.Parse(FaceShare.Export(wc, 0), out string err, out int dropped);
        var pf = page?.Widgets.Find(x => x.Type == "folder");
        bool shared = pf != null && pf.Name == "VRChat" && pf.Children?.Count == 8 && pf.Children.Select(c => c.Type).SequenceEqual(f.Children.Select(c => c.Type));
        var code = FaceShare.Parse(FaceShare.ClipboardText(wc, 0), out _, out _)?.Widgets.Find(x => x.Type == "folder");
        // a folder in a folder, an unknown kind in it: left out on import
        var bad = Folder(0, 0, 2, 2);
        bad.Children.Add(W(0, 2, 1, 1, "folder"));
        bad.Children.Add(W(2, 2, 1, 1, "hologram"));
        var wb = new WristConfig { Face = "custom", Widgets = new List<WatchWidget> { bad } };
        var pb = FaceShare.Parse(FaceShare.Export(wb, 0), out _, out _)?.Widgets.Find(x => x.Type == "folder");
        Log.Info($"folder share: exported and imported its name and 8 widgets {shared} (want True){(err != null ? " " + err : "")}, as clipboard text " +
                 $"{code?.Children?.Count == 8} (want True); a folder in it and an unknown kind left out {pb?.Children?.Count == 8} (want True)");
        // Clean: a widget outside the folder's grid or over another dropped, the rest kept
        var cleaned = WatchWidgets.Clean(new List<WatchWidget> { new() { Type = "folder", W = 1, H = 1, Children = new List<WatchWidget> { W(0, 0, 2, 1, "time"), W(1, 0, 1, 1, "fps"), W(0, 3, 1, 1, "cpu") } } });
        Log.Info($"folder clean: its widgets on its 6 x 3 grid, an overlap and one outside it dropped {cleaned[0].Children.Count} (want 1)");
    }

    // ---- each style ----

    static void Tap(WristPanel wrist, PanelData data, Vector2 p)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
        wrist.PointerLeave(Hand.Left); wrist.Draw(data);
    }

    static void Settle(WristPanel wrist, PanelData data)
    {
        for (int k = 0; k < 11; k++) { Thread.Sleep(40); wrist.Draw(data); }
    }

    static Vector2 Mid(System.Drawing.RectangleF c)
    {
        float s = Theme.Classic ? 2 : 1;
        return new Vector2((c.X + c.Width / 2) * s, (c.Y + c.Height / 2) * s);
    }

    public static void Run(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir, string tag)
    {
        var wcfg = cfg.Data.Wrist;
        var keep = (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background);
        var act0 = wrist.OnAction;
        try
        {
            if (tag == "") DataChecks();
            wcfg.Face = "custom";
            wcfg.Background = "panel";
            wcfg.Grid = null;
            wcfg.Pages = null;
            wcfg.Page = 0;
            Checks(wrist, data, wcfg, tag);
            Pictures(wrist, data, wcfg, gpu, dir, tag);
        }
        finally
        {
            wrist.OnAction = act0;
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
            (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background) = keep;
        }
    }

    static void Checks(WristPanel wrist, PanelData data, WristConfig wcfg, string tag)
    {
        bool classic = Theme.Classic;
        var f = Folder(0, 0, 2, 2);
        wcfg.Widgets = Face(f);
        wrist.Settle();
        wrist.Draw(data); wrist.Draw(data);
        Tap(wrist, data, Mid(WatchWidgets.CellRect(0, 0, 2, 2, classic)));
        Settle(wrist, data);
        bool opened = wrist.IsOpen && wrist.OpenWidget == f;
        // a button in it acts: the keyboard (3, 0)
        string acted = null;
        wrist.OnAction = a => acted = a;
        Tap(wrist, data, Mid(WatchWidgets.CellRect(3, 0, 1, 1, classic, GridSpec.Folder)));
        bool button = acted == "keyboard" && wrist.OpenWidget == f;
        // the frame rate in it (2, 0): its view opens over the folder; its X comes back to the folder
        Tap(wrist, data, Mid(WatchWidgets.CellRect(2, 0, 1, 1, classic, GridSpec.Folder)));
        Settle(wrist, data);
        var fps = f.Children.Find(c => c.Type == "fps");
        bool child = wrist.IsOpen && wrist.OpenWidget == fps && wrist.FolderUnderOpen == f;
        var x = wrist.ControlPoint(data, classic ? "wd95x" : "wclose");
        if (x is { } xp) Tap(wrist, data, xp);
        Settle(wrist, data);
        bool back = wrist.IsOpen && wrist.OpenWidget == f && wrist.FolderUnderOpen == null;
        // the folder's own X closes it
        var fx = wrist.ControlPoint(data, classic ? "wf95x" : "wfclose");
        if (fx is { } fxp) Tap(wrist, data, fxp);
        Settle(wrist, data);
        bool shut = !wrist.IsOpen && wrist.OpenWidget == null;
        wrist.OnAction = null;
        Log.Info($"folder{tag}: a tap opened it {opened} (want True), a button in it acted {button} (want True), a widget in it opened its view over the folder " +
                 $"{child} (want True), its X came back to the folder {back} (want True), the folder's X closed it {shut} (want True)");
        // no garbage a frame: the closed folder at rest, then open
        long Bytes()
        {
            wrist.Settle();
            for (int i = 0; i < 4; i++) wrist.Draw(data);
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) wrist.Draw(data);
            return (GC.GetAllocatedBytesForCurrentThread() - a0) / 20;
        }
        wcfg.Widgets = new List<WatchWidget> { Folder(0, 0, 2, 2), Folder(2, 0, 1, 1), Folder(3, 0, 2, 1) };
        long rest = Bytes();
        wrist.FolderOpenForTest(wcfg.Widgets[0]);
        long open = Bytes();
        wrist.CloseForTest();
        Log.Info($"folder{tag}: a frame of closed folders at rest makes {rest} B of garbage (want 0); open {open} B" +
                 (Theme.Classic ? " - its widgets' own windows' frame, as on the page" : " (want 0)"));
    }

    static void Pictures(WristPanel wrist, PanelData data, WristConfig wcfg, Gpu gpu, string dir, string tag)
    {
        void Shot(string name, List<WatchWidget> face, string grid, WatchWidget open)
        {
            wcfg.Grid = grid;
            wcfg.Widgets = face;
            wrist.Settle();
            if (open != null) wrist.FolderOpenForTest(open);
            wrist.Draw(data); wrist.Draw(data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_folder_{name}{tag}.png"));
            wrist.CloseForTest();
        }
        var big = Folder(0, 0, 2, 2);
        var empty = new WatchWidget { Type = "folder", X = 4, Y = 0, W = 1, H = 1 };
        var face = new List<WatchWidget> { big, Folder(2, 0, 1, 1), Folder(3, 0, 1, 1), empty, Folder(2, 1, 2, 1), W(4, 1, 2, 1, "media"),
            W(0, 2, 2, 1, "time"), W(2, 2, 4, 2, "graph"), W(0, 3, 1, 1, "keyboard"), W(1, 3, 1, 1, "night") };
        Shot("closed", face, null, null);
        var f43 = Folder(0, 0, 2, 2);
        Shot("closed43", new List<WatchWidget> { f43, Folder(2, 0, 1, 1), Folder(3, 0, 1, 1), Folder(2, 1, 2, 1), W(0, 2, 4, 1, "media") }, "4x3", null);
        Shot("open", face, null, big);
        Shot("open_empty", face, null, empty);
        // a widget of it opened over it
        wcfg.Grid = null;
        wcfg.Widgets = face;
        wrist.Settle();
        wrist.FolderOpenForTest(big);
        wrist.Draw(data);
        Tap(wrist, data, Mid(WatchWidgets.CellRect(4, 0, 2, 2, Theme.Classic, GridSpec.Folder)));      // the batteries
        Settle(wrist, data);
        wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_folder_child{tag}.png"));
        wrist.CloseForTest();
        Log.Info($"folder{tag}: 5 pictures (wrist_folder_*{tag}.png)");
    }

    // ---- the editor (Modern) ----

    public static void EditorChecks(SettingsView view, Ui ui, Surface surf, PointerState ptr, Painter painter, Gpu gpu, ConfigStore cfg, WristPanel wrist,
        PanelData data, string dir)
    {
        var wcfg = cfg.Data.Wrist;
        var keep = (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, view.Tab, view.EditingWatch, view.PaletteCat);
        try
        {
            cfg.Data.Appearance = new AppearanceConfig { Clock24h = true };
            Theme.Apply(cfg.Data.Appearance);
            wcfg.Face = "custom";
            wcfg.Grid = null;
            wcfg.Pages = null;
            wcfg.Page = 0;
            var f = Folder(0, 0, 2, 2);
            f.Children.RemoveAll(c => c.Y == 2);            // room in it
            wcfg.Widgets = Face(f);
            view.Tab = SettingsView.TabWrist;
            view.EditingWatch = true;
            view.PaletteCat = 0;
            void FrameM() { wrist.Draw(data); ui.Begin(ptr, 1.5f); painter.Begin(surf.Bitmap, Theme.Bg, 1.5f); view.Draw(ui); painter.End(); ui.End(); }
            void ClickM(float x, float y) { ptr.Move(x * 1.5f, y * 1.5f); FrameM(); ptr.Press(); FrameM(); ptr.Release(); FrameM(); }
            Vector2 CellM(int cx, int cy, GridSpec g)
            {
                var pv = SettingsView.EditorPrev;
                var c = WatchWidgets.CellRect(cx, cy, 1, 1, false, g);
                return new(pv.X + (c.X + c.Width / 2) * pv.Width / WristPanel.TW, pv.Y + (c.Y + c.Height / 2) * pv.Height / WristPanel.TH);
            }
            ui.ResetAnimations();
            ptr.Leave(); FrameM();
            var c0 = CellM(0, 0, null);
            ClickM(c0.X, c0.Y);                              // the folder selected
            ClickM(724 + 75, 180 + 17);                      // Open folder
            bool inside = view.InFolder && wrist.EditFolder == f;
            int n0 = f.Children.Count;
            var pal = WatchWidgets.PaletteFor(0, null);
            ClickM(222 + 38, SettingsView.PaletteY + 13);    // the palette's first (Time) widget: into the folder
            bool added = f.Children.Count == n0 + 1 && f.Children.Last().Type == pal[0].Id;
            ptr.Leave(); wrist.Settle(); FrameM(); ui.ResetAnimations(); FrameM();
            surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_folder.png"));
            var moved = f.Children.Last();
            ClickM(222 + 486 - 114, 76 + 16);                // Move out of the folder
            bool outside = !f.Children.Contains(moved) && wcfg.Widgets.Contains(moved);
            ClickM(222 + 48, 76 + 16);                       // Back
            bool backPage = !view.InFolder && wrist.EditFolder == null;
            Log.Info($"folder editor: Open folder works in it {inside} (want True), the palette added into it {added} (want True), Move out put it on the page " +
                     $"{outside} (want True), Back to the page {backPage} (want True)");
            // drag a widget onto the folder: into it
            var kb = wcfg.Widgets.Find(x => x.Type == "keyboard");
            var from = CellM(kb.X, kb.Y, null);
            ptr.Move(from.X * 1.5f, from.Y * 1.5f); FrameM(); ptr.Press(); FrameM();
            ptr.Move(c0.X * 1.5f, c0.Y * 1.5f); FrameM(); ptr.Release(); FrameM();
            Log.Info($"folder editor: a widget dragged onto the folder went into it {f.Children.Contains(kb) && !wcfg.Widgets.Contains(kb)} (want True)");
            ptr.Leave(); FrameM();
            surf.SavePng(gpu, Path.Combine(dir, "settings_wrist_folder_page.png"));
        }
        finally
        {
            ptr.Leave();
            (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, view.Tab, view.EditingWatch, view.PaletteCat) = keep;
            wrist.EditFolder = null;
        }
    }

    // ---- the sheet: every style side by side ----

    static readonly (string tag, string name)[] Styles =
    {
        ("", "Modern"), ("_cyber", "Cyber"), ("_95", "Windows 95"), ("_xp", "Windows XP"), ("_aero", "Windows 7"), ("_mac", "Mac"), ("_term", "Terminal"),
    };

    public static void Sheet(string dir)
    {
        string[] cases = { "closed", "closed43", "open", "child", "open_empty" };
        const int Cw = 400, Ch = 275, Gap = 10, Label = 22, Side = 110;
        using var bmp = new System.Drawing.Bitmap(Side + cases.Length * (Cw + Gap) + Gap, Styles.Length * (Ch + Label + Gap) + Gap + Label);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(255, 38, 40, 46));
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        using var font = new System.Drawing.Font("Segoe UI", 10f);
        using var bold = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold);
        for (int j = 0; j < cases.Length; j++) g.DrawString(cases[j], font, System.Drawing.Brushes.Gainsboro, Side + Gap + j * (Cw + Gap), 4);
        int n = 0;
        for (int i = 0; i < Styles.Length; i++)
        {
            int y = Label + Gap + i * (Ch + Label + Gap);
            g.DrawString(Styles[i].name, bold, System.Drawing.Brushes.White, 8, y + Ch / 2 - 8);
            for (int j = 0; j < cases.Length; j++)
            {
                string f = Path.Combine(dir, $"wrist_folder_{cases[j]}{Styles[i].tag}.png");
                if (!File.Exists(f)) continue;
                using var src = new System.Drawing.Bitmap(f);
                g.DrawImage(src, new System.Drawing.Rectangle(Side + Gap + j * (Cw + Gap), y, Cw, Ch));
                n++;
            }
        }
        bmp.Save(Path.Combine(dir, "folder_preview.png"), System.Drawing.Imaging.ImageFormat.Png);
        Log.Info($"folder: folder_preview.png - {n} pictures of {Styles.Length * cases.Length}");
    }
}

static partial class Tools
{
    // ORBIT_SNAP_ONLY=folder: the folder's checks and pictures in every style, its layout check, the sheet, the editor - in seconds
    static int FolderOnly(WristPanel wrist, PanelData data, ConfigStore cfg, Painter painter, Gpu gpu, string dir)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = SnapCulture;
        foreach (var (style, tag) in new[] { ("modern", ""), ("classic", "_95"), ("xp", "_xp"), ("aero", "_aero"), ("mac", "_mac"), ("terminal", "_term"), (CyberHud.Id, "_cyber") })
        {
            cfg.Data.Appearance = new AppearanceConfig { Style = style, Clock24h = true };
            Theme.Apply(cfg.Data.Appearance);
            FolderViewTools.Run(wrist, data, cfg, gpu, dir, tag);
            string f0 = cfg.Data.Wrist.Face;
            LayoutCheckRun(wrist, data, cfg, painter, gpu, dir, tag.Length > 0 ? tag[1..] : "modern", onlyKind: "folder");
            cfg.Data.Wrist.Face = f0;
            cfg.Data.Wrist.Widgets = null;
        }
        cfg.Data.Appearance = new AppearanceConfig { Clock24h = true };
        Theme.Apply(cfg.Data.Appearance);
        FolderViewTools.Sheet(dir);
        var host = new FakeHost { Cfg = cfg, GpuName = gpu.AdapterName, MonitorList = new List<MonitorInfo>(), Data = data, Wrist = wrist };
        using (var surf = new Surface(gpu, 1500, 960))
            FolderViewTools.EditorChecks(new SettingsView(host), new Ui(painter), surf, new PointerState(), painter, gpu, cfg, wrist, data, dir);
        CultureInfo.CurrentCulture = culture;
        Log.Info("snapshot (folder only) written to " + dir);
        return 0;
    }
}
