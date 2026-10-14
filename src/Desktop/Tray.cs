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

    // Family-style squircle: indigo -> cyan gradient, white orbit ring + moon.
    public static Icon MakeIcon(int size)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = size * 0.24f;
            using var path = new GraphicsPath();
            var rc = new RectangleF(0.5f, 0.5f, size - 1, size - 1);
            path.AddArc(rc.X, rc.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rc.Right - r * 2, rc.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rc.Right - r * 2, rc.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rc.X, rc.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            using var grad = new LinearGradientBrush(new PointF(0, size), new PointF(size, 0), Color.FromArgb(0x6D, 0x5C, 0xFF), Color.FromArgb(0x45, 0xC1, 0xFF));
            g.FillPath(grad, path);
            float c = size / 2f, ring = size * 0.23f;
            using var pen = new Pen(Color.White, Math.Max(2f, size * 0.075f));
            g.DrawEllipse(pen, c - ring, c - ring, ring * 2, ring * 2);
            float moon = size * 0.1f;
            g.FillEllipse(Brushes.White, c + ring * 0.72f - moon, c - ring * 0.72f - moon, moon * 2, moon * 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
