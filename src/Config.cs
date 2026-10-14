using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Orbit;

// Where an overlay lives. Attach = world | left | right | head.
// M is the OpenVR 3x4 matrix (m0..m11): world transform for "world",
// otherwise the transform relative to that tracked device. Null = not placed yet.
public sealed class PlacementConfig
{
    public string Attach { get; set; } = "world";
    public float[] M { get; set; }
}

public sealed class WristConfig
{
    public bool Enabled { get; set; } = true;
    public string Hand { get; set; } = "left";
    public bool AlwaysVisible { get; set; } = false;
    public float Width { get; set; } = 0.13f;
    public bool ShowPerformance { get; set; } = true;
    public bool ShowBatteries { get; set; } = true;
    public bool ShowMedia { get; set; } = true;
    public string Face { get; set; } = "full";          // full | analog | minimal | time
    public bool PerfGraph { get; set; } = false;        // frame-time graph instead of the numbers
    public float[] Offset { get; set; }   // relative to the hand; null = default watch spot
    public bool Locked { get; set; }      // the grip can't move or resize the watch (no accidental nudges)
}

public sealed class DesktopConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public float Width { get; set; } = 0.9f;
    public float Opacity { get; set; } = 1f;
    public float Curvature { get; set; } = 0f;
    public bool Mouse { get; set; } = true;
    public PlacementConfig Place { get; set; } = new();
    public string Group { get; set; }        // monitors snapped together share a group (one row)
    public int GroupIndex { get; set; }      // position in that row, left to right
}

// One application window mirrored into VR. Found again after a restart by program + window class
// (the last seen title breaks ties between several windows of the same program).
public sealed class WindowConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Process { get; set; } = "";
    public string Class { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public float Width { get; set; } = 0.7f;
    public float Opacity { get; set; } = 1f;
    public float Curvature { get; set; } = 0f;
    public bool Mouse { get; set; } = true;
    public PlacementConfig Place { get; set; } = new();
}

public sealed class KeyboardConfig
{
    public bool Visible { get; set; } = false;
    public bool Sounds { get; set; } = true;
    public string SoundStyle { get; set; } = "click";  // click | muted (older values fall back to click)
    public float SoundVolume { get; set; } = 0.6f;
    public string Language { get; set; } = "windows";  // windows = follow the active Windows layout | en-US = built-in English
    public bool AutoShow { get; set; } = true;         // pops up when a click from VR lands in a text field
    public float Width { get; set; } = 0.62f;
    public PlacementConfig Place { get; set; } = new();
}

public sealed class SettingsPanelConfig
{
    public float Width { get; set; } = 0.85f;
    public PlacementConfig Place { get; set; } = new();
}

public sealed class GeneralConfig
{
    public string LaserHand { get; set; } = "both";
    public bool Haptics { get; set; } = true;
    public int CaptureFps { get; set; } = 60;
    public bool DesktopsVisible { get; set; } = true;
    public bool AutoStart { get; set; } = true;       // wanted state; applied whenever SteamVR is reachable
    public bool QuickToggle { get; set; } = true;     // double-tap both grips = hide/show monitors + keyboard
    public bool Osc { get; set; } = true;             // XSOverlay-style avatar parameters (DXOverlay etc.)
    public int OscPort { get; set; } = 9000;
    public bool RunAsAdmin { get; set; } = true;      // lets the VR mouse/keyboard reach admin windows
    public bool Notifications { get; set; } = true;   // XSOverlay-format notifications on UDP 42069
    public bool SmartCapture { get; set; } = true;    // full frame rate only for monitors you look / point at
    public bool PauseWhenOff { get; set; } = true;    // headset taken off: stop capturing and drawing
    public bool HighGpuPriority { get; set; } = true; // Orbit's small GPU jobs go ahead of the game's frames (admin)
    public string Raycast { get; set; } = "shadow";    // steamvr | shadow (compare, log) | local (no IPC)
    public string LastVersion { get; set; }            // the version that ran last: tells when an update happened
    public bool NotificationSound { get; set; } = true;
    public float NotificationAngle { get; set; } = 20f;  // degrees below the centre of view (first card)
    public int LaserSmoothing { get; set; } = 2;      // 0 off, 1 low, 2 medium, 3 high
}

public sealed class AppearanceConfig
{
    public uint Accent { get; set; } = 0xFF45C1FF;
    public uint PanelColor { get; set; } = 0xFF15171C;
    public float PanelOpacity { get; set; } = 0.92f;
    public uint LaserColor { get; set; } = 0;          // 0 = same as accent
    public bool Clock24h { get; set; } = true;
    public float LaserThickness { get; set; } = 3.5f;  // mm
    public bool LaserAlways { get; set; } = false;     // beam also when not pointing at a panel
    public bool NightMode { get; set; } = false;       // every panel in VR dimmed
    public string Style { get; set; } = "modern";      // modern | classic (Windows 95)
    public float NightBrightness { get; set; } = 0.45f;
}

// One saved arrangement ("Desk", "Bed", ...): where every panel was, its size and curve, night mode.
public sealed class LayoutProfile
{
    public string Name { get; set; } = "Desk";
    public bool OnWrist { get; set; } = true;          // the watch's layout button cycles through these
    public DateTime? Saved { get; set; }               // null = not used yet
    public bool NightMode { get; set; }
    public List<PanelSnapshot> Panels { get; set; } = new();
}

public sealed class PanelSnapshot
{
    public string Kind { get; set; }                   // desktop | window | keyboard
    public string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public float Width { get; set; }
    public float Curvature { get; set; }
    public float Opacity { get; set; } = 1f;
    public PlacementConfig Place { get; set; }
    public string Group { get; set; }
    public int GroupIndex { get; set; }
}

