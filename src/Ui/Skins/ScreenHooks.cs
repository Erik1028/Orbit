using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orbit;

// The screen hooks: where a retro style draws a whole screen its own way instead of the shared 95 one. Each hook is a partial
// method per style - SettingsXp, SettingsAero, SettingsMac, SettingsTerm ... - that a style implements in its own file
// (src/Ui/Skins/<Style>/<Class>.<Style>.cs, "partial class WristPanel { partial void FaceXp(PanelData d, ref bool done) { ...; done = true; } }"),
// with all of the class's fields and methods at hand (the shared 95 pieces too: Media95, Dial95, Status95...). Not set done = the
// shared 95 screen is drawn in the style's skin. A hook not implemented costs nothing (the call is compiled away).
// This file only declares them: a style never edits it.

public sealed partial class SettingsView
{
    // the whole settings sheet (Draw95: the canvas is open, W95W x W95H; What's New over it is yours too)
    partial void SettingsXp(Ui ui, ref bool done);
    partial void SettingsAero(Ui ui, ref bool done);
    partial void SettingsMac(Ui ui, ref bool done);
    partial void SettingsTerm(Ui ui, ref bool done);
    bool SkinSettings(Ui ui)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: SettingsXp(ui, ref done); break;
            case RetroStyle.Aero: SettingsAero(ui, ref done); break;
            case RetroStyle.Mac: SettingsMac(ui, ref done); break;
            case RetroStyle.Terminal: SettingsTerm(ui, ref done); break;
        }
        return done;
    }

    // the Wrist tab's watch layout editor (Wrist95Editor)
    partial void WatchEditorXp(Ui ui, ref bool done);
    partial void WatchEditorAero(Ui ui, ref bool done);
    partial void WatchEditorMac(Ui ui, ref bool done);
    partial void WatchEditorTerm(Ui ui, ref bool done);
    bool SkinWatchEditor(Ui ui)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: WatchEditorXp(ui, ref done); break;
            case RetroStyle.Aero: WatchEditorAero(ui, ref done); break;
            case RetroStyle.Mac: WatchEditorMac(ui, ref done); break;
            case RetroStyle.Terminal: WatchEditorTerm(ui, ref done); break;
        }
        return done;
    }
}

public sealed partial class WristPanel
{
    // the fixed watch face - full / analog / minimal / time (Window95: the canvas is open, TW95 x TH95)
    partial void FaceXp(PanelData d, ref bool done);
    partial void FaceAero(PanelData d, ref bool done);
    partial void FaceMac(PanelData d, ref bool done);
    partial void FaceTerm(PanelData d, ref bool done);
    bool SkinFace(PanelData d)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: FaceXp(d, ref done); break;
            case RetroStyle.Aero: FaceAero(d, ref done); break;
            case RetroStyle.Mac: FaceMac(d, ref done); break;
            case RetroStyle.Terminal: FaceTerm(d, ref done); break;
        }
        return done;
    }

    // the whole custom face (Custom95: the canvas is open) - the widgets, the taskbar, the opened view
    partial void CustomXp(PanelData d, ref bool done);
    partial void CustomAero(PanelData d, ref bool done);
    partial void CustomMac(PanelData d, ref bool done);
    partial void CustomTerm(PanelData d, ref bool done);
    bool SkinCustom(PanelData d)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: CustomXp(d, ref done); break;
            case RetroStyle.Aero: CustomAero(d, ref done); break;
            case RetroStyle.Mac: CustomMac(d, ref done); break;
            case RetroStyle.Terminal: CustomTerm(d, ref done); break;
        }
        return done;
    }

    // one widget of the custom face, its frame included (r: its cell area; its tap is already handled)
    partial void WidgetXp(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id, ref bool done);
    partial void WidgetAero(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id, ref bool done);
    partial void WidgetMac(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id, ref bool done);
    partial void WidgetTerm(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id, ref bool done);
    bool SkinWidget(WatchWidget w, WatchWidgets.Kind k, RectangleF r, PanelData d, int id)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: WidgetXp(w, k, r, d, id, ref done); break;
            case RetroStyle.Aero: WidgetAero(w, k, r, d, id, ref done); break;
            case RetroStyle.Mac: WidgetMac(w, k, r, d, id, ref done); break;
            case RetroStyle.Terminal: WidgetTerm(w, k, r, d, id, ref done); break;
        }
        return done;
    }

    // an opened widget's window, after the zoom (kind: media, battery, clock, calendar, timer, stopwatch, perf, system, volume, mines, room, dock)
    partial void DetailXp(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win, ref bool done);
    partial void DetailAero(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win, ref bool done);
    partial void DetailMac(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win, ref bool done);
    partial void DetailTerm(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win, ref bool done);
    bool SkinDetail(PanelData d, WatchWidgets.Kind k, string kind, RectangleF win)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: DetailXp(d, k, kind, win, ref done); break;
            case RetroStyle.Aero: DetailAero(d, k, kind, win, ref done); break;
            case RetroStyle.Mac: DetailMac(d, k, kind, win, ref done); break;
            case RetroStyle.Terminal: DetailTerm(d, k, kind, win, ref done); break;
        }
        return done;
    }

    // the pages' taskbar along the bottom of the custom face
    partial void TaskbarXp(int count, int cur, ref bool done);
    partial void TaskbarAero(int count, int cur, ref bool done);
    partial void TaskbarMac(int count, int cur, ref bool done);
    partial void TaskbarTerm(int count, int cur, ref bool done);
    bool SkinTaskbar(int count, int cur)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: TaskbarXp(count, cur, ref done); break;
            case RetroStyle.Aero: TaskbarAero(count, cur, ref done); break;
            case RetroStyle.Mac: TaskbarMac(count, cur, ref done); break;
            case RetroStyle.Terminal: TaskbarTerm(count, cur, ref done); break;
        }
        return done;
    }
}

