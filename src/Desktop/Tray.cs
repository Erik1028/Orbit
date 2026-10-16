using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Orbit;

public sealed class Tray : IDisposable
{
    readonly NotifyIcon _icon;
    public readonly Icon AppIcon;

    public Tray(Action settings, Action recenter, Action toggleDesktops, Action toggleKeyboard, Action exit)
    {
        AppIcon = MakeIcon(64);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => settings());
        menu.Items.Add("Recenter desktops", null, (_, _) => recenter());
        menu.Items.Add("Show / hide desktops", null, (_, _) => toggleDesktops());
        menu.Items.Add("Keyboard", null, (_, _) => toggleKeyboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());
        _icon = new NotifyIcon { Icon = MakeIcon(32), Text = "OrbitOverlay (SteamVR overlay)", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) settings(); };
    }

    // Erik's logo (orbitoverlay.svg, 64 x 64 units; the same picture as assets\orbit.ico, the exe's icon): a dark rounded
    // square, a cyan orbit ring and a purple moon whose dark rim cuts the ring.
    public static Icon MakeIcon(int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float u = size / 64f;
            var bg = Color.FromArgb(0x17, 0x1A, 0x24);
            float r = 16 * u;
            using var path = new GraphicsPath();
            var rc = new RectangleF(0, 0, size, size);
            path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(0x3C, 0xC3, 0xFF), 6 * u))
                g.DrawEllipse(pen, (31 - 17) * u, (34 - 17) * u, 34 * u, 34 * u);
            using (var b = new SolidBrush(bg)) g.FillEllipse(b, (46 - 8) * u, (19 - 8) * u, 16 * u, 16 * u);
            using (var b = new SolidBrush(Color.FromArgb(0x8B, 0x5C, 0xF6))) g.FillEllipse(b, (46 - 5) * u, (19 - 5) * u, 10 * u, 10 * u);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
