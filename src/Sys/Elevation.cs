using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Orbit;

// Running elevated lets the VR mouse/keyboard reach admin windows (Windows UIPI blocks a normal
// process from sending input to them). To avoid a UAC prompt on every start, the first elevated run
// registers a scheduled task "Orbit (admin)" (on-demand only, no trigger); later starts from the
// shortcut or from SteamVR just run that task, which starts Orbit elevated without asking.
static class Elevation
{
    public const string TaskName = "Orbit (admin)";

    public static bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    static string Exe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Orbit.exe");

    static dynamic Service()
    {
        dynamic s = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
        s.Connect();
        return s;
    }

    static dynamic GetTask(string name)
    {
        try { return Service().GetFolder("\\").GetTask(name); }
        catch { return null; }
    }

    // true when the task exists and starts THIS exe (not an old copy somewhere else)
    public static bool TaskReady(string name = TaskName)
    {
        try
        {
            dynamic t = GetTask(name);
            if (t == null) return false;
            string xml = t.Xml;
            return xml.Contains(Exe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // Starts Orbit through the task; 'args' reach the new process after --from-task.
    public static bool RunTask(string args, string name = TaskName)
    {
        try
        {
            if (!TaskReady(name)) return false;
            dynamic t = GetTask(name);
            t.Run(string.IsNullOrEmpty(args) ? "--show" : args);
            Log.Info($"started through task '{name}' with '{args}'");
            return true;
        }
        catch (Exception e) { Log.Warn("run task: " + e.Message); return false; }
    }

    // Needs an elevated process when highest = true.
    public static bool InstallTask(bool highest = true, string name = TaskName)
    {
        string esc(string s) => System.Security.SecurityElement.Escape(s);
        string xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Starts Orbit (SteamVR overlay) with administrator rights so its VR mouse and keyboard can reach admin windows. Created by Orbit; removed when 'Run as administrator' is switched off.</Description>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>{(highest ? "HighestAvailable" : "LeastPrivilege")}</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{esc(Exe)}</Command>
      <Arguments>--from-task $(Arg0)</Arguments>
      <WorkingDirectory>{esc(Path.GetDirectoryName(Exe))}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        try
        {
            dynamic folder = Service().GetFolder("\\");
            folder.RegisterTask(name, xml, 6 /* TASK_CREATE_OR_UPDATE */, null, null, 3 /* TASK_LOGON_INTERACTIVE_TOKEN */, null);
            Log.Info($"task '{name}' registered for {Exe}");
            return true;
        }
        catch (Exception e) { Log.Warn("register task: " + e.Message); return false; }
    }

    public static void RemoveTask(string name = TaskName)
    {
        try { Service().GetFolder("\\").DeleteTask(name, 0); Log.Info($"task '{name}' removed"); }
        catch (Exception e) { Log.Warn("remove task: " + e.Message); }
    }

    // One UAC prompt: starts an elevated copy that installs the task. False if the user said no.
    public static bool RelaunchWithPrompt(string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Exe, args) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(Exe) });
            return true;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            Log.Warn("elevation declined");
            return false;
        }
        catch (Exception e) { Log.Warn("elevate: " + e.Message); return false; }
    }
}

// Named mutex/events that a normal and an elevated Orbit can both use (an elevated process's
// objects are otherwise high-integrity and a normal --exit / second start could not signal them).
static class NamedObjects
{
    const string Sddl = "D:(A;;GA;;;WD)(A;;GA;;;SY)(A;;GA;;;BA)S:(ML;;NW;;;LW)";
    const uint SYNCHRONIZE = 0x00100000, EVENT_MODIFY_STATE = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint rev, out IntPtr sd, IntPtr size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateMutexW(ref SECURITY_ATTRIBUTES sa, bool owner, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenMutexW(uint access, bool inherit, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateEventW(ref SECURITY_ATTRIBUTES sa, bool manual, bool initial, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenEventW(uint access, bool inherit, string name);
    [DllImport("kernel32.dll")] static extern bool SetEvent(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);

    static SECURITY_ATTRIBUTES Attributes(out IntPtr sd)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, 1, out sd, IntPtr.Zero)) sd = IntPtr.Zero;
        return new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), lpSecurityDescriptor = sd };
    }

    static IntPtr _mutex;

    // Takes the single-instance mutex for this process' lifetime; false if another Orbit holds it.
    public static bool TryTakeInstance(string name)
    {
        var sa = Attributes(out var sd);
        try
        {
            IntPtr h = CreateMutexW(ref sa, false, name);
            int err = Marshal.GetLastWin32Error();
            if (h == IntPtr.Zero) return err != 5 && err != 183;   // access denied = exists (other integrity level)
            if (err == 183) { CloseHandle(h); return false; }     // ERROR_ALREADY_EXISTS
            _mutex = h;
            return true;
        }
        finally { if (sd != IntPtr.Zero) LocalFree(sd); }
    }

    public static bool InstanceExists(string name)
    {
        IntPtr h = OpenMutexW(SYNCHRONIZE, false, name);
        if (h != IntPtr.Zero) { CloseHandle(h); return true; }
        return Marshal.GetLastWin32Error() == 5;
    }

    public static EventWaitHandle CreateEvent(string name)
    {
        var sa = Attributes(out var sd);
        try
        {
            IntPtr h = CreateEventW(ref sa, false, false, name);
            if (h == IntPtr.Zero) return new EventWaitHandle(false, EventResetMode.AutoReset);   // unnamed fallback
            var ev = new EventWaitHandle(false, EventResetMode.AutoReset);
            ev.SafeWaitHandle = new SafeWaitHandle(h, true);
            return ev;
        }
        finally { if (sd != IntPtr.Zero) LocalFree(sd); }
    }

    public static bool Signal(string name)
    {
        IntPtr h = OpenEventW(EVENT_MODIFY_STATE, false, name);
        if (h == IntPtr.Zero) return false;
        bool ok = SetEvent(h);
        CloseHandle(h);
        return ok;
    }
}
