using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Orbit;

// A made-up output device: apps with their levels, a master, what was written counted. The tools' mixer and the watch's until the app
// hands it the real one - nothing real is ever touched through it.
public sealed class FakeMixerDevice : IMixerDevice
{
    public string Name { get; set; } = "Speakers (Virtual Desktop Audio)";
    public bool Present { get; set; } = true;
    public bool HardwareVolume { get; set; } = true;
    public float MasterLevel = 0.62f;
    public bool MasterMute;
    public int MasterWrites, SessionWrites;
    bool _notified;
    public List<MixerSession> Sessions { get; } = new();
    readonly Dictionary<MixerSession, MixerIdentity> _ids = new();

    public bool Follow() => false;
    public bool ReadMaster(out float level, out bool mute) { level = Present ? MasterLevel : -1; mute = MasterMute; return Present; }
    public bool WriteMaster(float level, bool mute) { MasterLevel = Math.Clamp(level, 0, 1); MasterMute = mute; MasterWrites++; return Present; }
    public bool TakeNotified() { bool n = _notified; _notified = false; return n; }
    public void Enumerate() { }
    public void Read(bool peaks) { }
    public bool Write(MixerSession s, float level, bool mute) { s.Level = Math.Clamp(level, 0, 1); s.Muted = mute; SessionWrites++; return true; }
    public MixerIdentity Identify(MixerSession s) => _ids.TryGetValue(s, out var i) ? i : new MixerIdentity(s.Id, s.Id, null, null);
    public void Dispose() { }

    // someone else moved the master (Windows' slider, the volume keys, Hexpad's knob): its callback tells
    public void Outside(float level, bool mute) { MasterLevel = level; MasterMute = mute; _notified = true; }
    public void Notify() => _notified = true;

    public MixerSession Add(string name, float level, bool active, float peak, bool muted = false, bool system = false, uint color = 0, string mark = null)
    {
        var s = new MixerSession { Id = "fake:" + name + ":" + Sessions.Count, Pid = 100 + Sessions.Count, Level = level, Muted = muted, Active = active, Peak = peak, System = system };
        Sessions.Add(s);
        uint[] big = null, small = null;
        if (color != 0) (big, small) = (MixerFake.Icon(32, color, mark ?? name[..1]), MixerFake.Icon(16, color, mark ?? name[..1]));
        _ids[s] = new MixerIdentity(system ? "sys" : name.ToLowerInvariant(), name, big, small);
        return s;
    }

    public MixerSession Find(string name) => Sessions.Find(s => _ids.TryGetValue(s, out var i) && i.Name == name);
}

// The made-up apps (the watch's until the app hands it the real mixer; the snapshot's pictures and checks)
public static class MixerFake
{
    public static AudioMixer Standard() => new(StandardDevice(), true);

    // added out of order on purpose: the list must sort them - VRChat, Spotify, Discord (playing, the loudest first), then SteamVR and
    // Chrome as they came, the system sounds last
    public static FakeMixerDevice StandardDevice()
    {
        var d = new FakeMixerDevice();
        d.Add("Discord", 0.65f, true, 0.35f, color: 0xFF5865F2);
        d.Add("SteamVR", 1f, false, 0, color: 0xFF1B2838, mark: "S");
        d.Add("Google Chrome", 0.3f, false, 0, muted: true, color: 0xFFDB4437, mark: "G");
        d.Add("Spotify", 0.45f, true, 0.48f, color: 0xFF1DB954);
        d.Add("System sounds", 1f, false, 0, system: true);
        d.Add("VRChat", 0.8f, true, 0.62f, color: 0xFF2B2F3A);
        return d;
    }

    public static AudioMixer With(Action<FakeMixerDevice> fill)
    {
        var d = new FakeMixerDevice();
        fill(d);
        return new AudioMixer(d, true);
    }

