using System;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The light widget's one setting (Sys/RoomLights.cs): which bulb Lumos is asked about - its IP address, typed in the desktop window (the
// settings have no text box of their own: a small Windows dialog asks for it; in VR it is shown only). Nothing of the Tapo account: Lumos
// keeps that. In the watch editor, when a light widget is selected: Modern / Cyber a row over its look, the 95 family a line in the
// Selected widget box. The RGB widget needs none (Prisma's pipe has one name).
public sealed partial class SettingsView
{
    // the desktop window asks; the answer is the setting (empty: none)
    void AskBulb()
    {
        AskText?.Invoke("Light bulb", "The bulb's IP address, as Lumos shows it (e.g. 192.168.0.20). Lumos keeps the Tapo account; OrbitOverlay only asks it about this bulb.",
            C.Lights?.Bulb ?? "", text =>
            {
                if (text == null) return;
                text = text.Trim();
                C.Lights = text.Length == 0 ? null : new LightsConfig { Bulb = text };
                _h.Cfg.MarkDirty();
            });
    }

    // the setting as its button / box shows it
    string BulbShown => C.Lights?.Bulb is { Length: > 0 } b ? b : Lang.T("(not set)");

    // how Lumos answers about it (the state the watch has)
    string BulbFound()
    {
        if (C.Lights?.Bulb is not { Length: > 0 }) return AskText != null ? "" : Lang.T("Type it in the desktop window");
        return (_h.Data?.Lamp?.State.Status ?? LinkStatus.Off) switch
        {
            LinkStatus.Ready => Lang.T("Lumos answers"),
            LinkStatus.NoApp => Lang.T("Lumos isn't running"),
            LinkStatus.Error => Lang.T("Bulb not reachable"),
            LinkStatus.Looking => Lang.T("Looking up..."),
            _ => "",
        };
    }

    // ---- Modern / Cyber: the editor's row for a selected light widget (x, y: its top-left; 64 px tall) ----

    void BulbRow(Ui ui, float x, float y, float w)
    {
        ui.Section(new RectangleF(x, y, w, 22), "Bulb (Lumos)");
        ui.P.Text(BulbFound(), new RectangleF(x + 110, y, w - 110, 22), Theme.Dim, 12, FontWeight.SemiBold, TextAlignment.Trailing);
        if (ui.Button("we.bulb", new RectangleF(x, y + 24, w, 34), BulbShown, "", false, AskText != null, 13)) AskBulb();
    }

    // ---- the 95 family: a line in the editor's Selected widget box ----

    void Bulb95Row(Ui ui, float x, float y)
    {
        Lbl(ui, x, y, "&Bulb:");
        ui.Edit95(new RectangleF(x + 42, y, 210, RowH), BulbShown);
        if (ui.Button95("we95.bulb", new RectangleF(647, y - 1, 75, 23), "C&hange...", AskText != null)) AskBulb();
    }
}
