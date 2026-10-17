using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit;

// The evening's recap (Erik 10-17; the "recap" widget, WristRecap.cs). A session is the time the headset is worn while SteamVR runs;
// it ends once the headset has been off for 3 minutes (the moment it came off is its end) or when SteamVR goes. One of 20 minutes or
// more gets a recap: how long, the VRChat photos taken meanwhile (counted by their names - VRChat writes when into them - in the latest
// photo widget's folder), the worlds visited and how long in each, the people met (everyone who was in your instance meanwhile, the
// friends among them), the longest stay. The worlds and people are the Room's data (VRChat's log, VRCX's friend list: followed read
// only already); the room not followed: time and photos only. Shown once as a notification card (so it is in the Notifications list
// too), kept as the recap widget's - and in %LOCALAPPDATA%\Orbit\recap.json (its own file, never config.json), so the last one is
// there after a restart. Nothing leaves the PC.
public sealed class SessionRecap
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Photos { get; set; } = -1;                    // -1: not counted (no picture folder, or not yet)
    public bool Room { get; set; }                           // the room was followed (else nothing is known of worlds and people)
    public List<RecapWorld> Worlds { get; set; } = new();    // the visits in order (a world visited twice is there twice)
    public int People { get; set; }                          // met: everyone in your instance meanwhile
    public int Friends { get; set; }                         // the friends among them
    public List<string> Names { get; set; } = new();         // a few of them, the friends first

    [JsonIgnore] public TimeSpan Length => End - Start;

    // the worlds by name (twice in one = one)
    [JsonIgnore]
    public int WorldCount
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in Worlds) seen.Add(w.Name);
            return seen.Count;
        }
    }

    // the longest single stay (null: no world)
    [JsonIgnore]
    public RecapWorld Longest
    {
        get
        {
            RecapWorld best = null;
            foreach (var w in Worlds) if (best == null || w.Minutes > best.Minutes) best = w;
            return best;
        }
    }
}

public sealed class RecapWorld
{
    public string Name { get; set; } = "";
    public DateTime Start { get; set; }
    public double Minutes { get; set; }
}

// Follows the session from the main loop: one call a frame (at rest a few comparisons, nothing made); the room's snapshot is looked
// at only when a new one comes (the room changed).
public sealed class SessionTracker
{
    public static readonly TimeSpan MinLength = TimeSpan.FromMinutes(20), OffFor = TimeSpan.FromMinutes(3);

    DateTime? _start, _off;
    RoomWatcher.Snapshot _seen;
    bool _roomSeen;
    readonly List<(RecapWorld w, DateTime end)> _visits = new();      // end: MaxValue while there
    string _curWorld;
    DateTime _curSince;
    readonly Dictionary<string, (string name, bool friend, DateTime at)> _met = new(StringComparer.Ordinal);

    public bool Running => _start != null;
    public DateTime? Started => _start;

    // A session that just ended, if it was long enough: its recap (the photos not counted yet - RecapStore.Finish). card: it ended with
    // the headset off for 3 minutes (a card is shown for it), not with SteamVR gone.
    public SessionRecap Tick(DateTime now, bool connected, bool worn, RoomWatcher.Snapshot room, out bool card)
    {
        card = false;
        if (connected && worn)
        {
            if (_start == null) Begin(now);
            _off = null;
        }
        else if (connected && _start != null && _off == null) _off = now;
        if (_start == null) return null;
        if (!ReferenceEquals(room, _seen))
        {
            _seen = room;
            See(room, now);
        }
        if (!connected) return End(_off ?? now);
        if (_off is { } off && now - off >= OffFor)
        {
            card = true;
            return End(off);
        }
        return null;
    }

    void Begin(DateTime now)
    {
        _start = now;
        _off = null;
        _seen = null;
        _roomSeen = false;
        _visits.Clear();
        _curWorld = null;
        _met.Clear();
    }