public sealed partial class KeyboardPanel
{
    // the whole keyboard (Draw95, before its canvas is made: make, draw and show it the way Draw95 does; the keys' places stay)
    partial void KeyboardXp(ref bool done);
    partial void KeyboardAero(ref bool done);
    partial void KeyboardMac(ref bool done);
    partial void KeyboardTerm(ref bool done);
    bool SkinKeyboard()
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: KeyboardXp(ref done); break;
            case RetroStyle.Aero: KeyboardAero(ref done); break;
            case RetroStyle.Mac: KeyboardMac(ref done); break;
            case RetroStyle.Terminal: KeyboardTerm(ref done); break;
        }
        return done;
    }
}

public sealed partial class TheaterBar
{
    // the whole bar (Draw95, before its canvas: draw it the way Draw95 does and end with Act)
    partial void TheaterXp(ref bool done);
    partial void TheaterAero(ref bool done);
    partial void TheaterMac(ref bool done);
    partial void TheaterTerm(ref bool done);
    bool SkinTheater()
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: TheaterXp(ref done); break;
            case RetroStyle.Aero: TheaterAero(ref done); break;
            case RetroStyle.Mac: TheaterMac(ref done); break;
            case RetroStyle.Terminal: TheaterTerm(ref done); break;
        }
        return done;
    }

    // the bar canvas width (Relayout; the 95 one sums its buttons, trackbars and check box) - set w95 for your own layout
    partial void TheaterWidthXp(ref int w95, ref bool done);
    partial void TheaterWidthAero(ref int w95, ref bool done);
    partial void TheaterWidthMac(ref int w95, ref bool done);
    partial void TheaterWidthTerm(ref int w95, ref bool done);
    bool SkinTheaterWidth(ref int w95)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: TheaterWidthXp(ref w95, ref done); break;
            case RetroStyle.Aero: TheaterWidthAero(ref w95, ref done); break;
            case RetroStyle.Mac: TheaterWidthMac(ref w95, ref done); break;
            case RetroStyle.Terminal: TheaterWidthTerm(ref w95, ref done); break;
        }
        return done;
    }
}

public static partial class WhatsNewView
{
    // the What's New window in r (Draw95; closed = OK or the close box clicked)
    static partial void WhatsNewXp(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done);
    static partial void WhatsNewAero(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done);
    static partial void WhatsNewMac(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done);
    static partial void WhatsNewTerm(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed, ref bool done);
    static bool SkinWhatsNew(Ui ui, RectangleF r, IReadOnlyList<WhatsNew.Entry> list, string sub, ref bool closed)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: WhatsNewXp(ui, r, list, sub, ref closed, ref done); break;
            case RetroStyle.Aero: WhatsNewAero(ui, r, list, sub, ref closed, ref done); break;
            case RetroStyle.Mac: WhatsNewMac(ui, r, list, sub, ref closed, ref done); break;
            case RetroStyle.Terminal: WhatsNewTerm(ui, r, list, sub, ref closed, ref done); break;
        }
        return done;
    }
}

public sealed partial class NotificationPanel
{
    // one notification card at y on the half-size canvas (DrawClassicCard)
    partial void CardXp(Notification c, float y, ref bool done);
    partial void CardAero(Notification c, float y, ref bool done);
    partial void CardMac(Notification c, float y, ref bool done);
    partial void CardTerm(Notification c, float y, ref bool done);
    bool SkinCard(Notification c, float y)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: CardXp(c, y, ref done); break;
            case RetroStyle.Aero: CardAero(c, y, ref done); break;
            case RetroStyle.Mac: CardMac(c, y, ref done); break;
            case RetroStyle.Terminal: CardTerm(c, y, ref done); break;
        }
        return done;
    }
}

public sealed partial class DockPanel
{
    // the whole Hexpad panel (Draw95, before its canvas: keep _hit filled - the input reads it)
    partial void DockXp(double now, ref bool done);
    partial void DockAero(double now, ref bool done);
    partial void DockMac(double now, ref bool done);
    partial void DockTerm(double now, ref bool done);
    bool SkinDock(double now)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: DockXp(now, ref done); break;
            case RetroStyle.Aero: DockAero(now, ref done); break;
            case RetroStyle.Mac: DockMac(now, ref done); break;
            case RetroStyle.Terminal: DockTerm(now, ref done); break;
        }
        return done;
    }
}

public sealed partial class RoomPanel
{
    // the whole Room panel (Draw95, before its canvas)
    partial void RoomXp(DateTime now, ref bool done);
    partial void RoomAero(DateTime now, ref bool done);
    partial void RoomMac(DateTime now, ref bool done);
    partial void RoomTerm(DateTime now, ref bool done);
    bool SkinRoom(DateTime now)
    {
        bool done = false;
        switch (Theme.Retro)
        {
            case RetroStyle.XP: RoomXp(now, ref done); break;
            case RetroStyle.Aero: RoomAero(now, ref done); break;
            case RetroStyle.Mac: RoomMac(now, ref done); break;
            case RetroStyle.Terminal: RoomTerm(now, ref done); break;
        }
        return done;
    }
}
