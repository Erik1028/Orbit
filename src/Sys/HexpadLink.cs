using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Orbit;

// Hexpad (an app for the Ajazz AKP03 stream dock) publishes on the local pipe "Hexpad_mirror_v1" what the dock's
// six keys show, which page / folder it is on and what every key, round button and knob does; we can press keys,
// turn knobs and wake its idle screen. One JSON object per line (see Hexpad's dock/mirror.py).
// Its OTHER pipe, AjazzDock_ipc_v1, is only ever looked for by name, never opened: any message there but
// "status" pops Hexpad's window on the desktop.
public sealed class HexpadLink : IDisposable
{
    public const string PipeName = "Hexpad_mirror_v1", OldPipe = "AjazzDock_ipc_v1";
    public const int Keys = 6;

    public enum LinkState { Off, NotRunning, OldHexpad, Connecting, Unplugged, ScreenOff, Ok }

    public sealed class Control
    {
        public string Label = "", Left = "", Right = "", Push = "";
        public bool Danger;
    }

    // what the panel draws; Faces are replaced, never changed in place, so a copy of the array is a safe picture
    public sealed class Snapshot
    {
        public int Version;
        public LinkState State;
        public bool Ambient, Dark, InFolder;
        public string Profile = "", PageName = "", FolderName = "";
        public int Page, Pages = 1, FolderPage, FolderPages;
        public uint[][] Faces = new uint[Keys][];
        public int[] FaceVersion = new int[Keys];
        public int FaceW = 68, FaceH = 68;
        public Dictionary<string, Control> Controls = new();
        public string Echo;                 // the last physical input on the dock ("key3", "enc1"...)
        public double EchoAt = -10;

        public Snapshot Copy()
        {
            var s = (Snapshot)MemberwiseClone();
            s.Faces = (uint[][])Faces.Clone();
            s.FaceVersion = (int[])FaceVersion.Clone();
            return s;
        }
    }

    readonly object _lock = new();
    readonly Snapshot _s = new();
    readonly ConcurrentQueue<string> _out = new();
    readonly AutoResetEvent _wake = new(false);
    Thread _thread;
    volatile bool _stop, _active;
    volatile bool _present, _oldPresent;
    // what hello said about the device JPEGs
    int _rot = 90, _dx, _dy;
    bool _mirror;

    // set every tick: VR is on and something shows the dock (its panel or a watch widget)
    public bool Active
    {
        get => _active;
        set
        {
            if (value == _active) return;
            _active = value;
            if (_thread == null && value) Start();
            _wake.Set();
        }
    }

    // Hexpad's mirror pipe exists (checked every few seconds while VR runs, without connecting)
    public bool Present => _present;
    public bool Connected => Get().State is LinkState.Ok or LinkState.Unplugged or LinkState.ScreenOff;

    public Snapshot Get() { lock (_lock) return _s.Copy(); }
    public int Version { get { lock (_lock) return _s.Version; } }

