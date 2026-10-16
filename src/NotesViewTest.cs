using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;

namespace Orbit;

// --snapshot's part for the notifications widget opened (WristNotes.cs), on made-up notifications only (the names are the snapshot's
// own made-up ones): a tap on the card opens it, the newest first, its X closes it (the retro styles' OK too), the stick scrolls a
// long list down and back, the empty state, Clear empties the list; the pictures wrist_notes_<case><tag>.png - the widget's sizes on
// a face, opened with 8, none, one, long texts, 20 (and 20 scrolled to the end). tag: "" modern, "_95", "_xp", ... "_cyber".
static class NotesViewTools
{
    // the made-up notifications, newest first: where from, the title, the text, how many minutes ago
    static readonly (string src, string title, string text, double ago)[] All =
    {
        ("VRCX", "Friend online", "Sam is in The Black Cat (Friends+)", 0.3),
        ("Discord", "New message", "Alex: are you in VR? The new watch looks great, the cursor is way smoother now.", 3),
        ("Room", "Tobi joined", "The Black Cat", 6),
        ("VRCX", "Friend request", "Friend request from Vega", 11),
        ("Room", "Mira is here", "The Black Cat", 18),
        ("Steam", "Download complete", "VRChat is ready to play", 34),
        ("", "Battery low", "", 52),
        ("Discord", "Pixel Fox", "Voice channel: late night hangout - four people are in it now, come over when you are done", 75),
        ("VRCX", "Friend offline", "Kite", 96),
        ("Room", "Nova left", "The Black Cat", 120),
        ("VRCX", "Invite", "Lumen invited you to Midnight Rooftop", 150),
        ("Discord", "New message", "Quill: see you tomorrow", 185),
        ("Steam", "Friend online", "Sable is online", 220),
        ("VRCX", "Friend online", "Juniper is in Quiet Meadow", 260),
        ("Room", "Static joined", "Quiet Meadow", 300),
        ("Discord", "Mention", "Moss mentioned you in #vr-hangout", 340),
        ("VRCX", "Friend request", "Friend request from Echo", 400),
        ("Steam", "Update ready", "SteamVR has an update", 460),
        ("Room", "Wren is here", "Quiet Meadow", 520),
        ("VRCX", "Friend online", "Basil is in a private world", 600),
    };

    // the hard case: long words everywhere, a long app name, the title alone, the text alone
    public static readonly (string src, string title, string text, double ago)[] Long =
    {
        ("VRChat Notifications Bridge Plus", "A very long notification title that goes on and on past the edge of the card",
            "And its text is long too: it goes on for more than one line, so it must be cut somewhere on the second one at the latest", 1),
        ("Discord", "", "Only a text, no title: it takes both lines of the card and is cut at the end of the second one if it is still too long", 4),
        ("VRCX", "Only a title, no text: it takes both lines too when it is this long and does not fit on one", "", 9),
        ("Room", "Orbit Fan 2000 With A Very Long Name Too joined", "The Black Cat – Midnight Rooftop Lounge (Extended Directors Cut)", 26),
        ("Steam", "Download complete", "VRChat is ready to play", 70),
        ("", "No app said where it came from", "Its text", 240),
        ("VRCX", "Friend online", "Sam is in The Black Cat (Friends+)", 1500),
        ("Discord", "New message", "Alex: see you", 3000),
    };

    public static NotificationHistory Fake(int n = 8) => Fake(All[..Math.Min(n, All.Length)]);

    public static NotificationHistory Fake((string src, string title, string text, double ago)[] list)
    {
        var h = new NotificationHistory();
        var now = DateTime.Now;
        for (int i = list.Length - 1; i >= 0; i--)          // the oldest first: each new one goes on top
            h.Add(list[i].src, list[i].title, list[i].text, now.AddMinutes(-list[i].ago));
        return h;
    }

    static void Draw2(WristPanel wrist, PanelData data) { wrist.Draw(data); wrist.Draw(data); }

