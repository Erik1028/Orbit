using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Orbit;

// The custom watch face: widgets on a 6 x 4 grid. What can go on the watch and in which sizes, the grid
// geometry for both styles, and the layout rules (inside the grid, no overlap, a start from the old faces).
public static class WatchWidgets
{
    public const int Cols = 6, Rows = 4;

    // Sizes are in cells, smallest first; Default is the index of the size a new one gets. Tone = its colour
    // in the modern style, from the user's palette: 0 = the Look tab's accent, 1 = the logo's indigo,
    // 2 = between the two, 3 = Orbit's green (see WristPanel.ToneColor).
    public sealed record Kind(string Id, string Name, string Title95, string Icon, W95Icons.Id Icon95, (int W, int H)[] Sizes,
        int Default = 0, bool Button = false, int Tone = 0);

    static readonly (int, int)[] Btn = { (1, 1), (2, 1) };
    static readonly (int, int)[] Stat = { (1, 1), (2, 1) };

    public static readonly Kind[] Kinds =
    {
        // clocks
        new("time", "Time", "Clock", "", W95Icons.Id.Clock, new[] { (2, 1), (3, 1), (4, 1), (3, 2), (4, 2), (6, 2), (6, 3), (6, 4) }, 4),
        new("date", "Date", "Date", "", W95Icons.Id.Clock, new[] { (2, 1), (3, 1), (4, 1), (6, 1) }, 1, Tone: 0),
        new("calendar", "Calendar", "Calendar", "", W95Icons.Id.Clock, new[] { (1, 1), (2, 1), (2, 2) }, 0, Tone: 0),
        new("analog", "Analog clock", "Clock", "", W95Icons.Id.Clock, new[] { (2, 2), (3, 3), (4, 4) }, Tone: 0),
        new("timer", "Timer", "Timer", "", W95Icons.Id.Clock, new[] { (1, 1), (2, 1), (2, 2) }, 2, Tone: 0),
        new("stopwatch", "Stopwatch", "Stopwatch", "", W95Icons.Id.Clock, new[] { (1, 1), (2, 1) }, 1, Tone: 0),
        new("session", "Time in VR", "Session", "", W95Icons.Id.Computer, Stat, Tone: 1),
        // what is going on
        new("battery", "Batteries", "Power Meter", "", W95Icons.Id.Computer, new[] { (1, 1), (2, 1), (2, 2), (3, 2) }, 2, Tone: 3),
        new("media", "Now playing", "CD Player", "", W95Icons.Id.Note, new[] { (2, 1), (3, 1), (4, 1), (6, 1), (4, 2), (6, 2) }, 2, Tone: 1),
        new("volume", "Volume", "Volume Control", "", W95Icons.Id.Note, new[] { (1, 1), (2, 1), (3, 1) }, 1, Tone: 2),
        new("perf", "Performance", "Performance", "", W95Icons.Id.Computer, new[] { (4, 1), (6, 1) }, 1, Tone: 0),
        new("graph", "Frame graph", "Frame Graph", "", W95Icons.Id.Computer, new[] { (3, 1), (4, 1), (6, 1), (4, 2), (6, 2) }, 2, Tone: 0),
        new("fps", "Frame rate", "Frame Rate", "", W95Icons.Id.Computer, Stat, Tone: 0),
        new("reproj", "Reprojection", "Reprojection", "", W95Icons.Id.Computer, Stat, Tone: 1),
        new("gpu", "GPU time", "GPU Time", "", W95Icons.Id.Computer, Stat, Tone: 2),
        new("vram", "VRAM", "Video Memory", "", W95Icons.Id.Computer, Stat, Tone: 1),
        new("cpu", "CPU", "CPU Usage", "", W95Icons.Id.Computer, Stat, Tone: 0),
        new("ram", "Memory", "Memory", "", W95Icons.Id.Computer, Stat, Tone: 2),
        // buttons
        new("keyboard", "Keyboard", "Keyboard", Theme.IcoKeyboard, W95Icons.Id.Keyboard, Btn, 0, true, 0),
        new("desktops", "Monitors", "Monitors", Theme.IcoMonitor, W95Icons.Id.Computer, Btn, 0, true, 0),
        new("recenter", "Recenter", "Recenter", Theme.IcoRecenter, W95Icons.Id.Center, Btn, 0, true, 0),
        new("layout", "Layout", "Layout", Theme.IcoLayouts, W95Icons.Id.Layouts, Btn, 0, true, 2),
        new("night", "Night mode", "Night", Theme.IcoNight, W95Icons.Id.Moon, Btn, 0, true, 1),
        new("settings", "Settings", "Settings", Theme.IcoSettings, W95Icons.Id.Gear, Btn, 0, true, 0),
        // the stream dock (Hexpad): its keys live; a tap opens it as a panel. Offered only while Hexpad runs.
        new("dock", "Hexpad", "Hexpad", "\uE80A", W95Icons.Id.Dock, new[] { (1, 1), (2, 1), (2, 2) }, 1, false, 1),
    };