    // started with the app: while nothing shows the dock it only looks for Hexpad's pipe every 3 s
    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "hexpad link" };
        _thread.Start();
    }

    // ---- what we send ----
    public void Input(string id, bool down) => Send($"{{\"t\":\"input\",\"id\":\"{id}\",\"down\":{(down ? "true" : "false")}}}");
    public void Turn(string id, int dir) => Send($"{{\"t\":\"input\",\"id\":\"{id}\",\"turn\":{(dir > 0 ? 1 : -1)}}}");
    public void Wake() => Send("{\"t\":\"wake\"}");

    void Send(string line)
    {
        if (!_active) return;
        _out.Enqueue(line);
        _wake.Set();
    }

    // ---- the worker ----
    static bool PipeExists(string name)
    {
        try
        {
            foreach (var p in Directory.EnumerateFiles(@"\\.\pipe\"))
                if (p.EndsWith(name, StringComparison.OrdinalIgnoreCase) && p.Length >= name.Length + 9 && p[^(name.Length + 1)] == '\\')
                    return true;
        }
        catch { }
        return false;
    }

    void SetState(LinkState st)
    {
        lock (_lock)
        {
            if (_s.State == st) return;
            _s.State = st;
            _s.Version++;
        }
    }

    void Run()
    {
        double nextScan = 0;
        while (!_stop)
        {
            double now = Clock.Now;
            if (now >= nextScan)
            {
                nextScan = now + 3;
                _present = PipeExists(PipeName);
                _oldPresent = !_present && PipeExists(OldPipe);
            }
            if (!_active || !_present)
            {
                while (_out.TryDequeue(out _)) { }
                SetState(!_active ? LinkState.Off : _oldPresent ? LinkState.OldHexpad : LinkState.NotRunning);
                _wake.WaitOne(_active ? 1000 : 3000);
                continue;
            }
            try
            {
                SetState(LinkState.Connecting);
                Session();
            }
            catch (Exception e) when (e is IOException or TimeoutException or ObjectDisposedException or UnauthorizedAccessException
                                         or JsonException or InvalidOperationException or AggregateException)
            {
                Log.Info("hexpad link: " + e.Message);
            }
            catch (Exception e)
            {
                Log.Warn("hexpad link: " + e);
            }
            if (!_stop) { SetState(LinkState.Connecting); _wake.WaitOne(2000); nextScan = 0; }
        }
        SetState(LinkState.Off);
    }

    // one connection: read lines, write what is queued, until Hexpad goes away or we are not needed any more
    void Session()
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        pipe.Connect(1500);
        var hello = Encoding.UTF8.GetBytes($"{{\"t\":\"hello\",\"client\":\"Orbit\",\"ver\":\"{Updater.Current.ToString(3)}\"}}\n");
        pipe.Write(hello, 0, hello.Length);
        Log.Info("hexpad link: connected");
        var buf = new byte[64 * 1024];
        var line = new MemoryStream();
        Task<int> read = null;
        while (!_stop && _active)
        {
            read ??= pipe.ReadAsync(buf, 0, buf.Length);
            if (read.Wait(30))
            {
                int n = read.Result;
                read = null;
                if (n <= 0) throw new IOException("Hexpad closed the pipe");
                for (int i = 0; i < n; i++)
                {
                    if (buf[i] != (byte)'\n') { line.WriteByte(buf[i]); continue; }
                    if (line.Length > 0) Handle(line.GetBuffer().AsSpan(0, (int)line.Length));
                    line.SetLength(0);
                }
                if (line.Length > 16 * 1024 * 1024) throw new IOException("line too long");
            }
            while (_out.TryDequeue(out var msg))
            {
                var b = Encoding.UTF8.GetBytes(msg + "\n");
                pipe.Write(b, 0, b.Length);
            }
        }
        Log.Info("hexpad link: closed (not needed)");
    }

    void Handle(ReadOnlySpan<byte> json)
    {
        using var doc = JsonDocument.Parse(json.ToArray());
        var m = doc.RootElement;
        string t = m.TryGetProperty("t", out var tv) ? tv.GetString() : null;
        switch (t)
        {
            case "hello":
                _rot = Int(m, "rot", 90);
                _mirror = m.TryGetProperty("mirror", out var mv) && mv.ValueKind == JsonValueKind.True;
                if (m.TryGetProperty("shift", out var sh) && sh.ValueKind == JsonValueKind.Array && sh.GetArrayLength() == 2)
                {
                    _dx = sh[0].GetInt32();
                    _dy = sh[1].GetInt32();
                }
                break;
            case "state":
                lock (_lock)
                {
                    bool conn = Bool(m, "connected"), disp = Bool(m, "display_on", true);
                    _s.State = !conn ? LinkState.Unplugged : !disp ? LinkState.ScreenOff : LinkState.Ok;
                    _s.Ambient = Bool(m, "ambient");
                    _s.Profile = Str(m, "profile");
                    _s.Page = Int(m, "page", 0);
                    _s.Pages = Math.Max(1, Int(m, "pages", 1));
                    _s.PageName = Str(m, "page_name");
                    _s.InFolder = m.TryGetProperty("folder", out var f) && f.ValueKind == JsonValueKind.String;
                    _s.FolderName = Str(m, "folder_name");
                    _s.FolderPage = Int(m, "folder_page", 0);
                    _s.FolderPages = Int(m, "folder_pages", 0);
                    _s.Version++;
                }
                break;
            case "controls":
                var ctl = new Dictionary<string, Control>();
                foreach (var p in m.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Object) continue;
                    var v = p.Value;
                    ctl[p.Name] = new Control
                    {
                        Label = Str(v, "label"), Left = Str(v, "left"), Right = Str(v, "right"), Push = Str(v, "push"),
                        Danger = Bool(v, "danger"),
                    };
                }
                lock (_lock) { _s.Controls = ctl; _s.Version++; }
                break;
            case "faces":
                bool dark = Bool(m, "dark");
                var faces = new List<(int, uint[])>();
                if (m.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Object)
                    foreach (var k in keys.EnumerateObject())
                    {
                        if (!int.TryParse(k.Name, out int i) || i < 0 || i >= Keys) continue;
                        faces.Add((i, k.Value.ValueKind == JsonValueKind.String ? Decode(k.Value.GetString(), out _, out _) : null));
                    }
                lock (_lock)
                {
                    foreach (var (i, px) in faces) { _s.Faces[i] = px; _s.FaceVersion[i]++; }
                    _s.Dark = dark;
                    _s.Version++;
                }
                break;
            case "input":
                lock (_lock)
                {
                    _s.Echo = Str(m, "id");
                    _s.EchoAt = Clock.Now;
                    _s.Version++;
                }
                break;
        }
    }

    // the device JPEG back to the face Hexpad drew: undo the 90 degree turn, the mirror and the calibration shift
    uint[] Decode(string b64, out int w, out int h)
    {
        w = h = 0;
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(b64));
            using var img = new Bitmap(ms);
            img.RotateFlip(_rot switch { 90 => RotateFlipType.Rotate270FlipNone, 180 => RotateFlipType.Rotate180FlipNone,
                270 => RotateFlipType.Rotate90FlipNone, _ => RotateFlipType.RotateNoneFlipNone });
            if (_mirror) img.RotateFlip(RotateFlipType.RotateNoneFlipX);
            w = img.Width; h = img.Height;
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                g.DrawImageUnscaled(img, -_dx, -_dy);
            }
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var px = new uint[w * h];
            unsafe
            {
                fixed (uint* dst = px)
                    for (int y = 0; y < h; y++)
                        Buffer.MemoryCopy((byte*)data.Scan0 + y * data.Stride, dst + y * w, w * 4L, w * 4L);
            }
            bmp.UnlockBits(data);
            lock (_lock) { _s.FaceW = w; _s.FaceH = h; }
            return px;
        }
        catch (Exception e)
        {
            Log.Info("hexpad link: bad face: " + e.Message);
            return null;
        }
    }

    static string Str(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static int Int(JsonElement e, string n, int def) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    static bool Bool(JsonElement e, string n, bool def = false) =>
        e.TryGetProperty(n, out var v) ? v.ValueKind == JsonValueKind.True || (v.ValueKind != JsonValueKind.False && def) : def;

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        _thread?.Join(2000);
        _wake.Dispose();
    }
}
