using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;

namespace Orbit;

// --snapshot's part for the room widget opened on the watch (WristRoom.cs): a tap on the room card (every size) opens the watch's
// own opened view - not the floating Room panel -, the stick scrolls its list, its X shrinks it back and closes it; and the
// pictures wrist_open_room_<case><tag>.png: nobody here, 3 people, 30 with friends, starred and who left (also scrolled down), a
// long world name with long names, no VRChat log, and the room not followed. tag: "" modern, "_95" the 95 style.
static class RoomViewTools
{
    public static void Run(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir, string tag)
    {
        var wcfg = cfg.Data.Wrist;
        string face0 = wcfg.Face, bg0 = wcfg.Background;
        var widgets0 = wcfg.Widgets;
        var room0 = data.Room;
        var stars0 = new List<string>(cfg.Data.Room.Starred);
        var act0 = wrist.OnAction;
        try
        {
            wcfg.Face = "custom";
            wcfg.Background = "panel";
            Taps(wrist, data, wcfg, tag);
            Pictures(wrist, data, cfg, gpu, dir, tag);
        }
        finally
        {
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
            wrist.OnAction = act0;
            data.Room = room0;
            cfg.Data.Room.Starred = stars0;
            wcfg.Face = face0;
            wcfg.Background = bg0;
            wcfg.Widgets = widgets0;
        }
    }

    static readonly string[] Names =
    {
        "Nova", "Mira", "Pixel Fox", "Tobi", "Moss", "Juniper", "Static", "Kite", "Orbit Fan 2000", "Lumen", "Quill", "Sable", "Wren", "Echo",
        "Basil", "Cinder", "Dune", "Fable", "Gale", "Haze", "Indigo", "Jolt", "Koi", "Lark", "Marrow", "Nyx", "Opal", "Pike", "Rune", "Tansy",
    };

    // a made-up room: n here (every fifth a friend, from the second), 'gone' who left lately, there since 'since' minutes
    static RoomWatcher.Snapshot Room(int n, int gone = 0, string world = "The Black Cat", double since = 47)
    {
        var s = new RoomWatcher.Snapshot { Version = 100 + n, LogFound = true, InRoom = true, World = world, VrcxFound = true, Friends = 120,
            Since = DateTime.Now.AddMinutes(-since) };
        for (int i = 0; i < n; i++)
            s.Here.Add(new RoomWatcher.Person { Id = $"usr_{i}", Name = Names[i % Names.Length], Joined = DateTime.Now.AddMinutes(-1.5 * i - 0.5), Friend = i % 5 == 1 });
        for (int i = 0; i < gone; i++)
            s.Gone.Add(new RoomWatcher.Person { Id = $"usr_g{i}", Name = new[] { "Drifter", "Comet", "Ash" }[i % 3], Joined = DateTime.Now.AddMinutes(-60),
                Left = DateTime.Now.AddMinutes(-2 - 4 * i), Friend = i == 1 });
        return s;
    }

    static void Draw2(WristPanel wrist, PanelData data) { wrist.Draw(data); wrist.Draw(data); }