    // a made-up app icon: a rounded tile in its colour with a white letter (premultiplied BGRA, n x n)
    public static uint[] Icon(int n, uint color, string mark)
    {
        using var bmp = new System.Drawing.Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(System.Drawing.Color.Transparent);
            float r = n * 0.22f;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 0, 2 * r, 2 * r, 180, 90);
            path.AddArc(n - 1 - 2 * r, 0, 2 * r, 2 * r, 270, 90);
            path.AddArc(n - 1 - 2 * r, n - 1 - 2 * r, 2 * r, 2 * r, 0, 90);
            path.AddArc(0, n - 1 - 2 * r, 2 * r, 2 * r, 90, 90);
            path.CloseFigure();
            using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(unchecked((int)color)));
            g.FillPath(fill, path);
            using var font = new System.Drawing.Font("Segoe UI", n * 0.5f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            using var ink = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var fmt = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
            g.DrawString(mark, font, ink, new System.Drawing.RectangleF(0, 0, n, n + 1), fmt);
        }
        var px = new uint[n * n];
        var bd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, n, n), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try { System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, (int[])(object)px, 0, px.Length); }
        finally { bmp.UnlockBits(bd); }
        return px;
    }
}

// --snapshot's part for the volume mixer (WristMixer.cs, AudioMixer.cs), on made-up apps only: the list's order, a drag along a slider,
// a tap on an icon (mute), the stick over a slider, a tap that opens it, the opened view's slider and its scrolling; the pictures
// wrist_mixer_<case><tag>.png (a 6 x 4 face with the three sizes, a 4 x 3 page with M and L, opened; none, one, ten apps). And once
// (MasterChecks) the master sync with a made-up device: an outside change applied to the apps once, the watch's own change not again,
// a new app, an app moved by itself, mute, which devices.
static class MixerViewTools
{
    static void Draw2(WristPanel wrist, PanelData data) { wrist.Draw(data); wrist.Draw(data); }

