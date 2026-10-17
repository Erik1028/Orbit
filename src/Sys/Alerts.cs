using System;
using System.Collections.Generic;

namespace Orbit;

// General > Alerts: a card (Orbit's own notification panel) and a short buzz in a controller when
// - a controller's or tracker's battery drops under the threshold (15 %): once per device and drop, armed again only once it has been
//   charged 5 points above it; never while it charges; a device seen for under 10 s, or one low reading, does not count (some report
//   0 % for a moment when they connect). The buzz is in that controller (a tracker's: in the watch hand).
// - the game's frame rate stays under FpsPct % of the headset's rate (80 %) for 10 s: then nothing for 2 minutes, and the next 10 s
//   are counted only after those (still low then: another card). Only while the headset is worn, a game runs and it is not loading
//   (the compositor's loading / not-responding frames break the 10 s).
// - the GPU or the CPU stays at or over HotC (85 °C) for 30 s (off by default): once, armed again only after both have been 5 °C under
//   it (a card each time it creeps over the line would be noise). Only with the headset on and a reading of the last few seconds
//   (Sys/Temps.cs samples every 2 s); a reading of "–" breaks the 30 s.
// The logic is here without SteamVR (the --snapshot checks drive it with made-up readings); OrbitApp feeds it VrStats once a frame.
public sealed class Alerts
{
    public const float RearmPoints = 0.05f;            // a fired device is armed again at threshold + 5 points
    public const double SeenFor = 10, LowFor = 10, Quiet = 120;

    // ---- batteries ----
    const int Max = 64;                                 // OpenVR's k_unMaxTrackedDeviceCount
    readonly bool[] _fired = new bool[Max];
    readonly double[] _seenAt = new double[Max];
    readonly byte[] _lowReads = new byte[Max];

    public Alerts() { Array.Fill(_seenAt, -1); }

    // one reading of every device (VrStats.Devices, once a second); the ones that just went low are added to 'fire'
    public void Battery(double now, IReadOnlyList<BatteryDevice> devices, float threshold, List<BatteryDevice> fire)
    {
        for (int k = 0; k < devices.Count; k++)          // (an index loop: no enumerator box once a second)
        {
            var d = devices[k];
            if (d.Index >= Max || d.Pct < 0) continue;
            int i = (int)d.Index;
            if (_seenAt[i] < 0) _seenAt[i] = now;
            if (_fired[i])
            {
                if (d.Pct >= threshold + RearmPoints) { _fired[i] = false; _lowReads[i] = 0; }
                continue;
            }
            bool low = d.Pct < threshold && !d.Charging;
            if (!low) { _lowReads[i] = 0; continue; }
            if (_lowReads[i] < 255) _lowReads[i]++;
            if (_lowReads[i] < 2 || now - _seenAt[i] < SeenFor) continue;
            _fired[i] = true;
            fire.Add(d);
        }
    }

    // ---- the frame rate ----
    double _lowSince = -1, _lastAlert = double.NegativeInfinity;

    // every frame: valid = the reading means something now (worn, a game, not loading, measured in the last 1.5 s); low = under the
    // threshold. True once: the moment to show the card
    public bool Fps(double now, bool valid, bool low)
    {
        if (!valid || !low || now - _lastAlert < Quiet) { _lowSince = -1; return false; }
        if (_lowSince < 0) _lowSince = now;
        if (now - _lowSince < LowFor) return false;
        _lastAlert = now;
        _lowSince = -1;
        return true;
    }

    public bool FpsQuiet(double now) => now - _lastAlert < Quiet;

    // ---- the GPU / CPU hot ----
    public const double HotFor = 30;                    // s at or over the threshold
    public const float HotEase = 5;                     // °C under it (both) before it can come again
    double _hotSince = -1;
    bool _hotFired;

    // every frame: valid = a reading of the last few seconds with the headset on; gpu / cpu in °C (NaN: not read). 1 (the GPU) or 2 (the
    // CPU) once: the moment to show the card; 0 else
    public int Hot(double now, bool valid, float gpu, float cpu, float threshold)
    {
        if (!valid) { _hotSince = -1; return 0; }
        if (_hotFired)
        {
            if (!(gpu >= threshold - HotEase) && !(cpu >= threshold - HotEase)) _hotFired = false;      // cooled down: armed again
            _hotSince = -1;
            return 0;
        }
        bool g = gpu >= threshold, c = cpu >= threshold;
        if (!g && !c) { _hotSince = -1; return 0; }
        if (_hotSince < 0) _hotSince = now;
        if (now - _hotSince < HotFor) return 0;
        _hotFired = true;
        _hotSince = -1;
        return g ? 1 : 2;
    }

    // SteamVR went away: the devices start over (their indices mean nothing on the next connect); the quiet time stays
    public void Reset()
    {
        Array.Clear(_fired);
        Array.Clear(_lowReads);
        Array.Fill(_seenAt, -1);
        _lowSince = -1;
        _hotSince = -1;
    }

    // ---- what the card says ----

    public static string DeviceName(BatteryDevice d, int trackerNo) => d.Kind switch
    {
        BatteryDevice.Left => "Left controller",
        BatteryDevice.Right => "Right controller",
        BatteryDevice.Tracker => trackerNo > 0 ? Lang.F("Tracker {0}", trackerNo) : "Tracker",
        _ => "Controller",
    };

