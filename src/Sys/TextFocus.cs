using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Accessibility;

namespace Orbit;

// Tells whether the keyboard focus (anywhere in Windows) is in an editable text field, for the
// pop-up VR keyboard. A WinEvent hook (EVENT_OBJECT_FOCUS) runs on its own thread with a message
// loop. Win32 edit controls are recognised by their window class; everything else (browsers,
// Discord, Windows apps) by the accessibility role "editable text" - but only while InspectFor is
// active (right after a click from VR), because asking a Chromium app for its accessibility tree
// switches that tree on, and that costs the app some speed.
public sealed class TextFocusWatcher : IDisposable
{
    delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    delegate void TimerProc(IntPtr hwnd, uint msg, IntPtr id, uint time);

    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint ms, TimerProc proc);
    [DllImport("user32.dll")] static extern bool KillTimer(IntPtr hwnd, IntPtr id);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder s, int n);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("oleacc.dll")]
    static extern int AccessibleObjectFromEvent(IntPtr hwnd, int idObject, int idChild,
        [MarshalAs(UnmanagedType.Interface)] out IAccessible acc, out object child);

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; public uint lPrivate; }

    const uint EVENT_OBJECT_FOCUS = 0x8005, WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2, WM_QUIT = 0x12;
    const int ROLE_SYSTEM_TEXT = 0x2A, STATE_SYSTEM_UNAVAILABLE = 0x1, STATE_SYSTEM_READONLY = 0x40;
    const long ES_READONLY = 0x800;
    const int GWL_STYLE = -16;

    readonly object _lock = new();
    int _seq, _state = -1;      // 1 = text field, 0 = something else, -1 = not known (not inspected)
    double _at;
    long _inspectUntilMs;
    Thread _thread;
    uint _tid;
    WinEventProc _proc;         // kept referenced: the hook calls into it
    TimerProc _timerProc;
    (IntPtr hwnd, int obj, int child, int ticket) _recheck;
    // Classify's cross-process calls pump messages, so a newer focus event can run inside an older
    // one (re-entrant hook). Every event takes a ticket; only the newest one may report.
    int _ticket;

    readonly bool _skipOwn;
    public TextFocusWatcher(bool skipOwnProcess = true) { _skipOwn = skipOwnProcess; }

    public void Start()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "focus watch" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    void Loop()
    {
        _tid = GetCurrentThreadId();
        _proc = OnFocus;
        _timerProc = OnRecheck;
        var hook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero, _proc, 0, 0,
            WINEVENT_OUTOFCONTEXT | (_skipOwn ? WINEVENT_SKIPOWNPROCESS : 0));
        if (hook == IntPtr.Zero) { Log.Warn("focus watch: SetWinEventHook failed"); return; }
        while (GetMessage(out var m, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref m);
            DispatchMessage(ref m);
        }
        UnhookWinEvent(hook);
    }

    // look closely at focus changes until 'seconds' from now
    public void InspectFor(double seconds)
    {
        long until = (long)((Clock.Now + seconds) * 1000);
        long cur = Interlocked.Read(ref _inspectUntilMs);
        if (until > cur) Interlocked.Exchange(ref _inspectUntilMs, until);
    }

    bool Inspecting => Clock.Now * 1000 < Interlocked.Read(ref _inspectUntilMs);

    public (int seq, int state, double at) Snapshot()
    {
        lock (_lock) return (_seq, _state, _at);
    }

    void Report(int state)
    {
        lock (_lock) { _seq++; _state = state; _at = Clock.Now; }
    }

    public Action<string> Trace;     // dev: every focus event and how it was classified

    void OnFocus(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        int me = ++_ticket;
        _recheck = default;                       // focus moved on: an older pending recheck is void
        int st = Classify(hwnd, idObject, idChild, Inspecting, out bool vague);
        Trace?.Invoke($"focus event: {ClassOf(hwnd)} obj={idObject} child={idChild} -> {st}{(vague ? " (vague)" : "")}{(me != _ticket ? " (superseded)" : "")}");
        if (me != _ticket) return;                // a newer event arrived meanwhile and reported for itself
        Report(st);
        // a browser often reports focus on its whole page first, and builds its accessibility tree
        // only once asked: look again shortly
        if (vague && Inspecting)
        {
            _recheck = (hwnd, idObject, idChild, me);
            SetTimer(IntPtr.Zero, IntPtr.Zero, 350, _timerProc);
        }
    }

    void OnRecheck(IntPtr hwnd, uint msg, IntPtr id, uint time)
    {
        KillTimer(IntPtr.Zero, id);
        var (h, o, c, t) = _recheck;
        _recheck = default;
        if (h == IntPtr.Zero || t != _ticket) return;
        int st = Classify(h, o, c, true, out _);
        if (st == 1 && t == _ticket) Report(1);
    }

    static string ClassOf(IntPtr h)
    {
        var sb = new StringBuilder(128);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    // vague = the focus is on a document / window whose focused element could not be found (yet)
    internal static int Classify(IntPtr hwnd, int idObject, int idChild, bool deep, out bool vague)
    {
        vague = false;
        try
        {
            string cls = ClassOf(hwnd);
            if (cls == "Edit" || cls.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase))
                return ((long)GetWindowLongPtr(hwnd, GWL_STYLE) & ES_READONLY) == 0 ? 1 : 0;
            if (!deep) return -1;
            if (AccessibleObjectFromEvent(hwnd, idObject, idChild, out var acc, out object child) != 0 || acc == null) return -1;
            var got = new List<IAccessible> { acc };
            try
            {
                for (int depth = 0; depth < 5; depth++)
                {
                    int role = acc.get_accRole(child) is int r ? r : 0;
                    int state = acc.get_accState(child) is int s ? s : 0;
                    if (role == ROLE_SYSTEM_TEXT)
                        return (state & (STATE_SYSTEM_READONLY | STATE_SYSTEM_UNAVAILABLE)) == 0 ? 1 : 0;
                    if (child is int c && c != 0) break;               // a simple child that is not text
                    object f;
                    try { f = acc.accFocus; } catch { f = null; }
                    if (f is IAccessible inner && !ReferenceEquals(inner, acc)) { got.Add(inner); acc = inner; child = 0; continue; }
                    if (f is int fc && fc != 0) { child = fc; continue; }
                    // a page / window / pane with nothing focused inside (yet)
                    vague = f == null && role is 0x09 or 0x0A or 0x0E or 0x0F or 0x10 or 0x14;
                    break;
                }
                return 0;
            }
            finally
            {
                foreach (var a in got)
                    try { Marshal.ReleaseComObject(a); } catch { }
            }
        }
        catch
        {
            return -1;      // the app went away or did not answer
        }
    }

    public void Dispose()
    {
        if (_tid != 0) PostThreadMessage(_tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(500);
    }
}