    static void Tap(WristPanel wrist, PanelData data, Vector2 p)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
        wrist.PointerLeave(Hand.Left); wrist.Draw(data);
    }

    // frames over 0.45 s: the zoom (spring / the style's window zoom) comes to rest
    static void Settle(WristPanel wrist, PanelData data)
    {
        for (int k = 0; k < 11; k++) { Thread.Sleep(40); wrist.Draw(data); }
    }

    static Vector2 Middle(System.Drawing.RectangleF c)
    {
        float s = Theme.Classic ? 2 : 1;          // the 95 canvas is shown 2x
        return new Vector2((c.X + c.Width / 2) * s, (c.Y + c.Height / 2) * s);
    }

    public static void Run(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir, string tag)
    {
        var wcfg = cfg.Data.Wrist;
        var keep = (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background);
        var notes0 = data.Notes;
        try
        {
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
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
            data.Notes = notes0;
            (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background) = keep;
        }
    }

    static void Checks(WristPanel wrist, PanelData data, WristConfig wcfg, string tag)
    {
        bool classic = Theme.Classic;
        string xId = classic ? "wd95x" : "wclose", clearId = classic ? "wn95clr" : "wnclr";
        data.Notes = Fake(8);
        wcfg.Widgets = new List<WatchWidget> { new() { Type = "notifications", X = 0, Y = 2, W = 2, H = 1 } };
        wrist.Settle();
        Draw2(wrist, data);
        // a tap on the card opens it, the newest first; its X shrinks it back and closes it
        Tap(wrist, data, Middle(WatchWidgets.CellRect(0, 2, 2, 1, classic)));
        Settle(wrist, data);
        bool opened = wrist.IsOpen && wrist.DetailKind == "notifications";
        bool newest = wrist.NotesFirstShown == "Friend online";
        var x = wrist.ControlPoint(data, xId);
        if (x is { } xp) Tap(wrist, data, xp);
        bool shut = !wrist.IsOpen;
        Settle(wrist, data);
        bool closedX = x != null && shut && !wrist.DetailShown;
        string ok = "";
        if (classic)
        {
            // the window's OK closes it too
            wrist.Settle();
            wrist.OpenForTest("notifications");
            Draw2(wrist, data);
            var o = wrist.ControlPoint(data, "wd95ok");
            if (o is { } op) Tap(wrist, data, op);
            bool okShut = o != null && !wrist.IsOpen;
            Settle(wrist, data);
            ok = $"; its OK closed it {okShut && !wrist.DetailShown} (want True)";
        }
        wrist.CloseForTest();
        Log.Info($"notes view{tag}: a tap on the card opened it {opened} (want True), the newest first {newest} (want True); its X closed it {closedX} (want True){ok}");
        // 20: the stick down over the list scrolls it, up again back to its top
        data.Notes = Fake(20);
        wrist.Settle();
        wrist.OpenForTest("notifications");
        Draw2(wrist, data);
        var mid = Middle(WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, classic));
        wrist.PointerMove(Hand.Left, mid);
        wrist.Draw(data);
        for (int k = 0; k < 6; k++) { wrist.PointerScroll(Hand.Left, -1.5f, mid); wrist.Draw(data); }
        float down = wrist.NotesScroll;
        for (int k = 0; k < 6; k++) { wrist.PointerScroll(Hand.Left, 6, mid); wrist.Draw(data); }
        float back = wrist.NotesScroll;
        wrist.PointerLeave(Hand.Left);
        wrist.CloseForTest();
        // none: the empty state
        data.Notes = new NotificationHistory();
        wrist.Settle();
        wrist.OpenForTest("notifications");
        Draw2(wrist, data);
        bool empty = wrist.NotesEmptyShown && wrist.NotesFirstShown == null;
        wrist.CloseForTest();
        // Clear: Orbit's own list emptied, the view says so (and stays open)
        data.Notes = Fake(8);
        wrist.Settle();
        wrist.OpenForTest("notifications");
        Draw2(wrist, data);
        var c = wrist.ControlPoint(data, clearId);
        if (c is { } cp) Tap(wrist, data, cp);
        bool cleared = c != null && data.Notes.Items.Count == 0;
        Draw2(wrist, data);
        bool says = wrist.NotesEmptyShown && wrist.IsOpen;
        wrist.CloseForTest();
        Log.Info($"notes view{tag}: 20 of them - the stick scrolled the list down {down > 0} (want True) and back to its top {back == 0} (want True); none - " +
                 $"the empty state shows {empty} (want True); Clear emptied the list {cleared} (want True) and the view says so {says} (want True)");
    }

    static void Pictures(WristPanel wrist, PanelData data, WristConfig wcfg, Gpu gpu, string dir, string tag)
    {
        // a face around it (dimmed behind the opened view): the widget's three sizes
        List<WatchWidget> Face() => new()
        {
            new() { Type = "time", X = 0, Y = 0, W = 4, H = 2 }, new() { Type = "notifications", X = 4, Y = 0, W = 2, H = 2 },
            new() { Type = "notifications", X = 0, Y = 2, W = 3, H = 1 }, new() { Type = "notifications", X = 3, Y = 2, W = 2, H = 1 },
            new() { Type = "battery", X = 5, Y = 2, W = 1, H = 1 },
            new() { Type = "keyboard", X = 0, Y = 3 }, new() { Type = "desktops", X = 1, Y = 3 }, new() { Type = "recenter", X = 2, Y = 3 },
            new() { Type = "layout", X = 3, Y = 3 }, new() { Type = "night", X = 4, Y = 3 }, new() { Type = "settings", X = 5, Y = 3 },
        };
        void Shot(string name, NotificationHistory h, bool open, float scroll = 0)
        {
            data.Notes = h;
            wcfg.Widgets = Face();
            wrist.Settle();
            if (open && !wrist.OpenForTest("notifications")) Log.Warn("notes view: no notifications widget to open");
            Draw2(wrist, data);
            if (scroll != 0)
            {
                // the stick down to the end
                var mid = Middle(WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, Theme.Classic));
                wrist.PointerMove(Hand.Left, mid);
                wrist.Draw(data);
                for (int k = 0; k < 12; k++) { wrist.PointerScroll(Hand.Left, scroll / 12, mid); wrist.Draw(data); }
                wrist.PointerLeave(Hand.Left);
                Draw2(wrist, data);
            }
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_notes_{name}{tag}.png"));
            wrist.CloseForTest();
        }
        Shot("face", Fake(8), false);
        Shot("open", Fake(8), true);
        Shot("none_open", new NotificationHistory(), true);
        Shot("one_open", Fake(1), true);
        Shot("long_open", Fake(Long), true);
        Shot("twenty_open", Fake(20), true);
        Shot("twenty_end", Fake(20), true, -90);
        Log.Info($"notes view{tag}: 7 pictures (wrist_notes_*{tag}.png)");
    }
}

