using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Orbit;

// --snapshot's 'overlaps' lines (Erik, 10-16: "What's new" sat on "Check for updates" and on the Notifications title): every settings
// tab in every style drawn once, each control's hit area noted (Ui.HitSpy), and two that cross reported - the layout lines only see a
// text leaving its own box. One inside another (a row and its switch) is not a crossing. ORBIT_SNAP_ONLY=overlaps runs this alone.
static partial class Tools
{
    static int OverlapRun(WristPanel wrist, KeyboardPanel kb, SettingsView view, Ui ui, Surface surf, PointerState ptr, Painter painter,
        ConfigStore cfg, PanelData data)
    {
        var keep = cfg.Data.Appearance;
        int tab0 = view.Tab, total = 0;
        var hits = new List<(string id, RectangleF r)>();
        ui.HitSpy = (id, r) => hits.Add((id, r));
        static bool Inside(RectangleF a, RectangleF b) =>
            a.Left <= b.Left + 0.5f && a.Top <= b.Top + 0.5f && a.Right >= b.Right - 0.5f && a.Bottom >= b.Bottom - 0.5f;

        void Style(string name, AppearanceConfig a, Action draw)
        {
            cfg.Data.Appearance = a;
            Theme.Apply(a);
            var found = new List<string>();
            for (int t = 0; t < SettingsView.TabCount; t++)
            {
                view.Tab = t;
                ui.ResetAnimations();
                if (view.ShowsWristPreview) wrist.Draw(data);
                if (view.ShowsKeyboardPreview) kb.Draw();
                hits.Clear();
                draw();
                // one entry per control (where it was drawn last), then every pair that crosses
                var list = hits.GroupBy(h => h.id).Select(g => g.Last()).Where(h => h.r.Width > 0 && h.r.Height > 0).ToList();
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var x = RectangleF.Intersect(list[i].r, list[j].r);
                        if (x.Width < 1.5f || x.Height < 1.5f || Inside(list[i].r, list[j].r) || Inside(list[j].r, list[i].r)) continue;
                        found.Add($"tab {t} '{list[i].id}' x '{list[j].id}' ({x.Width:0} x {x.Height:0})");
                    }
            }
            total += found.Count;
            Log.Info($"overlaps {name}: {found.Count} control(s) crossing another (want 0){(found.Count > 0 ? ": " + string.Join("; ", found.Take(16)) : "")}");
        }

        void Modern() { ui.Begin(ptr, 1.5f); painter.Begin(surf.Bitmap, Theme.Bg, 1.5f); view.Draw(ui); painter.End(); ui.End(); }
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        Style("modern", new AppearanceConfig { Clock24h = true }, Modern);
        foreach (var sk in Skins.All)
            Style(sk.Tag, new AppearanceConfig { Style = sk.Id, Clock24h = true }, () => view.Render95(ui, ptr, surf.Bitmap, 2, 0, 0, Theme.W95Teal));
        Style("cyber", new AppearanceConfig { Style = CyberHud.Id, Clock24h = true }, Modern);
        System.Globalization.CultureInfo.CurrentCulture = culture;
        ui.HitSpy = null;
        cfg.Data.Appearance = keep;
        Theme.Apply(keep);
        view.Tab = tab0;
        return total;
    }
}
