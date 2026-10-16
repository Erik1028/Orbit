using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Orbit;

// --snapshot's pictures of the Cyber style's own opened views (WristCyberDetail.cs, the notifications' plates in WristNotes.cs), at a
// fixed moment (the same digits every run): the clock opened from each of its widgets - with the time in VR and without, a timer
// running and one that is up, the 12-hour clock - wrist_cyber_open_<case>.png; and two frames with the effects live (as in the
// headset): the time card's minute glitch, a new notification scanning in.
static class CyberDetailTools
{
    static void Draw2(WristPanel wrist, PanelData data) { wrist.Draw(data); wrist.Draw(data); }

    public static void Run(WristPanel wrist, PanelData data, ConfigStore cfg, Gpu gpu, string dir)
    {
        var wcfg = cfg.Data.Wrist;
        var keep = (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background);
        var since0 = data.VrSince;
        var notes0 = data.Notes;
        bool h24 = cfg.Data.Appearance.Clock24h, still = Motion.Still;
        var at = new DateTime(2026, 10, 16, 18, 21, 49);
        try
        {
            wcfg.Face = "custom";
            wcfg.Background = "panel";
            wcfg.Grid = null;
            wcfg.Pages = null;
            wcfg.Page = 0;
            wcfg.Widgets = new List<WatchWidget>
            {
                new() { Type = "time", X = 0, Y = 0, W = 4, H = 2 }, new() { Type = "analog", X = 4, Y = 0, W = 2, H = 2 },
                new() { Type = "date", X = 0, Y = 2, W = 3, H = 1 }, new() { Type = "session", X = 3, Y = 2, W = 1, H = 1 },
                new() { Type = "notifications", X = 4, Y = 2, W = 2, H = 1 },
                new() { Type = "keyboard", X = 0, Y = 3 }, new() { Type = "desktops", X = 1, Y = 3 }, new() { Type = "recenter", X = 2, Y = 3 },
                new() { Type = "layout", X = 3, Y = 3 }, new() { Type = "night", X = 4, Y = 3 }, new() { Type = "settings", X = 5, Y = 3 },
            };
            wrist.TestNow = at;
            void Shot(string name, string type, Action setup = null)
            {
                data.VrSince = at.AddMinutes(-83).AddSeconds(-7);
                setup?.Invoke();
                wrist.Settle();
                if (!wrist.OpenForTest(type)) Log.Warn("cyber detail: no widget " + type);
                Draw2(wrist, data);
                wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_cyber_open_{name}.png"));
                wrist.CloseForTest();
                wrist.TimerForTest(0);
                cfg.Data.Appearance.Clock24h = h24;
            }
            Shot("clock", "time");
            Shot("clock_analog", "analog");
            Shot("clock_date", "date");
            Shot("clock_novr", "time", () => data.VrSince = null);
            Shot("clock_timer", "session", () => wrist.TimerForTest(3));
            Shot("clock_12h", "time", () => { cfg.Data.Appearance.Clock24h = false; wrist.TimerForTest(10); });
            // live: the minute's glitch in its third step, a new notification half scanned in
            CyberFx.Live = wrist.CyberLive = true;
            Motion.Still = false;
            data.VrSince = at.AddMinutes(-83);
            wrist.Settle();
            wrist.OpenForTest("time");
            Draw2(wrist, data);
            wrist.CyberTest("glitch");
            Thread.Sleep(80);
            wrist.Draw(data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_cyber_open_clock_glitch.png"));
            wrist.CloseForTest();
            wrist.TestNow = null;
            data.Notes = NotesViewTools.Fake(7);
            wrist.Settle();
            wrist.OpenForTest("notifications");
            Draw2(wrist, data);
            data.Notes.Add("VRCX", "Friend online", "Kite is in The Black Cat", DateTime.Now);
            wrist.Draw(data);
            Thread.Sleep(200);
            wrist.Draw(data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, "wrist_cyber_open_notes_scan.png"));
            for (int k = 0; k < 8; k++) { Thread.Sleep(40); wrist.Draw(data); }
            bool rest = !wrist.Animating;
            wrist.CloseForTest();
            Log.Info($"cyber detail: 8 pictures (wrist_cyber_open_*.png); a new notification scanned in and came to rest {rest} (want True)");
        }
        finally
        {
            CyberFx.Live = wrist.CyberLive = false;
            Motion.Still = still;
            wrist.TestNow = null;
            wrist.TimerForTest(0);
            wrist.CloseForTest();
            cfg.Data.Appearance.Clock24h = h24;
            data.VrSince = since0;
            data.Notes = notes0;
            (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background) = keep;
            wrist.Settle();
        }
    }
}