static partial class Tools
{
    // ORBIT_SNAP_ONLY=notes: the opened notifications' checks, pictures and layout check (its sizes and opened only) in every style,
    // and the Cyber opened views (CyberDetailTools), in seconds
    static int NotesOnly(WristPanel wrist, PanelData data, ConfigStore cfg, Painter painter, Gpu gpu, string dir)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = Tools.SnapCulture;
        foreach (var (style, tag) in new[] { ("modern", ""), ("classic", "_95"), ("xp", "_xp"), ("aero", "_aero"), ("mac", "_mac"), ("terminal", "_term"), (CyberHud.Id, "_cyber") })
        {
            cfg.Data.Appearance = new AppearanceConfig { Style = style, Clock24h = true };
            Theme.Apply(cfg.Data.Appearance);
            NotesViewTools.Run(wrist, data, cfg, gpu, dir, tag);
            string f0 = cfg.Data.Wrist.Face;
            LayoutCheckRun(wrist, data, cfg, painter, gpu, dir, tag.Length > 0 ? tag[1..] : "modern", onlyKind: "notifications");
            if (style == CyberHud.Id)
            {
                CyberDetailTools.Run(wrist, data, cfg, gpu, dir);
                foreach (var kind in new[] { "time", "date", "analog", "session", "media", "fps", "cpu", "volume" })
                    LayoutCheckRun(wrist, data, cfg, painter, gpu, dir, "cyber", onlyKind: kind);
            }
            cfg.Data.Wrist.Face = f0;
            cfg.Data.Wrist.Widgets = null;
        }
        System.Globalization.CultureInfo.CurrentCulture = culture;
        Log.Info("snapshot (notes only) written to " + dir);
        return 0;
    }
}