    // what the editor offers: the dock only when Hexpad is there (set by the app)
    public static bool DockAvailable;
    static readonly Kind[] _noDock = Array.FindAll(Kinds, k => k.Id != "dock");
    public static Kind[] Palette => DockAvailable ? Kinds : _noDock;

    public static Kind Find(string id) => Array.Find(Kinds, k => k.Id == id);

    // ---- geometry: modern = the 640 x 440 texture, 95 = the 320 x 220 canvas ----
    // The same gap between every two widgets and between a widget and the edge (modern 10 px: with the
    // watch's 34 px corners the cards' 24 px corners are concentric; 95: 4 px). Edges on whole pixels, so
    // every gap is exactly that wide (a cell may be a pixel wider than its neighbour instead).

    static (float m, float g, float w, float h) Metrics(bool classic) =>
        classic ? (4f, 4f, WristPanel.TW95, WristPanel.TH95) : (10f, 10f, WristPanel.TW, WristPanel.TH);

    public static RectangleF CellRect(int x, int y, int w, int h, bool classic)
    {
        var (m, g, W, H) = Metrics(classic);
        float cw = (W - 2 * m - (Cols - 1) * g) / Cols, ch = (H - 2 * m - (Rows - 1) * g) / Rows;
        float x0 = m + x * (cw + g), y0 = m + y * (ch + g);
        float x1 = m + (x + w) * (cw + g) - g, y1 = m + (y + h) * (ch + g) - g;
        return RectangleF.FromLTRB(MathF.Round(x0), MathF.Round(y0), MathF.Round(x1), MathF.Round(y1));
    }

    public static RectangleF Rect(WatchWidget w, bool classic) => CellRect(w.X, w.Y, w.W, w.H, classic);

    // the cell under a point of the texture / canvas, clamped to the grid
    public static (int x, int y) CellAt(float px, float py, bool classic)
    {
        var (m, g, W, H) = Metrics(classic);
        float cw = (W - 2 * m - (Cols - 1) * g) / Cols, ch = (H - 2 * m - (Rows - 1) * g) / Rows;
        int cx = (int)MathF.Floor((px - m + g / 2) / (cw + g)), cy = (int)MathF.Floor((py - m + g / 2) / (ch + g));
        return (Math.Clamp(cx, 0, Cols - 1), Math.Clamp(cy, 0, Rows - 1));
    }

    // ---- layout rules ----

    // overflow-safe: a hand-edited config can hold int.MaxValue
    public static bool InGrid(int x, int y, int w, int h) =>
        x >= 0 && y >= 0 && w >= 1 && h >= 1 && w <= Cols && h <= Rows && x <= Cols - w && y <= Rows - h;

    // is this area inside the grid and free (ignoring 'skip', the widget being moved)?
    public static bool Free(IReadOnlyList<WatchWidget> list, WatchWidget skip, int x, int y, int w, int h)
    {
        if (!InGrid(x, y, w, h)) return false;
        foreach (var o in list)
            if (o != skip && x < o.X + o.W && o.X < x + w && y < o.Y + o.H && o.Y < y + h) return false;
        return true;
    }

