using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Valve.VR;
using Vortice.DirectWrite;

namespace Orbit;

// Files the app needs next to its exe (SteamVR reads them by path). They are embedded in the
// assembly, so a single-file build can (re)create them wherever it runs.
static class AppFiles
{
    const string Prefix = "files/";

    static void Write(string dir, bool onlyIfDifferent)
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(Prefix)))
        {
            string path = Path.Combine(dir, name[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar));
            using var s = asm.GetManifestResourceStream(name);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            byte[] data = ms.ToArray();
            if (onlyIfDifferent && File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(data)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }

    public static void EnsureNextToExe()
    {
        try { Write(AppContext.BaseDirectory, true); }
        catch (Exception e) { Log.Warn("support files: " + e.Message); }
    }

    public static void WriteAll(string dir) => Write(dir, false);

    // relative paths of the support files (what the uninstaller may delete)
    public static string[] Names => Assembly.GetExecutingAssembly().GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix)).Select(n => n[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar)).ToArray();
}

public sealed class InstallOptions
{
    public string Dir;
    public bool DesktopShortcut = true;
    public bool StartWithSteamVR = true;
    public bool RunAsAdmin = true;
    public bool WriteConfig = true;
    public bool StopRunning = true;      // dev: --no-stop keeps a running Orbit alive while testing
    public bool Update;                  // in-app update: replace the files only, leave shortcuts and settings alone
}

