using System;
using System.Runtime.InteropServices;

namespace Orbit;

// CPU and memory load for the watch's widgets: GetSystemTimes deltas (the total Task Manager shows) and
// GlobalMemoryStatusEx. Sampled at most once a second, and only while a widget on the watch shows them.
public sealed class SystemStats
{
    [StructLayout(LayoutKind.Sequential)]
    struct FILETIME { public uint Lo, Hi; public ulong V => ((ulong)Hi << 32) | Lo; }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    ulong _idle, _kernel, _user;
    double _next;
    public float Cpu = -1;                       // 0..1, -1 = not measured yet
    public const int History = 60;               // the last minute of CPU load (for the opened widget)
    public readonly float[] CpuHistory = new float[History];
    public int CpuCount, CpuHead;
    public float RamUsedGB = -1, RamTotalGB = -1;

    public void Sample(double now)
    {
        if (now < _next) return;
        _next = now + 1;
        if (GetSystemTimes(out var i, out var k, out var u))
        {
            // kernel time includes idle time
            ulong di = i.V - _idle, busy = (k.V - _kernel) + (u.V - _user);
            if (_kernel != 0 && busy > 0)
            {
                Cpu = Math.Clamp(1f - (float)di / busy, 0, 1);
                CpuHistory[CpuHead] = Cpu;
                CpuHead = (CpuHead + 1) % History;
                CpuCount = Math.Min(History, CpuCount + 1);
            }
            _idle = i.V; _kernel = k.V; _user = u.V;
        }
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref m))
        {
            RamTotalGB = m.ullTotalPhys / 1073741824f;
            RamUsedGB = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824f;
        }
    }
}

// The default output device's master volume and mute (Core Audio), for the watch's volume widget.
// The endpoint is looked up again every few seconds (the default device can change) and released.
public sealed class SystemVolume : IDisposable
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    const int eRender = 0, eMultimedia = 1, CLSCTX_ALL = 23;
    static Guid _ctx = Guid.Empty;
    IMMDeviceEnumerator _en;
    IMMDevice _dev;
    IAudioEndpointVolume _vol;
    double _next, _nextDevice;
    public float Level = -1;          // 0..1, -1 = no output device
    public bool Muted;

    public void Sample(double now)
    {
        if (now < _next) return;
        _next = now + 0.25;
        try
        {
            if (_vol == null || now >= _nextDevice) { Acquire(); _nextDevice = now + 5; }
            if (_vol == null || _vol.GetMasterVolumeLevelScalar(out float l) != 0) { Level = -1; return; }
            Level = l;
            Muted = _vol.GetMute(out bool m) == 0 && m;
        }
        catch (Exception e)
        {
            Log.Warn("volume: " + e.Message);
            Release();
            Level = -1;
            _next = now + 5;
        }
    }

    // up / down in steps (0.05 = 5 %); unmutes on the way up
    public void Step(float delta)
    {
        if (_vol == null || Level < 0) return;
        try
        {
            float l = Math.Clamp(Level + delta, 0, 1);
            _vol.SetMasterVolumeLevelScalar(l, ref _ctx);
            if (delta > 0 && Muted) _vol.SetMute(false, ref _ctx);
            Level = l;
            _next = 0;
        }
        catch (Exception e) { Log.Warn("volume: " + e.Message); }
    }

    public void Set(float level)
    {
        if (_vol == null) return;
        try
        {
            level = Math.Clamp(level, 0, 1);
            _vol.SetMasterVolumeLevelScalar(level, ref _ctx);
            Level = level;
            _next = 0;
        }
        catch (Exception e) { Log.Warn("volume: " + e.Message); }
    }

    public void ToggleMute()
    {
        if (_vol == null) return;
        try { _vol.SetMute(!Muted, ref _ctx); Muted = !Muted; _next = 0; }
        catch (Exception e) { Log.Warn("volume: " + e.Message); }
    }

    void Acquire()
    {
        Release();
        _en ??= (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        if (_en.GetDefaultAudioEndpoint(eRender, eMultimedia, out _dev) != 0 || _dev == null) return;
        var iid = typeof(IAudioEndpointVolume).GUID;
        if (_dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object o) == 0) _vol = (IAudioEndpointVolume)o;
    }

    void Release()
    {
        if (_vol != null) { Marshal.ReleaseComObject(_vol); _vol = null; }
        if (_dev != null) { Marshal.ReleaseComObject(_dev); _dev = null; }
    }

    public void Dispose()
    {
        Release();
        if (_en != null) { Marshal.ReleaseComObject(_en); _en = null; }
    }
}