    public static Notification BatteryCard(BatteryDevice d, int trackerNo, int thresholdPct) => new()
    {
        Source = "Battery",
        Title = $"{Lang.T(DeviceName(d, trackerNo))}: {Math.Round(d.Pct * 100):0} %",
        Content = Lang.F("Under {0} % - time to charge it.", thresholdPct),
        Timeout = 6, Volume = 0.8f, Icon = Theme.Classic ? null : BatteryIcon(),       // a retro card keeps its own icon
    };

    public static Notification FpsCard(float fps, float hz, int pct) => new()
    {
        Source = "Frame rate",
        Title = Lang.F("Frame rate low: {0:0} fps", Math.Round(fps)),
        Content = Lang.F("Under {0} % of the headset's {1:0} Hz for {2:0} s.", pct, Math.Round(hz), LowFor),
        Timeout = 6, Volume = 0.8f, Icon = Theme.Classic ? null : FpsIcon(),
    };

    public static Notification HotCard(bool gpu, float c, int threshold) => new()
    {
        Source = "Temperatures",
        Title = Lang.F(gpu ? "GPU hot: {0:0} °C" : "CPU hot: {0:0} °C", Math.Round(c)),
        Content = Lang.F("At or over {0} °C for {1:0} s.", threshold, HotFor),
        Timeout = 6, Volume = 0.8f, Icon = Theme.Classic ? null : HotIcon(),
    };

    // ---- the cards' icons (NotificationListener.IconSize square, premultiplied BGRA), drawn once ----

    static uint[] _battery, _fps, _hot;
    const uint Red = 0xFFFF5A5A, Grey = 0xFF8A90A6, Dark = 0xFF1D2132;

    static uint[] Canvas()
    {
        int n = NotificationListener.IconSize;
        var px = new uint[n * n];
        Fill(px, 0, 0, n, n, Dark, 22);
        return px;
    }

    // a filled rectangle with round corners (radius r), straight into the pixels
    static void Fill(uint[] px, float x0, float y0, float x1, float y1, uint c, float r = 0)
    {
        int n = NotificationListener.IconSize;
        for (int y = Math.Max(0, (int)y0); y < Math.Min(n, (int)MathF.Ceiling(y1)); y++)
            for (int x = Math.Max(0, (int)x0); x < Math.Min(n, (int)MathF.Ceiling(x1)); x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float dx = MathF.Max(MathF.Max(x0 + r - cx, cx - (x1 - r)), 0), dy = MathF.Max(MathF.Max(y0 + r - cy, cy - (y1 - r)), 0);
                float a = r <= 0 ? 1 : Math.Clamp(r + 0.5f - MathF.Sqrt(dx * dx + dy * dy), 0, 1);
                if (a <= 0) continue;
                px[y * n + x] = Over(px[y * n + x], c, a);
            }
    }

    static uint Over(uint dst, uint c, float a)
    {
        float sa = ((c >> 24) & 0xFF) / 255f * a;
        uint Ch(int sh) => (uint)Math.Clamp(MathF.Round(((c >> sh) & 0xFF) * sa + ((dst >> sh) & 0xFF) * (1 - sa)), 0, 255);
        uint da = (uint)Math.Clamp(MathF.Round(255 * sa + ((dst >> 24) & 0xFF) * (1 - sa)), 0, 255);
        return (da << 24) | (Ch(16) << 16) | (Ch(8) << 8) | Ch(0);
    }

    // a battery on its side, its last bit of charge red
    public static uint[] BatteryIcon()
    {
        if (_battery != null) return _battery;
        var px = Canvas();
        Fill(px, 18, 32, 72, 64, Grey, 7);
        Fill(px, 22, 36, 68, 60, Dark, 4);
        Fill(px, 72, 41, 79, 55, Grey, 2);
        Fill(px, 26, 40, 36, 56, Red, 2);
        return _battery = px;
    }

    // a frame-time graph: bars, the last one fallen low and red
    public static uint[] FpsIcon()
    {
        if (_fps != null) return _fps;
        var px = Canvas();
        float[] h = { 30, 34, 31, 36, 33, 12 };
        for (int i = 0; i < h.Length; i++)
            Fill(px, 18 + i * 10.5f, 70 - h[i], 18 + i * 10.5f + 7, 70, i == h.Length - 1 ? Red : Grey, 2);
        Fill(px, 16, 73, 80, 75, Grey);
        return _fps = px;
    }

    // a thermometer, its red column up to the top
    public static uint[] HotIcon()
    {
        if (_hot != null) return _hot;
        var px = Canvas();
        Fill(px, 40, 16, 56, 66, Grey, 8);
        Fill(px, 34, 56, 62, 84, Grey, 14);
        Fill(px, 44, 20, 52, 66, Dark, 4);
        Fill(px, 45, 24, 51, 70, Red, 3);
        Fill(px, 38, 60, 58, 80, Red, 10);
        for (int i = 0; i < 3; i++) Fill(px, 58, 26 + i * 10, 66, 28 + i * 10, Grey);
        return _hot = px;
    }
}
