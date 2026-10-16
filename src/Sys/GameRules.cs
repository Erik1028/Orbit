using System;
using System.Collections.Generic;
using System.Text;
using Valve.VR;

namespace Orbit;

// Layouts > Per game: when a SteamVR app starts, OrbitOverlay switches to the layout and / or the watch page set for it (or the
// "any other app" rule). Only when the app CHANGES: what you pick by hand afterwards stays until the next app starts. An app that was
// already running when OrbitOverlay connected (OrbitOverlay restarted or updated mid-game) switches nothing - your current layout stays.
// An app must stay for 2 s before it counts (a launcher handing over, a quick restart).
public sealed class GameRuleEngine
{
    public const string AnyOther = "*";
    public const string VrchatKey = "steam.app.438100";
    public const double Stable = 2;

    string _cur, _pending = "\0";
    double _pendingSince;
    bool _first = true;

    public string Current => _cur;

    // every poll: the scene app's key now (null = none). The rule to apply, once, when the app changed - else null
    public GameRule Step(double now, string key, IReadOnlyList<GameRule> rules)
    {
        if (key != _pending) { _pending = key; _pendingSince = now; }
        if ((!_first && _pending == _cur) || now - _pendingSince < Stable) return null;
        bool baseline = _first;
        _first = false;
        _cur = _pending;
        if (baseline || _cur == null || rules == null) return null;
        return Find(rules, _cur);
    }

    public static GameRule Find(IReadOnlyList<GameRule> rules, string key)
    {
        GameRule other = null;
        foreach (var r in rules)
        {
            if (r == null) continue;
            if (r.App == key) return r;
            if (r.App == AnyOther) other = r;
        }
        return other;
    }

    // a new SteamVR connection: the next app seen is the baseline again
    public void Reset() { _cur = null; _pending = "\0"; _first = true; }
}

// The SteamVR scene app (the game): its process asked once a second, its key and name only when the process changes.
public sealed class SceneApp
{
    uint _pid = uint.MaxValue;
    double _next;
    public string Key { get; private set; }
    public string Name { get; private set; }

    public void Poll(double now)
    {
        if (now < _next) return;
        _next = now + 1;
        try
        {
            var apps = OpenVR.Applications;
            if (apps == null) return;
            uint pid = apps.GetCurrentSceneProcessId();
            if (pid == _pid) return;
            _pid = pid;
            if (pid == 0) { Key = Name = null; return; }
            var sb = new StringBuilder(256);
            string key = apps.GetApplicationKeyByProcessId(pid, sb, (uint)sb.Capacity) == EVRApplicationError.None ? sb.ToString() : null;
            string name = null;
            if (!string.IsNullOrEmpty(key))
            {
                sb.Clear();
                var e = EVRApplicationError.None;
                apps.GetApplicationPropertyString(key, EVRApplicationProperty.Name_String, sb, (uint)sb.Capacity, ref e);
                if (e == EVRApplicationError.None && sb.Length > 0) name = sb.ToString();
            }
            else
            {
                // not a registered app (started outside Steam): its program's name stands for it
                try { using var p = System.Diagnostics.Process.GetProcessById((int)pid); key = "exe:" + p.ProcessName.ToLowerInvariant(); name = p.ProcessName; }
                catch { key = null; }
            }
            Key = key;
            Name = name ?? key;
            Log.Info($"scene app: {(Key == null ? "none" : $"{Name} ({Key})")}");
        }
        catch (Exception e) { Log.Warn("scene app: " + e.Message); _next = now + 10; }
    }

    public void Reset() { _pid = uint.MaxValue; Key = Name = null; _next = 0; }

    // a friendly name for a rule's app in the settings
    public static string Label(GameRule r) => r.App == GameRuleEngine.AnyOther ? "Any other app" : string.IsNullOrWhiteSpace(r.Name) ? r.App : r.Name;
}
