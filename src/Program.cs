using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Orbit;

static class Program
{
    public const string MutexName = @"Local\OrbitOverlaySingleton";

    // Modes:
    //   (none)            normal run: tray + settings window, overlays while SteamVR runs
    //   --steamvr         launched by SteamVR auto-start: start hidden in the tray
    //   --exit            ask the running Orbit to quit
    //   --no-elevate      run without admin rights this time even if 'Run as administrator' is on
    //   --from-task / --install-task / --restart   internal (elevated start, see Elevation)
    //   --snapshot <dir>  render every panel to PNG (no VR needed) and exit
    //   --selftest        connect to a running SteamVR, check overlay/UV conventions, exit
    //   --scene [--dash]  dev: act as a running game for 12 s (closes the dashboard; --dash opens Orbit's tab)
    //   --osctest <port>  dev: send sample OSC messages
    //   --tasktest        dev: check the scheduled-task start path with a temporary, non-elevated task
    //   --filtertest      dev: laser smoothing jitter/lag per level
    //   --focustest       dev: text-field detection on an off-screen test window
    //   --leaktest <gpu|media|idle> [seconds]   dev: private memory of one background component
    //   --raytest         dev: LocalRaycast vs SteamVR's ComputeOverlayIntersection on a hidden overlay
    //   --updatetest      dev: GitHub release check + download + verify (does not install)
    //   --setup [--install-dir <dir>] [--no-config] [--quiet]   installer (also when the exe name contains "setup")
    //   (setup) --update --install-dir <dir>   started by "Check for updates": silent install over that copy, restart
    //   --uninstall [--quiet]   remove the installed copy (used by Settings > Apps)
    // set by the installer's --update when it starts the new version (shown as "updated from ...")
    public static string UpdatedFrom;