    static void Tap(WristPanel wrist, PanelData data, Vector2 p)
    {
        wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, p); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, p); wrist.Draw(data);
        wrist.PointerLeave(Hand.Left); wrist.Draw(data);
    }

    static void Drag(WristPanel wrist, PanelData data, Vector2 from, Vector2 to)
    {
        wrist.PointerMove(Hand.Left, from); wrist.Draw(data);
        wrist.PointerButton(Hand.Left, PtrButton.Primary, true, from); wrist.Draw(data);
        for (int k = 1; k <= 4; k++) { wrist.PointerMove(Hand.Left, Vector2.Lerp(from, to, k / 4f)); wrist.Draw(data); }
        wrist.PointerButton(Hand.Left, PtrButton.Primary, false, to); wrist.Draw(data);
        wrist.PointerLeave(Hand.Left); wrist.Draw(data);
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
        var mixer0 = wrist.Mixer;
        try
        {
            wcfg.Face = "custom";
            wcfg.Background = "panel";
            wcfg.Grid = null;
            wcfg.Pages = null;
            wcfg.Page = 0;
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;   // the numbers in the log
            try { Checks(wrist, data, wcfg, tag); }
            finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
            Pictures(wrist, data, wcfg, gpu, dir, tag);
        }
        finally
        {
            wrist.CloseForTest();
            wrist.PointerLeave(Hand.Left);
            wrist.UseMixer(mixer0);
            (wcfg.Face, wcfg.Widgets, wcfg.Grid, wcfg.Pages, wcfg.Page, wcfg.Background) = keep;
        }
    }

    static string Order(WristPanel wrist) => string.Join(", ", wrist.MixerShown.Select(a => a.Name));

    static void Checks(WristPanel wrist, PanelData data, WristConfig wcfg, string tag)
    {
        var dev = MixerFake.StandardDevice();
        wrist.UseMixer(new AudioMixer(dev, true));
        wcfg.Widgets = new List<WatchWidget> { new() { Type = "mixer", X = 0, Y = 0, W = 2, H = 2 } };
        wrist.Settle();
        Draw2(wrist, data);
        string order = Order(wrist);
        const string want = "VRChat, Spotify, Discord, SteamVR, Google Chrome, System sounds";
        // a drag along Spotify's slider (shown in the 2 x 2 in every style) to a quarter
        bool hasS = wrist.MixerHit("Spotify", out var s0, out var s1, out _);
        if (hasS) Drag(wrist, data, Vector2.Lerp(s0, s1, 0.6f), Vector2.Lerp(s0, s1, 0.25f));
        float spot = dev.Find("Spotify").Level;
        // a tap on Discord's mute (its icon), then again
        bool hasD = wrist.MixerHit("Discord", out _, out _, out var dm);
        if (hasD) Tap(wrist, data, dm);
        bool muted = dev.Find("Discord").Muted;
        if (hasD) Tap(wrist, data, dm);
        bool unmuted = !dev.Find("Discord").Muted;
        // the stick pushed up over VRChat's slider
        float vr0 = dev.Find("VRChat").Level;
        if (wrist.MixerHit("VRChat", out var v0, out var v1, out _))
        {
            var p = Vector2.Lerp(v0, v1, 0.5f);
            wrist.PointerMove(Hand.Left, p); wrist.Draw(data);
            for (int k = 0; k < 3; k++) { wrist.PointerScroll(Hand.Left, 1, p); wrist.Draw(data); }
            wrist.PointerLeave(Hand.Left); wrist.Draw(data);
        }
        float vr1 = dev.Find("VRChat").Level;
        // a tap on the card away from its controls (the caption row; 95: its title bar) opens it
        var cell = WatchWidgets.CellRect(0, 0, 2, 2, Theme.Classic);
        float sc = Theme.Classic ? 2 : 1;
        Tap(wrist, data, new Vector2((cell.X + cell.Width * 0.5f) * sc, (cell.Y + (Theme.Classic ? 8 : 16)) * sc));
        bool opened = wrist.IsOpen && wrist.DetailKind == "mixer";
        wrist.CloseForTest();
        Log.Info($"mixer{tag}: the made-up apps loudest first: {order} (want {want}); a real device opened {!wrist.Mixer.Fake} (want False)");
        Log.Info($"mixer{tag}: a drag along Spotify's slider set it to {spot:0.00} (want 0.25 +-0.03) {MathF.Abs(spot - 0.25f) <= 0.03f} (want True); a tap on " +
                 $"Discord's icon muted it {muted} (want True), again unmuted it {unmuted} (want True); the stick over VRChat's slider " +
                 $"{vr0:0.00} -> {vr1:0.00}, higher {vr1 > vr0} (want True); a tap beside the controls opened it {opened} (want True)");
        // opened: a drag on a slider there, the mute, and with ten apps the stick off the sliders scrolls
        wrist.Settle();
        wrist.OpenForTest("mixer");
        Draw2(wrist, data);
        bool hasO = wrist.MixerHit("Discord", out var o0, out var o1, out var om);
        if (hasO) Drag(wrist, data, Vector2.Lerp(o0, o1, 0.3f), Vector2.Lerp(o0, o1, 0.7f));
        float disc = dev.Find("Discord").Level;
        if (hasO) Tap(wrist, data, om);
        bool omuted = dev.Find("Discord").Muted;
        wrist.CloseForTest();
        var ten = MixerFake.With(d => Ten(d));
        wrist.UseMixer(ten);
        wrist.Settle();
        wrist.OpenForTest("mixer");
        Draw2(wrist, data);
        var all = WatchWidgets.CellRect(0, 0, WatchWidgets.Cols, WatchWidgets.Rows, Theme.Classic);
        // off the sliders: modern - the names column's left edge (the icons); 95 - a column's name
        var off = Theme.Classic ? new Vector2((all.X + 100) * 2, (all.Y + 50) * 2) : new Vector2(all.X + 40, all.Y + 200);
        wrist.PointerMove(Hand.Left, off); wrist.Draw(data);
        for (int k = 0; k < 2; k++) { wrist.PointerScroll(Hand.Left, -1, off); wrist.Draw(data); }
        int top = wrist.MixerTop;
        wrist.PointerLeave(Hand.Left);
        wrist.CloseForTest();
        Log.Info($"mixer{tag}: opened, a drag on Discord's slider set it to {disc:0.00} (want 0.70 +-0.03) {MathF.Abs(disc - 0.7f) <= 0.03f} (want True), its Mute " +
                 $"{omuted} (want True); ten apps: the stick off the sliders scrolled to {top} (want more than 0) {top > 0} (want True)");
    }

    static void Ten(FakeMixerDevice d)
    {
        string[] names = { "VRChat", "Discord", "Spotify", "Microsoft Flight Simulator 2024 (Steam Edition)", "Google Chrome", "Firefox", "SteamVR",
            "Virtual Desktop Streamer", "OBS Studio", "Steam" };
        uint[] cols = { 0xFF2B2F3A, 0xFF5865F2, 0xFF1DB954, 0xFF0B5394, 0xFFDB4437, 0xFFFF7139, 0xFF1B2838, 0xFF2E7DD7, 0xFF302E31, 0xFF171A21 };
        for (int i = 0; i < names.Length; i++) d.Add(names[i], 1 - i * 0.07f, i < 4, 0.9f - i * 0.1f, muted: i == 5, color: cols[i]);
        d.Add("System sounds", 1, false, 0, system: true);
    }

    static void Pictures(WristPanel wrist, PanelData data, WristConfig wcfg, Gpu gpu, string dir, string tag)
    {
        void Shot(string name, AudioMixer m, List<WatchWidget> face, string grid, bool open)
        {
            wrist.UseMixer(m);
            wcfg.Widgets = face;
            wcfg.Grid = grid;
            wrist.Settle();
            if (open && !wrist.OpenForTest("mixer")) Log.Warn("mixer: no mixer to open");
            Draw2(wrist, data);
            wrist.Surf.SavePng(gpu, Path.Combine(dir, $"wrist_mixer_{name}{tag}.png"));
            wrist.CloseForTest();
        }
        List<WatchWidget> Face64() => new()
        {
            new() { Type = "mixer", X = 0, Y = 0, W = 3, H = 2 }, new() { Type = "mixer", X = 3, Y = 0, W = 2, H = 2 },
            new() { Type = "battery", X = 5, Y = 0, W = 1, H = 1 }, new() { Type = "fps", X = 5, Y = 1, W = 1, H = 1 },
            new() { Type = "mixer", X = 0, Y = 2, W = 2, H = 1 }, new() { Type = "volume", X = 2, Y = 2, W = 2, H = 1 },
            new() { Type = "time", X = 4, Y = 2, W = 2, H = 1 },
            new() { Type = "keyboard", X = 0, Y = 3 }, new() { Type = "desktops", X = 1, Y = 3 }, new() { Type = "recenter", X = 2, Y = 3 },
            new() { Type = "layout", X = 3, Y = 3 }, new() { Type = "night", X = 4, Y = 3 }, new() { Type = "settings", X = 5, Y = 3 },
        };
        List<WatchWidget> Face43() => new()
        {
            new() { Type = "mixer", X = 0, Y = 0, W = 2, H = 2 }, new() { Type = "mixer", X = 2, Y = 0, W = 2, H = 1 },
            new() { Type = "volume", X = 2, Y = 1, W = 1, H = 1 }, new() { Type = "mic", X = 3, Y = 1, W = 1, H = 1 },
            new() { Type = "media", X = 0, Y = 2, W = 2, H = 1 }, new() { Type = "buttons", X = 2, Y = 2, W = 2, H = 1, Items = new List<string>(WatchWidgets.GroupDefault) },
        };
        Shot("face", MixerFake.Standard(), Face64(), null, false);
        Shot("face43", MixerFake.Standard(), Face43(), "4x3", false);
        Shot("open", MixerFake.Standard(), Face64(), null, true);
        Shot("none", MixerFake.With(d => { }), Face64(), null, false);
        Shot("none_open", MixerFake.With(d => { }), Face64(), null, true);
        Shot("one", MixerFake.With(d => d.Add("VRChat", 0.8f, true, 0.6f, color: 0xFF2B2F3A)), Face64(), null, false);
        Shot("one_open", MixerFake.With(d => d.Add("VRChat", 0.8f, true, 0.6f, color: 0xFF2B2F3A)), Face64(), null, true);
        Shot("ten", MixerFake.With(Ten), Face64(), null, false);
        Shot("ten_open", MixerFake.With(Ten), Face64(), null, true);
        wcfg.Grid = null;
        Log.Info($"mixer{tag}: 9 pictures (wrist_mixer_*{tag}.png)");
    }

    // ---- the master sync on a made-up device (once: it draws nothing) ----

    public static void MasterChecks()
    {
        string mode0 = AudioMixer.Mode;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            AudioMixer.Mode = "auto";
            var d = new FakeMixerDevice { MasterLevel = 0.3f };
            var vrc = d.Add("VRChat", 0.8f, true, 0.6f);
            var dis = d.Add("Discord", 0.6f, true, 0.3f);
            var spo = d.Add("Spotify", 0.4f, true, 0.2f);
            var chr = d.Add("Google Chrome", 0.3f, false, 0, muted: true);
            var m = new AudioMixer(d, true);
            double t = 0;
            void Tick(double dt = 0.3) { t += dt; m.Tick(t, true); }
            Tick();
            bool on = m.PerApp;
            float adopted = d.MasterLevel;
            int w0 = d.SessionWrites;
            Log.Info($"mixer sync: on '{d.Name}' the apps follow the master {on} (want True); it began at the loudest app's level {adopted:0.00} (want 0.80) " +
                     $"and wrote no app {w0} (want 0)");
            // Windows' slider (or Hexpad's knob) to 40 %: each app by its balance, once
            d.Outside(0.4f, false);
            Tick();
            int w1 = d.SessionWrites - w0;
            Tick();
            Tick(1);
            int w2 = d.SessionWrites - w0 - w1;
            Log.Info($"mixer sync: Windows' volume to 0.40 - apps written {w1} (want 4): VRChat {vrc.Level:0.00} (want 0.40), Discord {dis.Level:0.00} (want 0.30), " +
                     $"Spotify {spo.Level:0.00} (want 0.20), Chrome muted still {chr.Muted} (want True); the looks after it wrote {w2} (want 0)");
            // the watch's own change: the endpoint and the apps at once - and nothing again after, even when the callback tells of it
            int mw = d.MasterWrites, sw = d.SessionWrites;
            m.SetMaster(0.5f, false);
            int own = d.SessionWrites - sw;
            d.Notify();
            Tick();
            Tick(1);
            int echo = d.SessionWrites - sw - own;
            Log.Info($"mixer sync: the watch's own change to 0.50 - endpoint {d.MasterLevel:0.00} (want 0.50) written {d.MasterWrites - mw} (want 1), apps " +
                     $"written {own} (want 4), VRChat {vrc.Level:0.00} (want 0.50); after it written {echo} (want 0)");
            // an app that starts later gets the master; one moved by itself (Windows' mixer) is not moved back - and keeps its balance
            var game = d.Add("Game", 1f, true, 0.5f);
            Tick(1);
            float gameAt = game.Level;
            dis.Level = 0.1f;
            Tick(1);
            float disKept = dis.Level;
            d.Outside(1f, false);
            Tick();
            Log.Info($"mixer sync: an app that started later {gameAt:0.00} (want 0.50); Discord moved by itself to 0.10 stays {disKept:0.00} (want 0.10), " +
                     $"the master to 1.00 -> Discord {dis.Level:0.00} (want 0.20), VRChat {vrc.Level:0.00} (want 1.00)");
            // the watch's mixer moved one app: its balance from then on
            var vapp = m.Apps.Count > 0 ? null : (MixerApp)null;
            m.Want(t);
            Tick();
            vapp = m.Apps.Find(a => a.Name == "VRChat");
            if (vapp != null) m.SetApp(vapp, 0.5f);
            d.Outside(0.6f, false);
            Tick();
            Log.Info($"mixer sync: VRChat set to 0.50 on the watch's mixer, then the master 1.00 -> 0.60: VRChat {vrc.Level:0.00} (want 0.30)");
            // mute from outside: every app; unmuted: the one muted by itself stays muted
            d.Outside(0.6f, true);
            Tick();
            bool allMuted = d.Sessions.All(s => s.Muted);
            d.Outside(0.6f, false);
            Tick();
            Log.Info($"mixer sync: muted from outside - all apps muted {allMuted} (want True); unmuted - Chrome (muted by itself) stays muted {chr.Muted} " +
                     $"(want True), VRChat plays {!vrc.Muted} (want True)");
            // off: nothing follows
            AudioMixer.Mode = "off";
            Tick();
            int wo = d.SessionWrites;
            d.Outside(0.2f, false);
            Tick();
            Log.Info($"mixer sync: VolumeToApps off - the apps follow {m.PerApp} (want False), written {d.SessionWrites - wo} (want 0)");
            AudioMixer.Mode = "auto";
            const string vd = "Speakers (Virtual Desktop Audio)", rt = "Speakers (Realtek(R) Audio)";
            Log.Info($"mixer sync: which devices - Virtual Desktop's auto {AudioMixer.IgnoresOwnVolume(vd, true, "auto")} (want True), without hardware volume " +
                     $"{AudioMixer.IgnoresOwnVolume(vd, false, "auto")} (want False), off {AudioMixer.IgnoresOwnVolume(vd, true, "off")} (want False); " +
                     $"another device auto {AudioMixer.IgnoresOwnVolume(rt, true, "auto")} (want False), on with hardware volume " +
                     $"{AudioMixer.IgnoresOwnVolume(rt, true, "on")} (want True), on without {AudioMixer.IgnoresOwnVolume(rt, false, "on")} (want False)");
            // SteamVR off: nothing follows
            var d2 = new FakeMixerDevice();
            d2.Add("VRChat", 0.8f, true, 0.6f);
            var m2 = new AudioMixer(d2, true);
            m2.Tick(0.3, false);
            d2.Outside(0.2f, false);
            m2.Tick(0.6, false);
            Log.Info($"mixer sync: SteamVR not running - the apps follow {m2.PerApp} (want False), written {d2.SessionWrites} (want 0)");
        }
        finally { AudioMixer.Mode = mode0; System.Globalization.CultureInfo.CurrentCulture = culture; }
    }
    // --mixertest: the real default output device read the way the app does - its name, whether it says it sets its volume itself,
    // the master, every app (name, level, mute, playing, meter, icon). READ ONLY: no level or mute is written and the sync does not run
    // (it runs only while SteamVR is connected), so nothing changes.
    public static int Probe()
    {
        using var m = new AudioMixer();
        double t = Clock.Now;
        m.Want(t);
        for (int i = 0; i < 6; i++)
        {
            t += 0.3;
            m.Tick(t, false);
            Thread.Sleep(150);            // the names and icons come from a worker
        }
        var d = m.Device;
        Log.Info($"mixertest: '{d.Name}' present {d.Present}, sets its volume itself {d.HardwareVolume}, apps would follow the master in VR " +
                 $"{AudioMixer.IgnoresOwnVolume(d.Name, d.HardwareVolume, "auto")} (auto), master {m.Master:0.00}{(m.MasterMuted ? " muted" : "")}, sessions {d.Sessions.Count}, " +
                 $"apps {m.Apps.Count}, wrote nothing: sync off {!m.PerApp}");
        foreach (var a in m.Apps)
            Log.Info($"mixertest:   {a.Name} level {a.Level:0.00}{(a.Muted ? " muted" : "")}{(a.Active ? " playing" : "")} peak {a.Peak:0.00} icon " +
                     $"{(a.Icon32 != null ? "32" : "-")}/{(a.Icon16 != null ? "16" : "-")} sessions {a.Sessions.Count}{(a.System ? " (system)" : "")}");
        return 0;
    }
}

