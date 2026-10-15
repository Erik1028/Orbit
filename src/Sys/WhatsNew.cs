using System;
using System.Collections.Generic;
using System.IO;

namespace Orbit;

// What changed in each version, from docs/CHANGELOG.md built into the exe (no network): "## <version> | <title>",
// then "- " points; an indented line continues the point above it. After an update the "What's new" window shows
// every section newer than the version you came from (General.WhatsNewFrom until you press OK).
public static class WhatsNew
{
    public sealed record Entry(Version Version, string Title, List<string> Points);

    static List<Entry> _all;
    public static IReadOnlyList<Entry> All => _all ??= Load();

    static List<Entry> Load()
    {
        var list = new List<Entry>();
        try
        {
            using var s = typeof(WhatsNew).Assembly.GetManifestResourceStream("whatsnew/CHANGELOG.md");
            if (s == null) return list;
            using var r = new StreamReader(s);
            return Parse(r.ReadToEnd());
        }
        catch (Exception e) { Log.Warn("what's new: " + e.Message); return list; }
    }

    public static List<Entry> Parse(string md)
    {
        var list = new List<Entry>();
        Entry cur = null;
        foreach (var raw in md.Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("## "))
            {
                var head = raw[3..];
                int bar = head.IndexOf('|');
                string ver = (bar >= 0 ? head[..bar] : head).Trim(), title = bar >= 0 ? head[(bar + 1)..].Trim() : "";
                cur = Version.TryParse(ver, out var v) ? new Entry(v, title, new List<string>()) : null;
                if (cur != null) list.Add(cur);
                continue;
            }
            if (cur == null) continue;
            if (raw.StartsWith("- ")) cur.Points.Add(raw[2..].Trim());
            else if (raw.StartsWith("  ") && raw.Trim().Length > 0 && cur.Points.Count > 0)
                cur.Points[^1] += " " + raw.Trim();
        }
        list.Sort((a, b) => b.Version.CompareTo(a.Version));      // newest first
        return list;
    }

    // the sections newer than `from` up to the running version (none when `from` can't be read or nothing is newer)
    public static List<Entry> Since(string from)
    {
        var res = new List<Entry>();
        if (string.IsNullOrEmpty(from) || !Version.TryParse(from, out var f)) return res;
        var cur = Updater.Current;
        foreach (var e in All)
            if (e.Version.CompareTo(f) > 0 && e.Version.CompareTo(cur) <= 0) res.Add(e);
        return res;
    }

    // the last few versions up to the running one (Settings > General > What's new)
    public static List<Entry> Latest(int n)
    {
        var res = new List<Entry>();
        var cur = Updater.Current;
        foreach (var e in All)
            if (e.Version.CompareTo(cur) <= 0 && res.Count < n) res.Add(e);
        return res;
    }
}