    static void Tap(WristPanel wrist, PanelData data, Vector2 p)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
    }

    // frames over 0.45 s: the zoom (spring / 95 outline) comes to rest
    static void Settle(WristPanel wrist, PanelData data)
    {
        for (int k = 0; k < 11; k++) { Thread.Sleep(40); wrist.Draw(data); }
    }

    static Vector2 Middle(System.Drawing.RectangleF c)
    {
        float s = Theme.Classic ? 2 : 1;          // the 95 canvas is shown 2x
        return new Vector2((c.X + c.Width / 2) * s, (c.Y + c.Height / 2) * s);
    }

    static void Taps(WristPanel wrist, PanelData data, WristConfig wcfg, string tag)
    {
        string action = null;
        wrist.OnAction = a => action ??= a;
        data.Room = Room(30, 3);
        // a tap on the room card of every size opens the watch's opened view, no action goes out (the Room panel stays shut)
        var miss = new List<string>();
        int n = 0;
        foreach (var (w, h) in WatchWidgets.Find("room").SizesFor(Theme.Classic))
        {
            n++;
            wcfg.Widgets = new List<WatchWidget> { new() { Type = "room", X = 3, Y = 2, W = w, H = h } };
            wrist.Settle();
            Draw2(wrist, data);
            action = null;
            Tap(wrist, data, Middle(WatchWidgets.CellRect(3, 2, w, h, Theme.Classic)));
            if (!(wrist.IsOpen && wrist.DetailKind == "room" && action == null)) miss.Add($"{w}x{h}{(action != null ? " sent " + action : "")}");
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
        }
        // the 3 x 1 opened for good: the stick scrolls the list down and back up, the X shrinks it into its tile and closes it
        wcfg.Widgets = new List<WatchWidget> { new() { Type = "room", X = 3, Y = 2, W = 3, H = 1 } };
        wrist.Settle();
        Draw2(wrist, data);
        Tap(wrist, data, Middle(WatchWidgets.CellRect(3, 2, 3, 1, Theme.Classic)));
        Settle(wrist, data);
        var mid = Middle(WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, Theme.Classic));
        wrist.PointerMove(Hand.Left, mid);
        wrist.Draw(data);
        wrist.PointerScroll(Hand.Left, -2, mid);       // the stick pushed down
        wrist.Draw(data);
        float down = wrist.RoomScroll;
        wrist.PointerScroll(Hand.Left, 30, mid);
        wrist.Draw(data);
        float back = wrist.RoomScroll;
        Tap(wrist, data, wrist.DetailClosePoint);
        bool shrinking = !wrist.IsOpen && wrist.DetailShown;
        Settle(wrist, data);
        bool closed = !wrist.DetailShown && action == null;
        wrist.PointerLeave(Hand.Left);
        Log.Info($"room view{tag}: a tap on the room card opened the watch's view, not the Room panel, sizes: {n - miss.Count} of {n} (want {n} of {n})" +
                 (miss.Count > 0 ? ", not: " + string.Join(", ", miss) : ""));
        string scroll = Theme.Classic ? "" : $"the stick scrolled the list down {down > 0 && down <= wrist.RoomScrollMax} (want True) and back up {back == 0} (want True); ";
        Log.Info($"room view{tag}: {scroll}its X shrank it back {shrinking} (want True) and closed it {closed} (want True)");
    }

    static void Pictures(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir, string tag)
    {
        var wcfg = cfg.Data.Wrist;
        // a face around it (dimmed behind the opened card): the room as the 3 x 1 the 4 x 3-style design draws
        wcfg.Widgets = new List<WatchWidget>
        {
            new() { Type = "time", X = 0, Y = 0, W = 4, H = 2 }, new() { Type = "battery", X = 4, Y = 0, W = 2, H = 2 },
            new() { Type = "media", X = 0, Y = 2, W = 3, H = 1 }, new() { Type = "room", X = 3, Y = 2, W = 3, H = 1 },
            new() { Type = "keyboard", X = 0, Y = 3, W = 2, H = 1 }, new() { Type = "desktops", X = 2, Y = 3, W = 2, H = 1 },
            new() { Type = "night", X = 4, Y = 3, W = 1, H = 1 }, new() { Type = "settings", X = 5, Y = 3, W = 1, H = 1 },
        };
        cfg.Data.Room.Starred = new List<string> { "usr_7", "usr_12" };
        string longWorld = "The Black Cat – Midnight Rooftop Lounge (Extended Directors Cut) by Somebody With A Long Name";
        var lng = Room(5, 1, longWorld, 135);
        lng.Here[0].Name = "Captain Extraordinarily Long Display Name";
        lng.Here[1].Name = "Orbit Fan 2000 With A Very Long Name Too";
        var cases = new (string name, RoomWatcher.Snapshot s, float scroll)[]
        {
            ("none", Room(0, 0, "Quiet Meadow", 0.3), 0),
            ("3", Room(3, 0, "Midnight Rooftop", 25), 0),
            ("30", Room(30, 3), 0),
            ("30_scrolled", Room(30, 3), -60),
            ("longname", lng, 0),
            ("nolog", new RoomWatcher.Snapshot { Version = 3 }, 0),
            ("notworld", new RoomWatcher.Snapshot { Version = 4, LogFound = true, VrcxFound = false }, 0),
            ("off", null, 0),
        };
        foreach (var (name, s, scroll) in cases)
        {
            data.Room = s;
            wrist.Settle();
            if (!wrist.OpenForTest("room")) { Log.Warn("room view: no room widget"); return; }
            Draw2(wrist, data);
            if (scroll != 0)
            {
                // the stick down to the end (95: the list scrolls under the laser)
                var mid = Middle(WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, Theme.Classic));
                wrist.PointerMove(Hand.Left, mid);
                wrist.Draw(data);
                for (int k = 0; k < 12; k++) { wrist.PointerScroll(Hand.Left, scroll / 12, mid); wrist.Draw(data); }
                wrist.PointerLeave(Hand.Left);
                wrist.Draw(data);
            }
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_open_room_{name}{tag}.png"));
            wrist.CloseForTest();
        }
        Log.Info($"room view{tag}: {cases.Length} pictures (wrist_open_room_*{tag}.png)");
    }
}