static partial class Tools
{
    // ORBIT_SNAP_ONLY=mixer: the mixer's checks, pictures and layout check (its sizes and opened only) in every style, in seconds
    static int MixerOnly(WristPanel wrist, PanelData data, ConfigStore cfg, Painter painter, Gpu gpu, string dir)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = Tools.SnapCulture;
        MixerViewTools.MasterChecks();
        foreach (var (style, tag) in new[] { ("modern", ""), ("classic", "_95"), ("xp", "_xp"), ("aero", "_aero"), ("mac", "_mac"), ("terminal", "_term"), (CyberHud.Id, "_cyber") })
        {
            cfg.Data.Appearance = new AppearanceConfig { Style = style, Clock24h = true };
            Theme.Apply(cfg.Data.Appearance);
            MixerViewTools.Run(wrist, data, cfg, gpu, dir, tag);
            string f0 = cfg.Data.Wrist.Face;
            LayoutCheckRun(wrist, data, cfg, painter, gpu, dir, tag.Length > 0 ? tag[1..] : "modern", onlyKind: "mixer");
            cfg.Data.Wrist.Face = f0;
            cfg.Data.Wrist.Widgets = null;
        }
        System.Globalization.CultureInfo.CurrentCulture = culture;
        Log.Info("snapshot (mixer only) written to " + dir);
        return 0;
    }
}