    [STAThread]
    static int Main(string[] args)
    {
        int uf = Array.IndexOf(args, "--updated-from");
        if (uf >= 0 && uf + 1 < args.Length) UpdatedFrom = args[uf + 1];
        bool has(string a) => args.Contains(a);
        bool tool = has("--snapshot") || has("--faceshot") || has("--selftest") || has("--scene") || has("--osctest") || has("--tasktest") || has("--filtertest") || has("--focustest") || has("--leaktest") || has("--raytest") || has("--updatetest") || has("--wintest") || has("--docktest") || has("--roomtest") || has("--theatertest") || has("--perfbench") || has("--ovdump") || has("--pacetest") || has("--pacertest") || has("--capturetest");
        if (tool) return RunTool(args);

        // install / uninstall (the same exe: named "OrbitSetup.exe" it is the installer)
        if (has("--remove-task"))
            return Installer.RemoveTaskFor(args[Array.IndexOf(args, "--remove-task") + 1]);
        if (has("--uninstall"))
        {
            ApplicationConfiguration.Initialize();
            return Installer.Uninstall(has("--quiet"), !has("--no-stop"));
        }
        if (Installer.IsSetupMode(args))
        {
            ApplicationConfiguration.Initialize();
            return Installer.RunSetup(args);
        }

        if (has("--exit"))
        {
            NamedObjects.Signal(OrbitApp.ExitEventName);
            return 0;
        }

        bool hidden = has("--steamvr");
        bool restart = has("--restart");
        if (!restart && NamedObjects.InstanceExists(MutexName))
        {
            // already running: bring its settings window up (unless SteamVR started us)
            if (!hidden) NamedObjects.Signal(OrbitApp.ShowEventName);
            return 0;
        }

        ApplicationConfiguration.Initialize();

        bool wantAdmin = new ConfigStore().Data.General.RunAsAdmin;
        if (wantAdmin && !Elevation.IsElevated && !has("--from-task") && !has("--no-elevate"))
        {
            Log.Init("orbit-launch.log");
            string pass = (hidden ? "--steamvr" : "--show") + (restart ? " --restart" : "") +
                          (UpdatedFrom != null ? " --updated-from " + UpdatedFrom : "");
            if (Elevation.RunTask(pass)) return 0;
            // no task yet (first run, or the exe moved): one UAC prompt, the elevated copy installs it.
            // Not from SteamVR though - a prompt on the desktop while in VR helps nobody.
            if (!hidden && Elevation.RelaunchWithPrompt("--install-task " + pass)) return 0;
            Log.Warn("continuing without admin rights");
        }

        if (restart)
        {
            // the previous instance is shutting down; wait for it to let go
            for (int i = 0; i < 100 && NamedObjects.InstanceExists(MutexName); i++) Thread.Sleep(100);
        }
        if (!NamedObjects.TryTakeInstance(MutexName))
        {
            if (!hidden) NamedObjects.Signal(OrbitApp.ShowEventName);
            return 0;
        }

        Log.Init("orbit.log");
        AppFiles.EnsureNextToExe();    // SteamVR manifest + input bindings next to the exe
        bool elevated = Elevation.IsElevated;
        Log.Info($"Orbit {typeof(Program).Assembly.GetName().Version} start, args: {string.Join(' ', args)}, elevated: {elevated}");
        try
        {
            if (elevated && wantAdmin && (has("--install-task") || !Elevation.TaskReady()))
                Elevation.InstallTask();

            using var app = new OrbitApp(startHidden: hidden);
            app.Run();
            return 0;
        }
        catch (Exception e)
        {
            Log.Error("fatal: " + e);
            MessageBox.Show(e.Message, "OrbitOverlay crashed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static int RunTool(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Log.Init("orbit-tool.log");
        Log.Info($"Orbit {typeof(Program).Assembly.GetName().Version} tool, args: {string.Join(' ', args)}, elevated: {Elevation.IsElevated}");
        try
        {
            if (args.Contains("--snapshot"))
            {
                int i = Array.IndexOf(args, "--snapshot");
                return Tools.Snapshot(i + 1 < args.Length ? args[i + 1] : "snapshot");
            }
            if (args.Contains("--faceshot"))
            {
                int i = Array.IndexOf(args, "--faceshot");
                return Tools.FaceShot(i + 1 < args.Length ? args[i + 1] : "faceshot");
            }
            if (args.Contains("--roomtest"))
            {
                int i = Array.IndexOf(args, "--roomtest");
                return Tools.RoomTest(i + 1 < args.Length ? args[i + 1] : "roomtest");
            }
            if (args.Contains("--capturetest"))
                return CaptureTest.Run();
            if (args.Contains("--pacertest"))
                return PaceTest.Pacer();
            if (args.Contains("--pacetest"))
            {
                int i = Array.IndexOf(args, "--pacetest");
                return PaceTest.Run(i + 1 < args.Length ? double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : 20);
            }
            if (args.Contains("--ovdump"))
            {
                int i = Array.IndexOf(args, "--ovdump");
                return OverlayDump.Run(i + 1 < args.Length ? double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : 60);
            }
            if (args.Contains("--perfbench"))
            {
                int i = Array.IndexOf(args, "--perfbench");
                return PerfBench.Run(i + 1 < args.Length ? args[i + 1] : "perfbench");
            }
            if (args.Contains("--theatertest"))
            {
                int i = Array.IndexOf(args, "--theatertest");
                return TheaterTest.Run(i + 1 < args.Length ? args[i + 1] : "theatertest");
            }
            if (args.Contains("--docktest"))
            {
                int i = Array.IndexOf(args, "--docktest");
                return Tools.DockTest(i + 1 < args.Length ? args[i + 1] : "docktest");
            }
            if (args.Contains("--wintest"))
            {
                int i = Array.IndexOf(args, "--wintest");
                return Tools.WinTest(i + 1 < args.Length ? args[i + 1] : "wintest");
            }
            if (args.Contains("--osctest"))
                return Tools.OscTest(int.Parse(args[Array.IndexOf(args, "--osctest") + 1]));
            if (args.Contains("--scene"))
                return Tools.FakeScene(args.Contains("--dash"));
            if (args.Contains("--tasktest"))
                return Tools.TaskTest();
            if (args.Contains("--filtertest"))
                return Tools.FilterTest();
            if (args.Contains("--focustest"))
                return Tools.FocusTest();
            if (args.Contains("--raytest"))
                return RayTest.Run();
            if (args.Contains("--updatetest"))
                return Updater.Test();
            if (args.Contains("--leaktest"))
            {
                int li = Array.IndexOf(args, "--leaktest");
                string what = li + 1 < args.Length ? args[li + 1] : "idle";
                int secs = li + 2 < args.Length && int.TryParse(args[li + 2], out int s2) ? s2 : 60;
                return Tools.LeakTest(what, secs);
            }
            return Tools.SelfTest();
        }
        catch (Exception e)
        {
            Log.Error("tool failed: " + e);
            return 1;
        }
    }
}