    // a new room snapshot: a new instance starts a visit (the one before ends then), leaving ends it; everyone there is met
    void See(RoomWatcher.Snapshot r, DateTime now)
    {
        if (r == null) return;
        _roomSeen = true;
        if (r.InRoom && r.World.Length > 0)
        {
            if (_curWorld != r.World || _curSince != r.Since)
            {
                var at = r.Since != default && r.Since < now ? r.Since : now;
                if (at < _start.Value) at = _start.Value;
                Leave(at);
                _visits.Add((new RecapWorld { Name = r.World, Start = at }, DateTime.MaxValue));
                (_curWorld, _curSince) = (r.World, r.Since);
            }
        }
        else if (_curWorld != null)
        {
            Leave(now);
            _curWorld = null;
        }
        foreach (var p in r.Here)
        {
            if (!_met.TryGetValue(p.Key, out var m)) _met[p.Key] = (p.Name, p.Friend, now);
            else if (p.Friend && !m.friend) _met[p.Key] = (m.name, true, m.at);      // VRCX's list came later
        }
    }

    void Leave(DateTime at)
    {
        if (_visits.Count == 0 || _visits[^1].end != DateTime.MaxValue) return;
        var (w, _) = _visits[^1];
        _visits[^1] = (w, at < w.Start ? w.Start : at);
    }

    SessionRecap End(DateTime end)
    {
        var start = _start.Value;
        _start = _off = null;
        _seen = null;
        if (end - start < MinLength) return null;
        var r = new SessionRecap { Start = start, End = end, Room = _roomSeen };
        foreach (var (w, e) in _visits)
        {
            if (w.Start >= end) continue;                       // arrived while the headset was off
            var until = e < end ? e : end;
            r.Worlds.Add(new RecapWorld { Name = w.Name, Start = w.Start, Minutes = Math.Max(0, (until - w.Start).TotalMinutes) });
        }
        var friends = new List<string>();
        var others = new List<string>();
        foreach (var (_, m) in _met)
        {
            if (m.at > end) continue;                           // came while the headset was off
            r.People++;
            if (m.friend) { r.Friends++; friends.Add(m.name); }
            else others.Add(m.name);
        }
        foreach (var n in friends) if (r.Names.Count < 8) r.Names.Add(n);
        foreach (var n in others) if (r.Names.Count < 8) r.Names.Add(n);
        _visits.Clear();
        _met.Clear();
        return r;
    }
}

// The recap's file and its photos (off the frame: the app's thread for it, or the exit).
public static class RecapStore
{
    public static string DefaultPath => Path.Combine(Log.Dir, "recap.json");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    // the photos counted, the recap written; whatever fails leaves the recap as it is (it is shown anyway)
    public static void Finish(SessionRecap r, string pictures, string path)
    {
        try { r.Photos = CountPhotos(pictures, r.Start, r.End); }
        catch (Exception e) { Log.Info("recap: photos not counted: " + e.Message); }
        Save(path, r);
    }

    public static void Save(string path, SessionRecap r)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(r, Json));
            File.Move(tmp, path, true);
        }
        catch (Exception e) { Log.Info("recap: not saved: " + e.Message); }
    }

    public static SessionRecap Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<SessionRecap>(File.ReadAllText(path), Json) : null; }
        catch (Exception e) { Log.Info("recap: not read: " + e.Message); return null; }
    }

    // VRChat's photos taken between start and end: the folder itself and its month folders of those days (VRChat_<date>_<time>...png -
    // the time from the name, else the file's); -1: no folder
    public static int CountPhotos(string folder, DateTime start, DateTime end)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return -1;
        var dirs = new List<string> { folder };
        for (var m = new DateTime(start.Year, start.Month, 1); m <= end; m = m.AddMonths(1))
        {
            string d = Path.Combine(folder, m.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            if (Directory.Exists(d)) dirs.Add(d);
        }
        int n = 0;
        foreach (var d in dirs)
            foreach (var f in Directory.EnumerateFiles(d, "VRChat_*.png"))
            {
                var t = PhotoWatcher.TakenOf(f);
                if (t >= start && t <= end) n++;
            }
        return n;
    }
}