    public static WatchWidget At(IReadOnlyList<WatchWidget> list, int cx, int cy)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var o = list[i];
            if (cx >= o.X && cx < o.X + o.W && cy >= o.Y && cy < o.Y + o.H) return o;
        }
        return null;
    }

    // A new widget where it fits: in the preferred cell (any size that fits there), else its default size
    // anywhere, then any other size from the smallest. Null when there is no room.
    public static WatchWidget Add(List<WatchWidget> list, Kind k, int prefX = -1, int prefY = -1)
    {
        var sizes = new List<(int W, int H)> { k.Sizes[k.Default] };
        sizes.AddRange(k.Sizes.Where((s, i) => i != k.Default));
        if (prefX >= 0)
            foreach (var (w, h) in sizes)
                if (Free(list, null, prefX, prefY, w, h)) return Place(list, k, prefX, prefY, w, h);
        foreach (var (w, h) in sizes)
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                    if (Free(list, null, x, y, w, h)) return Place(list, k, x, y, w, h);
        return null;
    }

    static WatchWidget Place(List<WatchWidget> list, Kind k, int x, int y, int w, int h)
    {
        var n = new WatchWidget { Type = k.Id, X = x, Y = y, W = w, H = h };
        list.Add(n);
        return n;
    }

    // the allowed size closest to what the pointer asks for (a corner drag)
    public static (int W, int H) SnapSize(Kind k, int wantW, int wantH)
    {
        wantW = Math.Max(1, wantW);
        wantH = Math.Max(1, wantH);
        return k.Sizes.OrderBy(s => Math.Abs(s.W - wantW) + Math.Abs(s.H - wantH)).ThenBy(s => s.W * s.H).First();
    }

    // the next smaller (-1) or larger (+1) allowed size that fits where the widget is, or null
    public static (int W, int H)? Step(IReadOnlyList<WatchWidget> list, WatchWidget w, int dir)
    {
        var k = Find(w.Type);
        if (k == null) return null;
        int area = w.W * w.H;
        var order = k.Sizes.OrderBy(s => s.W * s.H).ThenBy(s => s.W).ToList();
        int at = order.FindIndex(s => s.W == w.W && s.H == w.H);
        if (at < 0)
        {
            // a size this version doesn't list: step from where its area would sit
            if (dir > 0) at = order.FindLastIndex(s => s.W * s.H <= area);
            else { at = order.FindIndex(s => s.W * s.H >= area); if (at < 0) at = order.Count; }
        }
        for (int i = at + dir; i >= 0 && i < order.Count; i += dir)
            if (Free(list, w, w.X, w.Y, order[i].W, order[i].H)) return order[i];
        return null;
    }

    // ---- a start from the old faces, so the custom face first looks like what the user had ----

    public static readonly string[] Presets = { "full", "analog", "minimal", "time", "corners" };
    public static readonly string[] PresetNames = { "Full", "Analog", "Minimal", "Time only", "Corners" };

    public static List<WatchWidget> Seed(WristConfig c, string face)
    {
        var l = new List<WatchWidget>();
        void Put(string t, int x, int y, int w, int h) => l.Add(new WatchWidget { Type = t, X = x, Y = y, W = w, H = h });
        switch (face)
        {
            case "time":
                Put("time", 0, 0, 6, 4);
                return l;
            case "minimal":
                Put("time", 0, 0, 6, 3);
                break;
            case "corners":
                // a clock in the middle, a little widget in each corner (smartwatch complications)
                Put("time", 1, 0, 4, 2);
                Put("date", 1, 2, 4, 1);
                Put("battery", 0, 0, 1, 1);
                Put("fps", 5, 0, 1, 1);
                Put("volume", 0, 2, 1, 1);
                Put("gpu", 5, 2, 1, 1);
                break;
            case "analog":
                Put("analog", 0, 0, 2, 2);
                Put("time", 2, 0, 2, 1);
                Put("date", 2, 1, 2, 1);
                if (c.ShowBatteries) Put("battery", 4, 0, 2, 2);
                Row2(c, Put);
                break;
            default:
                if (c.ShowBatteries) { Put("time", 0, 0, 4, 2); Put("battery", 4, 0, 2, 2); }
                else Put("time", 0, 0, 6, 2);
                Row2(c, Put);
                break;
        }
        string[] buttons = { "keyboard", "desktops", "recenter", "layout", "night", "settings" };
        for (int i = 0; i < buttons.Length; i++) Put(buttons[i], i, 3, 1, 1);
        return l;
    }

    static void Row2(WristConfig c, Action<string, int, int, int, int> put)
    {
        if (c.ShowMedia && c.ShowPerformance)
        {
            if (c.PerfGraph) { put("media", 0, 2, 3, 1); put("graph", 3, 2, 3, 1); }
            else { put("media", 0, 2, 4, 1); put("fps", 4, 2, 1, 1); put("gpu", 5, 2, 1, 1); }
        }
        else if (c.ShowMedia) put("media", 0, 2, 6, 1);
        else if (c.ShowPerformance) put(c.PerfGraph ? "graph" : "perf", 0, 2, 6, 1);
    }

    // What a hand-edited (or newer) config got wrong: outside the grid or overlapping = dropped; a size this
    // version doesn't offer = the largest offered size inside its box (or the nearest, if that fits).
    // Unknown types are KEPT (a newer Orbit's widgets survive a round trip; they draw as nothing here).
    public static List<WatchWidget> Clean(List<WatchWidget> list)
    {
        var ok = new List<WatchWidget>();
        foreach (var w in list ?? new List<WatchWidget>())
        {
            if (w == null || string.IsNullOrEmpty(w.Type) || !InGrid(w.X, w.Y, w.W, w.H)) continue;
            var k = Find(w.Type);
            if (k != null && !k.Sizes.Contains((w.W, w.H)))
            {
                var inside = k.Sizes.Where(s => s.W <= w.W && s.H <= w.H).OrderByDescending(s => s.W * s.H).ToList();
                var s = inside.Count > 0 ? inside[0] : SnapSize(k, w.W, w.H);
                if (!Free(ok, null, w.X, w.Y, s.W, s.H)) continue;
                w.W = s.W;
                w.H = s.H;
            }
            if (Free(ok, null, w.X, w.Y, w.W, w.H)) ok.Add(w);
        }
        return ok;
    }
}
