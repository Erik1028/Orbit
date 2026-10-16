using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orbit;

// The screens XP draws the shared 95 way, in its skin: each only says what is behind its first window's rounded top corners
// (XpSkin.CornerBack) - nothing: the window fills its own canvas, its corners show the world.

public sealed partial class SettingsView
{
    partial void SettingsXp(Ui ui, ref bool done) => XpSkin.CornerBack = 0;
}

public sealed partial class RoomPanel
{
    partial void RoomXp(DateTime now, ref bool done) => XpSkin.CornerBack = 0;
}

public sealed partial class DockPanel
{
    partial void DockXp(double now, ref bool done) => XpSkin.CornerBack = 0;
}

public static partial class WhatsNewView
{
    // its own panel (the window is the canvas); over the settings sheet the sheet's caption has set the face already
    static partial void WhatsNewXp(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done)
    {
        if (r.X <= 0 && r.Y <= 0) XpSkin.CornerBack = 0;
    }
}