public sealed class Config
{
    public int Version { get; set; } = 1;
    public WristConfig Wrist { get; set; } = new();
    public KeyboardConfig Keyboard { get; set; } = new();
    public GeneralConfig General { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public SettingsPanelConfig SettingsPanel { get; set; } = new();
    public List<DesktopConfig> Desktops { get; set; } = new();
    public List<WindowConfig> Windows { get; set; } = new();
    public List<LayoutProfile> Layouts { get; set; } = DefaultLayouts();
    public string ActiveLayout { get; set; } = "Desk";

    public static List<LayoutProfile> DefaultLayouts() => new()
    {
        new() { Name = "Desk" }, new() { Name = "Bed" }, new() { Name = "Couch", OnWrist = false },
    };
}

// JSON config in %LOCALAPPDATA%\Orbit\config.json.
// Writes go to a temp file first and replace the old file, which is kept as config.json.bak.
// A file that fails to parse is renamed aside (never deleted) and defaults are used.
public sealed class ConfigStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Path { get; }
    public Config Data { get; private set; }
    bool _dirty;
    double _dirtySince;

    public ConfigStore(string path = null)
    {
        Path = path ?? System.IO.Path.Combine(Log.Dir, "config.json");
        Data = Load();
    }

    // set when the file exists but could not be read (locked): saving is refused, so a
    // temporary read error can't replace the user's settings with defaults
    bool _readOnly;

    Config Load()
    {
        string text = null;
        if (File.Exists(Path))
        {
            for (int attempt = 0; text == null; attempt++)
            {
                try { text = File.ReadAllText(Path); }
                catch (IOException) when (attempt < 20) { System.Threading.Thread.Sleep(100); }
                catch (Exception e)
                {
                    Log.Warn($"config can't be read ({e.Message}) - using defaults and NOT saving over it");
                    _readOnly = true;
                    return new Config();
                }
            }
        }
        try
        {
            if (text != null)
            {
                var cfg = JsonSerializer.Deserialize<Config>(text, Json);
                if (cfg != null)
                {
                    cfg.Wrist ??= new(); cfg.Keyboard ??= new(); cfg.General ??= new(); cfg.Appearance ??= new(); cfg.SettingsPanel ??= new(); cfg.SettingsPanel.Place ??= new();
                    cfg.Desktops = (cfg.Desktops ?? new()).Where(d => d != null && !string.IsNullOrEmpty(d.Id)).ToList();
                    foreach (var d in cfg.Desktops) d.Place ??= new();
                    cfg.Windows = (cfg.Windows ?? new()).Where(w => w != null && !string.IsNullOrEmpty(w.Id))
                        .GroupBy(w => w.Id).Select(g => g.First()).ToList();     // one overlay key per window
                    foreach (var w in cfg.Windows) { w.Place ??= new(); w.Title ??= ""; w.Process ??= ""; w.Class ??= ""; }
                    cfg.Layouts = (cfg.Layouts ?? new()).Where(l => l != null && !string.IsNullOrWhiteSpace(l.Name))
                        .GroupBy(l => l.Name).Select(g => g.First()).ToList();
                    if (cfg.Layouts.Count == 0) cfg.Layouts = Config.DefaultLayouts();
                    foreach (var l in cfg.Layouts) l.Panels = (l.Panels ?? new()).Where(s => s?.Id != null && s.Kind != null).ToList();
                    if (!cfg.Layouts.Any(l => l.Name == cfg.ActiveLayout)) cfg.ActiveLayout = cfg.Layouts[0].Name;
                    cfg.Keyboard.Place ??= new();
                    return cfg;
                }
            }
        }
        catch (JsonException e)
        {
            string bad = Path + $".bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            Log.Warn($"config unreadable ({e.Message}), moved to {bad}");
            try { File.Move(Path, bad); } catch { }
        }
        return new Config();
    }

    static double Now => Clock.Now;

    double _lastChange;

    bool _soft, _failWarned;

    // a change worth keeping but not worth a disk write of its own (a mirrored window's title changes
    // all the time): saved together with the next real change, or when Orbit closes
    public void MarkSoftDirty() => _soft = true;

    public bool Dirty => _dirty;      // a change not on disk yet (the 95 sheet's Apply button)

    public void MarkDirty()
    {
        if (!_dirty) _dirtySince = Now;
        _lastChange = Now;
        _dirty = true;
    }

    // Saves once things have been quiet for a second (sliders fire every frame).
    public void SaveIfDue(bool force = false)
    {
        // grabbing a panel marks dirty every frame: wait for a quiet second, but never longer than 10 s
        if (!(_dirty || (force && _soft)) || (!force && Now - _lastChange < 1.0 && Now - _dirtySince < 10.0)) return;
        if (_readOnly) { _dirty = _soft = false; Log.Warn("config not saved: it could not be read at start"); return; }
        _dirty = _soft = false;
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                string tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Json), new System.Text.UTF8Encoding(false));
                if (File.Exists(Path))
                    File.Replace(tmp, Path, Path + ".bak");
                else
                    File.Move(tmp, Path);
                _failWarned = false;
                return;
            }
            catch (IOException) when (force && attempt < 5) { System.Threading.Thread.Sleep(100); }   // closing: try a little longer
            catch (Exception e)
            {
                // someone has the file open (another Orbit starting, a backup tool): try again in a second
                _dirty = true;
                _dirtySince = _lastChange = Now;
                if (!_failWarned) { _failWarned = true; Log.Warn("config save failed (will retry): " + e.Message); }
                return;
            }
        }
    }
}