// Per-user install (no admin needed): %LOCALAPPDATA%\Programs\OrbitOverlay, Start menu + desktop
// shortcuts, an entry in Settings > Apps. Started as "OrbitOverlaySetup.exe" it shows the installer;
// "OrbitOverlay.exe --uninstall" removes it again. Until 1.6 the app was "Orbit" (Orbit.exe, Orbit.lnk): installing or
// updating over such a folder moves it over (MoveFromOrbit). The ids nobody sees stay: the Apps key, the admin task,
// the settings folder %LOCALAPPDATA%\Orbit, SteamVR's app key.
static class Installer
{
    public const string ExeName = "OrbitOverlay.exe", OldExeName = "Orbit.exe";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Orbit";
    // a new install goes to Programs\OrbitOverlay; one that exists already (maybe Programs\Orbit) is updated where it is
    public static string DefaultDir =>
        Registry.CurrentUser.OpenSubKey(UninstallKey)?.GetValue("InstallLocation") as string is { Length: > 0 } reg && Directory.Exists(reg) ? reg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OrbitOverlay");
    static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "OrbitOverlay.lnk");
    static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "OrbitOverlay.lnk");
    static string OldStartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Orbit.lnk");
    static string OldDesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Orbit.lnk");

    // the app's exe in a folder: OrbitOverlay.exe, or the Orbit.exe of 1.6 and older
    static string ExeIn(string dir) => File.Exists(Path.Combine(dir, ExeName)) || !File.Exists(Path.Combine(dir, OldExeName))
        ? Path.Combine(dir, ExeName) : Path.Combine(dir, OldExeName);
    public static string Version => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static bool IsSetupMode(string[] args) =>
        args.Contains("--setup") ||
        (Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "").Contains("setup", StringComparison.OrdinalIgnoreCase)
         && !args.Contains("--uninstall"));

    public static int RunSetup(string[] args)
    {
        Log.Init("orbit-setup.log");
        Log.Info($"setup {Version} from {Environment.ProcessPath}");
        int i = Array.IndexOf(args, "--install-dir");
        var opt = new InstallOptions
        {
            Dir = i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : DefaultDir,
            WriteConfig = !args.Contains("--no-config"),
            StopRunning = !args.Contains("--no-stop"),
        };
        // an existing install keeps its choices
        var cfg = new ConfigStore();
        opt.StartWithSteamVR = cfg.Data.General.AutoStart;
        opt.RunAsAdmin = cfg.Data.General.RunAsAdmin;
        if (args.Contains("--update"))
        {
            // started by "Check for updates": replace the copy in --install-dir without asking
            // (settings, shortcuts and the Apps entry stay as they are), then start it again
            opt.WriteConfig = false;
            opt.Update = true;
            // the version being replaced, read before it is overwritten - the new Orbit announces the update
            string from = null;
            try
            {
                var old = FileVersionInfo.GetVersionInfo(ExeIn(opt.Dir)).ProductVersion;
                if (!string.IsNullOrEmpty(old)) from = old.Split('+')[0];
            }
            catch { }
            Log.Info($"update {from ?? "?"} -> {Version} in {opt.Dir}");
            try
            {
                Install(opt);
                if (args.Contains("--no-start")) { Log.Info("update installed (--no-start)"); return 0; }   // tests
                Log.Info("update installed, starting OrbitOverlay");
                var psi = new ProcessStartInfo(Path.Combine(opt.Dir, ExeName)) { UseShellExecute = false, WorkingDirectory = opt.Dir };
                if (VrSession.RuntimeRunning()) psi.ArgumentList.Add("--steamvr");
                if (from != null && from != Version) { psi.ArgumentList.Add("--updated-from"); psi.ArgumentList.Add(from); }
                Process.Start(psi);
                return 0;
            }
            catch (Exception e)
            {
                Log.Error("update failed: " + e);
                MessageBox.Show("OrbitOverlay could not be updated:\n" + e.Message, "OrbitOverlay update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
        }
        if (args.Contains("--quiet"))
        {
            Install(opt);
            return 0;
        }
        using var win = new InstallerWindow(opt);
        Application.Run(win);
        return 0;
    }

    // the folder of the installed copy, when the running exe IS that copy (null for a development folder)
    public static string InstalledCopyDir()
    {
        try
        {
            string reg = Registry.CurrentUser.OpenSubKey(UninstallKey)?.GetValue("InstallLocation") as string;
            if (string.IsNullOrEmpty(reg)) return null;
            string here = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\');
            return string.Equals(Path.GetFullPath(reg).TrimEnd('\\'), here, StringComparison.OrdinalIgnoreCase) ? here : null;
        }
        catch { return null; }
    }

    static void StopRunningOrbit()
    {
        if (!NamedObjects.InstanceExists(Program.MutexName)) return;
        Log.Info("asking the running OrbitOverlay to quit");
        NamedObjects.Signal(OrbitApp.ExitEventName);
        for (int i = 0; i < 80 && NamedObjects.InstanceExists(Program.MutexName); i++) Thread.Sleep(100);
        Thread.Sleep(300);
    }

    public static void Install(InstallOptions o)
    {
        if (o.StopRunning) StopRunningOrbit();
        Directory.CreateDirectory(o.Dir);
        string exe = Path.Combine(o.Dir, ExeName);
        for (int attempt = 0; ; attempt++)
        {
            try { File.Copy(Environment.ProcessPath, exe, true); break; }
            catch (IOException) when (attempt < 20) { Thread.Sleep(250); }
        }
        AppFiles.WriteAll(o.Dir);
        MoveFromOrbit(o.Dir, exe);

        if (o.Update)
        {
            // shortcuts stay exactly as the user has them; the Apps entry is refreshed only if it is this copy's
            using var uk = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
            if (uk?.GetValue("InstallLocation") is string loc &&
                string.Equals(Path.GetFullPath(loc).TrimEnd('\\'), Path.GetFullPath(o.Dir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                uk.SetValue("DisplayVersion", Version);
                uk.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
            Log.Info($"updated {o.Dir} to {Version}");
            return;
        }

        MakeShortcut(StartMenuLink, exe, o.Dir);
        if (o.DesktopShortcut) MakeShortcut(DesktopLink, exe, o.Dir);
        else if (LinkTarget(DesktopLink)?.StartsWith(o.Dir, StringComparison.OrdinalIgnoreCase) == true) File.Delete(DesktopLink);

        using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", "OrbitOverlay");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "OrbitOverlay");
            k.SetValue("DisplayIcon", exe + ",0");
            k.SetValue("InstallLocation", o.Dir);
            k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            k.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
        }

        if (o.WriteConfig)
        {
            var store = new ConfigStore();
            store.Data.General.AutoStart = o.StartWithSteamVR;
            store.Data.General.RunAsAdmin = o.RunAsAdmin;
            store.MarkDirty();
            store.SaveIfDue(force: true);
        }
        Log.Info($"installed to {o.Dir}");
    }

    // A folder of the "Orbit" days (1.6 and older): what started Orbit.exe now starts OrbitOverlay.exe - the admin task
    // (when we may: the in-app update runs elevated), the Start menu / desktop shortcuts (renamed OrbitOverlay.lnk), the
    // Apps entry - then the old exe and the old quick start go. SteamVR follows by itself: the manifest written next to the
    // exe names OrbitOverlay.exe, and the app registers it again when the path it finds differs.
    static void MoveFromOrbit(string dir, string exe)
    {
        string old = Path.Combine(dir, OldExeName);
        if (!File.Exists(old)) return;
        Log.Info($"moving {old} over to {exe}");
        try
        {
            if (TaskStarts(old))
            {
                if (Elevation.IsElevated) Elevation.InstallTask(true, Elevation.TaskName, exe);
                else Log.Info("admin task still starts Orbit.exe: OrbitOverlay asks once (UAC) to point it at itself");
            }
        }
        catch (Exception e) { Log.Warn("admin task: " + e.Message); }
        foreach (var (oldLink, newLink) in new[] { (OldStartMenuLink, StartMenuLink), (OldDesktopLink, DesktopLink) })
        {
            if (LinkTarget(oldLink)?.Equals(old, StringComparison.OrdinalIgnoreCase) != true) continue;
            MakeShortcut(newLink, exe, dir);
            try { File.Delete(oldLink); } catch { }
            Log.Info($"shortcut {oldLink} -> {newLink}");
        }
        using (var uk = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true))
            if (uk?.GetValue("InstallLocation") is string loc &&
                string.Equals(Path.GetFullPath(loc).TrimEnd('\\'), Path.GetFullPath(dir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                uk.SetValue("DisplayName", "OrbitOverlay");
                uk.SetValue("Publisher", "OrbitOverlay");
                uk.SetValue("DisplayIcon", exe + ",0");
                uk.SetValue("UninstallString", $"\"{exe}\" --uninstall");
                uk.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
            }
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try { File.Delete(old); break; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Thread.Sleep(250); }
        }
        try { File.Delete(Path.Combine(dir, "Orbit Quick Start.txt")); } catch { }
    }

    public static void Launch(string dir)
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(dir, ExeName)) { UseShellExecute = true, WorkingDirectory = dir }); }
        catch (Exception e) { Log.Warn("launch: " + e.Message); }
    }

    public static void OpenQuickStart(string dir)
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(dir, "OrbitOverlay Quick Start.txt")) { UseShellExecute = true }); }
        catch (Exception e) { Log.Warn("quick start: " + e.Message); }
    }

    // ---------------- uninstall ----------------

    public static int Uninstall(bool quiet, bool stopRunning = true)
    {
        Log.Init("orbit-setup.log");
        string dir = Path.GetDirectoryName(Environment.ProcessPath);
        string exe = Environment.ProcessPath;
        if (!quiet && MessageBox.Show("Remove OrbitOverlay from this PC?\n\nYour settings in %LOCALAPPDATA%\\Orbit are kept.",
                "Uninstall OrbitOverlay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return 1;
        Log.Info($"uninstall {dir}");
        string registeredDir = Registry.CurrentUser.OpenSubKey(UninstallKey)?.GetValue("InstallLocation") as string;
        if (stopRunning) StopRunningOrbit();
        UnregisterFromSteamVR(Path.Combine(dir, "orbit.vrmanifest"), exe);

        // the admin task, if it starts this copy (needs admin to delete -> one prompt)
        if (TaskStarts(exe))
        {
            Elevation.RemoveTask();
            if (TaskStarts(exe)) Elevation.RelaunchWithPrompt($"--remove-task \"{exe}\"");
        }
        foreach (var link in new[] { StartMenuLink, DesktopLink, OldStartMenuLink, OldDesktopLink })
            if (LinkTarget(link)?.Equals(exe, StringComparison.OrdinalIgnoreCase) == true)
                try { File.Delete(link); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

        // Delete only the files the installer wrote, and only in the folder it registered, so an
        // "--uninstall" from a portable copy or a shared folder can't take other files with it.
        // The exe can't delete itself: a hidden cmd removes it (and the then-empty folders) after we exit.
        if (dir != null && !string.IsNullOrEmpty(registeredDir) &&
            string.Equals(Path.GetFullPath(dir).TrimEnd('\\'), Path.GetFullPath(registeredDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            foreach (var f in AppFiles.Names)
                try { File.Delete(Path.Combine(dir, f)); } catch { }
            string assets = Path.Combine(dir, "assets");
            Process.Start(new ProcessStartInfo("cmd.exe",
                $"/c ping 127.0.0.1 -n 3 > nul & del /q \"{exe}\" & rmdir \"{assets}\" & rmdir \"{dir}\"")
            { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
        }
        else Log.Warn($"not deleting files: {dir} is not the registered install folder ({registeredDir})");
        Log.Info("uninstalled");
        if (!quiet) MessageBox.Show("OrbitOverlay has been removed.", "Uninstall OrbitOverlay", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    static bool TaskStarts(string exe)
    {
        try
        {
            dynamic s = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
            s.Connect();
            dynamic t = s.GetFolder("\\").GetTask(Elevation.TaskName);
            string xml = t.Xml;
            return xml.Contains(exe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // elevated helper started by the uninstaller
    public static int RemoveTaskFor(string exe)
    {
        Log.Init("orbit-setup.log");
        if (TaskStarts(exe)) Elevation.RemoveTask();
        return 0;
    }

    // Takes Orbit out of SteamVR's startup apps: through the API if SteamVR runs, otherwise by
    // removing the manifest path from Steam\config\appconfig.json (backed up first).
    static void UnregisterFromSteamVR(string manifest, string exe)
    {
        try
        {
            if (VrSession.RuntimeRunning())
            {
                var e = EVRInitError.None;
                OpenVR.Init(ref e, EVRApplicationType.VRApplication_Background);
                if (e == EVRInitError.None)
                {
                    try
                    {
                        var apps = OpenVR.Applications;
                        var sb = new System.Text.StringBuilder(1024);
                        var pe = EVRApplicationError.None;
                        apps.GetApplicationPropertyString(OrbitApp.AppKey, EVRApplicationProperty.BinaryPath_String, sb, (uint)sb.Capacity, ref pe);
                        if (sb.ToString().Equals(exe, StringComparison.OrdinalIgnoreCase))
                            apps.SetApplicationAutoLaunch(OrbitApp.AppKey, false);
                        Log.Info("SteamVR: remove manifest -> " + apps.RemoveApplicationManifest(manifest));
                    }
                    finally { OpenVR.Shutdown(); }
                    return;
                }
            }
            string steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
            if (string.IsNullOrEmpty(steam)) return;
            string cfg = Path.Combine(steam.Replace('/', '\\'), "config", "appconfig.json");
            if (!File.Exists(cfg)) return;
            var root = JsonNode.Parse(File.ReadAllText(cfg));
            if (root?["manifest_paths"] is not JsonArray arr) return;
            string norm(string p) => Path.GetFullPath(p.Replace('/', '\\')).TrimEnd('\\');
            var hits = arr.Where(n => n is JsonValue v && v.TryGetValue(out string p) &&
                                       string.Equals(norm(p), norm(manifest), StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 0) return;
            File.Copy(cfg, cfg + ".orbit-uninstall-bak", true);
            foreach (var h in hits) arr.Remove(h);
            File.WriteAllText(cfg, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Log.Info("removed OrbitOverlay from " + cfg);
        }
        catch (Exception ex) { Log.Warn("unregister from SteamVR: " + ex.Message); }
    }

    // ---------------- shortcuts ----------------

    static void MakeShortcut(string path, string target, string workDir)
    {
        try
        {
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            dynamic lnk = sh.CreateShortcut(path);
            lnk.TargetPath = target;
            lnk.WorkingDirectory = workDir;
            lnk.IconLocation = target + ",0";
            lnk.Description = "OrbitOverlay - SteamVR overlay";
            lnk.Save();
        }
        catch (Exception e) { Log.Warn($"shortcut {path}: {e.Message}"); }
    }

    static string LinkTarget(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            return sh.CreateShortcut(path).TargetPath;
        }
        catch { return null; }
    }
}

// The installer window, drawn with the same D2D widgets as the settings.
sealed class InstallerWindow : Form
{
    const float LW = 600, LH = 440;
    readonly InstallOptions _o;
    readonly Gpu _g;
    readonly Painter _p;
    readonly Ui _ui;
    readonly PointerState _ptr = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    Vortice.DXGI.IDXGISwapChain1 _swap;
    Vortice.Direct2D1.ID2D1Bitmap1 _target;
    enum Page { Options, Working, Done, Failed }
    Page _page = Page.Options;
    string _error;
    readonly bool _update;

    public InstallerWindow(InstallOptions o)
    {
        _o = o;
        _update = File.Exists(Path.Combine(o.Dir, Installer.ExeName)) || File.Exists(Path.Combine(o.Dir, Installer.OldExeName));
        _g = new Gpu(-1);
        Theme.Apply(new ConfigStore().Data.Appearance);
        _p = new Painter(_g);
        _ui = new Ui(_p);
        Text = _update ? "Update OrbitOverlay" : "Install OrbitOverlay";
        Icon = Tray.MakeIcon(64);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0x15, 0x17, 0x1C);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        ClientSize = new Size((int)(LW * Scale), (int)(LH * Scale));
        _timer.Tick += (_, _) => Render();
    }

    new float Scale => DeviceDpi / 96f;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        var desc = new Vortice.DXGI.SwapChainDescription1
        {
            Width = (uint)ClientSize.Width, Height = (uint)ClientSize.Height,
            Format = Vortice.DXGI.Format.B8G8R8A8_UNorm, SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
            BufferUsage = Vortice.DXGI.Usage.RenderTargetOutput, BufferCount = 2,
            SwapEffect = Vortice.DXGI.SwapEffect.FlipDiscard, Scaling = Vortice.DXGI.Scaling.Stretch,
            AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
        };
        _swap = _g.Dxgi.CreateSwapChainForHwnd(_g.Device, Handle, desc, null, null);
        using var surf = _swap.GetBuffer<Vortice.DXGI.IDXGISurface>(0);
        _target = _g.CreateTargetBitmap(surf, false);
        _timer.Start();
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnMouseMove(MouseEventArgs e) => _ptr.Move(e.X, e.Y);
    protected override void OnMouseLeave(EventArgs e) { if (!_ptr.Down) _ptr.Inside = false; }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _ptr.Move(e.X, e.Y); _ptr.Press(); Capture = true; } }
    protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _ptr.Move(e.X, e.Y); _ptr.Release(); Capture = false; } }

    void Render()
    {
        if (_target == null) return;
        _ui.Begin(_ptr, Scale);
        _p.Begin(_target, Theme.Bg, Scale);
        Draw();
        _p.End();
        _ui.End();
        _swap.Present(1, Vortice.DXGI.PresentFlags.None);
    }

    void Draw()
    {
        var p = _p;
        p.Fill(new RectangleF(0, 0, LW, 104), Theme.Panel);
        p.Line(0, 104, LW, 104, Theme.Border, 1);
        p.Logo(new RectangleF(30, 26, 52, 52));
        p.Text("OrbitOverlay", new RectangleF(96, 22, 400, 40), Theme.Text, 30, FontWeight.SemiBold);
        p.Text("v" + Installer.Version, new RectangleF(200, 30, 120, 30), Theme.Dim, 14);
        p.Text("Wrist watch, desktop mirrors and a VR keyboard for SteamVR", new RectangleF(98, 60, 480, 26), Theme.Dim, 14);

        switch (_page)
        {
            case Page.Options:
            {
                p.Text("INSTALL TO", new RectangleF(32, 122, 300, 18), Theme.Dim, 11, FontWeight.SemiBold);
                p.Text(_o.Dir, new RectangleF(32, 140, 540, 22), Theme.Text, 13);
                bool b = _o.DesktopShortcut;
                if (_ui.Toggle("i.desk", new RectangleF(22, 172, 556, 46), "Desktop shortcut", ref b)) _o.DesktopShortcut = b;
                b = _o.StartWithSteamVR;
                if (_ui.Toggle("i.vr", new RectangleF(22, 222, 556, 52), "Start with SteamVR", ref b, "OrbitOverlay starts and stops together with SteamVR")) _o.StartWithSteamVR = b;
                b = _o.RunAsAdmin;
                if (_ui.Toggle("i.adm", new RectangleF(22, 278, 556, 52), "Run as administrator", ref b,
                        "the VR mouse also works in admin windows - Windows asks once")) _o.RunAsAdmin = b;
                if (_ui.Button("i.cancel", new RectangleF(LW - 32 - 160 - 12 - 110, 376, 110, 40), "Cancel", null, false, true, 15))
                    Close();
                if (_ui.Button("i.go", new RectangleF(LW - 32 - 160, 376, 160, 40), _update ? "Update" : "Install", null, true, true, 15))
                    StartInstall();
                break;
            }
            case Page.Working:
            {
                float a = (float)(Clock.Now % 1.2 / 1.2) * MathF.PI * 2;
                p.Ring(LW / 2, 230, 22, Theme.Panel3, 5);
                for (int i = 0; i < 6; i++)
                    p.Circle(LW / 2 + 22 * MathF.Cos(a + i * 0.25f), 230 + 22 * MathF.Sin(a + i * 0.25f), 3.5f - i * 0.4f, Theme.Accent);
                _ui.Animating = true;
                p.Text("Installing…", new RectangleF(0, 270, LW, 30), Theme.Text, 16, FontWeight.Normal, TextAlignment.Center);
                break;
            }
            case Page.Done:
            {
                float s = _ui.Anim("done", 1, 8);
                _ui.Anim("done0", 0);   // first frame of the page starts small
                p.Circle(LW / 2, 168, 30 * (0.6f + 0.4f * s), Theme.Accent);
                p.Icon(Theme.IcoCheck, new RectangleF(LW / 2 - 30, 138, 60, 60), Theme.OnAccent, 26 * s);
                p.Text(_update ? "OrbitOverlay is updated" : "OrbitOverlay is installed", new RectangleF(0, 210, LW, 34), Theme.Text, 22, FontWeight.SemiBold, TextAlignment.Center);
                p.Text("Start SteamVR - OrbitOverlay comes with it (look for the tray icon).\nTurn your left wrist towards you to see the watch.",
                    new RectangleF(40, 250, LW - 80, 60), Theme.Dim, 14, FontWeight.Normal, TextAlignment.Center, ParagraphAlignment.Near, null, true);
                float bx = 32;
                if (_ui.Button("d.guide", new RectangleF(bx, 376, 150, 40), "Quick start", null, false, true, 15))
                    Installer.OpenQuickStart(_o.Dir);
                if (_ui.Button("d.close", new RectangleF(LW - 32 - 160 - 12 - 110, 376, 110, 40), "Close", null, false, true, 15))
                    Close();
                if (_ui.Button("d.launch", new RectangleF(LW - 32 - 160, 376, 160, 40), "Launch OrbitOverlay", null, true, true, 15))
                {
                    Installer.Launch(_o.Dir);
                    Close();
                }
                break;
            }
            case Page.Failed:
            {
                p.Text("Installation failed", new RectangleF(32, 140, LW - 64, 34), Theme.Bad, 20, FontWeight.SemiBold);
                p.Text(_error ?? "", new RectangleF(32, 180, LW - 64, 150), Theme.Dim, 13, FontWeight.Normal, TextAlignment.Leading, ParagraphAlignment.Near, null, true);
                if (_ui.Button("f.close", new RectangleF(LW - 32 - 110, 376, 110, 40), "Close", null, false, true, 15))
                    Close();
                break;
            }
        }
    }

    void StartInstall()
    {
        _page = Page.Working;
        var t = new Thread(() =>
        {
            try
            {
                Installer.Install(_o);
                BeginInvoke(() => _page = Page.Done);
            }
            catch (Exception e)
            {
                Log.Error("install: " + e);
                BeginInvoke(() => { _error = e.Message; _page = Page.Failed; });
            }
        }) { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);    // shortcuts go through COM
        t.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _target?.Dispose(); _swap?.Dispose();
            _p.Dispose(); _g.Dispose();
        }
        base.Dispose(disposing);
    }
}
