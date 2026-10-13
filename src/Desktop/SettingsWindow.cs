using System;
using System.Drawing;
using System.Windows.Forms;
using Vortice.Direct2D1;
using Vortice.DXGI;

namespace Orbit;

// Desktop settings window: a DXGI swap chain that the shared SettingsView draws into.
// Closing it only hides it (Orbit keeps running in the tray).
public sealed class SettingsWindow : Form
{
    readonly Gpu _g;
    readonly Painter _p;
    readonly Ui _ui;
    public readonly SettingsView View;
    readonly Action _beforeDraw;
    readonly PointerState _ptr = new();
    IDXGISwapChain1 _swap;
    ID2D1Bitmap1 _target;
    public bool Dirty = true;
    public bool AllowClose;
    double _nextRefresh;

    public SettingsWindow(Gpu g, Painter p, SettingsView view, Action beforeDraw, Icon icon)
    {
        _g = g; _p = p; View = view; _beforeDraw = beforeDraw;
        _ui = new Ui(p);
        Text = "Orbit";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0x15, 0x17, 0x1C);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        float s = DeviceDpi / 96f;
        ClientSize = new Size((int)(SettingsView.W * s), (int)(SettingsView.H * s));
    }

    float DpiScale => DeviceDpi / 96f;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
        CreateSwapChain();
    }

    void CreateSwapChain()
    {
        var desc = new SwapChainDescription1
        {
            Width = (uint)Math.Max(1, ClientSize.Width), Height = (uint)Math.Max(1, ClientSize.Height),
            Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput, BufferCount = 2,
            SwapEffect = SwapEffect.FlipDiscard, Scaling = Scaling.Stretch, AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
        };
        _swap = _g.Dxgi.CreateSwapChainForHwnd(_g.Device, Handle, desc, null, null);
        _g.Dxgi.MakeWindowAssociation(Handle, WindowAssociationFlags.IgnoreAll);
    }

    void EnsureTarget()
    {
        if (_target != null) return;
        using var surf = _swap.GetBuffer<IDXGISurface>(0);
        _target = _g.CreateTargetBitmap(surf, false);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ClientSize = new Size((int)(SettingsView.W * DpiScale), (int)(SettingsView.H * DpiScale));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_swap == null || ClientSize.Width < 1 || ClientSize.Height < 1) return;
        _target?.Dispose(); _target = null;
        _swap.ResizeBuffers(2, (uint)ClientSize.Width, (uint)ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
        Dirty = true;
    }

    protected override void OnPaint(PaintEventArgs e) { Dirty = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_ptr.Down && (e.Button & MouseButtons.Left) == 0) CancelDrag();     // the up was never delivered
        _ptr.Move(e.X, e.Y);
        if (_ui.NeedsRedrawForMove(_ptr, DpiScale)) Dirty = true;
    }

    // Alt+Tab / another window taking the mouse mid-drag: no MouseUp comes, so the slider would stay
    // grabbed. Drop the press without a click.
    void CancelDrag()
    {
        _ptr.Down = false;
        _ptr.PressedLatch = false;
        Dirty = true;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (_ptr.Down && !Capture) CancelDrag();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (_ptr.Down) CancelDrag();
    }
    protected override void OnMouseLeave(EventArgs e) { if (!_ptr.Down) _ptr.Inside = false; Dirty = true; }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _ptr.Move(e.X, e.Y); _ptr.Press(); Capture = true; Dirty = true;
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _ptr.Move(e.X, e.Y); _ptr.Release(); Capture = false; Dirty = true;
    }
    protected override void OnMouseWheel(MouseEventArgs e) { _ptr.Wheel += e.Delta / 120f; Dirty = true; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    public void ShowAndFocus()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        Dirty = true;
    }

    // Called from the main loop. Redraws on input, otherwise twice a second for live values.
    public void Tick(double now)
    {
        if (!Visible || WindowState == FormWindowState.Minimized || _swap == null) return;
        if (!Dirty && now < _nextRefresh) return;
        Dirty = false;
        _nextRefresh = now + 0.5;
        _beforeDraw?.Invoke();
        EnsureTarget();
        _ui.Begin(_ptr, DpiScale);
        _p.Begin(_target, Theme.Bg, DpiScale);
        View.Draw(_ui);
        _p.End();
        _ui.End();
        _swap.Present(0, PresentFlags.None);
        if (_ui.Interacting || _ui.Changed || _ui.Animating || _ui.Pressed || _ui.Released) Dirty = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _target?.Dispose(); _swap?.Dispose(); }
        base.Dispose(disposing);
    }
}
